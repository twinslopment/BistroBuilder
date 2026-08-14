from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Dict, List, Tuple

import bmesh
import bpy
from mathutils import Vector

from .analysis import AnalysisSnapshot, analyse_session, apply_snapshot_to_session
from ._bundled.assets4all_core.models import GateState
from .session import get_work_objects, top_level_work_objects, translate_work_z


@dataclass
class RepairResult:
    accepted: bool
    changes: int
    passes: int
    summary: str
    before: AnalysisSnapshot
    after: AnalysisSnapshot


def _cleanup_object_mesh(obj: bpy.types.Object) -> int:
    """Only deterministic cleanup is allowed in the foundation pass.

    Extreme Meshy shell fragmentation is deliberately NOT deleted here. A
    disconnected shell is not automatically debris; RCE/deep-repair must
    decide that from geometry, location, symmetry and semantic evidence.
    """
    if obj.type != "MESH" or obj.data is None:
        return 0

    bm = bmesh.new()
    changes = 0
    try:
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()

        loose = [
            vert
            for vert in bm.verts
            if not vert.link_edges and not vert.link_faces
        ]
        if loose:
            changes += len(loose)
            bmesh.ops.delete(bm, geom=loose, context="VERTS")

        degenerate = [
            face
            for face in bm.faces
            if face.calc_area() <= 1.0e-12
        ]
        if degenerate:
            changes += len(degenerate)
            bmesh.ops.delete(bm, geom=degenerate, context="FACES_ONLY")

        bm.faces.ensure_lookup_table()
        if bm.faces:
            nonmanifold_overflow = any(
                len(edge.link_faces) > 2
                for edge in bm.edges
            )
            if not nonmanifold_overflow:
                bmesh.ops.recalc_face_normals(
                    bm,
                    faces=list(bm.faces),
                )

        bm.to_mesh(obj.data)
        obj.data.update()
    finally:
        bm.free()

    return changes


def _apply_uniform_scale(session, factor: float) -> int:
    """Bake a uniform scale into WORK geometry and hierarchy positions.

    SOURCE remains untouched. Object scale values stay unchanged, so the
    normalized WORK does not accumulate a transform-scale dependency.
    """
    factor = float(factor)
    if not math.isfinite(factor) or factor <= 0.0:
        return 0
    if abs(factor - 1.0) <= 1.0e-5:
        return 0

    changed = 0
    for obj in get_work_objects(session):
        obj.location *= factor
        if obj.type == "MESH" and obj.data is not None:
            for vertex in obj.data.vertices:
                vertex.co *= factor
            obj.data.update()
            changed += 1
    return changed


def _repair_localized_ground_spike(
    session,
    snapshot: AnalysisSnapshot,
) -> int:
    grounding = snapshot.grounding
    if grounding is None or grounding.state != GateState.FAIL:
        return 0

    robust_z = grounding.robust_support_z
    height = max(snapshot.dimension_z, 0.01)
    outlier_depth = max(0.0035, height * 0.0035)
    cutoff = robust_z - outlier_depth

    candidates: List[Tuple[bpy.types.Object, int, Vector]] = []
    total_vertices = 0
    for obj in get_work_objects(session):
        if obj.type != "MESH" or obj.data is None:
            continue
        total_vertices += len(obj.data.vertices)
        matrix = obj.matrix_world
        for vertex in obj.data.vertices:
            world = matrix @ vertex.co
            if world.z < cutoff:
                candidates.append((obj, vertex.index, world))

    if not candidates or total_vertices <= 0:
        return 0

    # Localized repair only. Larger affected areas are structural and must be
    # handled by deep repair rather than silently flattened.
    fraction = len(candidates) / total_vertices
    if fraction > 0.025:
        return 0

    inverse_cache: Dict[bpy.types.Object, object] = {}
    for obj, index, world in candidates:
        inverse = inverse_cache.setdefault(
            obj,
            obj.matrix_world.inverted_safe(),
        )
        corrected = world.copy()
        corrected.z = robust_z
        obj.data.vertices[index].co = inverse @ corrected
        obj.data.update()

    return len(candidates)


def _ground_rigidly_if_needed(
    session,
    snapshot: AnalysisSnapshot,
) -> int:
    """Move the complete WORK support plane to Z=0 before local repair.

    An imported AI model may be centred around its origin, so a large vertical
    offset is normal and must not be confused with geometry penetrating the
    floor. Robust support decides the rigid translation; residual penetration
    is analysed only after this normalization.
    """
    grounding = snapshot.grounding
    if grounding is None:
        return 0

    delta = float(grounding.translation_z)
    if not math.isfinite(delta) or abs(delta) <= 0.0005:
        return 0

    # Origin offsets can be larger than the asset itself. Only absurd numeric
    # values are rejected here; SOURCE/transactional rollback remain safety nets.
    sanity_limit = max(50.0, snapshot.dimension_z * 25.0)
    if abs(delta) > sanity_limit:
        return 0

    translate_work_z(session, delta)
    return 1


def _ground_state_rank(snapshot: AnalysisSnapshot) -> int:
    if snapshot.grounding is None:
        return 3
    return {
        GateState.FAIL: 0,
        GateState.REVIEW: 1,
        GateState.PASS: 3,
        GateState.NA: 3,
    }.get(snapshot.grounding.state, 0)


def _dimension_change(
    before: AnalysisSnapshot,
    after: AnalysisSnapshot,
    expected_scale: float,
) -> float:
    values = []
    for old, new in (
        (before.dimension_x, after.dimension_x),
        (before.dimension_y, after.dimension_y),
        (before.dimension_z, after.dimension_z),
    ):
        expected = old * expected_scale
        values.append(
            abs(new - expected) / max(abs(expected), 0.01)
        )
    return max(values)


def _accept_repair(
    before: AnalysisSnapshot,
    after: AnalysisSnapshot,
    expected_scale: float,
) -> Tuple[bool, str]:
    if _ground_state_rank(after) < _ground_state_rank(before):
        return False, "Ground Integrity empeoró."

    if after.pvs.score < before.pvs.score - 2.0:
        return (
            False,
            f"PVS cayó {before.pvs.score:.1f} -> {after.pvs.score:.1f}.",
        )

    if _dimension_change(before, after, expected_scale) > 0.035:
        return (
            False,
            "La reparación alteró la silueta más de lo esperado por la normalización.",
        )

    if before.triangle_count > 0:
        loss = (
            before.triangle_count - after.triangle_count
        ) / before.triangle_count
        allowed = 0.06 if (
            before.degenerate_faces or before.loose_vertices
        ) else 0.025
        if loss > allowed:
            return (
                False,
                f"Pérdida geométrica excesiva ({loss * 100.0:.1f}%).",
            )

    if _ground_state_rank(after) > _ground_state_rank(before):
        return True, "Ground Integrity mejoró sin regresiones."

    if after.pvs.score >= before.pvs.score - 0.25:
        return True, "La reparación es estable y no degrada la calidad."

    return False, "No se ha demostrado una mejora segura."


def run_self_healing_repair(context, session) -> RepairResult:
    before = analyse_session(context, session)
    work_objects = [
        obj
        for obj in get_work_objects(session)
        if obj.type == "MESH" and obj.data is not None
    ]
    if not work_objects:
        raise RuntimeError("No hay mallas WORK que reparar.")

    original_meshes: Dict[bpy.types.Object, bpy.types.Mesh] = {}
    original_locations = {
        obj: obj.location.copy()
        for obj in get_work_objects(session)
    }

    for obj in work_objects:
        original_meshes[obj] = obj.data
        trial = obj.data.copy()
        trial.name = f"{obj.data.name}__A4A_TRIAL"
        obj.data = trial

    changes = 0
    passes = 1
    scale_factor = 1.0

    try:
        # Stage 0 — profile-aware uniform scale normalization.
        if before.scale_recommended:
            scale_factor = before.scale_suggestion
            scale_changes = _apply_uniform_scale(
                session,
                scale_factor,
            )
            changes += scale_changes
            if scale_changes:
                passes += 1

        intermediate = analyse_session(context, session)

        # Stage 1 — rigid grounding. This deliberately happens even when the
        # initial gate is FAIL: a centred import must first be moved as a whole.
        ground_changes = _ground_rigidly_if_needed(
            session,
            intermediate,
        )
        changes += ground_changes
        if ground_changes:
            passes += 1
            intermediate = analyse_session(context, session)

        # Stage 2 — only now repair localized residual penetration/outliers.
        spike_changes = _repair_localized_ground_spike(
            session,
            intermediate,
        )
        changes += spike_changes
        if spike_changes:
            passes += 1
            intermediate = analyse_session(context, session)

        # Stage 3 — deterministic topology cleanup. Fragmented shells are kept.
        cleanup_changes = 0
        for obj in work_objects:
            cleanup_changes += _cleanup_object_mesh(obj)
        changes += cleanup_changes
        if cleanup_changes:
            passes += 1
            intermediate = analyse_session(context, session)

        # Cleanup can alter the support set by millimetres; ground once more.
        reground_changes = _ground_rigidly_if_needed(
            session,
            intermediate,
        )
        changes += reground_changes
        if reground_changes:
            passes += 1

        after = analyse_session(context, session)
        accepted, reason = _accept_repair(
            before,
            after,
            scale_factor,
        )

        if not accepted:
            for obj, original in original_meshes.items():
                current = obj.data
                obj.data = original
                if current is not None and current.users == 0:
                    bpy.data.meshes.remove(current)
            for obj, location in original_locations.items():
                obj.location = location

            after = analyse_session(context, session)
            apply_snapshot_to_session(session, after)
            session.repair_gate = "REVIEW"
            session.last_repair_summary = (
                f"Autorreparación revertida: {reason}"
            )
            return RepairResult(
                False,
                changes,
                passes,
                session.last_repair_summary,
                before,
                after,
            )

        for original in original_meshes.values():
            if original.users == 0:
                bpy.data.meshes.remove(original)

        apply_snapshot_to_session(session, after)
        unresolved_failures = sum(
            1
            for issue in session.issues
            if issue.severity == "FAIL"
        )
        session.repair_gate = (
            "PASS" if unresolved_failures == 0 else "REVIEW"
        )
        session.repair_passes += passes
        session.repair_changes += changes
        session.state = (
            "PREPARED" if unresolved_failures == 0 else "REVIEW"
        )
        session.last_repair_summary = (
            f"Autorreparación aceptada · {changes} cambio(s) · "
            f"{passes} pasada(s) · escala x{scale_factor:.4f}. {reason}"
        )
        return RepairResult(
            True,
            changes,
            passes,
            session.last_repair_summary,
            before,
            after,
        )

    except Exception:
        for obj, original in original_meshes.items():
            current = obj.data
            obj.data = original
            if current is not None and current.users == 0:
                bpy.data.meshes.remove(current)
        for obj, location in original_locations.items():
            obj.location = location
        raise
