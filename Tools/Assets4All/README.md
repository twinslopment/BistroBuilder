# Assets4All

Assets4All is an experimental, engine-agnostic pipeline for turning existing 3D assets into game-ready assets with minimal human intervention.

Initial proving ground:

`Meshy / GLB / FBX / OBJ -> Assets4All -> Unity Adapter -> Bistro Builder Adapter`

The core must not depend on Bistro Builder or Meshy. Bistro Builder is the first production consumer and regression environment.

## Product goals

- Typical automatic processing time for an accepted static asset: **< 3 minutes**.
- Typical human review time: **< 30 seconds**.
- Manual polygon/face selection in the normal workflow: **0**.
- If predicted manual repair exceeds the configured profitability threshold, recommend regenerating the source instead of repairing it.
- Preserve an immutable source and make all processing non-destructive.
- Every floor-standing furniture asset must pass the Ground Integrity Gate before export.

## Core concepts

- `SOURCE`: immutable source geometry.
- `WORK`: non-destructive processing copy.
- `PREVIEW`: disposable visualization.
- `EXPORT`: approved output.
- `ProcessingViabilityScore`: repairability/technical viability score.
- `ConversionSuccessEstimate`: independent probability-oriented estimate of reaching game-ready state within the human-time budget.
- `Region Consensus Engine`: multi-signal, stability-tested region discovery system.
- `Ground Integrity Gate`: verifies that floor-standing assets neither penetrate nor float above the ground plane.

See `docs/Assets4All_v0.1_SPEC.md` for the current functional specification.
