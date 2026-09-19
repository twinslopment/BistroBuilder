# Assets4All Blender Extension — v0.1 foundation

This folder is the first usable Blender-side foundation for Assets4All.

Implemented in this slice:

- persistent SOURCE / WORK session identity;
- immutable SOURCE protection and disposable WORK copies;
- large-view workflow using the real Blender 3D viewport;
- real mesh analysis (geometry, UV, topology, components, dimensions, symmetry proxy);
- dual PVS + CSE decision model;
- Ground Integrity with robust support level instead of trusting the lowest vertex;
- a transactional self-healing repair cycle for common AI/Meshy anomalies;
- automatic localized downward-spike correction;
- automatic grounding for floor profiles;
- rollback when repair worsens PVS, Ground Integrity or dimensional silhouette;
- persistent `.asset4all.json` snapshot as a Blender Text datablock;
- quality gates and issue list.

## Automatic-repair principle

Assets4All should repair generator artifacts automatically whenever it can prove the repair is non-regressive. The v0.1 cycle currently covers safe topology cleanup, extremely tiny isolated debris, localized ground spikes and grounding. Future A4A-003+ extends this with Region Consensus / Region DNA so larger fused anomalies can be repaired semantically rather than by blind polygon rules.

The repair path is transactional: mutate WORK, re-analyse, compare against baseline, and keep the mutation only if quality remains stable or improves. SOURCE is never modified.
