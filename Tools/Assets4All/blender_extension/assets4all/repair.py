from __future__ import annotations

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

def _components(bm: bmesh.types.BMesh) -> List[List[bmesh.types.BMVert]]:
    unseen = set(bm.verts)
    result: List[List[bmesh.types.BMVert]] = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        component = [seed]
        while stack:
            vert = stack.pop()
            for edge in vert.link_edges:
                other = edge.other_vert(vert)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
                    component.append(other)
        result.append(component)
    return result

def _cleanup_object_mesh(obj: bpy.types.Object) -> int:
    if obj.type != "MESH" or obj.data is None:
        return 0
    bm = bmesh.new()
    changes = 0
    try:
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table(); bm.faces.ensure_lookup_table()
        loose = [vert for vert in bm.verts if not vert.link_edges and not vert.link_faces]
        if loose:
            changes += len(loose)
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        degenerate = [face for face in bm.faces if face.calc_area() <= 1.0e-12]
        if degenerate:
            changes += len(degenerate)
            bmesh.ops.delete(bm, geom=degenerate, context="FACES_ONLY")
        bm.verts.ensure_lookup_table()
        comps = _components(bm)
        total_verts = max(1, sum(len(comp) for comp in comps))
        removable: List[bmesh.types.BMVert] = []
        for comp in comps:
            fraction = len(comp) / total_verts
            if len(comp) <= 18 and fraction < 0.00035:
                removable.extend(comp)
        if removable:
            changes += len(removable)
            bmesh.ops.delete(bm, geom=removable, context="VERTS")
        bm.faces.ensure_lookup_table()
        if bm.faces:
            nonmanifold_overflow = any(len(edge.link_faces) > 2 for edge in bm.edges)
            if not nonmanifold_overflow:
                bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        obj.data.update()
    finally:
        bm.free()
    return changes

def _repair_localized_ground_spike(session, snapshot: AnalysisSnapshot) -> int:
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
    fraction = len(candidates) / total_vertices
    if fraction > 0.025:
        return 0
    inverse_cache: Dict[bpy.types.Object, object] = {}
    for obj, index, world in candidates:
        inverse = inverse_cache.setdefault(obj, obj.matrix_world.inverted_safe())
        corrected = world.copy(); corrected.z = robust_z
        obj.data.vertices[index].co = inverse @ corrected
        obj.data.update()
    return len(candidates)

def _ground_state_rank(snapshot: AnalysisSnapshot) -> int:
    if snapshot.grounding is None:
        return 3
    return {GateState.FAIL: 0, GateState.REVIEW: 1, GateState.PASS: 3, GateState.NA: 3}.get(snapshot.grounding.state, 0)

def _dimension_change(before: AnalysisSnapshot, after: AnalysisSnapshot) -> float:
    values = []
    for old, new in ((before.dimension_x, after.dimension_x), (before.dimension_y, after.dimension_y), (before.dimension_z, after.dimension_z)):
        values.append(abs(new - old) / max(abs(old), 0.01))
    return max(values)

def _accept_repair(before: AnalysisSnapshot, after: AnalysisSnapshot) -> Tuple[bool, str]:
    if _ground_state_rank(after) < _ground_state_rank(before):
        return False, "Ground Integrity empeoró."
    if after.pvs.score < before.pvs.score - 2.0:
        return False, f"PVS cayó {before.pvs.score:.1f} -> {after.pvs.score:.1f}."
    if _dimension_change(before, after) > 0.035:
        return False, "La reparación alteró demasiado la silueta dimensional."
    if before.triangle_count > 0:
        loss = (before.triangle_count - after.triangle_count) / before.triangle_count
        allowed = 0.10 if (before.degenerate_faces or before.loose_vertices or before.tiny_components) else 0.04
        if loss > allowed:
            return False, f"Pérdida geométrica excesiva ({loss * 100.0:.1f}%)."
    if _ground_state_rank(after) > _ground_state_rank(before):
        return True, "Ground Integrity mejoró sin regresiones."
    if after.pvs.score >= before.pvs.score - 0.25:
        return True, "La reparación es estable y no degrada la calidad."
    return False, "No se ha demostrado una mejora segura."

def run_self_healing_repair(context, session) -> RepairResult:
    before = analyse_session(context, session)
    work_objects = [obj for obj in get_work_objects(session) if obj.type == "MESH" and obj.data is not None]
    if not work_objects:
        raise RuntimeError("No hay mallas WORK que reparar.")
    original_meshes: Dict[bpy.types.Object, bpy.types.Mesh] = {}
    original_locations = {obj: obj.location.copy() for obj in top_level_work_objects(session)}
    for obj in work_objects:
        original_meshes[obj] = obj.data
        trial = obj.data.copy(); trial.name = f"{obj.data.name}__A4A_TRIAL"; obj.data = trial
    changes = 0; passes = 1
    try:
        for obj in work_objects:
            changes += _cleanup_object_mesh(obj)
        intermediate = analyse_session(context, session)
        spike_changes = _repair_localized_ground_spike(session, intermediate)
        changes += spike_changes
        if spike_changes:
            passes += 1
            intermediate = analyse_session(context, session)
        grounding = intermediate.grounding
        if grounding is not None and grounding.state == GateState.REVIEW:
            delta = grounding.translation_z
            max_reasonable = max(0.05, intermediate.dimension_z * 0.25)
            if abs(delta) <= max_reasonable:
                translate_work_z(session, delta)
                changes += 1; passes += 1
        after = analyse_session(context, session)
        accepted, reason = _accept_repair(before, after)
        if not accepted:
            for obj, original in original_meshes.items():
                current = obj.data; obj.data = original
                if current is not None and current.users == 0: bpy.data.meshes.remove(current)
            for obj, location in original_locations.items(): obj.location = location
            after = analyse_session(context, session); apply_snapshot_to_session(session, after)
            session.repair_gate = "REVIEW"; session.last_repair_summary = f"Autorreparación revertida: {reason}"
            return RepairResult(False, changes, passes, session.last_repair_summary, before, after)
        for original in original_meshes.values():
            if original.users == 0: bpy.data.meshes.remove(original)
        apply_snapshot_to_session(session, after)
        unresolved_failures = sum(1 for issue in session.issues if issue.severity == "FAIL")
        session.repair_gate = "PASS" if unresolved_failures == 0 else "REVIEW"
        session.repair_passes += passes; session.repair_changes += changes
        session.state = "PREPARED" if unresolved_failures == 0 else "REVIEW"
        session.last_repair_summary = f"Autorreparación aceptada · {changes} cambio(s) · {passes} pasada(s). {reason}"
        return RepairResult(True, changes, passes, session.last_repair_summary, before, after)
    except Exception:
        for obj, original in original_meshes.items():
            current = obj.data; obj.data = original
            if current is not None and current.users == 0: bpy.data.meshes.remove(current)
        for obj, location in original_locations.items(): obj.location = location
        raise
