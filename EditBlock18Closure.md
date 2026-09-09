# Edit Mode 18M — canonical scene closure

Prototype_Restaurant now contains the verified recovery candidate, applied with explicit owner authorization on 2026-09-09. The original scene GUID is preserved.

Unity 6000.3.19f1 / Tundra compilation succeeded. The canonical scene Queen Test passed after application: economic commit, BBSIS, Navigation, Save/Load, service-state gate and rollback. See EditBlock18QueenTestReport.txt. Scene validation passed with 79 checks and no failures during Play Mode.

Previously executed on the identical candidate scene: Edit Core 84/0, lifecycle and SaveGuard 20/0, Finance 20/0, BBSIS 26/0; Navigation 17 Play Mode PASS; active-service Save/Load 368EF PASS (customer, waiter, preparing order, kitchen time, inventory, temporary table reservation and clock restored). This does not certify meal delivery and final payment end to end.

Finance uses the existing owner-authorized provisional playtest tariffs, revision playtest-2026-09-08-r1. No prices were invented. Production balancing remains outside this verification.

The branch includes a separate restoration commit for shared dependencies missing from the BBSIS baseline, followed by the 18M scene and verification changes. Unsorted safety history, backup files, temporary reference scene and export scripts are excluded. Other feature branches are unchanged.

Pre-application scene SHA256: DBAA786925AEDA1A4DA895E52BBBBEA637804BE34A2B028937348322901E474A
Verified candidate/applied scene SHA256: D2AF8936074A367060F44F087FAD1C84ED93539D80000ED25EC6283D7E1A5325
