import bpy
import math
import json
import os
from mathutils import Vector

ROOT = r"C:\Users\mruperez\ProyectoBB\BistroBuilder_Worktrees\character-animation-v1"
OUT = os.path.join(ROOT, "Assets", "Art", "Characters", "Waiter", "VisualV2")
LOG = os.path.join(ROOT, "Logs", "WaiterAnimationV2")
os.makedirs(OUT, exist_ok=True)
os.makedirs(LOG, exist_ok=True)

BLEND_PATH = os.path.join(OUT, "BB_Waiter_VisualV2.blend")
GLB_PATH = os.path.join(OUT, "BB_Waiter_VisualV2.glb")
FBX_PATH = os.path.join(OUT, "BB_Waiter_VisualV2.fbx")
REPORT_PATH = os.path.join(LOG, "waiter_visual_v2_tests.json")

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
scene.world.color = (0.035, 0.038, 0.045)

char_col = bpy.data.collections.new("CHARACTER")
scene.collection.children.link(char_col)
prop_col = bpy.data.collections.new("PROPS")
scene.collection.children.link(prop_col)
env_col = bpy.data.collections.new("ENV")
scene.collection.children.link(env_col)
detail_col = bpy.data.collections.new("DETAILS")
scene.collection.children.link(detail_col)

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

MAT_SKIN = mat("M_Skin_Warm", (0.43, 0.205, 0.115, 1), 0.0, 0.58)
MAT_SKIN_LIGHT = mat("M_Skin_Light", (0.56, 0.29, 0.17, 1), 0.0, 0.52)
MAT_SKIN_SHADOW = mat("M_Skin_Shadow", (0.24, 0.095, 0.055, 1), 0.0, 0.68)
MAT_WHITE = mat("M_Shirt_White", (0.94, 0.95, 0.97, 1), 0.0, 0.38)
MAT_BLACK = mat("M_Vest_Black", (0.012, 0.014, 0.018, 1), 0.0, 0.58)
MAT_PANTS = mat("M_Trousers_Black", (0.016, 0.018, 0.022, 1), 0.0, 0.62)
MAT_HAIR = mat("M_Hair_Dark", (0.006, 0.005, 0.004, 1), 0.0, 0.72)
MAT_SHOE = mat("M_Shoes_Leather", (0.008, 0.009, 0.012, 1), 0.10, 0.24)
MAT_SOLE = mat("M_Shoe_Sole", (0.004, 0.004, 0.005, 1), 0.0, 0.72)
MAT_METAL = mat("M_Tray_Metal", (0.25, 0.27, 0.30, 1), 0.80, 0.18)
MAT_EYE_WHITE = mat("M_Eye_White", (0.94, 0.93, 0.89, 1), 0.0, 0.30)
MAT_EYE = mat("M_Eye_Brown", (0.035, 0.017, 0.009, 1), 0.0, 0.32)
MAT_LIP = mat("M_Lips", (0.31, 0.10, 0.075, 1), 0.0, 0.48)
MAT_FLOOR = mat("M_Floor", (0.115, 0.12, 0.13, 1), 0.0, 0.78)

def smooth(obj):
    if hasattr(obj.data, "polygons"):
        for p in obj.data.polygons:
            p.use_smooth = True

def add_bevel(obj, width=0.01, segments=3):
    mod = obj.modifiers.new("SoftEdges", 'BEVEL')
    mod.width = width
    mod.segments = segments

def add_uv(name, loc, scale, material, col=char_col, segments=40, rings=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(o)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_box(name, loc, dims, material, bevel=0.02, col=char_col):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object
    o.name = name
    o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(o, bevel, 4)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_cone_between(name, a, b, r1, r2, material, col=char_col, vertices=32):
    a = Vector(a); b = Vector(b)
    v = b - a
    length = v.length
    mid = (a + b) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=length, location=mid)
    o = bpy.context.object
    o.name = name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0,0,1)).rotation_difference(v.normalized())
    smooth(o)
    add_bevel(o, min(r1, r2) * 0.11, 3)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_disc(name, loc, radius, depth, material, col=prop_col):
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=radius, depth=depth, location=loc)
    o = bpy.context.object
    o.name = name
    smooth(o)
    add_bevel(o, min(depth*0.45,0.009), 3)
    o.data.materials.append(material)
    move_to_collection(o, col)
    return o

def add_loft(name, loops, material, segments=40, col=char_col):
    verts=[]; faces=[]
    for z, rx, ry in loops:
        for i in range(segments):
            a=2*math.pi*i/segments
            verts.append((rx*math.cos(a), ry*math.sin(a), z))
    for li in range(len(loops)-1):
        for i in range(segments):
            n=(i+1)%segments
            a=li*segments+i
            b=li*segments+n
            c=(li+1)*segments+n
            d=(li+1)*segments+i
            faces.append((a,b,c,d))
    faces.append(tuple(range(segments-1,-1,-1)))
    top=(len(loops)-1)*segments
    faces.append(tuple(top+i for i in range(segments)))
    mesh=bpy.data.meshes.new(name+"_Mesh")
    mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh)
    col.objects.link(o)
    smooth(o)
    bevel=o.modifiers.new("TailoredSoftness",'BEVEL'); bevel.width=0.010; bevel.segments=3
    o.data.materials.append(material)
    return o

def add_prism_xz(name, points, yfront, depth, material, col=detail_col, bevel=0.005):
    n=len(points)
    verts=[]
    yback=yfront+depth
    for x,z in points: verts.append((x,yfront,z))
    for x,z in points: verts.append((x,yback,z))
    faces=[]
    faces.append(tuple(range(n)))
    faces.append(tuple(range(2*n-1,n-1,-1)))
    for i in range(n):
        j=(i+1)%n
        faces.append((i,j,n+j,n+i))
    mesh=bpy.data.meshes.new(name+"_Mesh")
    mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh)
    col.objects.link(o)
    add_bevel(o,bevel,2)
    o.data.materials.append(material)
    return o

def parent_bone_keep_world(obj, arm, bone):
    mw=obj.matrix_world.copy()
    obj.parent=arm
    obj.parent_type='BONE'
    obj.parent_bone=bone
    obj.matrix_world=mw

# Rig
bpy.ops.object.armature_add(enter_editmode=True, location=(0,0,0))
arm=bpy.context.object
arm.name="BB_Waiter_Rig_V2"
arm.data.name="BB_Waiter_Rig_V2_Data"
move_to_collection(arm,char_col)
arm.show_in_front=True
eb=arm.data.edit_bones
root=eb[0]
root.name="Root"; root.head=(0,0,0.02); root.tail=(0,0,0.18); root.use_deform=False

def bone(name,head,tail,parent=None,deform=True):
    b=eb.new(name); b.head=head; b.tail=tail; b.use_deform=deform
    if parent:
        b.parent=eb[parent]; b.use_connect=False
    return b

bone("Hips",(0,0,0.96),(0,0,1.10),"Root")
bone("Spine",(0,0,1.08),(0,0,1.31),"Hips")
bone("Chest",(0,0,1.30),(0,0,1.53),"Spine")
bone("Neck",(0,0,1.52),(0,0,1.65),"Chest")
bone("Head",(0,0,1.64),(0,0,1.86),"Neck")
bone("LeftUpperLeg",(-0.105,0,0.99),(-0.105,0.006,0.59),"Hips")
bone("LeftLowerLeg",(-0.105,0.006,0.59),(-0.105,-0.002,0.17),"LeftUpperLeg")
bone("LeftFoot",(-0.105,-0.002,0.17),(-0.105,-0.25,0.08),"LeftLowerLeg")
bone("RightUpperLeg",(0.105,0,0.99),(0.105,0.006,0.59),"Hips")
bone("RightLowerLeg",(0.105,0.006,0.59),(0.105,-0.002,0.17),"RightUpperLeg")
bone("RightFoot",(0.105,-0.002,0.17),(0.105,-0.25,0.08),"RightLowerLeg")
bone("LeftShoulder",(-0.045,0,1.48),(-0.205,0,1.46),"Chest")
bone("LeftUpperArm",(-0.205,0,1.46),(-0.39,0.004,1.25),"LeftShoulder")
bone("LeftLowerArm",(-0.39,0.004,1.25),(-0.455,-0.005,1.01),"LeftUpperArm")
bone("LeftHand",(-0.455,-0.005,1.01),(-0.465,-0.045,0.90),"LeftLowerArm")
bone("RightShoulder",(0.045,0,1.48),(0.205,0,1.46),"Chest")
bone("RightUpperArm",(0.205,0,1.46),(0.39,0.004,1.25),"RightShoulder")
bone("RightLowerArm",(0.39,0.004,1.25),(0.455,-0.005,1.01),"RightUpperArm")
bone("RightHand",(0.455,-0.005,1.01),(0.465,-0.045,0.90),"RightLowerArm")
bone("TraySocket",(-0.405,-0.245,1.195),(-0.405,-0.245,1.31),"Root",False)

bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode='XYZ'
bpy.ops.object.mode_set(mode='OBJECT')

# Torso / vest tailored loft
vest=add_loft("Waiter_Vest",[
    (1.085,0.205,0.115),
    (1.18,0.215,0.120),
    (1.35,0.235,0.127),
    (1.49,0.245,0.128),
    (1.55,0.185,0.112),
],MAT_BLACK,44)
parent_bone_keep_world(vest,arm,"Chest")

# White shirt V insert and collar
shirt_v=add_prism_xz("Shirt_V_Insert",[(-0.115,1.535),(0.115,1.535),(0.0,1.315)],-0.134,0.020,MAT_WHITE,detail_col,0.004)
parent_bone_keep_world(shirt_v,arm,"Chest")
collar_l=add_prism_xz("Collar_L",[(-0.118,1.555),(-0.015,1.555),(-0.002,1.455),(-0.092,1.505)],-0.150,0.027,MAT_WHITE,detail_col,0.004)
collar_r=add_prism_xz("Collar_R",[(0.118,1.555),(0.015,1.555),(0.002,1.455),(0.092,1.505)],-0.150,0.027,MAT_WHITE,detail_col,0.004)
parent_bone_keep_world(collar_l,arm,"Chest"); parent_bone_keep_world(collar_r,arm,"Chest")

# Vest front seam, pockets, buttons
seam=add_box("Vest_Center_Seam",(0,-0.136,1.31),(0.008,0.012,0.35),MAT_BLACK,0.002,detail_col)
parent_bone_keep_world(seam,arm,"Chest")
for z in (1.39,1.30,1.21):
    btn=add_uv("Vest_Button",(0,-0.151,z),(0.012,0.007,0.012),MAT_SHOE,detail_col,24,12)
    parent_bone_keep_world(btn,arm,"Chest")
for sx in (-1,1):
    pocket=add_box("Vest_Pocket", (0.115*sx,-0.146,1.205),(0.105,0.012,0.012),MAT_BLACK,0.003,detail_col)
    pocket.rotation_euler[1]=math.radians(-3*sx)
    parent_bone_keep_world(pocket,arm,"Chest")

# Waist / belt
waist=add_loft("Trousers_Waist",[(0.91,0.188,0.115),(1.00,0.202,0.118),(1.08,0.198,0.112)],MAT_PANTS,40)
parent_bone_keep_world(waist,arm,"Hips")
belt=add_box("Belt",(0,-0.002,1.005),(0.406,0.238,0.048),MAT_SHOE,0.006,detail_col)
parent_bone_keep_world(belt,arm,"Hips")
buckle=add_box("Belt_Buckle",(0,-0.126,1.005),(0.067,0.018,0.048),MAT_METAL,0.004,detail_col)
parent_bone_keep_world(buckle,arm,"Hips")

# Legs with smoother tailoring
for side,x in (("Left",-0.105),("Right",0.105)):
    upper=add_cone_between(side+"_Trouser_Upper",(x,0,0.98),(x,0.006,0.59),0.101,0.086,MAT_PANTS)
    parent_bone_keep_world(upper,arm,side+"UpperLeg")
    knee=add_uv(side+"_Trouser_Knee",(x,0.003,0.585),(0.086,0.081,0.095),MAT_PANTS,char_col,32,18)
    parent_bone_keep_world(knee,arm,side+"LowerLeg")
    lower=add_cone_between(side+"_Trouser_Lower",(x,0.003,0.60),(x,-0.002,0.18),0.087,0.069,MAT_PANTS)
    parent_bone_keep_world(lower,arm,side+"LowerLeg")
    shoe=add_box(side+"_Shoe",(x,-0.105,0.095),(0.155,0.315,0.115),MAT_SHOE,0.032,char_col)
    parent_bone_keep_world(shoe,arm,side+"Foot")
    sole=add_box(side+"_Sole",(x,-0.108,0.038),(0.158,0.322,0.032),MAT_SOLE,0.010,detail_col)
    parent_bone_keep_world(sole,arm,side+"Foot")

# Shirt sleeves / hands
for side,sx in (("Left",-1),("Right",1)):
    a0=(0.205*sx,0,1.46); a1=(0.39*sx,0.004,1.25); a2=(0.455*sx,-0.005,1.01)
    shoulder=add_uv(side+"_Shirt_Shoulder",(0.205*sx,0,1.455),(0.083,0.086,0.093),MAT_WHITE,char_col,32,18)
    parent_bone_keep_world(shoulder,arm,side+"UpperArm")
    upper=add_cone_between(side+"_Shirt_Upper",a0,a1,0.079,0.066,MAT_WHITE)
    parent_bone_keep_world(upper,arm,side+"UpperArm")
    elbow=add_uv(side+"_Shirt_Elbow",(0.39*sx,0.004,1.25),(0.066,0.065,0.076),MAT_WHITE,char_col,32,18)
    parent_bone_keep_world(elbow,arm,side+"LowerArm")
    lower=add_cone_between(side+"_Shirt_Lower",a1,a2,0.066,0.052,MAT_WHITE)
    parent_bone_keep_world(lower,arm,side+"LowerArm")
    cuff=add_box(side+"_Cuff",(0.455*sx,-0.010,1.025),(0.105,0.102,0.065),MAT_WHITE,0.012,detail_col)
    parent_bone_keep_world(cuff,arm,side+"LowerArm")
    palm=add_uv(side+"_Hand",(0.465*sx,-0.023,0.945),(0.052,0.043,0.082),MAT_SKIN_LIGHT,char_col,32,18)
    parent_bone_keep_world(palm,arm,side+"Hand")
    # thumb cue
    thumb=add_uv(side+"_Thumb",(0.423*sx,-0.045,0.955),(0.020,0.018,0.050),MAT_SKIN_LIGHT,detail_col,24,12)
    parent_bone_keep_world(thumb,arm,side+"Hand")

# Neck and head
neck=add_cone_between("Neck_Skin",(0,0,1.53),(0,0,1.66),0.072,0.068,MAT_SKIN_LIGHT)
parent_bone_keep_world(neck,arm,"Neck")
head=add_uv("Head_Mesh",(0,-0.004,1.755),(0.112,0.102,0.148),MAT_SKIN_LIGHT,char_col,56,32)
parent_bone_keep_world(head,arm,"Head")
# jaw / chin subtly square
# hair cap and side masses
hair=add_uv("Hair_Cap",(0,0.009,1.848),(0.116,0.105,0.082),MAT_HAIR,char_col,48,24)
parent_bone_keep_world(hair,arm,"Head")
for sx in (-1,1):
    sidehair=add_uv("Hair_Side",(0.092*sx,0.008,1.805),(0.026,0.086,0.085),MAT_HAIR,detail_col,28,16)
    parent_bone_keep_world(sidehair,arm,"Head")
    ear=add_uv("Ear",(0.112*sx,0.0,1.755),(0.023,0.016,0.038),MAT_SKIN_LIGHT,detail_col,24,14)
    parent_bone_keep_world(ear,arm,"Head")
    eye_white=add_uv("Eye_White",(0.040*sx,-0.099,1.775),(0.021,0.007,0.012),MAT_EYE_WHITE,detail_col,24,12)
    parent_bone_keep_world(eye_white,arm,"Head")
    iris=add_uv("Eye_Iris",(0.040*sx,-0.107,1.774),(0.0075,0.004,0.0075),MAT_EYE,detail_col,20,10)
    parent_bone_keep_world(iris,arm,"Head")
    brow=add_box("Brow",(0.041*sx,-0.108,1.807),(0.054,0.008,0.010),MAT_HAIR,0.003,detail_col)
    brow.rotation_euler[1]=math.radians(-5*sx)
    parent_bone_keep_world(brow,arm,"Head")
nose=add_uv("Nose",(0,-0.113,1.748),(0.021,0.034,0.038),MAT_SKIN_LIGHT,detail_col,28,14)
parent_bone_keep_world(nose,arm,"Head")
nose_tip=add_uv("Nose_Tip",(0,-0.132,1.736),(0.026,0.022,0.021),MAT_SKIN_LIGHT,detail_col,24,12)
parent_bone_keep_world(nose_tip,arm,"Head")
mouth=add_box("Mouth",(0,-0.105,1.690),(0.044,0.005,0.005),MAT_LIP,0.002,detail_col)
parent_bone_keep_world(mouth,arm,"Head")
chin=add_uv("Chin",(0,-0.094,1.659),(0.035,0.014,0.020),MAT_SKIN_LIGHT,detail_col,24,12)
parent_bone_keep_world(chin,arm,"Head")

# Tray and service props
tray=add_disc("Service_Tray",(-0.405,-0.245,1.195),0.285,0.022,MAT_METAL,prop_col)
parent_bone_keep_world(tray,arm,"TraySocket")
# actual rim torus
bpy.ops.mesh.primitive_torus_add(major_radius=0.278,minor_radius=0.010,major_segments=64,minor_segments=12,location=(-0.405,-0.245,1.207))
rim=bpy.context.object; rim.name="Service_Tray_Rim"; rim.data.materials.append(MAT_METAL); smooth(rim); move_to_collection(rim,prop_col); parent_bone_keep_world(rim,arm,"TraySocket")
plate=add_disc("Tray_Plate",(-0.475,-0.245,1.218),0.091,0.012,MAT_WHITE,prop_col)
parent_bone_keep_world(plate,arm,"TraySocket")
# low bowl / dish
bowl=add_uv("Tray_Dish",(-0.475,-0.245,1.236),(0.057,0.057,0.020),MAT_WHITE,prop_col,32,16)
parent_bone_keep_world(bowl,arm,"TraySocket")
# glass
glass=add_cone_between("Tray_Glass",(-0.305,-0.245,1.215),(-0.305,-0.245,1.33),0.029,0.036,MAT_WHITE,prop_col,32)
parent_bone_keep_world(glass,arm,"TraySocket")

# Animation helpers
if not arm.animation_data:
    arm.animation_data_create()

def clear_pose():
    for pb in arm.pose.bones:
        pb.location=(0,0,0); pb.rotation_euler=(0,0,0); pb.scale=(1,1,1)

def activate_action(name,start,end):
    action=bpy.data.actions.get(name) or bpy.data.actions.new(name)
    action.use_fake_user=True
    arm.animation_data.action=action
    scene.frame_start=start; scene.frame_end=end
    # Every clip explicitly owns a neutral value for every bone.
    # This prevents pose leakage when switching actions and makes FBX/GLB baking deterministic.
    for pb in arm.pose.bones:
        pb.location=(0,0,0); pb.rotation_euler=(0,0,0); pb.scale=(1,1,1)
        pb.keyframe_insert(data_path="rotation_euler",frame=start,group=pb.name)
        pb.keyframe_insert(data_path="location",frame=start,group=pb.name)
        pb.keyframe_insert(data_path="rotation_euler",frame=end,group=pb.name)
        pb.keyframe_insert(data_path="location",frame=end,group=pb.name)
    return action

def key_bone(name,frame,rot=(0,0,0),loc=(0,0,0)):
    pb=arm.pose.bones[name]
    pb.rotation_mode='XYZ'
    pb.rotation_euler=tuple(math.radians(v) for v in rot)
    pb.location=loc
    pb.keyframe_insert(data_path="rotation_euler",frame=frame,group=name)
    pb.keyframe_insert(data_path="location",frame=frame,group=name)

def soften(action):
    try:
        for fc in action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation='BEZIER'
    except Exception:
        pass

def carry_left(frame):
    key_bone("LeftUpperArm",frame,rot=(-38,0,-10))
    key_bone("LeftLowerArm",frame,rot=(-68,0,4))
    key_bone("LeftHand",frame,rot=(16,0,1))

# Idle
clear_pose(); idle=activate_action("BB_Waiter_Idle_02",1,120)
for f,z,chx,headz in ((1,0,0,0),(30,0.004,0.45,-0.5),(60,0,0,0),(90,0.004,-0.4,0.5),(120,0,0,0)):
    key_bone("Root",f,loc=(0,0,z))
    key_bone("Chest",f,rot=(chx,0,0))
    key_bone("Head",f,rot=(0,0,headz))
    key_bone("LeftUpperArm",f,rot=(1.5,0,-1.5))
    key_bone("RightUpperArm",f,rot=(-1.5,0,1.5))
soften(idle)

walk_poses={
    1: dict(ll=25,rl=-20,lk=7,rk=22,la=-19,ra=19,z=0.000,tw=-2.0),
    11:dict(ll=4,rl=-7,lk=34,rk=8,la=-4,ra=4,z=0.020,tw=-0.5),
    21:dict(ll=-20,rl=25,lk=22,rk=7,la=19,ra=-19,z=0.000,tw=2.0),
    31:dict(ll=-7,rl=4,lk=8,rk=34,la=4,ra=-4,z=0.020,tw=0.5),
    41:dict(ll=25,rl=-20,lk=7,rk=22,la=-19,ra=19,z=0.000,tw=-2.0),
}

# Walk
clear_pose(); walk=activate_action("BB_Waiter_Walk_02",1,41)
for f,p in walk_poses.items():
    key_bone("Root",f,loc=(0,0,p['z']))
    key_bone("Hips",f,rot=(0,p['tw']*0.24,p['tw']*0.18))
    key_bone("Chest",f,rot=(0,-p['tw']*0.38,-p['tw']*0.12))
    key_bone("LeftUpperLeg",f,rot=(p['ll'],0,0)); key_bone("RightUpperLeg",f,rot=(p['rl'],0,0))
    key_bone("LeftLowerLeg",f,rot=(-p['lk'],0,0)); key_bone("RightLowerLeg",f,rot=(-p['rk'],0,0))
    key_bone("LeftFoot",f,rot=(max(-9,min(11,-p['ll']*0.32)),0,0))
    key_bone("RightFoot",f,rot=(max(-9,min(11,-p['rl']*0.32)),0,0))
    key_bone("LeftUpperArm",f,rot=(p['la'],0,-2)); key_bone("RightUpperArm",f,rot=(p['ra'],0,2))
    key_bone("LeftLowerArm",f,rot=(-9 if p['la']<0 else -13,0,0)); key_bone("RightLowerArm",f,rot=(-9 if p['ra']<0 else -13,0,0))
    key_bone("Head",f,rot=(0,0,-p['tw']*0.16))
soften(walk)

# Walk tray
clear_pose(); wtray=activate_action("BB_Waiter_WalkTray_02",1,41)
for f,p in walk_poses.items():
    key_bone("Root",f,loc=(0,0,p['z']*0.65))
    key_bone("Hips",f,rot=(0,p['tw']*0.18,p['tw']*0.12))
    key_bone("Chest",f,rot=(0,-p['tw']*0.10,-p['tw']*0.05))
    key_bone("LeftUpperLeg",f,rot=(p['ll']*0.90,0,0)); key_bone("RightUpperLeg",f,rot=(p['rl']*0.90,0,0))
    key_bone("LeftLowerLeg",f,rot=(-p['lk'],0,0)); key_bone("RightLowerLeg",f,rot=(-p['rk'],0,0))
    key_bone("LeftFoot",f,rot=(max(-8,min(10,-p['ll']*0.28)),0,0)); key_bone("RightFoot",f,rot=(max(-8,min(10,-p['rl']*0.28)),0,0))
    carry_left(f)
    key_bone("RightUpperArm",f,rot=(p['ra']*0.62,0,2)); key_bone("RightLowerArm",f,rot=(-11,0,0))
    key_bone("TraySocket",f,rot=(0,0,0),loc=(0,0,0))
    key_bone("Head",f,rot=(0,0,-p['tw']*0.07))
soften(wtray)

# Idle tray
clear_pose(); itray=activate_action("BB_Waiter_IdleTray_02",1,96)
for f,z,hz in ((1,0,0),(24,0.003,-0.4),(48,0,0),(72,0.003,0.4),(96,0,0)):
    key_bone("Root",f,loc=(0,0,z)); carry_left(f); key_bone("TraySocket",f,rot=(0,0,0)); key_bone("Head",f,rot=(0,0,hz))
soften(itray)

# Pickup tray: tray begins lower; waiter reaches then lifts
clear_pose(); pickup=activate_action("BB_Waiter_PickupTray_01",1,64)
pickup_data=[
    (1,-0.42, 5,-8,0),
    (18,-0.42,-20,-35,-7),
    (34,-0.22,-31,-52,-5),
    (50,-0.06,-37,-64,-2),
    (64,0.00,-38,-68,0),
]
for f,tz,ua,la,ch in pickup_data:
    key_bone("TraySocket",f,rot=(0,0,0),loc=(0,0,tz))
    key_bone("Chest",f,rot=(ch,0,0))
    key_bone("LeftUpperArm",f,rot=(ua,0,-8))
    key_bone("LeftLowerArm",f,rot=(la,0,4))
    key_bone("LeftHand",f,rot=(14,0,0))
    key_bone("RightUpperArm",f,rot=(-3,0,2))
soften(pickup)

# Serve
clear_pose(); serve=activate_action("BB_Waiter_Serve_02",1,64)
serve_data=[
    (1,0,-4,-9,-9,0),
    (16,-2,-18,-28,-18,-2),
    (32,-4,-38,-47,-14,-4),
    (48,-2,-19,-30,-18,-2),
    (64,0,-4,-9,-9,0),
]
for f,ch,rua,rla,rh,tw in serve_data:
    carry_left(f); key_bone("TraySocket",f,rot=(0,0,0))
    key_bone("Chest",f,rot=(ch,tw,0))
    key_bone("RightUpperArm",f,rot=(rua,0,7))
    key_bone("RightLowerArm",f,rot=(rla,0,-5))
    key_bone("RightHand",f,rot=(rh,0,0))
soften(serve)

# Set-down is reverse-ish from carry to lower surface
clear_pose(); putdown=activate_action("BB_Waiter_PutDownTray_01",1,64)
put_data=[
    (1,0.00,-38,-68,0),
    (16,-0.05,-37,-63,-2),
    (32,-0.20,-30,-50,-5),
    (48,-0.38,-19,-33,-7),
    (64,-0.42,4,-8,0),
]
for f,tz,ua,la,ch in put_data:
    key_bone("TraySocket",f,rot=(0,0,0),loc=(0,0,tz))
    key_bone("Chest",f,rot=(ch,0,0))
    key_bone("LeftUpperArm",f,rot=(ua,0,-8))
    key_bone("LeftLowerArm",f,rot=(la,0,4))
    key_bone("LeftHand",f,rot=(14,0,0))
soften(putdown)

# Environment
bpy.ops.mesh.primitive_plane_add(size=20,location=(0,0,0))
floor=bpy.context.object; floor.name="Studio_Floor"; floor.data.materials.append(MAT_FLOOR); move_to_collection(floor,env_col)
back=add_box("Backdrop",(0,2.55,2.0),(8,0.18,5.4),MAT_FLOOR,0.06,env_col)

def point_camera(obj,target):
    direction=Vector(target)-obj.location
    obj.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()

bpy.ops.object.camera_add(location=(3.2,-6.5,2.35))
cam=bpy.context.object; cam.name="Waiter_Camera"; cam.data.lens=66; cam.data.sensor_width=36; point_camera(cam,(0,0,0.98)); scene.camera=cam; move_to_collection(cam,env_col)

def add_area(name,loc,energy,size,color):
    bpy.ops.object.light_add(type='AREA',location=loc)
    l=bpy.context.object; l.name=name; l.data.energy=energy; l.data.shape='DISK'; l.data.size=size; l.data.color=color
    point_camera(l,(0,0,1.0)); move_to_collection(l,env_col); return l

add_area("Key",(-3.0,-3.8,4.8),1150,3.0,(1.0,0.86,0.74))
add_area("Fill",(3.4,-2.5,3.4),650,3.2,(0.72,0.82,1.0))
add_area("Rim",(1.0,2.8,4.2),900,2.4,(1.0,0.92,0.80))

# Validation
def set_action(name,frame):
    arm.animation_data.action=bpy.data.actions[name]
    scene.frame_set(frame); bpy.context.view_layer.update()

def tray_tilt_deg():
    z=tray.matrix_world.to_3x3()@Vector((0,0,1)); z.normalize()
    d=max(-1.0,min(1.0,z.dot(Vector((0,0,1)))))
    return math.degrees(math.acos(d))

tests=[]
def test(name,passed,value=None,limit=None,note=""):
    tests.append({"name":name,"passed":bool(passed),"value":value,"limit":limit,"note":note})

expected_actions=[
    "BB_Waiter_Idle_02","BB_Waiter_Walk_02","BB_Waiter_WalkTray_02","BB_Waiter_IdleTray_02",
    "BB_Waiter_PickupTray_01","BB_Waiter_Serve_02","BB_Waiter_PutDownTray_01"
]
for n in expected_actions: test("action_exists:"+n,n in bpy.data.actions)

# Loop checks
for action_name,end in (("BB_Waiter_Walk_02",41),("BB_Waiter_WalkTray_02",41)):
    set_action(action_name,1)
    snap={}
    for bn in ("Root","Hips","Chest","LeftUpperLeg","RightUpperLeg","LeftUpperArm","RightUpperArm"):
        pb=arm.pose.bones[bn]; snap[bn]=(Vector(pb.location),Vector(pb.rotation_euler))
    set_action(action_name,end)
    delta=0.0
    for bn,(l,r) in snap.items():
        pb=arm.pose.bones[bn]
        delta=max(delta,(Vector(pb.location)-l).length,(Vector(pb.rotation_euler)-r).length)
    test("loop_continuity:"+action_name,delta<1e-5,round(delta,7),1e-5)

# Tray stabilization
max_tilt=0.0; max_jitter=0.0
for action_name,frames in (
    ("BB_Waiter_WalkTray_02",range(1,42)),
    ("BB_Waiter_IdleTray_02",range(1,97,2)),
    ("BB_Waiter_Serve_02",range(1,65)),
):
    zs=[]; tilts=[]
    for f in frames:
        set_action(action_name,f); zs.append(tray.matrix_world.translation.z); tilts.append(tray_tilt_deg())
    mt=max(tilts); mj=max(zs)-min(zs)
    max_tilt=max(max_tilt,mt); max_jitter=max(max_jitter,mj)
    test("tray_tilt:"+action_name,mt<=0.5,round(mt,4),0.5,"degrees")
    test("tray_jitter:"+action_name,mj<=0.025,round(mj,4),0.025,"meters")

# Height / proportions
set_action("BB_Waiter_Idle_02",1)
mins=[]; maxs=[]
for o in list(char_col.objects)+list(detail_col.objects):
    if o.type=='MESH':
        for c in o.bound_box:
            w=o.matrix_world@Vector(c); mins.append(w.z); maxs.append(w.z)
height=max(maxs)-min(mins)
test("character_height",1.70<=height<=1.95,round(height,3),[1.70,1.95],"meters")

# Left hand near tray during carrying pose
set_action("BB_Waiter_WalkTray_02",11)
lh=arm.matrix_world@arm.pose.bones["LeftHand"].tail
tp=tray.matrix_world.translation
dist=(lh-tp).length
test("carry_hand_near_tray",dist<0.42,round(dist,3),0.42,"meters")

# Floor sanity using shoe/sole bbox bottoms in walk samples
min_bottom=99.0
for action_name in ("BB_Waiter_Walk_02","BB_Waiter_WalkTray_02"):
    for f in range(1,42,2):
        set_action(action_name,f)
        for name in ("Left_Sole","Right_Sole"):
            o=bpy.data.objects[name]
            min_bottom=min(min_bottom,min((o.matrix_world@Vector(c)).z for c in o.bound_box))
test("shoe_floor_penetration",min_bottom>-0.10,round(min_bottom,4),-0.10,"meters coarse")

finite=True
for action_name in expected_actions:
    end=120 if "Idle_02"==action_name.split("Waiter_")[-1] else 64
    for f in (1,min(16,scene.frame_end),min(32,scene.frame_end)):
        set_action(action_name,min(f,scene.frame_end))
        for o in bpy.data.objects:
            for row in o.matrix_world:
                for v in row:
                    if not math.isfinite(v): finite=False
test("finite_transforms",finite)

# Render helpers
def props_visible(show):
    for o in prop_col.objects:
        o.hide_render=not show

def render(action,frame,filename,camloc=None,target=(0,0,1.0),show_props=False):
    set_action(action,frame); props_visible(show_props)
    if camloc:
        cam.location=camloc; point_camera(cam,target)
    scene.render.filepath=os.path.join(LOG,filename)
    back_hidden = bool(camloc and camloc[1] > 3.0)
    back.hide_render = back_hidden
    bpy.ops.render.render(write_still=True)
    back.hide_render = False
    test("render:"+filename,os.path.exists(scene.render.filepath) and os.path.getsize(scene.render.filepath)>10000,os.path.getsize(scene.render.filepath) if os.path.exists(scene.render.filepath) else 0,10000)

# Neutral reference views
render("BB_Waiter_Idle_02",1,"01_neutral_front.png",(0,-6.3,2.25),(0,0,1.0),False)
render("BB_Waiter_Idle_02",1,"02_neutral_3q.png",(3.25,-5.7,2.35),(0,0,1.0),False)
render("BB_Waiter_Idle_02",1,"03_neutral_side.png",(6.1,0,2.25),(0,0,1.0),False)
render("BB_Waiter_Idle_02",1,"04_neutral_back.png",(0,6.3,2.25),(0,0,1.0),False)
render("BB_Waiter_Walk_02",1,"05_walk_contact.png",(3.25,-5.7,2.35),(0,0,1.0),False)
render("BB_Waiter_WalkTray_02",11,"06_walk_tray.png",(3.25,-5.7,2.35),(0,0,1.0),True)
render("BB_Waiter_Serve_02",32,"07_serve.png",(3.25,-5.7,2.35),(0,0,1.0),True)
render("BB_Waiter_PickupTray_01",34,"08_pickup_mid.png",(3.25,-5.7,2.35),(0,0,1.0),True)
render("BB_Waiter_PutDownTray_01",32,"09_putdown_mid.png",(3.25,-5.7,2.35),(0,0,1.0),True)

props_visible(True)
set_action("BB_Waiter_Idle_02",1)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.object.select_all(action='DESELECT')
for col in (char_col,detail_col,prop_col):
    for o in col.objects: o.select_set(True)
arm.select_set(True); bpy.context.view_layer.objects.active=arm

glb_ok=True; glb_error=""
try:
    bpy.ops.export_scene.gltf(filepath=GLB_PATH,export_format='GLB',use_selection=True,export_animations=True)
except Exception as e:
    glb_ok=False; glb_error=repr(e)
test("export_glb",glb_ok and os.path.exists(GLB_PATH) and os.path.getsize(GLB_PATH)>10000,os.path.getsize(GLB_PATH) if os.path.exists(GLB_PATH) else 0,10000,glb_error)

fbx_ok=True; fbx_error=""
try:
    bpy.ops.export_scene.fbx(filepath=FBX_PATH,use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_simplify_factor=0.0,axis_forward='-Z',axis_up='Y')
except Exception as e:
    fbx_ok=False; fbx_error=repr(e)
test("export_fbx",fbx_ok and os.path.exists(FBX_PATH) and os.path.getsize(FBX_PATH)>10000,os.path.getsize(FBX_PATH) if os.path.exists(FBX_PATH) else 0,10000,fbx_error)

report={
    "asset":"BB_Waiter_VisualV2",
    "blender_version":bpy.app.version_string,
    "visual_target":"semi-realistic game waiter based on supplied frontal/back/profile references",
    "actions":expected_actions,
    "metrics":{
        "bone_count":len(arm.data.bones),
        "mesh_count":len([o for o in bpy.data.objects if o.type=='MESH' and o.name not in ("Studio_Floor","Backdrop")]),
        "character_height_m":round(height,3),
        "max_tray_tilt_deg":round(max_tilt,4),
        "max_tray_vertical_jitter_m":round(max_jitter,4),
        "carry_hand_tray_distance_m":round(dist,3),
    },
    "summary":{"passed":sum(1 for t in tests if t["passed"]),"failed":sum(1 for t in tests if not t["passed"]),"total":len(tests)},
    "tests":tests,
    "outputs":{
        "blend":BLEND_PATH,"glb":GLB_PATH,"fbx":FBX_PATH,
        "renders":[os.path.join(LOG,f) for f in (
            "01_neutral_front.png","02_neutral_3q.png","03_neutral_side.png","04_neutral_back.png",
            "05_walk_contact.png","06_walk_tray.png","07_serve.png","08_pickup_mid.png","09_putdown_mid.png"
        )]
    }
}
with open(REPORT_PATH,"w",encoding="utf-8") as f: json.dump(report,f,indent=2,ensure_ascii=False)
print("WAITER_VISUAL_V2_COMPLETE")
print(json.dumps(report["summary"]))
print(json.dumps(report["metrics"]))
