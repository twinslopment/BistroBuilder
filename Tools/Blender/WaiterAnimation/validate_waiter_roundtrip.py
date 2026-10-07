import bpy
import json
import os

ROOT = r"C:\Users\mruperez\ProyectoBB\BistroBuilder_Worktrees\character-animation-v1"
ASSET = os.path.join(ROOT, "Assets", "Art", "Characters", "Waiter", "Prototype")
LOG = os.path.join(ROOT, "Logs", "WaiterAnimation")
GLB = os.path.join(ASSET, "BB_Waiter_AnimationLab_V1.glb")
FBX = os.path.join(ASSET, "BB_Waiter_AnimationLab_V1.fbx")
OUT = os.path.join(LOG, "waiter_roundtrip_tests.json")

expected = {
    "BB_Waiter_Idle_01",
    "BB_Waiter_Walk_01",
    "BB_Waiter_WalkTray_01",
    "BB_Waiter_IdleTray_01",
    "BB_Waiter_Serve_01",
}

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

def snapshot(label):
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    bones = sum(len(o.data.bones) for o in arms)
    actions = sorted(a.name for a in bpy.data.actions)
    matched = sorted(n for n in actions if any(k in n for k in expected))
    return {
        "label": label,
        "armatures": len(arms),
        "meshes": len(meshes),
        "bones": bones,
        "actions": actions,
        "expected_action_matches": matched,
        "has_tray": any("Tray" in o.name for o in bpy.data.objects),
        "has_rig": any("Waiter_Rig" in o.name for o in arms),
    }

results = {}

clear()
glb_ok = True
glb_error = ""
try:
    bpy.ops.import_scene.gltf(filepath=GLB)
except Exception as e:
    glb_ok = False
    glb_error = repr(e)
results["glb"] = snapshot("GLB")
results["glb"]["import_ok"] = glb_ok
results["glb"]["error"] = glb_error

clear()
fbx_ok = True
fbx_error = ""
try:
    bpy.ops.import_scene.fbx(filepath=FBX)
except Exception as e:
    fbx_ok = False
    fbx_error = repr(e)
results["fbx"] = snapshot("FBX")
results["fbx"]["import_ok"] = fbx_ok
results["fbx"]["error"] = fbx_error

checks = []
for key in ("glb","fbx"):
    r = results[key]
    checks.append({"name":key+"_import", "passed":r["import_ok"]})
    checks.append({"name":key+"_armature", "passed":r["armatures"] >= 1, "value":r["armatures"]})
    checks.append({"name":key+"_bones", "passed":r["bones"] >= 20, "value":r["bones"]})
    checks.append({"name":key+"_meshes", "passed":r["meshes"] >= 30, "value":r["meshes"]})
    checks.append({"name":key+"_tray", "passed":r["has_tray"]})
    checks.append({"name":key+"_actions", "passed":len(r["expected_action_matches"]) >= 5, "value":len(r["expected_action_matches"])})

report = {
    "results": results,
    "checks": checks,
    "summary": {
        "passed": sum(1 for c in checks if c["passed"]),
        "failed": sum(1 for c in checks if not c["passed"]),
        "total": len(checks),
    }
}
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(report, f, indent=2, ensure_ascii=False)
print("ROUNDTRIP_COMPLETE")
print(json.dumps(report["summary"]))
print(json.dumps({k:{kk:v for kk,v in results[k].items() if kk not in ("actions",)} for k in results}))
