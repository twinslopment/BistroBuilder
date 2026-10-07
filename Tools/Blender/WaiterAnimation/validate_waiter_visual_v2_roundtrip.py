import bpy
import json
import os

ROOT = r"C:\Users\mruperez\ProyectoBB\BistroBuilder_Worktrees\character-animation-v1"
ASSET = os.path.join(ROOT, "Assets", "Art", "Characters", "Waiter", "VisualV2")
LOG = os.path.join(ROOT, "Logs", "WaiterAnimationV2")
GLB = os.path.join(ASSET, "BB_Waiter_VisualV2.glb")
FBX = os.path.join(ASSET, "BB_Waiter_VisualV2.fbx")
OUT = os.path.join(LOG, "waiter_visual_v2_roundtrip.json")

expected = {
    "BB_Waiter_Idle_02",
    "BB_Waiter_Walk_02",
    "BB_Waiter_WalkTray_02",
    "BB_Waiter_IdleTray_02",
    "BB_Waiter_PickupTray_01",
    "BB_Waiter_Serve_02",
    "BB_Waiter_PutDownTray_01",
}

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

def snapshot(label):
    arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    actions=sorted(a.name for a in bpy.data.actions)
    matched=[]
    for e in expected:
        if any(e in n for n in actions):
            matched.append(e)
    return {
        "label":label,
        "armatures":len(arms),
        "meshes":len(meshes),
        "bones":sum(len(o.data.bones) for o in arms),
        "actions":actions,
        "matched_expected":sorted(matched),
        "has_tray":any("Tray" in o.name for o in bpy.data.objects),
        "has_vest":any("Vest" in o.name for o in bpy.data.objects),
        "has_face":all(any(k in o.name for o in bpy.data.objects) for k in ("Eye","Nose","Mouth")),
        "has_rig":any("Waiter_Rig" in o.name for o in arms),
    }

results={}
clear()
ok=True; err=""
try:
    bpy.ops.import_scene.gltf(filepath=GLB)
except Exception as e:
    ok=False; err=repr(e)
results["glb"]=snapshot("GLB"); results["glb"]["import_ok"]=ok; results["glb"]["error"]=err

clear()
ok=True; err=""
try:
    bpy.ops.import_scene.fbx(filepath=FBX)
except Exception as e:
    ok=False; err=repr(e)
results["fbx"]=snapshot("FBX"); results["fbx"]["import_ok"]=ok; results["fbx"]["error"]=err

checks=[]
def ck(name,passed,value=None):
    checks.append({"name":name,"passed":bool(passed),"value":value})

for key in ("glb","fbx"):
    r=results[key]
    ck(key+"_import",r["import_ok"])
    ck(key+"_armature",r["armatures"]==1,r["armatures"])
    ck(key+"_bones",r["bones"]==21,r["bones"])
    ck(key+"_meshes",r["meshes"]>=55,r["meshes"])
    ck(key+"_actions_all",len(r["matched_expected"])==7,len(r["matched_expected"]))
    ck(key+"_tray",r["has_tray"])
    ck(key+"_vest",r["has_vest"])
    ck(key+"_face",r["has_face"])
    ck(key+"_rig",r["has_rig"])

report={
    "results":results,
    "checks":checks,
    "summary":{
        "passed":sum(1 for c in checks if c["passed"]),
        "failed":sum(1 for c in checks if not c["passed"]),
        "total":len(checks),
    }
}
with open(OUT,"w",encoding="utf-8") as f:
    json.dump(report,f,indent=2,ensure_ascii=False)
print("WAITER_V2_ROUNDTRIP_COMPLETE")
print(json.dumps(report["summary"]))
print(json.dumps({k:{kk:v for kk,v in results[k].items() if kk!="actions"} for k in results}))
