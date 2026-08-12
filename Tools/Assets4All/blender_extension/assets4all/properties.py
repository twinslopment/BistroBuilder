from __future__ import annotations

import bpy
from bpy.props import BoolProperty, CollectionProperty, EnumProperty, FloatProperty, IntProperty, PointerProperty, StringProperty

PROFILE_ITEMS = (
    ("GENERIC_PROP", "Objeto genérico", "Objeto estático sin semántica especializada"),
    ("CHAIR", "Silla", "Silla o asiento individual"),
    ("TABLE", "Mesa", "Mesa de comedor, auxiliar o de trabajo"),
    ("SOFA", "Sofá / banco", "Sofás, bancos y asientos múltiples"),
    ("CABINET", "Mueble", "Armarios, estanterías y muebles de almacenaje"),
    ("LAMP", "Lámpara", "Lámparas y luminarias estáticas"),
    ("PLANT", "Planta", "Vegetación decorativa y macetas"),
    ("KITCHEN_EQUIPMENT", "Equipo de cocina", "Equipamiento estático de cocina"),
    ("DECORATION", "Decoración", "Props decorativos"),
)

FLOOR_PROFILES = {"GENERIC_PROP", "CHAIR", "TABLE", "SOFA", "CABINET", "PLANT", "KITCHEN_EQUIPMENT", "DECORATION"}

STATE_ITEMS = (
    ("EMPTY", "Sin sesión", ""),
    ("SOURCE_CAPTURED", "Source capturado", ""),
    ("ANALYSED", "Analizado", ""),
    ("PREPARED", "Preparado", ""),
    ("REVIEW", "Revisión", ""),
    ("APPROVED", "Aprobado", ""),
    ("EXPORTED", "Exportado", ""),
)

class A4AObjectRef(bpy.types.PropertyGroup):
    object: PointerProperty(type=bpy.types.Object)
    uid: StringProperty(default="")
    original_name: StringProperty(default="")

class A4AIssue(bpy.types.PropertyGroup):
    severity: EnumProperty(items=(("INFO", "Info", ""), ("REVIEW", "Revisión", ""), ("FAIL", "Fallo", "")), default="INFO")
    code: StringProperty(default="")
    message: StringProperty(default="")
    auto_repairable: BoolProperty(default=False)

class A4ASceneSession(bpy.types.PropertyGroup):
    session_id: StringProperty(default="")
    asset_id: StringProperty(name="Asset ID", default="")
    display_name: StringProperty(name="Nombre", default="")
    profile_id: EnumProperty(name="Tipo", items=PROFILE_ITEMS, default="GENERIC_PROP")
    state: EnumProperty(items=STATE_ITEMS, default="EMPTY")
    source_hash: StringProperty(default="")
    source_collection_name: StringProperty(default="")
    work_collection_name: StringProperty(default="")
    ground_object_name: StringProperty(default="")
    source_objects: CollectionProperty(type=A4AObjectRef)
    work_objects: CollectionProperty(type=A4AObjectRef)
    issues: CollectionProperty(type=A4AIssue)
    show_source: BoolProperty(name="Mostrar SOURCE", default=False)
    auto_repair_enabled: BoolProperty(name="Autorreparación", default=True)
    review_budget_seconds: FloatProperty(name="Presupuesto revisión", default=30.0, min=1.0, max=600.0)
    pvs: FloatProperty(default=0.0, min=0.0, max=100.0)
    cse: FloatProperty(default=0.0, min=0.0, max=100.0)
    score_disagreement: FloatProperty(default=0.0, min=0.0, max=100.0)
    final_decision: StringProperty(default="-")
    object_count: IntProperty(default=0)
    mesh_count: IntProperty(default=0)
    vertex_count: IntProperty(default=0)
    triangle_count: IntProperty(default=0)
    material_slot_count: IntProperty(default=0)
    missing_uv_meshes: IntProperty(default=0)
    boundary_edges: IntProperty(default=0)
    nonmanifold_edges: IntProperty(default=0)
    wire_edges: IntProperty(default=0)
    loose_vertices: IntProperty(default=0)
    degenerate_faces: IntProperty(default=0)
    connected_components: IntProperty(default=0)
    tiny_components: IntProperty(default=0)
    symmetry_score: FloatProperty(default=0.0)
    dimension_x: FloatProperty(default=0.0)
    dimension_y: FloatProperty(default=0.0)
    dimension_z: FloatProperty(default=0.0)
    min_z: FloatProperty(default=0.0)
    max_z: FloatProperty(default=0.0)
    ground_state: StringProperty(default="N/A")
    ground_message: StringProperty(default="")
    robust_support_z: FloatProperty(default=0.0)
    ground_translation_z: FloatProperty(default=0.0)
    support_fraction: FloatProperty(default=0.0)
    region_count: IntProperty(default=0)
    stable_region_count: IntProperty(default=0)
    ambiguous_region_count: IntProperty(default=0)
    region_stability: FloatProperty(default=0.0, min=0.0, max=1.0)
    regions_json: StringProperty(default="[]")
    geometry_gate: StringProperty(default="N/A")
    topology_gate: StringProperty(default="N/A")
    ground_gate: StringProperty(default="N/A")
    repair_gate: StringProperty(default="N/A")
    regions_gate: StringProperty(default="N/A")
    materials_gate: StringProperty(default="N/A")
    export_gate: StringProperty(default="N/A")
    last_analysis_summary: StringProperty(default="")
    last_repair_summary: StringProperty(default="")
    repair_passes: IntProperty(default=0)
    repair_changes: IntProperty(default=0)
    manifest_text_name: StringProperty(default="")

def is_floor_profile(profile_id: str) -> bool:
    return profile_id in FLOOR_PROFILES

_CLASSES = (A4AObjectRef, A4AIssue, A4ASceneSession)

def register():
    for cls in _CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.assets4all = PointerProperty(type=A4ASceneSession)

def unregister():
    if hasattr(bpy.types.Scene, "assets4all"):
        del bpy.types.Scene.assets4all
    for cls in reversed(_CLASSES):
        bpy.utils.unregister_class(cls)
