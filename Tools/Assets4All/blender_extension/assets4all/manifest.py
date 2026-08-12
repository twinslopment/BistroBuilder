from __future__ import annotations

import json
from datetime import datetime, timezone

import bpy

from .session import get_source_objects, get_work_objects


def build_manifest(session) -> dict:
    return {
        "schemaVersion": "0.1",
        "generator": {"name": "Assets4All", "version": "0.1.0"},
        "identity": {"assetId": session.asset_id, "displayName": session.display_name, "profile": session.profile_id},
        "session": {
            "sessionId": session.session_id,
            "state": session.state,
            "sourceHash": session.source_hash,
            "sourceObjects": [obj.name for obj in get_source_objects(session)],
            "workObjects": [obj.name for obj in get_work_objects(session)],
        },
        "dimensions": {"detectedM": [session.dimension_x, session.dimension_y, session.dimension_z], "minZ": session.min_z, "maxZ": session.max_z},
        "geometry": {
            "meshCount": session.mesh_count,
            "vertices": session.vertex_count,
            "triangles": session.triangle_count,
            "materialSlots": session.material_slot_count,
            "missingUvMeshes": session.missing_uv_meshes,
            "boundaryEdges": session.boundary_edges,
            "nonmanifoldEdges": session.nonmanifold_edges,
            "wireEdges": session.wire_edges,
            "looseVertices": session.loose_vertices,
            "degenerateFaces": session.degenerate_faces,
            "connectedComponents": session.connected_components,
            "tinyComponents": session.tiny_components,
            "symmetryScore": session.symmetry_score,
        },
        "grounding": {
            "state": session.ground_state,
            "robustSupportZ": session.robust_support_z,
            "supportFraction": session.support_fraction,
            "message": session.ground_message,
        },
        "decision": {
            "processingViabilityScore": session.pvs,
            "conversionSuccessEstimate": session.cse,
            "disagreement": session.score_disagreement,
            "final": session.final_decision,
        },
        "qualityGates": {
            "geometry": session.geometry_gate,
            "topology": session.topology_gate,
            "ground": session.ground_gate,
            "repair": session.repair_gate,
            "regions": session.regions_gate,
            "materials": session.materials_gate,
            "export": session.export_gate,
        },
        "repair": {"passes": session.repair_passes, "changes": session.repair_changes, "summary": session.last_repair_summary},
        "issues": [{"severity": issue.severity, "code": issue.code, "message": issue.message, "autoRepairable": issue.auto_repairable} for issue in session.issues],
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }


def write_manifest_text(session) -> bpy.types.Text:
    name = f"Assets4All_{session.asset_id}.asset4all.json"
    text = bpy.data.texts.get(name) or bpy.data.texts.new(name)
    text.clear()
    text.write(json.dumps(build_manifest(session), indent=2, ensure_ascii=False))
    session.manifest_text_name = text.name
    return text
