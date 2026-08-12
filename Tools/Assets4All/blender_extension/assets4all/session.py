from __future__ import annotations

import hashlib
import re
import uuid
from typing import Dict, Iterable, List, Sequence

import bpy

ROLE_KEY = "assets4all_role"
SESSION_KEY = "assets4all_session_id"
SOURCE_UID_KEY = "assets4all_source_uid"
PREV_HIDE_VIEWPORT_KEY = "assets4all_prev_hide_viewport"
PREV_HIDE_RENDER_KEY = "assets4all_prev_hide_render"
PREV_HIDE_SELECT_KEY = "assets4all_prev_hide_select"


def _safe_id(text: str) -> str:
    value = re.sub(r"[^A-Za-z0-9_]+", "_", (text or "").strip()).strip("_")
    return value or "Asset"


def _descendants(obj: bpy.types.Object) -> Iterable[bpy.types.Object]:
    for child in obj.children:
        yield child
        yield from _descendants(child)


def collect_source_candidates(context) -> List[bpy.types.Object]:
    result: Dict[int, bpy.types.Object] = {}
    for selected in context.selected_objects:
        if selected.get(ROLE_KEY) in {"SOURCE", "WORK", "GROUND"}:
            continue
        result[selected.as_pointer()] = selected
        for child in _descendants(selected):
            if child.get(ROLE_KEY) not in {"SOURCE", "WORK", "GROUND"}:
                result[child.as_pointer()] = child
    return list(result.values())


def _ensure_collection(scene: bpy.types.Scene, name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        scene.collection.children.link(collection)
    return collection


def _hash_sources(objects: Sequence[bpy.types.Object]) -> str:
    digest = hashlib.sha256()
    for obj in sorted(objects, key=lambda item: item.name_full):
        digest.update(obj.name_full.encode("utf-8", "replace"))
        for row in obj.matrix_world:
            for component in row:
                digest.update(f"{component:.9f};".encode("ascii"))
        if obj.type != "MESH" or obj.data is None:
            continue
        mesh = obj.data
        digest.update(f"v{len(mesh.vertices)}e{len(mesh.edges)}p{len(mesh.polygons)}".encode("ascii"))
        step = max(1, len(mesh.vertices) // 4096)
        for index in range(0, len(mesh.vertices), step):
            vertex = mesh.vertices[index]
            digest.update(f"{vertex.co.x:.7f},{vertex.co.y:.7f},{vertex.co.z:.7f};".encode("ascii"))
    return digest.hexdigest()


def _clear_refs(collection_property) -> None:
    while len(collection_property):
        collection_property.remove(len(collection_property) - 1)


def _add_ref(collection_property, obj: bpy.types.Object, original_name: str) -> None:
    ref = collection_property.add()
    ref.object = obj
    ref.uid = obj.get(SOURCE_UID_KEY, "")
    ref.original_name = original_name


def get_work_objects(session) -> List[bpy.types.Object]:
    result = []
    for ref in session.work_objects:
        obj = ref.object
        if obj is not None and obj.get(SESSION_KEY) == session.session_id and obj.get(ROLE_KEY) == "WORK":
            result.append(obj)
    return result


def get_source_objects(session) -> List[bpy.types.Object]:
    result = []
    for ref in session.source_objects:
        obj = ref.object
        if obj is not None and obj.get(SESSION_KEY) == session.session_id and obj.get(ROLE_KEY) == "SOURCE":
            result.append(obj)
    return result


def capture_session(context, asset_id: str, display_name: str) -> int:
    scene = context.scene
    session = scene.assets4all
    candidates = collect_source_candidates(context)
    mesh_candidates = [obj for obj in candidates if obj.type == "MESH"]
    if not mesh_candidates:
        raise RuntimeError("Selecciona un modelo con al menos una malla antes de crear la sesión.")

    if session.session_id:
        reset_session(context, preserve_sources=True)

    sid = uuid.uuid4().hex
    asset_id = _safe_id(asset_id or mesh_candidates[0].name)
    session.session_id = sid
    session.asset_id = asset_id
    session.display_name = display_name.strip() if display_name.strip() else asset_id.replace("_", " ")
    session.state = "SOURCE_CAPTURED"
    session.source_hash = _hash_sources(candidates)
    session.source_collection_name = f"A4A_SOURCE_{sid[:8]}"
    session.work_collection_name = f"A4A_WORK_{sid[:8]}"

    source_collection = _ensure_collection(scene, session.source_collection_name)
    work_collection = _ensure_collection(scene, session.work_collection_name)
    source_collection.hide_render = True

    _clear_refs(session.source_objects)
    _clear_refs(session.work_objects)
    _clear_refs(session.issues)

    source_set = set(candidates)
    copy_map: Dict[bpy.types.Object, bpy.types.Object] = {}

    for source in candidates:
        uid = uuid.uuid4().hex
        source[SOURCE_UID_KEY] = uid
        source[SESSION_KEY] = sid
        source[ROLE_KEY] = "SOURCE"
        source[PREV_HIDE_VIEWPORT_KEY] = bool(source.hide_viewport)
        source[PREV_HIDE_RENDER_KEY] = bool(source.hide_render)
        source[PREV_HIDE_SELECT_KEY] = bool(source.hide_select)
        if source_collection.objects.get(source.name) is None:
            source_collection.objects.link(source)
        source.hide_viewport = True
        source.hide_render = True
        source.hide_select = True
        _add_ref(session.source_objects, source, source.name)

        work = source.copy()
        if source.data is not None:
            work.data = source.data.copy()
        work.animation_data_clear()
        work.name = f"A4A_WORK__{source.name}"
        work[SESSION_KEY] = sid
        work[ROLE_KEY] = "WORK"
        work[SOURCE_UID_KEY] = uid
        work.hide_viewport = False
        work.hide_render = False
        work.hide_select = False
        work_collection.objects.link(work)
        copy_map[source] = work
        _add_ref(session.work_objects, work, source.name)

    for source, work in copy_map.items():
        if source.parent in source_set:
            work.parent = copy_map[source.parent]
            work.matrix_parent_inverse = source.matrix_parent_inverse.copy()
        else:
            work.parent = None
        work.matrix_world = source.matrix_world.copy()

    bpy.ops.object.select_all(action="DESELECT")
    for work in copy_map.values():
        work.select_set(True)
    context.view_layer.objects.active = next(iter(copy_map.values()))
    return len(copy_map)


def set_source_visible(session, visible: bool) -> None:
    session.show_source = visible
    for obj in get_source_objects(session):
        obj.hide_viewport = not visible
        obj.hide_render = True
        obj.hide_select = True


def top_level_work_objects(session) -> List[bpy.types.Object]:
    work = get_work_objects(session)
    work_set = set(work)
    return [obj for obj in work if obj.parent not in work_set]


def translate_work_z(session, delta: float) -> None:
    for obj in top_level_work_objects(session):
        obj.location.z += delta


def reset_session(context, preserve_sources: bool = True) -> None:
    session = context.scene.assets4all
    sid = session.session_id
    if not sid:
        return

    for obj in list(get_work_objects(session)):
        data = obj.data if obj.type == "MESH" else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)

    ground = bpy.data.objects.get(session.ground_object_name) if session.ground_object_name else None
    if ground is not None and ground.get(SESSION_KEY) == sid:
        data = ground.data if ground.type == "MESH" else None
        bpy.data.objects.remove(ground, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)

    for name in (session.work_collection_name, session.source_collection_name):
        collection = bpy.data.collections.get(name)
        if collection is not None:
            try:
                context.scene.collection.children.unlink(collection)
            except RuntimeError:
                pass
            if collection.users == 0:
                bpy.data.collections.remove(collection)

    for source in list(get_source_objects(session)):
        source.hide_viewport = bool(source.get(PREV_HIDE_VIEWPORT_KEY, False))
        source.hide_render = bool(source.get(PREV_HIDE_RENDER_KEY, False))
        source.hide_select = bool(source.get(PREV_HIDE_SELECT_KEY, False))
        for key in (ROLE_KEY, SESSION_KEY, SOURCE_UID_KEY, PREV_HIDE_VIEWPORT_KEY, PREV_HIDE_RENDER_KEY, PREV_HIDE_SELECT_KEY):
            if key in source:
                del source[key]

    _clear_refs(session.source_objects)
    _clear_refs(session.work_objects)
    _clear_refs(session.issues)
    session.session_id = ""
    session.state = "EMPTY"
    session.source_hash = ""
    session.source_collection_name = ""
    session.work_collection_name = ""
    session.ground_object_name = ""
    session.pvs = 0.0
    session.cse = 0.0
    session.final_decision = "-"
    session.last_analysis_summary = ""
    session.last_repair_summary = ""
