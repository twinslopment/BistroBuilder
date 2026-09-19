# Assets4All — Automatic Anomaly Repair Intelligence

## Product requirement

Assets4All treats common AI-generator/Meshy defects as problems to solve automatically, not as reasons to send the user into polygon editing.

The system still protects SOURCE and never keeps a speculative repair unless post-repair validation proves that the result is non-regressive.

## Self-Healing Repair Cycle (SHRC)

1. Analyse WORK and establish a baseline.
2. Detect anomaly classes and produce repair hypotheses.
3. Apply hypotheses only to transactional WORK data.
4. Re-analyse the complete asset.
5. Compare PVS, CSE, dimensions, Ground Integrity, topology and geometry loss against the baseline.
6. Accept the repair only when it improves the relevant gate without unacceptable regressions.
7. Otherwise rollback automatically and try the next future hypothesis.

This is the core policy for progressively handling increasingly difficult Meshy artifacts.

## v0.1 implemented anomaly classes

- loose vertices;
- degenerate faces;
- extremely tiny disconnected debris;
- normal recalculation where topology is safe enough;
- localized downward spikes below the robust support plane;
- floating floor assets that can be grounded by a safe translation;
- transactional rollback on PVS, Ground Integrity or dimensional regression.

## Next anomaly classes

A4A-003/004 will use Region Consensus + Region DNA before destructive decisions so Assets4All can address larger fused anomalies, duplicated semantic parts, malformed supports, accidental bridges between regions and inconsistent AI-generated substructures.

The intended end state is not “safe cleanup only”. It is a repair planner that can compare several candidate reconstructions and keep the best validated result automatically.

## Repair Tournament direction

For ambiguous defects Assets4All will be able to create several temporary WORK candidates, for example:

- preserve geometry;
- trim outlier;
- region-local remesh;
- symmetry reconstruction;
- remove suspicious region;
- reconstruct from symmetric peer;
- local surface relaxation;
- semantic separation and reconnect.

Each candidate is scored independently. The winner must pass hard invariants such as ground contact, dimensional envelope, profile plausibility and no severe topology regression.

This avoids one irreversible “magic repair” and turns uncertain repair into a measurable search problem.
