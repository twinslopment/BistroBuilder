from __future__ import annotations

import bpy

from .session import ROLE_KEY, SESSION_KEY


def ensure_ground_plane(context, session, span: float = 4.0) -> bpy.types.Object:
    span = max(1.0, float(span))
    existing = bpy.data.objects.get(session.ground_object_name) if session.ground_object_name else None
    if existing is not None and existing.get(SESSION_KEY) == session.session_id:
        existing.scale = (span * 0.5, span * 0.5, 1.0)
        return existing
    mesh = bpy.data.meshes.new(f"A4A_GroundMesh_{session.session_id[:8]}")
    mesh.from_pydata([(-1.0, -1.0, 0.0), (1.0, -1.0, 0.0), (1.0, 1.0, 0.0), (-1.0, 1.0, 0.0)], [], [(0, 1, 2, 3)])
    mesh.update()
    obj = bpy.data.objects.new(f"A4A_GROUND_{session.session_id[:8]}", mesh)
    obj[ROLE_KEY] = "GROUND"
    obj[SESSION_KEY] = session.session_id
    obj.hide_render = True
    obj.hide_select = True
    obj.display_type = "WIRE"
    obj.show_in_front = True
    obj.scale = (span * 0.5, span * 0.5, 1.0)
    context.scene.collection.objects.link(obj)
    session.ground_object_name = obj.name
    return obj
