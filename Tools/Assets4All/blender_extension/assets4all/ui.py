from __future__ import annotations

import bpy

_GATE_ICON = {"PASS": "CHECKMARK", "REVIEW": "QUESTION", "FAIL": "ERROR", "N/A": "REMOVE"}

def _score_box(layout, title: str, value: float):
    box = layout.box(); row = box.row(align=True); row.label(text=title); row.label(text=f"{value:.1f}/100"); return box

def _gate_row(layout, label: str, value: str):
    row = layout.row(align=True); row.label(text=label); row.label(text=value, icon=_GATE_ICON.get(value, "DOT"))

class A4A_PT_Main(bpy.types.Panel):
    bl_label = "Assets4All"
    bl_idname = "A4A_PT_main"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Assets4All"
    def draw(self, context):
        layout = self.layout; session = context.scene.assets4all
        header = layout.box(); row = header.row(align=True); row.label(text="ASSETS4ALL", icon="MESH_DATA"); row.label(text="v0.1")
        if not session.session_id:
            header.label(text="AI 3D → Game Ready")
            header.label(text="Selecciona el modelo importado de Meshy/GLB/FBX.")
            action = layout.row(); action.scale_y = 1.8; action.operator("assets4all.start_session", text="CREAR SESIÓN", icon="PLAY")
            return
        info = layout.box(); info.label(text=session.display_name or session.asset_id, icon="OBJECT_DATA"); info.prop(session, "profile_id", text="Tipo"); info.label(text=f"Estado: {session.state}")
        if session.last_analysis_summary: info.label(text=session.last_analysis_summary)
        preview = layout.row(align=True); preview.scale_y = 1.35
        preview.operator("assets4all.focus_preview", text="VISTA GRANDE", icon="FULLSCREEN_ENTER")
        preview.operator("assets4all.toggle_source", text="Ocultar SOURCE" if session.show_source else "Comparar SOURCE", icon="HIDE_OFF" if session.show_source else "HIDE_ON")
        layout.separator(); layout.label(text="DECISIÓN DOBLE", icon="VIEWZOOM")
        scores = layout.row(); _score_box(scores.column(), "PVS", session.pvs); _score_box(scores.column(), "CSE", session.cse)
        result = layout.box(); row = result.row(align=True); row.label(text="Estrategia"); row.label(text=session.final_decision or "-"); row = result.row(align=True); row.label(text="Desacuerdo"); row.label(text=f"{session.score_disagreement:.1f}")
        layout.separator(); layout.label(text="ACCIONES", icon="TOOL_SETTINGS")
        action = layout.row(); action.scale_y = 1.7; action.operator("assets4all.analyse", text="ANALIZAR", icon="VIEWZOOM")
        prepare = layout.row(); prepare.scale_y = 1.9; prepare.operator("assets4all.auto_prepare", text="PREPARAR + AUTORREPARAR", icon="MODIFIER")
        if session.issues:
            issue_box = layout.box(); auto_count = sum(1 for issue in session.issues if issue.auto_repairable); fail_count = sum(1 for issue in session.issues if issue.severity == "FAIL")
            issue_box.label(text=f"Incidencias: {len(session.issues)} · automáticas {auto_count} · bloqueantes {fail_count}")
        approve = layout.row(); approve.scale_y = 1.6; approve.operator("assets4all.approve", text="APROBAR WORK", icon="CHECKMARK")
        export = layout.row(); export.scale_y = 1.4; export.enabled = False; export.label(text="EXPORTAR A UNITY · A4A-008", icon="EXPORT")

class A4A_PT_Quality(bpy.types.Panel):
    bl_label = "Calidad y Ground Integrity"
    bl_idname = "A4A_PT_quality"
    bl_parent_id = "A4A_PT_main"
    bl_space_type = "VIEW_3D"; bl_region_type = "UI"; bl_category = "Assets4All"; bl_options = {"DEFAULT_CLOSED"}
    def draw(self, context):
        layout = self.layout; session = context.scene.assets4all; gates = layout.box()
        _gate_row(gates, "Geometría", session.geometry_gate); _gate_row(gates, "Topología", session.topology_gate); _gate_row(gates, "Suelo", session.ground_gate); _gate_row(gates, "Autorreparación", session.repair_gate); _gate_row(gates, "Regiones", session.regions_gate); _gate_row(gates, "Materiales", session.materials_gate); _gate_row(gates, "Export", session.export_gate)
        ground = layout.box(); ground.label(text="Ground Integrity", icon="GRID"); ground.label(text=f"Estado: {session.ground_state}"); ground.label(text=f"Support Z: {session.robust_support_z:.5f} m"); ground.label(text=f"Apoyo: {session.support_fraction * 100.0:.2f}%")
        if session.ground_message: ground.label(text=session.ground_message)
        regions = layout.box(); regions.label(text="Region Consensus Engine", icon="MOD_EDGESPLIT"); regions.label(text=f"Regiones: {session.region_count}"); regions.label(text=f"Estables: {session.stable_region_count}"); regions.label(text=f"Ambiguas: {session.ambiguous_region_count}"); regions.label(text=f"Estabilidad media: {session.region_stability * 100.0:.1f}%")

class A4A_PT_Diagnostics(bpy.types.Panel):
    bl_label = "Diagnóstico técnico"; bl_idname = "A4A_PT_diagnostics"; bl_parent_id = "A4A_PT_main"; bl_space_type = "VIEW_3D"; bl_region_type = "UI"; bl_category = "Assets4All"; bl_options = {"DEFAULT_CLOSED"}
    def draw(self, context):
        layout = self.layout; session = context.scene.assets4all; box = layout.box()
        box.label(text=f"Mallas: {session.mesh_count}"); box.label(text=f"Vértices: {session.vertex_count:,}"); box.label(text=f"Triángulos: {session.triangle_count:,}"); box.label(text=f"Componentes: {session.connected_components}"); box.label(text=f"Simetría: {session.symmetry_score:.1f}/100"); box.label(text=f"Non-manifold: {session.nonmanifold_edges}"); box.label(text=f"Sueltos: {session.loose_vertices}"); box.label(text=f"Degeneradas: {session.degenerate_faces}"); box.label(text=f"UV ausente: {session.missing_uv_meshes}")
        rce = layout.box(); rce.label(text="RCE / Region DNA"); rce.label(text=f"Regiones: {session.region_count}"); rce.label(text=f"Estables: {session.stable_region_count}"); rce.label(text=f"Ambiguas: {session.ambiguous_region_count}"); rce.label(text=f"Persistencia media: {session.region_stability:.3f}")
        if session.last_repair_summary:
            repair = layout.box(); repair.label(text="Última autorreparación"); repair.label(text=session.last_repair_summary)

class A4A_PT_Issues(bpy.types.Panel):
    bl_label = "Incidencias"; bl_idname = "A4A_PT_issues"; bl_parent_id = "A4A_PT_main"; bl_space_type = "VIEW_3D"; bl_region_type = "UI"; bl_category = "Assets4All"; bl_options = {"DEFAULT_CLOSED"}
    def draw(self, context):
        layout = self.layout; session = context.scene.assets4all
        if not session.issues:
            layout.label(text="Sin incidencias registradas.", icon="CHECKMARK"); return
        for issue in session.issues:
            box = layout.box(); icon = "ERROR" if issue.severity == "FAIL" else "QUESTION" if issue.severity == "REVIEW" else "INFO"; box.label(text=issue.code, icon=icon); box.label(text=issue.message)
            if issue.auto_repairable: box.label(text="Assets4All intentará resolverlo automáticamente.", icon="MODIFIER")

class A4A_PT_Session(bpy.types.Panel):
    bl_label = "Sesión"; bl_idname = "A4A_PT_session"; bl_parent_id = "A4A_PT_main"; bl_space_type = "VIEW_3D"; bl_region_type = "UI"; bl_category = "Assets4All"; bl_options = {"DEFAULT_CLOSED"}
    def draw(self, context):
        layout = self.layout; session = context.scene.assets4all
        layout.prop(session, "asset_id"); layout.prop(session, "display_name"); layout.prop(session, "review_budget_seconds"); layout.label(text=f"Session: {session.session_id[:12]}…"); layout.label(text=f"Source hash: {session.source_hash[:16]}…"); layout.operator("assets4all.reset_session", text="Cerrar sesión y restaurar SOURCE", icon="X")

_CLASSES = (A4A_PT_Main, A4A_PT_Quality, A4A_PT_Diagnostics, A4A_PT_Issues, A4A_PT_Session)

def register():
    for cls in _CLASSES: bpy.utils.register_class(cls)

def unregister():
    for cls in reversed(_CLASSES): bpy.utils.unregister_class(cls)
