from __future__ import annotations

import bpy
from bpy.props import StringProperty

from .analysis import analyse_session, apply_snapshot_to_session
from .ground_view import ensure_ground_plane
from .manifest import write_manifest_text
from .properties import is_floor_profile
from .regions import analyse_regions, apply_region_result
from .repair import run_self_healing_repair
from .session import capture_session, get_work_objects, reset_session, set_source_visible

class A4A_OT_StartSession(bpy.types.Operator):
    bl_idname = "assets4all.start_session"
    bl_label = "Crear sesión Assets4All"
    bl_options = {"REGISTER", "UNDO"}
    asset_id: StringProperty(name="Asset ID", default="")
    display_name: StringProperty(name="Nombre", default="")
    def invoke(self, context, event):
        selected = [obj for obj in context.selected_objects if obj.type == "MESH"]
        if selected and not self.asset_id:
            self.asset_id = selected[0].name
            self.display_name = selected[0].name.replace("_", " ")
        return context.window_manager.invoke_props_dialog(self, width=440)
    def execute(self, context):
        try:
            count = capture_session(context, self.asset_id, self.display_name)
            ensure_ground_plane(context, context.scene.assets4all, 4.0)
            self.report({"INFO"}, f"Assets4All: sesión creada con {count} objeto(s).")
            return {"FINISHED"}
        except Exception as exc:
            self.report({"ERROR"}, str(exc)); return {"CANCELLED"}

class A4A_OT_Analyse(bpy.types.Operator):
    bl_idname = "assets4all.analyse"
    bl_label = "Analizar asset"
    bl_options = {"REGISTER"}
    def execute(self, context):
        session = context.scene.assets4all
        if not session.session_id:
            self.report({"ERROR"}, "No existe una sesión Assets4All."); return {"CANCELLED"}
        try:
            snapshot = analyse_session(context, session)
            apply_snapshot_to_session(session, snapshot)
            region_result = analyse_regions(context, session)
            apply_region_result(session, region_result)
            session.state = "ANALYSED"
            ensure_ground_plane(context, session, max(session.dimension_x, session.dimension_y, 1.0) * 2.6)
            write_manifest_text(session)
            self.report(
                {"INFO"},
                f"PVS {session.pvs:.1f} · CSE {session.cse:.1f} · "
                f"RCE {session.region_count} regiones · {session.final_decision}",
            )
            return {"FINISHED"}
        except Exception as exc:
            self.report({"ERROR"}, f"No se pudo analizar: {exc}"); return {"CANCELLED"}

class A4A_OT_AutoPrepare(bpy.types.Operator):
    bl_idname = "assets4all.auto_prepare"
    bl_label = "Preparar automáticamente"
    bl_options = {"REGISTER", "UNDO"}
    def execute(self, context):
        session = context.scene.assets4all
        if not session.session_id:
            self.report({"ERROR"}, "No existe una sesión Assets4All."); return {"CANCELLED"}
        try:
            result = run_self_healing_repair(context, session)
            region_result = analyse_regions(context, session)
            apply_region_result(session, region_result)
            if session.regions_gate == "REVIEW" and session.state == "PREPARED":
                session.state = "REVIEW"
            ensure_ground_plane(context, session, max(session.dimension_x, session.dimension_y, 1.0) * 2.6)
            write_manifest_text(session)
            self.report({"INFO" if result.accepted else "WARNING"}, result.summary)
            return {"FINISHED"}
        except Exception as exc:
            self.report({"ERROR"}, f"Autorreparación fallida y revertida: {exc}"); return {"CANCELLED"}

class A4A_OT_ToggleSource(bpy.types.Operator):
    bl_idname = "assets4all.toggle_source"
    bl_label = "Comparar SOURCE / WORK"
    def execute(self, context):
        session = context.scene.assets4all
        set_source_visible(session, not session.show_source)
        return {"FINISHED"}

class A4A_OT_FocusPreview(bpy.types.Operator):
    bl_idname = "assets4all.focus_preview"
    bl_label = "Abrir vista grande"
    def execute(self, context):
        if context.area is None or context.area.type != "VIEW_3D":
            self.report({"WARNING"}, "Ejecuta esta acción desde una Vista 3D."); return {"CANCELLED"}
        objects = get_work_objects(context.scene.assets4all)
        if not objects: return {"CANCELLED"}
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.hide_viewport = False; obj.select_set(True)
        context.view_layer.objects.active = objects[0]
        context.space_data.shading.type = "MATERIAL"
        context.space_data.overlay.show_floor = True
        context.space_data.overlay.show_axis_x = True
        context.space_data.overlay.show_axis_y = True
        try: bpy.ops.view3d.view_selected(use_all_regions=False)
        except RuntimeError: pass
        try: bpy.ops.screen.screen_full_area(use_hide_panels=False)
        except RuntimeError: pass
        return {"FINISHED"}

class A4A_OT_Approve(bpy.types.Operator):
    bl_idname = "assets4all.approve"
    bl_label = "Aprobar WORK"
    def execute(self, context):
        session = context.scene.assets4all
        blocking = [issue for issue in session.issues if issue.severity == "FAIL"]
        if blocking:
            self.report({"ERROR"}, f"Hay {len(blocking)} fallo(s) bloqueante(s)."); return {"CANCELLED"}
        if is_floor_profile(session.profile_id) and session.ground_gate != "PASS":
            self.report({"ERROR"}, "Ground Integrity debe estar en PASS antes de aprobar."); return {"CANCELLED"}
        if session.regions_gate == "FAIL":
            self.report({"ERROR"}, "RCE no ha podido construir regiones válidas."); return {"CANCELLED"}
        session.state = "APPROVED"
        write_manifest_text(session)
        self.report({"INFO"}, "WORK aprobado. El exportador canónico se conectará en A4A-007/008.")
        return {"FINISHED"}

class A4A_OT_Reset(bpy.types.Operator):
    bl_idname = "assets4all.reset_session"
    bl_label = "Cerrar sesión Assets4All"
    bl_options = {"REGISTER"}
    def invoke(self, context, event): return context.window_manager.invoke_confirm(self, event)
    def execute(self, context):
        reset_session(context, preserve_sources=True)
        self.report({"INFO"}, "Sesión cerrada; SOURCE restaurado.")
        return {"FINISHED"}

_CLASSES = (
    A4A_OT_StartSession,
    A4A_OT_Analyse,
    A4A_OT_AutoPrepare,
    A4A_OT_ToggleSource,
    A4A_OT_FocusPreview,
    A4A_OT_Approve,
    A4A_OT_Reset,
)

def register():
    for cls in _CLASSES: bpy.utils.register_class(cls)

def unregister():
    for cls in reversed(_CLASSES): bpy.utils.unregister_class(cls)
