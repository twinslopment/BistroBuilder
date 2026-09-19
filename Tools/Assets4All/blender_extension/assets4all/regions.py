from __future__ import annotations

import json
import math
import statistics
from dataclasses import dataclass, field
from typing import Dict, List, Mapping, Set, Tuple

import bmesh
import bpy
from mathutils import Vector

from ._bundled.assets4all_core.models import BoundaryEvidence
from ._bundled.assets4all_core.region_consensus import (
    RegionConsensusConfig,
    classify_boundaries,
)
from .session import get_work_objects

REGION_ATTRIBUTE = "assets4all_region_id"
_ORIGINAL_FACE_LAYER = "__a4a_original_face"

_VIEW_WEIGHTS: Mapping[str, float] = {
    "topology": 0.30,
    "dihedral": 1.55,
    "curvature": 0.72,
    "thickness": 0.28,
    "normals": 0.95,
    "geodesic": 0.35,
    "level": 0.10,
    "symmetry": 0.22,
    "material_uv": 0.72,
}

_CONFIG = RegionConsensusConfig(
    boundary_threshold=0.66,
    perturbation_runs=19,
    threshold_jitter=0.070,
    evidence_jitter=0.022,
    min_persistence=0.66,
)

_PROFILE_REGION_CAP = {
    "CHAIR": 42,
    "TABLE": 48,
    "SOFA": 64,
    "CABINET": 64,
    "LAMP": 48,
    "PLANT": 72,
    "KITCHEN_EQUIPMENT": 80,
    "DECORATION": 56,
    "GENERIC_PROP": 72,
}


@dataclass
class RegionRecord:
    object_name: str
    region_id: int
    face_count: int
    area: float
    centroid: Tuple[float, float, float]
    bounds_size: Tuple[float, float, float]
    aspect_ratios: Tuple[float, float, float]
    ground_distance: float
    material_indices: Tuple[int, ...]
    neighbors: Tuple[int, ...]
    stability: float
    symmetry_peer: int | None = None


@dataclass
class ObjectRegionResult:
    records: List[RegionRecord] = field(default_factory=list)
    raw_shell_count: int = 0
    virtual_shell_count: int = 0
    virtual_stitch_tolerance: float = 0.0
    virtual_merged_vertices: int = 0
    premerge_region_count: int = 0


@dataclass
class RegionAnalysisResult:
    region_count: int = 0
    stable_region_count: int = 0
    ambiguous_region_count: int = 0
    mean_stability: float = 0.0
    records: List[RegionRecord] = field(default_factory=list)
    raw_shell_count: int = 0
    virtual_shell_count: int = 0
    virtual_stitch_tolerance: float = 0.0
    virtual_merged_vertices: int = 0
    premerge_region_count: int = 0


def _clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def _normal_angle01(first: Vector, second: Vector) -> float:
    dot = max(-1.0, min(1.0, first.dot(second)))
    angle = math.acos(dot)
    return _clamp01(angle / (math.pi * 0.5))


def _vertex_curvature_proxy(vert: bmesh.types.BMVert) -> float:
    faces = list(vert.link_faces)
    if len(faces) < 2:
        return 0.10
    mean = Vector((0.0, 0.0, 0.0))
    for face in faces:
        mean += face.normal
    if mean.length_squared <= 1.0e-12:
        return 0.35
    mean.normalize()
    spread = (
        sum(
            1.0 - max(-1.0, min(1.0, face.normal.dot(mean)))
            for face in faces
        )
        / len(faces)
    )
    return _clamp01(spread * 1.7)


def _local_edge_scale(vert: bmesh.types.BMVert) -> float:
    lengths = [
        edge.calc_length()
        for edge in vert.link_edges
        if edge.calc_length() > 1.0e-9
    ]
    if not lengths:
        return 0.0
    return statistics.median(lengths)


def _edge_midpoint_world(
    obj: bpy.types.Object,
    edge: bmesh.types.BMEdge,
) -> Vector:
    return obj.matrix_world @ (
        (edge.verts[0].co + edge.verts[1].co) * 0.5
    )


def _connected_components_count(
    bm: bmesh.types.BMesh,
) -> int:
    unseen = set(bm.verts)
    count = 0
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        while stack:
            vert = stack.pop()
            for edge in vert.link_edges:
                other = edge.other_vert(vert)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
        count += 1
    return count


def _median_edge_length_world(
    obj: bpy.types.Object,
    bm: bmesh.types.BMesh,
) -> float:
    if not bm.edges:
        return 0.0
    step = max(1, len(bm.edges) // 6000)
    values = []
    matrix = obj.matrix_world
    for edge in bm.edges[::step]:
        a = matrix @ edge.verts[0].co
        b = matrix @ edge.verts[1].co
        length = (b - a).length
        if length > 1.0e-9:
            values.append(length)
    if not values:
        return 0.0
    return statistics.median(values)


def _world_span(
    obj: bpy.types.Object,
    bm: bmesh.types.BMesh,
) -> float:
    points = [obj.matrix_world @ vert.co for vert in bm.verts]
    if not points:
        return 0.0
    return max(
        max(p.x for p in points) - min(p.x for p in points),
        max(p.y for p in points) - min(p.y for p in points),
        max(p.z for p in points) - min(p.z for p in points),
        1.0e-6,
    )


def _copy_with_original_face_ids(
    obj: bpy.types.Object,
) -> bmesh.types.BMesh:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    layer = bm.faces.layers.int.get(_ORIGINAL_FACE_LAYER)
    if layer is None:
        layer = bm.faces.layers.int.new(_ORIGINAL_FACE_LAYER)
    for face in bm.faces:
        face[layer] = face.index
    return bm


def _choose_virtual_stitch_mesh(
    obj: bpy.types.Object,
) -> Tuple[bmesh.types.BMesh, int, int, float, int]:
    base = _copy_with_original_face_ids(obj)
    raw_components = _connected_components_count(base)

    if raw_components <= 32:
        return base, raw_components, raw_components, 0.0, 0

    median_edge = _median_edge_length_world(obj, base)
    span = _world_span(obj, base)
    if median_edge <= 1.0e-9 or span <= 1.0e-9:
        return base, raw_components, raw_components, 0.0, 0

    candidates = []
    for edge_fraction, span_fraction in (
        (0.025, 0.00030),
        (0.060, 0.00065),
        (0.120, 0.00120),
    ):
        tolerance = min(
            median_edge * edge_fraction,
            span * span_fraction,
        )
        if tolerance > 1.0e-9:
            candidates.append(tolerance)

    target_components = max(48, int(raw_components * 0.18))
    best = None
    best_components = raw_components
    best_tolerance = 0.0
    best_merged = 0

    for tolerance in candidates:
        trial = base.copy()
        before_vertices = len(trial.verts)
        bmesh.ops.remove_doubles(
            trial,
            verts=list(trial.verts),
            dist=tolerance,
        )
        trial.verts.ensure_lookup_table()
        trial.edges.ensure_lookup_table()
        trial.faces.ensure_lookup_table()
        trial.normal_update()
        components = _connected_components_count(trial)
        merged = max(0, before_vertices - len(trial.verts))

        if components < best_components:
            if best is not None:
                best.free()
            best = trial
            best_components = components
            best_tolerance = tolerance
            best_merged = merged
        else:
            trial.free()

        if components <= target_components:
            break

    if best is None:
        return base, raw_components, raw_components, 0.0, 0

    reduction = (
        raw_components - best_components
    ) / max(raw_components, 1)

    if reduction < 0.08:
        best.free()
        return base, raw_components, raw_components, 0.0, 0

    base.free()
    return (
        best,
        raw_components,
        best_components,
        best_tolerance,
        best_merged,
    )


def _build_symmetry_lookup(
    obj: bpy.types.Object,
    bm: bmesh.types.BMesh,
    quantum: float,
) -> Set[Tuple[int, int, int]]:
    result: Set[Tuple[int, int, int]] = set()
    for edge in bm.edges:
        point = _edge_midpoint_world(obj, edge)
        result.add(
            (
                round(point.x / quantum),
                round(point.y / quantum),
                round(point.z / quantum),
            )
        )
    return result


def _edge_evidence(
    obj: bpy.types.Object,
    edge: bmesh.types.BMEdge,
    vertex_curvature: Dict[int, float],
    vertex_scale: Dict[int, float],
    symmetry_lookup: Set[Tuple[int, int, int]],
    center_x: float,
    quantum: float,
    min_z: float,
    height: float,
) -> BoundaryEvidence:
    linked = list(edge.link_faces)

    if len(linked) > 2:
        topology = 0.90
    elif len(linked) == 1:
        topology = 0.12
    else:
        topology = 0.0

    if len(linked) == 2:
        dihedral = _normal_angle01(
            linked[0].normal,
            linked[1].normal,
        )
        normal_signal = _clamp01(
            (1.0 - linked[0].normal.dot(linked[1].normal)) * 0.58
        )
        material_signal = (
            1.0
            if linked[0].material_index != linked[1].material_index
            else 0.0
        )
    else:
        dihedral = 0.18
        normal_signal = 0.15
        material_signal = 0.0

    curvature = max(
        vertex_curvature.get(edge.verts[0].index, 0.0),
        vertex_curvature.get(edge.verts[1].index, 0.0),
    )

    edge_length = max(edge.calc_length(), 1.0e-9)
    local_scale = max(
        1.0e-9,
        (
            vertex_scale.get(edge.verts[0].index, edge_length)
            + vertex_scale.get(edge.verts[1].index, edge_length)
        )
        * 0.5,
    )
    ratio = max(
        edge_length / local_scale,
        local_scale / edge_length,
    )
    thickness_proxy = _clamp01(
        math.log(max(1.0, ratio), 5.0)
    )

    valence_delta = abs(
        len(edge.verts[0].link_edges)
        - len(edge.verts[1].link_edges)
    )
    geodesic = _clamp01(valence_delta / 6.0)

    midpoint = _edge_midpoint_world(obj, edge)
    normalized_z = _clamp01(
        (midpoint.z - min_z) / max(height, 1.0e-6)
    )
    level = _clamp01(abs(normalized_z - 0.5) * 0.45)

    mirrored = Vector(
        (
            2.0 * center_x - midpoint.x,
            midpoint.y,
            midpoint.z,
        )
    )
    mirror_key = (
        round(mirrored.x / quantum),
        round(mirrored.y / quantum),
        round(mirrored.z / quantum),
    )
    symmetry = (
        0.62
        if mirror_key in symmetry_lookup
        else 0.18
    )

    uv_signal = 0.12 if edge.seam else 0.0
    material_uv = max(material_signal, uv_signal)

    return BoundaryEvidence(
        edge_id=edge.index,
        topology=topology,
        dihedral=dihedral,
        curvature=curvature,
        thickness=thickness_proxy,
        normals=normal_signal,
        geodesic=geodesic,
        level=level,
        symmetry=symmetry,
        material_uv=material_uv,
    )


def _initial_face_regions(
    bm: bmesh.types.BMesh,
    boundary_edges: Set[int],
) -> Dict[int, int]:
    unseen = set(bm.faces)
    labels: Dict[int, int] = {}
    region_id = 0

    while unseen:
        seed = unseen.pop()
        stack = [seed]
        labels[seed.index] = region_id

        while stack:
            face = stack.pop()
            for edge in face.edges:
                if edge.index in boundary_edges:
                    continue
                for neighbor in edge.link_faces:
                    if neighbor is face or neighbor not in unseen:
                        continue
                    unseen.remove(neighbor)
                    labels[neighbor.index] = region_id
                    stack.append(neighbor)

        region_id += 1

    return labels


def _region_faces(
    labels: Dict[int, int],
) -> Dict[int, List[int]]:
    result: Dict[int, List[int]] = {}
    for face_index, region_id in labels.items():
        result.setdefault(region_id, []).append(face_index)
    return result


def _merge_micro_regions(
    bm: bmesh.types.BMesh,
    labels: Dict[int, int],
    boundary_strength: Dict[int, float],
    target_regions: int,
) -> Dict[int, int]:
    if not labels:
        return labels

    target_regions = max(8, int(target_regions))

    for _ in range(10):
        region_faces = _region_faces(labels)
        if len(region_faces) <= target_regions:
            break

        average_faces = len(bm.faces) / max(len(region_faces), 1)
        minimum = max(
            10,
            min(
                96,
                int(
                    max(
                        average_faces * 0.55,
                        len(bm.faces) / max(target_regions, 1) * 0.22,
                    )
                ),
            ),
        )

        candidates = sorted(
            (
                (len(faces), rid)
                for rid, faces in region_faces.items()
                if len(faces) < minimum
            )
        )
        if not candidates:
            candidates = sorted(
                (len(faces), rid)
                for rid, faces in region_faces.items()
            )[: max(1, len(region_faces) // 6)]

        proposals: Dict[int, int] = {}
        for _, rid in candidates:
            neighbor_scores: Dict[int, List[float]] = {}
            for face_index in region_faces.get(rid, []):
                face = bm.faces[face_index]
                for edge in face.edges:
                    for neighbor in edge.link_faces:
                        nrid = labels.get(neighbor.index, rid)
                        if nrid == rid:
                            continue
                        neighbor_scores.setdefault(nrid, []).append(
                            boundary_strength.get(edge.index, 0.45)
                        )

            if not neighbor_scores:
                continue

            target = min(
                neighbor_scores,
                key=lambda nrid: (
                    sum(neighbor_scores[nrid]) / len(neighbor_scores[nrid]),
                    -len(region_faces.get(nrid, [])),
                ),
            )
            mean_boundary = (
                sum(neighbor_scores[target]) / len(neighbor_scores[target])
            )

            if mean_boundary <= 0.78:
                proposals[rid] = target

        if not proposals:
            break

        changed = False
        for face_index, rid in list(labels.items()):
            target = proposals.get(rid)
            if target is not None and target != rid:
                labels[face_index] = target
                changed = True

        if not changed:
            break

    remap = {
        rid: index
        for index, rid in enumerate(sorted(set(labels.values())))
    }
    return {
        face_index: remap[rid]
        for face_index, rid in labels.items()
    }


def _write_face_attribute(
    mesh: bpy.types.Mesh,
    labels: Dict[int, int],
) -> None:
    attribute = mesh.attributes.get(REGION_ATTRIBUTE)
    if (
        attribute is not None
        and (
            attribute.domain != "FACE"
            or attribute.data_type != "INT"
        )
    ):
        mesh.attributes.remove(attribute)
        attribute = None

    if attribute is None:
        attribute = mesh.attributes.new(REGION_ATTRIBUTE, "INT", "FACE")

    for polygon in mesh.polygons:
        attribute.data[polygon.index].value = int(
            labels.get(polygon.index, -1)
        )


def _records_for_object(
    obj: bpy.types.Object,
    bm: bmesh.types.BMesh,
    labels: Dict[int, int],
    consensus_by_edge: Dict[int, object],
) -> List[RegionRecord]:
    region_faces: Dict[int, List[bmesh.types.BMFace]] = {}
    for face in bm.faces:
        if face.index not in labels:
            continue
        region_faces.setdefault(labels[face.index], []).append(face)

    records: List[RegionRecord] = []

    for rid, faces in sorted(region_faces.items()):
        vertices = {vert for face in faces for vert in face.verts}
        points = [obj.matrix_world @ vert.co for vert in vertices]
        if not points:
            continue

        minimum = Vector(
            (
                min(p.x for p in points),
                min(p.y for p in points),
                min(p.z for p in points),
            )
        )
        maximum = Vector(
            (
                max(p.x for p in points),
                max(p.y for p in points),
                max(p.z for p in points),
            )
        )
        size = maximum - minimum
        sorted_dims = sorted(
            (
                max(size.x, 1.0e-6),
                max(size.y, 1.0e-6),
                max(size.z, 1.0e-6),
            ),
            reverse=True,
        )
        aspects = (
            sorted_dims[0] / sorted_dims[1],
            sorted_dims[1] / sorted_dims[2],
            sorted_dims[0] / sorted_dims[2],
        )

        area = 0.0
        weighted_centroid = Vector((0.0, 0.0, 0.0))
        materials = set()
        neighbor_ids = set()
        border_stability: List[float] = []
        face_set = set(faces)

        for face in faces:
            world_center = obj.matrix_world @ face.calc_center_median()
            face_area = max(face.calc_area(), 1.0e-12)
            area += face_area
            weighted_centroid += world_center * face_area
            materials.add(face.material_index)

            for edge in face.edges:
                for neighbor in edge.link_faces:
                    if neighbor in face_set:
                        continue
                    nrid = labels.get(neighbor.index)
                    if nrid is None:
                        continue
                    neighbor_ids.add(nrid)
                    consensus = consensus_by_edge.get(edge.index)
                    if consensus is not None:
                        border_stability.append(consensus.persistence)

        centroid = weighted_centroid / max(area, 1.0e-12)
        stability = (
            sum(border_stability) / len(border_stability)
            if border_stability
            else 0.62
        )

        records.append(
            RegionRecord(
                object_name=obj.name,
                region_id=rid,
                face_count=len(faces),
                area=area,
                centroid=(centroid.x, centroid.y, centroid.z),
                bounds_size=(size.x, size.y, size.z),
                aspect_ratios=aspects,
                ground_distance=minimum.z,
                material_indices=tuple(sorted(materials)),
                neighbors=tuple(sorted(neighbor_ids)),
                stability=stability,
            )
        )

    return records


def _assign_symmetry_peers(records: List[RegionRecord]) -> None:
    if len(records) < 2:
        return

    total_area = sum(max(record.area, 1.0e-9) for record in records)
    center_x = (
        sum(
            record.centroid[0] * max(record.area, 1.0e-9)
            for record in records
        )
        / total_area
    )

    used: Set[int] = set()

    for record in records:
        if record.region_id in used:
            continue

        target_x = 2.0 * center_x - record.centroid[0]
        best = None
        best_score = math.inf

        for candidate in records:
            if (
                candidate.region_id == record.region_id
                or candidate.region_id in used
            ):
                continue

            area_ratio = (
                max(record.area, candidate.area)
                / max(min(record.area, candidate.area), 1.0e-9)
            )
            if area_ratio > 1.8:
                continue

            distance = (
                abs(candidate.centroid[0] - target_x)
                + abs(candidate.centroid[1] - record.centroid[1])
                + abs(candidate.centroid[2] - record.centroid[2])
            )
            size_delta = sum(
                abs(a - b)
                for a, b in zip(candidate.bounds_size, record.bounds_size)
            )
            score = distance + size_delta * 0.6

            if score < best_score:
                best_score = score
                best = candidate

        reference = max(max(record.bounds_size), 0.05)
        if best is not None and best_score <= reference * 0.45:
            record.symmetry_peer = best.region_id
            best.symmetry_peer = record.region_id
            used.add(record.region_id)
            used.add(best.region_id)


def _target_region_budget(profile_id: str, face_count: int) -> int:
    complexity_target = max(
        18,
        min(88, int(math.sqrt(max(face_count, 1)) * 0.42)),
    )
    profile_cap = _PROFILE_REGION_CAP.get(profile_id, 72)
    return min(complexity_target, profile_cap)


def analyse_regions_for_object(
    obj: bpy.types.Object,
    profile_id: str,
) -> ObjectRegionResult:
    result = ObjectRegionResult()

    if obj.type != "MESH" or obj.data is None or not obj.data.polygons:
        return result

    bm = None
    try:
        (
            bm,
            result.raw_shell_count,
            result.virtual_shell_count,
            result.virtual_stitch_tolerance,
            result.virtual_merged_vertices,
        ) = _choose_virtual_stitch_mesh(obj)

        bm.normal_update()
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()

        world_points = [obj.matrix_world @ vert.co for vert in bm.verts]
        min_z = min(point.z for point in world_points)
        max_z = max(point.z for point in world_points)
        center_x = (
            min(point.x for point in world_points)
            + max(point.x for point in world_points)
        ) * 0.5

        span = max(
            max(point.x for point in world_points) - min(point.x for point in world_points),
            max(point.y for point in world_points) - min(point.y for point in world_points),
            max_z - min_z,
            1.0e-5,
        )
        quantum = span / 96.0

        vertex_curvature = {
            vert.index: _vertex_curvature_proxy(vert)
            for vert in bm.verts
        }
        vertex_scale = {
            vert.index: _local_edge_scale(vert)
            for vert in bm.verts
        }
        symmetry_lookup = _build_symmetry_lookup(obj, bm, quantum)

        evidences = [
            _edge_evidence(
                obj,
                edge,
                vertex_curvature,
                vertex_scale,
                symmetry_lookup,
                center_x,
                quantum,
                min_z,
                max_z - min_z,
            )
            for edge in bm.edges
        ]

        consensus = classify_boundaries(
            evidences,
            _CONFIG,
            _VIEW_WEIGHTS,
        )
        consensus_by_edge = {item.edge_id: item for item in consensus}
        boundary_edges = {
            item.edge_id
            for item in consensus
            if item.is_boundary
        }
        boundary_strength = {
            item.edge_id: item.consensus
            for item in consensus
        }

        labels = _initial_face_regions(bm, boundary_edges)
        result.premerge_region_count = len(set(labels.values()))

        labels = _merge_micro_regions(
            bm,
            labels,
            boundary_strength,
            _target_region_budget(profile_id, len(bm.faces)),
        )

        original_layer = bm.faces.layers.int.get(_ORIGINAL_FACE_LAYER)
        original_labels: Dict[int, int] = {}
        if original_layer is not None:
            for face in bm.faces:
                original_index = int(face[original_layer])
                original_labels[original_index] = labels.get(face.index, -1)

        _write_face_attribute(obj.data, original_labels)

        records = _records_for_object(
            obj,
            bm,
            labels,
            consensus_by_edge,
        )
        _assign_symmetry_peers(records)
        result.records = records
        return result

    finally:
        if bm is not None:
            bm.free()


def analyse_regions(context, session) -> RegionAnalysisResult:
    result = RegionAnalysisResult()
    global_records: List[RegionRecord] = []
    max_tolerance = 0.0

    for obj in get_work_objects(session):
        if obj.type != "MESH":
            continue

        object_result = analyse_regions_for_object(obj, session.profile_id)
        global_records.extend(object_result.records)
        result.raw_shell_count += object_result.raw_shell_count
        result.virtual_shell_count += object_result.virtual_shell_count
        result.virtual_merged_vertices += object_result.virtual_merged_vertices
        result.premerge_region_count += object_result.premerge_region_count
        max_tolerance = max(
            max_tolerance,
            object_result.virtual_stitch_tolerance,
        )

    result.virtual_stitch_tolerance = max_tolerance
    result.records = global_records
    result.region_count = len(global_records)

    if not global_records:
        result.ambiguous_region_count = 1
        return result

    result.mean_stability = (
        sum(record.stability for record in global_records)
        / len(global_records)
    )

    for record in global_records:
        stable = record.stability >= 0.68 and record.face_count >= 12
        if stable:
            result.stable_region_count += 1
        else:
            result.ambiguous_region_count += 1

    total_faces = sum(record.face_count for record in global_records)
    if result.region_count == 1 and total_faces > 600:
        result.ambiguous_region_count = max(1, result.ambiguous_region_count)
        result.stable_region_count = 0

    return result


def apply_region_result(session, result: RegionAnalysisResult) -> None:
    session.region_count = result.region_count
    session.stable_region_count = result.stable_region_count
    session.ambiguous_region_count = result.ambiguous_region_count
    session.region_stability = result.mean_stability
    session.rce_raw_shell_count = result.raw_shell_count
    session.rce_virtual_shell_count = result.virtual_shell_count
    session.rce_virtual_stitch_tolerance = result.virtual_stitch_tolerance
    session.rce_virtual_merged_vertices = result.virtual_merged_vertices
    session.rce_premerge_region_count = result.premerge_region_count

    session.regions_json = json.dumps(
        [
            {
                "object": record.object_name,
                "regionId": record.region_id,
                "faces": record.face_count,
                "area": record.area,
                "centroid": record.centroid,
                "boundsSize": record.bounds_size,
                "aspectRatios": record.aspect_ratios,
                "groundDistance": record.ground_distance,
                "materials": record.material_indices,
                "neighbors": record.neighbors,
                "stability": record.stability,
                "symmetryPeer": record.symmetry_peer,
            }
            for record in result.records
        ],
        separators=(",", ":"),
    )

    existing = [
        issue
        for issue in session.issues
        if issue.code == "RCE_AMBIGUITY"
    ]
    for issue in existing:
        index = next(
            (
                i
                for i, item in enumerate(session.issues)
                if item == issue
            ),
            -1,
        )
        if index >= 0:
            session.issues.remove(index)

    if result.region_count == 0:
        session.regions_gate = "FAIL"
    elif (
        result.ambiguous_region_count == 0
        and result.mean_stability >= 0.68
    ):
        session.regions_gate = "PASS"
    else:
        session.regions_gate = "REVIEW"
        issue = session.issues.add()
        issue.severity = "REVIEW"
        issue.code = "RCE_AMBIGUITY"
        issue.message = (
            f"RCE reconstruyó conectividad virtual "
            f"{result.raw_shell_count} -> {result.virtual_shell_count} shell(s), "
            f"y consolidó {result.premerge_region_count} micro-regiones "
            f"en {result.region_count}. "
            f"{result.ambiguous_region_count} siguen ambiguas."
        )
        issue.auto_repairable = True
