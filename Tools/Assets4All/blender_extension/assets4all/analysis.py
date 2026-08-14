from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import List, Sequence, Tuple

import bmesh
import bpy
from mathutils import Vector

from ._bundled.assets4all_core.grounding import analyse_grounding
from ._bundled.assets4all_core.models import (
    ConversionRiskInputs,
    GateState,
    GroundingInputs,
    GroundingSample,
    Metric,
    ViabilityInputs,
)
from ._bundled.assets4all_core.scoring import (
    conversion_success_estimate,
    processing_viability_score,
    resolve_dual_decision,
)
from .profiles import (
    get_profile_rule,
    infer_uniform_scale,
    profile_plausibility,
)
from .properties import is_floor_profile
from .session import get_work_objects


@dataclass
class AnalysisSnapshot:
    object_count: int = 0
    mesh_count: int = 0
    vertex_count: int = 0
    edge_count: int = 0
    face_count: int = 0
    triangle_count: int = 0
    material_slot_count: int = 0
    missing_uv_meshes: int = 0
    boundary_edges: int = 0
    nonmanifold_edges: int = 0
    wire_edges: int = 0
    loose_vertices: int = 0
    degenerate_faces: int = 0
    connected_components: int = 0
    tiny_components: int = 0
    negative_scale_objects: int = 0
    nonuniform_scale_objects: int = 0
    dimension_x: float = 0.0
    dimension_y: float = 0.0
    dimension_z: float = 0.0
    min_z: float = 0.0
    max_z: float = 0.0
    symmetry_score: float = 0.0
    fragmentation_score: float = 100.0
    scale_suggestion: float = 1.0
    scale_confidence: float = 0.0
    scale_recommended: bool = False
    scale_reason: str = ""
    grounding: object | None = None
    pvs: object | None = None
    cse: object | None = None
    dual: object | None = None
    issues: List[Tuple[str, str, str, bool]] = field(default_factory=list)


@dataclass
class _MeshStats:
    vertices: int = 0
    edges: int = 0
    faces: int = 0
    triangles: int = 0
    boundary_edges: int = 0
    nonmanifold_edges: int = 0
    wire_edges: int = 0
    loose_vertices: int = 0
    degenerate_faces: int = 0
    components: int = 0
    tiny_components: int = 0


def _component_stats(bm: bmesh.types.BMesh) -> Tuple[int, int]:
    unseen = set(bm.verts)
    sizes: List[int] = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        count = 0
        while stack:
            vert = stack.pop()
            count += 1
            for edge in vert.link_edges:
                other = edge.other_vert(vert)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
        sizes.append(count)
    if not sizes:
        return 0, 0
    total = sum(sizes)
    tiny = sum(
        1
        for size in sizes
        if size <= 24 and size / max(total, 1) < 0.001
    )
    return len(sizes), tiny


def _analyse_mesh(mesh: bpy.types.Mesh) -> _MeshStats:
    stats = _MeshStats(
        vertices=len(mesh.vertices),
        edges=len(mesh.edges),
        faces=len(mesh.polygons),
    )
    mesh.calc_loop_triangles()
    stats.triangles = len(mesh.loop_triangles)
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
        for edge in bm.edges:
            linked = len(edge.link_faces)
            if linked == 0:
                stats.wire_edges += 1
            elif linked == 1:
                stats.boundary_edges += 1
            elif linked > 2:
                stats.nonmanifold_edges += 1
        stats.loose_vertices = sum(
            1 for vert in bm.verts if not vert.link_edges and not vert.link_faces
        )
        stats.degenerate_faces = sum(
            1 for face in bm.faces if face.calc_area() <= 1.0e-12
        )
        stats.components, stats.tiny_components = _component_stats(bm)
    finally:
        bm.free()
    return stats


def _world_triangle_area(a: Vector, b: Vector, c: Vector) -> float:
    return (b - a).cross(c - a).length * 0.5


def _collect_geometry(objects: Sequence[bpy.types.Object]):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    vertex_points: List[Vector] = []
    ground_samples: List[GroundingSample] = []
    aggregate = _MeshStats()
    material_slots = 0
    missing_uv_meshes = 0

    for obj in objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh(
            preserve_all_data_layers=True,
            depsgraph=depsgraph,
        )
        try:
            mesh_stats = _analyse_mesh(mesh)
            for field_name in _MeshStats.__dataclass_fields__:
                setattr(
                    aggregate,
                    field_name,
                    getattr(aggregate, field_name)
                    + getattr(mesh_stats, field_name),
                )
            material_slots += len(obj.material_slots)
            if not mesh.uv_layers:
                missing_uv_meshes += 1

            matrix = evaluated.matrix_world
            world_vertices = [matrix @ vertex.co for vertex in mesh.vertices]
            for point in world_vertices:
                minimum.x = min(minimum.x, point.x)
                minimum.y = min(minimum.y, point.y)
                minimum.z = min(minimum.z, point.z)
                maximum.x = max(maximum.x, point.x)
                maximum.y = max(maximum.y, point.y)
                maximum.z = max(maximum.z, point.z)
            vertex_points.extend(world_vertices)

            weights = [0.0] * len(world_vertices)
            mesh.calc_loop_triangles()
            for tri in mesh.loop_triangles:
                i0, i1, i2 = tri.vertices
                area = _world_triangle_area(
                    world_vertices[i0],
                    world_vertices[i1],
                    world_vertices[i2],
                )
                share = area / 3.0
                weights[i0] += share
                weights[i1] += share
                weights[i2] += share
            for index, point in enumerate(world_vertices):
                ground_samples.append(
                    GroundingSample(
                        z=point.z,
                        area_weight=max(weights[index], 1.0e-10),
                    )
                )
        finally:
            evaluated.to_mesh_clear()

    return (
        aggregate,
        material_slots,
        missing_uv_meshes,
        minimum,
        maximum,
        vertex_points,
        ground_samples,
    )


def _symmetry_score(
    points: Sequence[Vector],
    minimum: Vector,
    maximum: Vector,
) -> float:
    if len(points) < 16:
        return 35.0
    center_x = (minimum.x + maximum.x) * 0.5
    span = max(
        maximum.x - minimum.x,
        maximum.y - minimum.y,
        maximum.z - minimum.z,
        1.0e-6,
    )
    quantum = span / 80.0
    step = max(1, len(points) // 4000)

    def key(point: Vector) -> Tuple[int, int, int]:
        return (
            round(point.x / quantum),
            round(point.y / quantum),
            round(point.z / quantum),
        )

    sampled = points[::step]
    voxels = {key(point) for point in sampled}
    matched = 0
    for point in sampled:
        mirrored = Vector((2.0 * center_x - point.x, point.y, point.z))
        if key(mirrored) in voxels:
            matched += 1
    return 100.0 * matched / max(len(sampled), 1)


def _fragmentation_score(components: int, vertices: int) -> float:
    """Quality of shell fragmentation, not a count of semantic regions.

    A handful of disconnected shells can help segmentation. Hundreds or
    thousands of shells are treated as fragmented topology and must never be
    interpreted as hundreds of meaningful parts.
    """
    if components <= 1:
        return 78.0
    if components <= 8:
        return 96.0
    if components <= 32:
        return 90.0
    if components <= 100:
        return 78.0
    if components <= 500:
        return 62.0
    density = components / max(vertices, 1)
    return max(28.0, 52.0 - min(20.0, density * 500.0))


def _region_separability(
    components: int,
    material_slots: int,
    fragmentation_score: float,
) -> float:
    if components <= 1:
        return 76.0 if material_slots >= 2 else 58.0
    if components <= 24:
        return min(94.0, 72.0 + components * 0.9)
    if components <= 100:
        return 74.0
    # Extreme shell counts are not semantic separability. Let RCE reconstruct
    # stable regions from geometry instead of rewarding fragmentation.
    return max(38.0, min(68.0, fragmentation_score + 6.0))


def _gate(value: float, fail: float = 45.0, review: float = 75.0) -> str:
    if value < fail:
        return "FAIL"
    if value < review:
        return "REVIEW"
    return "PASS"


def analyse_session(context, session) -> AnalysisSnapshot:
    work_objects = get_work_objects(session)
    mesh_objects = [obj for obj in work_objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("La sesión no contiene mallas WORK válidas.")

    snapshot = AnalysisSnapshot(
        object_count=len(work_objects),
        mesh_count=len(mesh_objects),
    )
    (
        aggregate,
        material_slots,
        missing_uv_meshes,
        minimum,
        maximum,
        points,
        ground_samples,
    ) = _collect_geometry(mesh_objects)

    snapshot.vertex_count = aggregate.vertices
    snapshot.edge_count = aggregate.edges
    snapshot.face_count = aggregate.faces
    snapshot.triangle_count = aggregate.triangles
    snapshot.material_slot_count = material_slots
    snapshot.missing_uv_meshes = missing_uv_meshes
    snapshot.boundary_edges = aggregate.boundary_edges
    snapshot.nonmanifold_edges = aggregate.nonmanifold_edges
    snapshot.wire_edges = aggregate.wire_edges
    snapshot.loose_vertices = aggregate.loose_vertices
    snapshot.degenerate_faces = aggregate.degenerate_faces
    snapshot.connected_components = aggregate.components
    snapshot.tiny_components = aggregate.tiny_components

    dims_vector = maximum - minimum
    snapshot.dimension_x = max(0.0, dims_vector.x)
    snapshot.dimension_y = max(0.0, dims_vector.y)
    snapshot.dimension_z = max(0.0, dims_vector.z)
    snapshot.min_z = minimum.z
    snapshot.max_z = maximum.z
    dims = (
        snapshot.dimension_x,
        snapshot.dimension_y,
        snapshot.dimension_z,
    )

    snapshot.symmetry_score = _symmetry_score(points, minimum, maximum)
    snapshot.fragmentation_score = _fragmentation_score(
        snapshot.connected_components,
        snapshot.vertex_count,
    )

    scale_inference = infer_uniform_scale(session.profile_id, dims)
    snapshot.scale_suggestion = scale_inference.factor
    snapshot.scale_confidence = scale_inference.confidence
    snapshot.scale_recommended = scale_inference.recommended
    snapshot.scale_reason = scale_inference.reason

    for obj in work_objects:
        scale = obj.scale
        if scale.x * scale.y * scale.z < 0.0:
            snapshot.negative_scale_objects += 1
        absolute = (abs(scale.x), abs(scale.y), abs(scale.z))
        if max(absolute) - min(absolute) > 1.0e-4:
            snapshot.nonuniform_scale_objects += 1

    if is_floor_profile(session.profile_id):
        snapshot.grounding = analyse_grounding(
            GroundingInputs(
                samples=ground_samples,
                min_support_fraction=0.001,
            )
        )

    total_faces = max(snapshot.face_count, 1)
    total_edges = max(snapshot.edge_count, 1)

    tiny_penalty = min(
        10.0,
        3.5 * math.log10(1.0 + max(0, snapshot.tiny_components)),
    )
    geometry_penalty = (
        min(25.0, 100.0 * snapshot.degenerate_faces / total_faces * 20.0)
        + min(
            20.0,
            100.0
            * snapshot.loose_vertices
            / max(snapshot.vertex_count, 1)
            * 10.0,
        )
        + tiny_penalty
    )
    geometry_integrity = max(0.0, 100.0 - geometry_penalty)

    topology_penalty = (
        min(45.0, 100.0 * snapshot.nonmanifold_edges / total_edges * 80.0)
        + min(18.0, 100.0 * snapshot.wire_edges / total_edges * 30.0)
        + min(12.0, 100.0 * snapshot.boundary_edges / total_edges * 2.5)
    )
    topology = max(0.0, 100.0 - topology_penalty)

    uv_readiness = (
        100.0
        if snapshot.missing_uv_meshes == 0
        else 40.0
        if snapshot.missing_uv_meshes == snapshot.mesh_count
        else 70.0
    )
    transform_score = max(
        20.0,
        100.0
        - snapshot.negative_scale_objects * 35.0
        - snapshot.nonuniform_scale_objects * 8.0,
    )
    plausibility = profile_plausibility(session.profile_id, dims)

    artifact_penalty = min(
        30.0,
        7.0 * math.log10(1.0 + max(0, snapshot.tiny_components)),
    )
    if snapshot.grounding is not None and snapshot.grounding.state == GateState.FAIL:
        artifact_penalty += 18.0
    artifact_inverse = max(0.0, 100.0 - artifact_penalty)

    region_separability = _region_separability(
        snapshot.connected_components,
        snapshot.material_slot_count,
        snapshot.fragmentation_score,
    )

    budget = int(get_profile_rule(session.profile_id)["budget"])
    optimization_headroom = (
        100.0
        if snapshot.triangle_count <= budget
        else max(
            15.0,
            100.0 / (snapshot.triangle_count / max(budget, 1)),
        )
    )

    snapshot.pvs = processing_viability_score(
        ViabilityInputs(
            geometry_integrity=Metric("geometry_integrity", geometry_integrity),
            topology=Metric("topology", topology),
            uv_readiness=Metric("uv_readiness", uv_readiness, 0.9),
            transform_orientation=Metric(
                "transform_orientation",
                transform_score,
            ),
            scale_plausibility=Metric(
                "scale_plausibility",
                plausibility,
                0.85,
            ),
            artifact_severity_inverse=Metric(
                "artifact_severity_inverse",
                artifact_inverse,
            ),
            region_separability=Metric(
                "region_separability",
                region_separability,
                0.7,
            ),
            symmetry_repetition=Metric(
                "symmetry_repetition",
                snapshot.symmetry_score,
                0.75,
            ),
            optimization_headroom=Metric(
                "optimization_headroom",
                optimization_headroom,
            ),
            profile_plausibility=Metric(
                "profile_plausibility",
                plausibility,
                0.85,
            ),
        )
    )

    repair_probability = max(
        0.42,
        min(
            0.99,
            0.50
            + geometry_integrity / 300.0
            + artifact_inverse / 500.0
            + (0.08 if snapshot.scale_recommended else 0.0),
        ),
    )
    segmentation_probability = max(
        0.40,
        min(
            0.96,
            0.38
            + region_separability / 260.0
            + snapshot.symmetry_score / 650.0,
        ),
    )
    semantic_probability = 0.88 if session.profile_id != "GENERIC_PROP" else 0.67
    grounding_probability = (
        0.96
        if snapshot.grounding is None
        else 0.99
        if snapshot.grounding.state == GateState.PASS
        else 0.90
        if snapshot.grounding.state == GateState.REVIEW
        else 0.62
    )
    optimization_probability = max(
        0.55,
        min(0.98, 0.55 + optimization_headroom / 230.0),
    )

    ambiguous = (
        (2 if snapshot.connected_components <= 1 and snapshot.material_slot_count <= 1 else 0)
        + (1 if snapshot.symmetry_score < 55.0 else 0)
        + (2 if session.profile_id == "GENERIC_PROP" else 0)
        + (1 if snapshot.missing_uv_meshes else 0)
    )
    if snapshot.connected_components > 100:
        ambiguous += min(
            6,
            max(2, int(math.ceil(math.log10(snapshot.connected_components / 100.0 + 1.0) * 4.0))),
        )

    severe_flags: List[str] = []
    if snapshot.triangle_count > 1_000_000:
        severe_flags.append("extreme_triangle_count")
    if snapshot.dimension_z <= 1.0e-6:
        severe_flags.append("zero_height")

    snapshot.cse = conversion_success_estimate(
        ConversionRiskInputs(
            repair_probability,
            segmentation_probability,
            semantic_probability,
            grounding_probability,
            optimization_probability,
            0.98,
            ambiguous,
            ambiguous * 7.0,
            session.review_budget_seconds,
            tuple(severe_flags),
        )
    )
    snapshot.dual = resolve_dual_decision(snapshot.pvs, snapshot.cse)

    if snapshot.scale_recommended:
        snapshot.issues.append(
            (
                "REVIEW",
                "AUTO_SCALE",
                snapshot.scale_reason,
                True,
            )
        )
    if snapshot.loose_vertices:
        snapshot.issues.append(
            (
                "REVIEW",
                "LOOSE_VERTICES",
                f"{snapshot.loose_vertices} vértices sueltos detectados.",
                True,
            )
        )
    if snapshot.degenerate_faces:
        snapshot.issues.append(
            (
                "REVIEW",
                "DEGENERATE_FACES",
                f"{snapshot.degenerate_faces} caras degeneradas detectadas.",
                True,
            )
        )
    if snapshot.tiny_components:
        snapshot.issues.append(
            (
                "REVIEW",
                "FRAGMENTED_SHELLS",
                f"{snapshot.connected_components} componentes conectados; "
                f"{snapshot.tiny_components} son microcomponentes. RCE los tratará como "
                "fragmentación, no como partes semánticas independientes.",
                False,
            )
        )
    if snapshot.nonmanifold_edges:
        snapshot.issues.append(
            (
                "REVIEW",
                "NONMANIFOLD",
                f"{snapshot.nonmanifold_edges} aristas con más de dos caras.",
                False,
            )
        )
    if snapshot.grounding is not None and snapshot.grounding.state != GateState.PASS:
        snapshot.issues.append(
            (
                "FAIL" if snapshot.grounding.state == GateState.FAIL else "REVIEW",
                "GROUND_INTEGRITY",
                snapshot.grounding.message,
                True,
            )
        )
    if snapshot.missing_uv_meshes:
        snapshot.issues.append(
            (
                "REVIEW",
                "MISSING_UV",
                f"{snapshot.missing_uv_meshes} mallas sin UV0.",
                False,
            )
        )

    return snapshot


def apply_snapshot_to_session(session, snapshot: AnalysisSnapshot) -> None:
    for field_name in (
        "object_count",
        "mesh_count",
        "vertex_count",
        "triangle_count",
        "material_slot_count",
        "missing_uv_meshes",
        "boundary_edges",
        "nonmanifold_edges",
        "wire_edges",
        "loose_vertices",
        "degenerate_faces",
        "connected_components",
        "tiny_components",
        "symmetry_score",
        "dimension_x",
        "dimension_y",
        "dimension_z",
        "min_z",
        "max_z",
        "fragmentation_score",
        "scale_suggestion",
        "scale_confidence",
    ):
        setattr(session, field_name, getattr(snapshot, field_name))

    session.scale_recommended = snapshot.scale_recommended
    session.scale_reason = snapshot.scale_reason
    session.scale_gate = "REVIEW" if snapshot.scale_recommended else "PASS"
    session.pvs = snapshot.pvs.score
    session.cse = snapshot.cse.score
    session.score_disagreement = snapshot.dual.disagreement
    session.final_decision = snapshot.dual.final_decision.value

    while len(session.issues):
        session.issues.remove(len(session.issues) - 1)
    for severity, code, message, auto_repairable in snapshot.issues:
        item = session.issues.add()
        item.severity = severity
        item.code = code
        item.message = message
        item.auto_repairable = auto_repairable

    geometry_quality = max(
        0.0,
        100.0
        - min(
            100.0,
            snapshot.degenerate_faces * 5.0
            + snapshot.loose_vertices * 0.2,
        ),
    )
    topology_quality = max(
        0.0,
        100.0
        - min(
            100.0,
            snapshot.nonmanifold_edges * 1.5
            + snapshot.wire_edges * 0.5,
        ),
    )
    session.geometry_gate = _gate(geometry_quality)
    session.topology_gate = _gate(topology_quality)

    if snapshot.grounding is None:
        session.ground_state = "N/A"
        session.ground_gate = "N/A"
        session.ground_message = "El perfil actual no requiere apoyo en suelo."
        session.robust_support_z = 0.0
        session.ground_translation_z = 0.0
        session.support_fraction = 0.0
    else:
        session.ground_state = snapshot.grounding.state.value
        session.ground_gate = snapshot.grounding.state.value
        session.ground_message = snapshot.grounding.message
        session.robust_support_z = snapshot.grounding.robust_support_z
        session.ground_translation_z = snapshot.grounding.translation_z
        session.support_fraction = snapshot.grounding.support_fraction

    session.regions_gate = "REVIEW"
    session.materials_gate = "REVIEW"
    session.export_gate = "REVIEW"
    session.last_analysis_summary = (
        f"{snapshot.mesh_count} malla(s) · {snapshot.triangle_count:,} tris · "
        f"{snapshot.dimension_x:.3f} × {snapshot.dimension_y:.3f} × "
        f"{snapshot.dimension_z:.3f} m"
    )
