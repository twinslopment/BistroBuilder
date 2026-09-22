import bpy
import math
from mathutils import Vector
from pathlib import Path

ASSET = "BB_WindowModule_3000x2500_B"
WIDTH = 3.00
HEIGHT = 2.50
DEPTH = 0.07
FRAME = 0.025
GLASS_Y = 0.0
ROOT_DIR = Path(__file__).resolve().parent
BLEND_PATH = ROOT_DIR / f"{ASSET}.blend"
FBX_PATH = ROOT_DIR.parents[4] / "Assets" / "Art" / "Blender" / "Architecture" / "Windows" / ASSET / "Models" / f"{ASSET}.fbx"
PREVIEW_PATH = ROOT_DIR / f"{ASSET}_preview.png"


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        pass


def add_box_parts(name, boxes):
    verts, faces = [], []
    for center, size in boxes:
        cx, cy, cz = center
        sx, sy, sz = size
        start = len(verts)
        for x, y, z in [
            (-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
            (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
            verts.append((cx+x*sx/2, cy+y*sy/2, cz+z*sz/2))
        faces += [
            (start+0,start+1,start+2,start+3),
            (start+4,start+7,start+6,start+5),
            (start+0,start+4,start+5,start+1),
            (start+1,start+5,start+6,start+2),
            (start+2,start+6,start+7,start+3),
            (start+4,start+0,start+3,start+7),
        ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def add_glass(name):
    hw = (WIDTH - 2*FRAME) / 2
    z0, z1 = FRAME, HEIGHT - FRAME
    verts = [(-hw,GLASS_Y,z0),(hw,GLASS_Y,z0),(hw,GLASS_Y,z1),(-hw,GLASS_Y,z1)]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], [(0,1,2,3)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def principled_input(mat, key, value):
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if not bsdf:
        return
    sock = bsdf.inputs.get(key)
    if sock is not None:
        sock.default_value = value


def make_materials():
    frame = bpy.data.materials.new("BB_MAT_Frame_Dark")
    frame.use_nodes = True
    principled_input(frame, "Base Color", (0.025,0.03,0.035,1.0))
    principled_input(frame, "Metallic", 0.75)
    principled_input(frame, "Roughness", 0.22)
    glass = bpy.data.materials.new("BB_MAT_Glass_Clear")
    glass.use_nodes = True
    principled_input(glass, "Base Color", (0.78,0.88,0.92,1.0))
    principled_input(glass, "Roughness", 0.045)
    principled_input(glass, "IOR", 1.45)
    principled_input(glass, "Alpha", 0.18)
    principled_input(glass, "Transmission Weight", 1.0)
    glass.diffuse_color = (0.78,0.88,0.92,0.18)
    try:
        glass.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return frame, glass


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def generate_uv(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project()
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def create_asset():
    clear_scene()
    frame_mat, glass_mat = make_materials()
    inner_w = WIDTH - 2*FRAME
    inner_h = HEIGHT - 2*FRAME
    boxes = [
        ((-WIDTH/2+FRAME/2,0,HEIGHT/2),(FRAME,DEPTH,HEIGHT)),
        (( WIDTH/2-FRAME/2,0,HEIGHT/2),(FRAME,DEPTH,HEIGHT)),
        ((0,0,FRAME/2),(inner_w,DEPTH,FRAME)),
        ((0,0,HEIGHT-FRAME/2),(inner_w,DEPTH,FRAME)),
    ]
    frame = add_box_parts("Frame", boxes)
    glass = add_glass("Glass")
    bevel = frame.modifiers.new(name="EdgeSoftening", type='BEVEL')
    bevel.width = 0.003
    bevel.segments = 2
    bevel.limit_method = 'ANGLE'
    frame.data.materials.append(frame_mat)
    glass.data.materials.append(glass_mat)
    generate_uv(frame)
    generate_uv(glass)
    frame["bb_surface"] = "frame"
    glass["bb_surface"] = "glass"
    glass["bb_unity_material"] = "BB_MAT_Glass_Clear"
    glass["bb_double_sided"] = True

    root = bpy.data.objects.new(ASSET, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = 'PLAIN_AXES'
    root["bb_asset_id"] = ASSET
    root["bb_asset_type"] = "ArchitectureWindowModule"
    root["bb_width_m"] = WIDTH
    root["bb_height_m"] = HEIGHT
    root["bb_depth_m"] = DEPTH
    root["bb_collider"] = "box_full_module"
    frame.parent = root
    glass.parent = root

    for obj in (frame, glass):
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.select_set(False)

    root.select_set(True)
    frame.select_set(True)
    glass.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(FBX_PATH), use_selection=True,
            object_types={'EMPTY','MESH'}, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
            add_leaf_bones=False, use_mesh_modifiers=True, bake_anim=False,
            path_mode='AUTO', embed_textures=False)
    except Exception as exc:
        print("FBX_EXPORT_ERROR|" + repr(exc))
        raise

    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    return root, frame, glass

def render_preview(root, frame, glass):
    floor_mat = bpy.data.materials.new("PreviewFloor")
    floor_mat.use_nodes = True
    principled_input(floor_mat, "Base Color", (0.10,0.11,0.12,1.0))
    principled_input(floor_mat, "Roughness", 0.72)
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0,0,0))
    floor = bpy.context.object
    floor.name = "_PreviewFloor"
    floor.data.materials.append(floor_mat)

    back_mat = bpy.data.materials.new("PreviewBackdrop")
    back_mat.use_nodes = True
    principled_input(back_mat, "Base Color", (0.18,0.20,0.22,1.0))
    principled_input(back_mat, "Roughness", 0.86)
    bpy.ops.mesh.primitive_plane_add(size=7, location=(0,1.6,2.0), rotation=(math.radians(90),0,0))
    backdrop = bpy.context.object
    backdrop.name = "_PreviewBackdrop"
    backdrop.data.materials.append(back_mat)

    bpy.ops.object.light_add(type='AREA', location=(2.4,-2.8,3.5))
    key = bpy.context.object
    key.data.energy = 1100
    key.data.shape = 'RECTANGLE'
    key.data.size = 2.6
    key.data.size_y = 3.0
    look_at(key, (0,0,1.25))

    bpy.ops.object.light_add(type='AREA', location=(-2.2,-0.8,2.6))
    fill = bpy.context.object
    fill.data.energy = 500
    fill.data.size = 2.5
    look_at(fill, (0,0,1.35))

    bpy.ops.object.light_add(type='AREA', location=(0,1.1,2.9))
    rim = bpy.context.object
    rim.data.energy = 650
    rim.data.size = 1.6
    look_at(rim, (0,0,1.45))

    bpy.ops.object.camera_add(location=(3.15,-4.9,1.72))
    cam = bpy.context.object
    cam.data.lens = 62
    look_at(cam, (0,0,1.25))
    bpy.context.scene.camera = cam

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1250
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = 'RGBA'
    scene.world.color = (0.035,0.04,0.05)
    scene.view_settings.look = 'AgX - Medium High Contrast'
    bpy.ops.render.render(write_still=True)

    for obj in (floor, backdrop, key, fill, rim, cam):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.materials.remove(floor_mat, do_unlink=True)
    bpy.data.materials.remove(back_mat, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))


def validate(root, frame, glass):
    assert abs(WIDTH - 3.00) < 1e-6
    assert abs(HEIGHT - 2.50) < 1e-6
    assert frame.parent == root and glass.parent == root
    assert len(frame.data.vertices) == 32
    assert len(glass.data.vertices) == 4
    assert len(frame.data.uv_layers) > 0
    assert len(glass.data.uv_layers) > 0
    assert root.location.length < 1e-6
    assert frame.scale == Vector((1,1,1))
    assert glass.scale == Vector((1,1,1))
    print(f"BB_WINDOW_ASSET_PASS|ASSET={ASSET}|SIZE={WIDTH:.2f}x{HEIGHT:.2f}x{DEPTH:.2f}|FRAME_VERTS={len(frame.data.vertices)}|GLASS_VERTS={len(glass.data.vertices)}")


if __name__ == "__main__":
    root, frame, glass = create_asset()
    render_preview(root, frame, glass)
    validate(root, frame, glass)

