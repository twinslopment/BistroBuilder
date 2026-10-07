import bpy
import math
import json
import os
from mathutils import Vector

# Bistro Builder - Waiter Animation Lab V1
# Procedural prototype for validating rig, locomotion, tray stability and Unity export.

ROOT = r"C:\Users\mruperez\ProyectoBB\BistroBuilder_Worktrees\character-animation-v1"
OUT = os.path.join(ROOT, "Assets", "Art", "Characters", "Waiter", "Prototype")
LOG = os.path.join(ROOT, "Logs", "WaiterAnimation")
os.makedirs(OUT, exist_ok=True)
os.makedirs(LOG, exist_ok=True)

BLEND_PATH = os.path.join(OUT, "BB_Waiter_AnimationLab_V1.blend")
GLB_PATH = os.path.join(OUT, "BB_Waiter_AnimationLab_V1.glb")
FBX_PATH = os.path.join(OUT, "BB_Waiter_AnimationLab_V1.fbx")
REPORT_PATH = os.path.join(LOG, "waiter_animation_tests.json")

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for datablocks in (bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for block in list(datablocks):
        datablocks.remove(block)

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 120
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 720
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.render.image_settings.color_mode = 'RGBA'
scene.world.color = (0.035, 0.035, 0.045)

# Collections
char_col = bpy.data.collections.new("CHARACTER")
scene.collection.children.link(char_col)
prop_col = bpy.data.collections.new("PROPS")
scene.collection.children.link(prop_col)
env_col = bpy.data.collections.new("ENV")
scene.collection.children.link(env_col)
debug_col = bpy.data.collections.new("DEBUG")
scene.collection.children.link(debug_col)

def move_to_collection(obj, col):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)

def mat(name, rgba, metallic=0.0, rough=0.45):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = rgba
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    if bsdf:
        bsdf.inputs['Base Color'].default_value = rgba
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = rough
    return m

MAT_SKIN = mat("M_Skin", (0.46, 0.235, 0.13, 1), 0.0, 0.55)
MAT_SKIN_LIGHT = mat("M_Skin_Light", (0.56, 0.30, 0.18, 1), 0.0, 0.5)
MAT_WHITE = mat("M_Shirt_White", (0.92, 0.93, 0.95, 1), 0.0, 0.35)
MAT_BLACK = mat("M_Vest_Pants_Black", (0.018, 0.02, 0.025, 1), 0.0, 0.52)
MAT_HAIR = mat("M_Hair", (0.008, 0.006, 0.005, 1), 0.0, 0.7)
MAT_SHOE = mat("M_Shoes", (0.012, 0.012, 0.015, 1), 0.05, 0.22)
MAT_METAL = mat("M_Metal", (0.22, 0.24, 0.27, 1), 0.82, 0.2)
MAT_EYE = mat("M_Eye", (0.015, 0.01, 0.008, 1), 0.0, 0.35)
MAT_DEBUG = mat("M_Rig_Debug", (0.05, 0.55, 1.0, 1), 0.0, 0.25)
MAT_FLOOR = mat("M_Floor", (0.11, 0.12, 0.14, 1), 0.0, 0.78)

def smooth(obj):
    if hasattr(obj.data, 'polygons'):
        for p in obj.data.polygons:
            p.use_smooth = True

def add_bevel(obj, width=0.01, segments=2):
    mod = obj.modifiers.new("SoftEdges", 'BEVEL')
    mod.width = width
    mod.segments = segments

def add_box(name, loc, dims, material, bevel=0.02, col=char_col):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object
    o.name = name
    o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(o, bevel, 3)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_uv(name, loc, scale, material, col=char_col, segments=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(o)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_cyl_between(name, a, b, radius, material, col=char_col, vertices=24):
    a = Vector(a)
    b = Vector(b)
    v = b - a
    length = v.length
    mid = (a + b) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=mid)
    o = bpy.context.object
    o.name = name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(v.normalized())
    smooth(o)
    add_bevel(o, min(radius * 0.20, 0.015), 2)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_disc(name, loc, radius, depth, material, col=prop_col):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=depth, location=loc)
    o = bpy.context.object
    o.name = name
    smooth(o)
    add_bevel(o, 0.008, 3)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def parent_bone_keep_world(obj, arm, bone):
    mw = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone
    obj.matrix_world = mw

# ---------------- Rig ----------------
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.object
arm.name = "BB_Waiter_Rig"
arm.data.name = "BB_Waiter_Rig_Data"
move_to_collection(arm, char_col)
arm.show_in_front = True

eb = arm.data.edit_bones
root = eb[0]
root.name = "Root"
root.head = (0, 0, 0.02)
root.tail = (0, 0, 0.18)
root.use_deform = False

def bone(name, head, tail, parent=None, deform=True):
    b = eb.new(name)
    b.head = head
    b.tail = tail
    b.use_deform = deform
    if parent:
        b.parent = eb[parent]
        b.use_connect = False
    return b

bone("Hips", (0, 0, 0.96), (0, 0, 1.10), "Root")
bone("Spine", (0, 0, 1.08), (0, 0, 1.32), "Hips")
bone("Chest", (0, 0, 1.30), (0, 0, 1.53), "Spine")
bone("Neck", (0, 0, 1.52), (0, 0, 1.66), "Chest")
bone("Head", (0, 0, 1.64), (0, 0, 1.86), "Neck")

bone("LeftUpperLeg", (-0.105, 0, 1.00), (-0.105, 0.005, 0.61), "Hips")
bone("LeftLowerLeg", (-0.105, 0.005, 0.61), (-0.105, -0.005, 0.18), "LeftUpperLeg")
bone("LeftFoot", (-0.105, -0.005, 0.18), (-0.105, -0.22, 0.09), "LeftLowerLeg")
bone("RightUpperLeg", (0.105, 0, 1.00), (0.105, 0.005, 0.61), "Hips")
bone("RightLowerLeg", (0.105, 0.005, 0.61), (0.105, -0.005, 0.18), "RightUpperLeg")
bone("RightFoot", (0.105, -0.005, 0.18), (0.105, -0.22, 0.09), "RightLowerLeg")

bone("LeftShoulder", (-0.04, 0, 1.48), (-0.19, 0, 1.47), "Chest")
bone("LeftUpperArm", (-0.19, 0, 1.47), (-0.37, 0.005, 1.25), "LeftShoulder")
bone("LeftLowerArm", (-0.37, 0.005, 1.25), (-0.43, -0.005, 1.02), "LeftUpperArm")
bone("LeftHand", (-0.43, -0.005, 1.02), (-0.44, -0.04, 0.91), "LeftLowerArm")

bone("RightShoulder", (0.04, 0, 1.48), (0.19, 0, 1.47), "Chest")
bone("RightUpperArm", (0.19, 0, 1.47), (0.37, 0.005, 1.25), "RightShoulder")
bone("RightLowerArm", (0.37, 0.005, 1.25), (0.43, -0.005, 1.02), "RightUpperArm")
bone("RightHand", (0.43, -0.005, 1.02), (0.44, -0.04, 0.91), "RightLowerArm")

# Stable world-oriented tray socket, deliberately parented to Root rather than torso.
bone("TraySocket", (-0.40, -0.22, 1.17), (-0.40, -0.22, 1.29), "Root", False)

bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------- Character ----------------
torso = add_box("Waiter_Vest", (0, 0, 1.34), (0.43, 0.235, 0.46), MAT_BLACK, 0.035)
parent_bone_keep_world(torso, arm, "Chest")
shirt_core = add_box("Waiter_Shirt_Core", (0, 0.018, 1.37), (0.37, 0.19, 0.43), MAT_WHITE, 0.028)
parent_bone_keep_world(shirt_core, arm, "Chest")
# Vest front plane to keep black front silhouette while leaving white collar visible.
vest_front = add_box("Waiter_Vest_Front", (0, -0.118, 1.33), (0.40, 0.035, 0.42), MAT_BLACK, 0.018)
parent_bone_keep_world(vest_front, arm, "Chest")

pelvis = add_box("Waiter_Trousers_Waist", (0, 0, 1.01), (0.39, 0.23, 0.24), MAT_BLACK, 0.035)
parent_bone_keep_world(pelvis, arm, "Hips")

# Collar, shirt opening and buttons
collar_l = add_box("Collar_L", (-0.055, -0.142, 1.535), (0.10, 0.025, 0.12), MAT_WHITE, 0.008)
collar_l.rotation_euler[1] = math.radians(-24)
parent_bone_keep_world(collar_l, arm, "Chest")
collar_r = add_box("Collar_R", (0.055, -0.142, 1.535), (0.10, 0.025, 0.12), MAT_WHITE, 0.008)
collar_r.rotation_euler[1] = math.radians(24)
parent_bone_keep_world(collar_r, arm, "Chest")
for z in (1.42, 1.33, 1.24):
    btn = add_uv("Vest_Button", (0, -0.145, z), (0.014, 0.008, 0.014), MAT_HAIR)
    parent_bone_keep_world(btn, arm, "Chest")

# Belt and buckle
belt = add_box("Belt", (0, -0.004, 0.99), (0.405, 0.245, 0.055), MAT_HAIR, 0.008)
parent_bone_keep_world(belt, arm, "Hips")
buckle = add_box("Belt_Buckle", (0, -0.137, 0.99), (0.075, 0.018, 0.055), MAT_METAL, 0.006)
parent_bone_keep_world(buckle, arm, "Hips")

# Legs
for side, x in (("Left", -0.105), ("Right", 0.105)):
    upper = add_cyl_between(side+"_Trouser_Upper", (x, 0, 0.98), (x, 0.004, 0.60), 0.095, MAT_BLACK)
    parent_bone_keep_world(upper, arm, side+"UpperLeg")
    lower = add_cyl_between(side+"_Trouser_Lower", (x, 0.004, 0.60), (x, -0.005, 0.18), 0.082, MAT_BLACK)
    parent_bone_keep_world(lower, arm, side+"LowerLeg")
    shoe = add_box(side+"_Shoe", (x, -0.095, 0.095), (0.17, 0.32, 0.13), MAT_SHOE, 0.028)
    parent_bone_keep_world(shoe, arm, side+"Foot")

# Arms in white shirt with cuffs and hands
for side, sx in (("Left", -1), ("Right", 1)):
    a0 = Vector((0.19*sx, 0, 1.47))
    a1 = Vector((0.37*sx, 0.005, 1.25))
    a2 = Vector((0.43*sx, -0.005, 1.02))
    upper = add_cyl_between(side+"_Shirt_Upper", a0, a1, 0.072, MAT_WHITE)
    parent_bone_keep_world(upper, arm, side+"UpperArm")
    lower = add_cyl_between(side+"_Shirt_Lower", a1, a2, 0.062, MAT_WHITE)
    parent_bone_keep_world(lower, arm, side+"LowerArm")
    cuff = add_box(side+"_Cuff", (0.435*sx, -0.008, 1.035), (0.125, 0.11, 0.075), MAT_WHITE, 0.012)
    parent_bone_keep_world(cuff, arm, side+"LowerArm")
    hand = add_uv(side+"_Hand", (0.44*sx, -0.025, 0.94), (0.055, 0.045, 0.095), MAT_SKIN_LIGHT)
    parent_bone_keep_world(hand, arm, side+"Hand")

# Head, neck, ears, hair and simple facial landmarks
neck = add_cyl_between("Neck_Skin", (0, 0, 1.55), (0, 0, 1.68), 0.075, MAT_SKIN_LIGHT)
parent_bone_keep_world(neck, arm, "Neck")
head = add_uv("Head_Mesh", (0, -0.004, 1.76), (0.115, 0.105, 0.155), MAT_SKIN_LIGHT)
parent_bone_keep_world(head, arm, "Head")
hair = add_uv("Hair", (0, 0.008, 1.84), (0.12, 0.108, 0.09), MAT_HAIR)
parent_bone_keep_world(hair, arm, "Head")
for sx in (-1, 1):
    ear = add_uv("Ear", (0.112*sx, 0.0, 1.76), (0.024, 0.015, 0.040), MAT_SKIN_LIGHT)
    parent_bone_keep_world(ear, arm, "Head")
    eye = add_uv("Eye", (0.043*sx, -0.101, 1.785), (0.012, 0.006, 0.009), MAT_EYE)
    parent_bone_keep_world(eye, arm, "Head")
    brow = add_box("Brow", (0.043*sx, -0.106, 1.81), (0.052, 0.009, 0.010), MAT_HAIR, 0.003)
    parent_bone_keep_world(brow, arm, "Head")
nose = add_uv("Nose", (0, -0.116, 1.755), (0.022, 0.035, 0.038), MAT_SKIN_LIGHT)
parent_bone_keep_world(nose, arm, "Head")
mouth = add_box("Mouth", (0, -0.111, 1.708), (0.055, 0.007, 0.009), MAT_EYE, 0.003)
parent_bone_keep_world(mouth, arm, "Head")

# Tray with raised rim and two simple service items.
tray = add_disc("Service_Tray", (-0.40, -0.22, 1.18), 0.285, 0.025, MAT_METAL)
parent_bone_keep_world(tray, arm, "TraySocket")
rim = add_disc("Service_Tray_Rim", (-0.40, -0.22, 1.198), 0.295, 0.018, MAT_METAL)
# Create inner recess by scaling Z only; visual rim remains subtle.
parent_bone_keep_world(rim, arm, "TraySocket")
plate = add_disc("Tray_Plate", (-0.47, -0.22, 1.218), 0.09, 0.012, MAT_WHITE)
parent_bone_keep_world(plate, arm, "TraySocket")
glass = add_cyl_between("Tray_Glass", (-0.30, -0.22, 1.215), (-0.30, -0.22, 1.33), 0.033, MAT_WHITE, prop_col, 32)
parent_bone_keep_world(glass, arm, "TraySocket")

# ---------------- Animation helpers ----------------
def activate_action(name, start, end):
    action = bpy.data.actions.get(name) or bpy.data.actions.new(name)
    action.use_fake_user = True
    if not arm.animation_data:
        arm.animation_data_create()
    arm.animation_data.action = action
    scene.frame_start = start
    scene.frame_end = end
    return action

def clear_pose():
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)

def key_bone(name, frame, rot=(0,0,0), loc=(0,0,0), interp='BEZIER'):
    pb = arm.pose.bones[name]
    pb.rotation_mode = 'XYZ'
    pb.rotation_euler = tuple(math.radians(v) for v in rot)
    pb.location = loc
    pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    pb.keyframe_insert(data_path="location", frame=frame, group=name)

def set_linear_keys(action):
    try:
        for fc in action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = 'BEZIER'
    except Exception:
        pass

# Idle 1-120
clear_pose()
idle = activate_action("BB_Waiter_Idle_01", 1, 120)
for f, z, chest_x, head_z in ((1,0,0,0),(30,0.006,0.6,-0.8),(60,0,0,0),(90,0.006,-0.5,0.8),(120,0,0,0)):
    key_bone("Root", f, loc=(0,0,z))
    key_bone("Chest", f, rot=(chest_x,0,0))
    key_bone("Head", f, rot=(0,0,head_z))
    key_bone("LeftUpperArm", f, rot=(1.5,0,-1.5))
    key_bone("RightUpperArm", f, rot=(-1.5,0,1.5))
set_linear_keys(idle)

# Walk 1-33 loop
clear_pose()
walk = activate_action("BB_Waiter_Walk_01", 1, 33)
walk_poses = {
    1:  dict(ll=22, rl=-22, lk=6, rk=26, la=-18, ra=18, z=0.000, twist=-2.2),
    9:  dict(ll=2, rl=-2, lk=36, rk=5, la=-3, ra=3, z=0.025, twist=0.0),
    17: dict(ll=-22, rl=22, lk=26, rk=6, la=18, ra=-18, z=0.000, twist=2.2),
    25: dict(ll=-2, rl=2, lk=5, rk=36, la=3, ra=-3, z=0.025, twist=0.0),
    33: dict(ll=22, rl=-22, lk=6, rk=26, la=-18, ra=18, z=0.000, twist=-2.2),
}
for f,p in walk_poses.items():
    key_bone("Root", f, loc=(0,0,p['z']))
    key_bone("Hips", f, rot=(0,p['twist']*0.25,0))
    key_bone("Chest", f, rot=(0,-p['twist']*0.45,0))
    key_bone("LeftUpperLeg", f, rot=(p['ll'],0,0))
    key_bone("RightUpperLeg", f, rot=(p['rl'],0,0))
    key_bone("LeftLowerLeg", f, rot=(-p['lk'],0,0))
    key_bone("RightLowerLeg", f, rot=(-p['rk'],0,0))
    key_bone("LeftFoot", f, rot=(max(-8,min(10,-p['ll']*0.28)),0,0))
    key_bone("RightFoot", f, rot=(max(-8,min(10,-p['rl']*0.28)),0,0))
    key_bone("LeftUpperArm", f, rot=(p['la'],0,-2))
    key_bone("RightUpperArm", f, rot=(p['ra'],0,2))
    key_bone("LeftLowerArm", f, rot=(-7 if p['la'] < 0 else -12,0,0))
    key_bone("RightLowerArm", f, rot=(-7 if p['ra'] < 0 else -12,0,0))
    key_bone("Head", f, rot=(0,0,-p['twist']*0.18))
set_linear_keys(walk)

# Walk with tray 1-33 loop. Reduced upper-body sway; tray socket remains level.
clear_pose()
walk_tray = activate_action("BB_Waiter_WalkTray_01", 1, 33)
for f,p in walk_poses.items():
    key_bone("Root", f, loc=(0,0,p['z']*0.70))
    key_bone("Hips", f, rot=(0,p['twist']*0.20,0))
    key_bone("Chest", f, rot=(0,-p['twist']*0.15,0))
    key_bone("LeftUpperLeg", f, rot=(p['ll']*0.92,0,0))
    key_bone("RightUpperLeg", f, rot=(p['rl']*0.92,0,0))
    key_bone("LeftLowerLeg", f, rot=(-p['lk'],0,0))
    key_bone("RightLowerLeg", f, rot=(-p['rk'],0,0))
    key_bone("LeftFoot", f, rot=(max(-7,min(9,-p['ll']*0.25)),0,0))
    key_bone("RightFoot", f, rot=(max(-7,min(9,-p['rl']*0.25)),0,0))
    # Tray arm: shoulder forward, elbow bent, wrist supporting from below.
    key_bone("LeftUpperArm", f, rot=(-42,0,-8))
    key_bone("LeftLowerArm", f, rot=(-63,0,3))
    key_bone("LeftHand", f, rot=(14,0,0))
    key_bone("RightUpperArm", f, rot=(p['ra']*0.65,0,2))
    key_bone("RightLowerArm", f, rot=(-10,0,0))
    key_bone("TraySocket", f, rot=(0,0,0), loc=(0,0,0.006 if f in (9,25) else 0))
    key_bone("Head", f, rot=(0,0,-p['twist']*0.08))
set_linear_keys(walk_tray)

# Idle with tray
clear_pose()
idle_tray = activate_action("BB_Waiter_IdleTray_01", 1, 96)
for f,z,head_z in ((1,0,0),(24,0.004,-0.6),(48,0,0),(72,0.004,0.6),(96,0,0)):
    key_bone("Root", f, loc=(0,0,z))
    key_bone("LeftUpperArm", f, rot=(-42,0,-8))
    key_bone("LeftLowerArm", f, rot=(-63,0,3))
    key_bone("LeftHand", f, rot=(14,0,0))
    key_bone("TraySocket", f, rot=(0,0,0))
    key_bone("Head", f, rot=(0,0,head_z))
set_linear_keys(idle_tray)

# Serve from tray with right arm
clear_pose()
serve = activate_action("BB_Waiter_Serve_01", 1, 56)
serve_frames = (
    (1,  0,   -8,  -10, 0),
    (14, 18,  -24, -25, -2),
    (28, 46,  -38, -18, -4),
    (42, 20,  -26, -22, -2),
    (56, 0,   -8,  -10, 0),
)
for f, rua, rla, rh, chest in serve_frames:
    key_bone("LeftUpperArm", f, rot=(-42,0,-8))
    key_bone("LeftLowerArm", f, rot=(-63,0,3))
    key_bone("LeftHand", f, rot=(14,0,0))
    key_bone("TraySocket", f, rot=(0,0,0))
    key_bone("Chest", f, rot=(0,chest,0))
    key_bone("RightUpperArm", f, rot=(-rua,0,6))
    key_bone("RightLowerArm", f, rot=(rla,0,-4))
    key_bone("RightHand", f, rot=(rh,0,0))
set_linear_keys(serve)

# ---------------- Environment ----------------
bpy.ops.mesh.primitive_plane_add(size=20, location=(0,0,0))
floor = bpy.context.object
floor.name = "Studio_Floor"
floor.data.materials.append(MAT_FLOOR)
move_to_collection(floor, env_col)

# Backdrop
back = add_box("Backdrop", (0, 2.3, 2.0), (8, 0.15, 5.5), MAT_FLOOR, 0.05, env_col)

def point_camera(cam, target):
    direction = Vector(target) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z','Y').to_euler()

bpy.ops.object.camera_add(location=(3.05, -6.4, 2.42))
cam = bpy.context.object
cam.name = "Waiter_Camera"
cam.data.lens = 62
cam.data.sensor_width = 36
point_camera(cam, (0,0,0.95))
scene.camera = cam
move_to_collection(cam, env_col)

def add_area(name, loc, energy, size, color):
    bpy.ops.object.light_add(type='AREA', location=loc)
    l = bpy.context.object
    l.name = name
    l.data.energy = energy
    l.data.shape = 'DISK'
    l.data.size = size
    l.data.color = color
    point_camera(l, (0,0,1.0))
    move_to_collection(l, env_col)
    return l

add_area("Key", (-3.0,-3.5,4.8), 1100, 3.2, (1.0,0.86,0.72))
add_area("Fill", (3.2,-2.4,3.4), 700, 3.0, (0.72,0.82,1.0))
add_area("Rim", (1.0,2.8,4.0), 900, 2.5, (1.0,0.92,0.80))

# ---------------- Validation ----------------
def set_action(name, frame):
    arm.animation_data.action = bpy.data.actions[name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()

def world_bone_point(name, which='head'):
    pb = arm.pose.bones[name]
    p = pb.head if which == 'head' else pb.tail
    return arm.matrix_world @ p

def object_up_angle_deg(obj):
    z = obj.matrix_world.to_3x3() @ Vector((0,0,1))
    z.normalize()
    d = max(-1.0, min(1.0, z.dot(Vector((0,0,1)))))
    return math.degrees(math.acos(d))

tests = []
def test(name, passed, value=None, limit=None, note=""):
    tests.append({"name":name,"passed":bool(passed),"value":value,"limit":limit,"note":note})

expected_actions = {
    "BB_Waiter_Idle_01": (1,120),
    "BB_Waiter_Walk_01": (1,33),
    "BB_Waiter_WalkTray_01": (1,33),
    "BB_Waiter_IdleTray_01": (1,96),
    "BB_Waiter_Serve_01": (1,56),
}
for n in expected_actions:
    test("action_exists:"+n, n in bpy.data.actions)

# Loop continuity: compare major bone transforms at first and last keyed frame.
for action_name in ("BB_Waiter_Walk_01","BB_Waiter_WalkTray_01"):
    set_action(action_name, 1)
    snap_a = {}
    for bn in ("Root","LeftUpperLeg","RightUpperLeg","LeftUpperArm","RightUpperArm","Chest"):
        pb = arm.pose.bones[bn]
        snap_a[bn] = (Vector(pb.location), Vector(pb.rotation_euler))
    set_action(action_name, 33)
    max_delta = 0.0
    for bn,(la,ra) in snap_a.items():
        pb = arm.pose.bones[bn]
        max_delta = max(max_delta, (Vector(pb.location)-la).length)
        max_delta = max(max_delta, (Vector(pb.rotation_euler)-ra).length)
    test("loop_continuity:"+action_name, max_delta < 1e-5, round(max_delta,7), 1e-5)

# Tray stability throughout tray actions.
max_tilt = 0.0
max_z_jitter = 0.0
for action_name, frames in (("BB_Waiter_WalkTray_01", range(1,34)), ("BB_Waiter_IdleTray_01", range(1,97,2)), ("BB_Waiter_Serve_01", range(1,57))):
    zs = []
    tilts = []
    for f in frames:
        set_action(action_name, f)
        zs.append(tray.matrix_world.translation.z)
        tilts.append(object_up_angle_deg(tray))
    local_max_tilt = max(tilts)
    local_jitter = max(zs)-min(zs)
    max_tilt = max(max_tilt, local_max_tilt)
    max_z_jitter = max(max_z_jitter, local_jitter)
    test("tray_tilt:"+action_name, local_max_tilt <= 1.0, round(local_max_tilt,4), 1.0, "degrees")
    test("tray_vertical_jitter:"+action_name, local_jitter <= 0.035, round(local_jitter,4), 0.035, "meters")

# Floor clearance / penetration on feet.
for action_name in ("BB_Waiter_Walk_01","BB_Waiter_WalkTray_01"):
    min_z = 99.0
    for f in range(1,34):
        set_action(action_name, f)
        min_z = min(min_z, world_bone_point("LeftFoot",'tail').z, world_bone_point("RightFoot",'tail').z)
    test("foot_floor_sanity:"+action_name, min_z > -0.12, round(min_z,4), -0.12, "bone-tail coarse sanity")

# Tray remains on waiter's left side at representative frame.
set_action("BB_Waiter_WalkTray_01", 9)
tray_pos = tray.matrix_world.translation
test("tray_side_position", tray_pos.x < -0.15 and 0.95 < tray_pos.z < 1.45, [round(tray_pos.x,3),round(tray_pos.y,3),round(tray_pos.z,3)])

# No NaN transforms.
finite_ok = True
for obj in bpy.data.objects:
    vals = list(obj.matrix_world)
    for row in vals:
        for v in row:
            if not math.isfinite(v):
                finite_ok = False
test("finite_world_transforms", finite_ok)

# ---------------- Renders ----------------
def render_action(action_name, frame, filename):
    set_action(action_name, frame)
    show_tray = ('Tray' in action_name) or ('Serve' in action_name)
    for o in prop_col.objects:
        o.hide_render = not show_tray
    scene.render.filepath = os.path.join(LOG, filename)
    bpy.ops.render.render(write_still=True)
    for o in prop_col.objects:
        o.hide_render = False

render_action("BB_Waiter_Idle_01", 30, "01_idle.png")
render_action("BB_Waiter_Walk_01", 1, "02_walk_contact.png")
render_action("BB_Waiter_Walk_01", 9, "03_walk_passing.png")
render_action("BB_Waiter_WalkTray_01", 9, "04_walk_tray.png")
render_action("BB_Waiter_IdleTray_01", 24, "05_idle_tray.png")
render_action("BB_Waiter_Serve_01", 28, "06_serve.png")

# Rig debug render: procedural bone rods at current pose.
set_action("BB_Waiter_WalkTray_01", 9)
debug_objs = []
for bn in ("Root","Hips","Spine","Chest","Neck","Head","LeftUpperLeg","LeftLowerLeg","LeftFoot","RightUpperLeg","RightLowerLeg","RightFoot","LeftUpperArm","LeftLowerArm","LeftHand","RightUpperArm","RightLowerArm","RightHand","TraySocket"):
    pb = arm.pose.bones[bn]
    a = arm.matrix_world @ pb.head
    b = arm.matrix_world @ pb.tail
    rod = add_cyl_between("DBG_"+bn, a, b, 0.010, MAT_DEBUG, debug_col, 12)
    debug_objs.append(rod)
scene.render.filepath = os.path.join(LOG, "07_rig_debug.png")
bpy.ops.render.render(write_still=True)
for o in debug_objs:
    bpy.data.objects.remove(o, do_unlink=True)

# Save before export.
set_action("BB_Waiter_Idle_01", 1)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# Export GLB and FBX with all actions.
# Hide environment from export, select only rig, character and props.
bpy.ops.object.select_all(action='DESELECT')
for col in (char_col, prop_col):
    for o in col.objects:
        o.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm

glb_ok = True
glb_error = ""
try:
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format='GLB', use_selection=True, export_animations=True)
except Exception as e:
    glb_ok = False
    glb_error = repr(e)
test("export_glb", glb_ok and os.path.exists(GLB_PATH) and os.path.getsize(GLB_PATH) > 10000, os.path.getsize(GLB_PATH) if os.path.exists(GLB_PATH) else 0, 10000, glb_error)

fbx_ok = True
fbx_error = ""
try:
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_simplify_factor=0.0,
        axis_forward='-Z',
        axis_up='Y'
    )
except Exception as e:
    fbx_ok = False
    fbx_error = repr(e)
test("export_fbx", fbx_ok and os.path.exists(FBX_PATH) and os.path.getsize(FBX_PATH) > 10000, os.path.getsize(FBX_PATH) if os.path.exists(FBX_PATH) else 0, 10000, fbx_error)

report = {
    "asset": "BB_Waiter_AnimationLab_V1",
    "blender_version": bpy.app.version_string,
    "reference_direction": "male waiter, white long-sleeve shirt, black vest/trousers/shoes, dark hair",
    "actions": list(expected_actions.keys()),
    "metrics": {
        "max_tray_tilt_deg": round(max_tilt, 4),
        "max_tray_vertical_jitter_m": round(max_z_jitter, 4),
        "object_count": len(bpy.data.objects),
        "bone_count": len(arm.data.bones),
    },
    "tests": tests,
    "summary": {
        "passed": sum(1 for t in tests if t["passed"]),
        "failed": sum(1 for t in tests if not t["passed"]),
        "total": len(tests),
    },
    "outputs": {
        "blend": BLEND_PATH,
        "glb": GLB_PATH,
        "fbx": FBX_PATH,
        "renders": [os.path.join(LOG, f"{i:02d}_{n}.png") for i,n in [
            (1,"idle"),(2,"walk_contact"),(3,"walk_passing"),(4,"walk_tray"),(5,"idle_tray"),(6,"serve"),(7,"rig_debug")
        ]]
    }
}
with open(REPORT_PATH, "w", encoding="utf-8") as f:
    json.dump(report, f, indent=2, ensure_ascii=False)

print("WAITer_BUILD_COMPLETE")
print(json.dumps(report["summary"]))
print(json.dumps(report["metrics"]))
