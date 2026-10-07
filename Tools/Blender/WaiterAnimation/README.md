# BB Waiter Animation Lab V1

Technical Blender 5.1 prototype for Bistro Builder waiter animation.

## Purpose

Validate a reusable humanoid rig and the first service animation set before binding it to final production character art.

The prototype follows the supplied visual direction:
- male waiter proportions;
- white long-sleeve shirt;
- black vest;
- black trousers;
- black shoes;
- dark short hair;
- service tray carried on the left side.

## Generated actions

- BB_Waiter_Idle_01
- BB_Waiter_Walk_01
- BB_Waiter_WalkTray_01
- BB_Waiter_IdleTray_01
- BB_Waiter_Serve_01

## Rig

21 bones including a non-deforming Root and a stable TraySocket.
The tray socket is parented to Root rather than the torso so torso sway does not tilt the tray.

## Outputs

Assets/Art/Characters/Waiter/Prototype/BB_Waiter_AnimationLab_V1.blend
Assets/Art/Characters/Waiter/Prototype/BB_Waiter_AnimationLab_V1.fbx
Assets/Art/Characters/Waiter/Prototype/BB_Waiter_AnimationLab_V1.glb

## Automated validation

Build/animation checks: 19/19 PASS.
GLB/FBX round-trip checks: 12/12 PASS.
Total: 31/31 PASS.

Verified:
- expected actions exist;
- walk and walk-with-tray loops close exactly;
- tray tilt stays at 0 degrees in current control implementation;
- tray vertical jitter stays within configured tolerance;
- coarse foot/floor sanity;
- finite transforms;
- FBX and GLB export;
- FBX and GLB re-import;
- 21-bone rig preserved;
- 41 meshes preserved;
- all five expected animation clips survive both round trips.

## Scripts

build_waiter_animation_lab.py rebuilds the source, exports and preview renders.
validate_waiter_roundtrip.py reimports both exported formats in clean Blender sessions.

This is an animation/rig prototype, not the final skinned production likeness.
