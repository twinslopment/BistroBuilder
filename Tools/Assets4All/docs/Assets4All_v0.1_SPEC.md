# Assets4All v0.1 — Functional specification

Status: foundation / design contract.

## 1. Product premise

Assets4All prepares an existing 3D asset for a game engine as quickly as possible while minimizing human intervention. The primary optimization target is **human time per usable asset**, not the number of editing features exposed.

The normal user must correct concepts (asset type, region role, material class), not vertices or polygons.

**Repair-first policy:** normal Meshy/AI-generation defects are problems for Assets4All to solve automatically. A poor initial score selects a deeper automatic repair strategy; it does not immediately tell the user to regenerate. `REGENERATE` is reserved for the exceptional case where the automatic repair tournament cannot produce a validated candidate or the source does not contain enough usable geometric information to reconstruct the intended asset.

## 2. Main experience

The primary UI is a **large dedicated Assets4All workspace**, not a narrow Blender sidebar.

Target layout:

```text
+----------------------------------------------------------------------------------+
| Assets4All | Asset | Profile | AUTO / REVIEW / EXPERT | destination | status     |
+------------------------+-----------------------------------------+---------------+
| Asset / analysis       |                                         | Quality       |
| - Source               |             LARGE 3D PREVIEW            | PVS           |
| - Profile              |                                         | CSE           |
| - Dimensions           |       ground plane always visible       | Gates         |
| - Source/Work state    |                                         | Issues        |
|                        |                                         |               |
+------------------------+-----------------------------------------+---------------+
| [Analyse] [Prepare automatically] [Review issues] [Approve] [Export]             |
+----------------------------------------------------------------------------------+
```

Design rules:

- The preview owns most of the screen.
- Primary actions are large, clearly ordered and state-aware.
- Technical controls stay hidden unless Review/Expert needs them.
- The ground plane is visible for floor-standing profiles.
- Region overlays, symmetry pairs, material roles, problem geometry and collision previews can be toggled without entering Blender Edit Mode.
- Normal flow should be: **Import -> Analyse -> Prepare -> Approve -> Export**.

## 3. Canonical asset session

Every asset has an explicit persistent session:

- `SOURCE`: immutable geometry and source metadata.
- `WORK`: current non-destructive processing copy.
- `PREVIEW`: disposable representation/overlay state.
- `EXPORT`: approved result.

Selection in Blender must never determine which object is the source.

Minimum session identity:

- AssetId
- SourceId
- SourceHash
- SourceObjectIds
- WorkObjectIds
- Revision
- ProfileId
- PipelineState
- AnalysisResult
- QualityGates
- ManualOverrides
- ExportTargets

## 4. Two independent conversion estimators

Assets4All must not make its process/repair-depth decision from one score only.

### 4.1 ProcessingViabilityScore (PVS)

Purpose: estimate the **technical repairability and suitability** of the incoming asset.

PVS is a weighted, interpretable 0–100 score built from observations such as:

- geometry integrity
- topology
- UV readiness
- transform/orientation state
- size plausibility
- artifact severity
- region separability
- symmetry/repetition confidence
- optimization burden
- functional-profile plausibility

PVS answers:

> “How suitable is this asset for the Assets4All processing pipeline?”

### 4.2 ConversionSuccessEstimate (CSE)

Purpose: independently estimate the **probability that automatic processing reaches a game-ready result inside the configured human-review budget**.

CSE must not be a second weighted copy of PVS. It uses a failure-risk model based on independent gate probabilities, uncertainty and weakest-link effects.

Inputs can include:

- probability of safe geometry repair
- probability of stable region segmentation
- probability of correct semantic/material assignment
- probability of ground/pivot/orientation success
- probability of meeting optimization budget without visible damage
- predicted number of ambiguous decisions
- predicted review seconds
- severe-failure flags

CSE answers:

> “What is the estimated chance that automatic conversion succeeds, and how deep a repair strategy should Assets4All use?”

### 4.3 Repair-depth decision matrix

PVS and CSE are shown side-by-side and their disagreement is meaningful.

Examples:

- High PVS + High CSE -> `AUTO`.
- High PVS + Medium/Low CSE -> `STANDARD_REPAIR`; technically good but interpretation is uncertain.
- Medium PVS + High CSE -> `STANDARD_REPAIR`; imperfect but likely easy to fix automatically.
- Low PVS or Low CSE -> `DEEP_REPAIR`; invoke the more expensive repair/region/symmetry strategies.
- Large disagreement -> do not average it away; select at least `STANDARD_REPAIR` and retain the disagreement as diagnostic evidence.

Neither PVS nor CSE emits `REGENERATE` as the normal initial decision. Regeneration can only become the final recommendation **after** automatic repair hypotheses have been tried and validation proves that none is acceptable.

## 5. Region Consensus Engine (RCE)

Assets4All needs a region system that works even when an AI generator produces one fused connected mesh.

The proposed RCE is an **ensemble + stability + semantic graph** process. It is an Assets4All design direction; no claim of research or patent novelty is made without a dedicated prior-art review.

### 5.1 Principle

Do not ask one segmentation algorithm for “the answer”. Generate several independent hypotheses, perturb them, measure which boundaries survive, then build a consensus region graph.

### 5.2 Independent geometric views

Candidate boundaries are generated from several views of the same mesh:

1. Topological connectivity/islands.
2. Dihedral-angle / sharp transition field.
3. Curvature discontinuity field.
4. Thickness / local-radius change field.
5. Surface-normal field.
6. Geodesic protrusion / branch structure.
7. Principal-axis / aspect-ratio cues.
8. Ground-contact and vertical-level cues.
9. Symmetry and repeated-part cues.
10. Existing material slots / UV island evidence when present.

No single view is authoritative.

### 5.3 Boundary persistence

The system reruns candidate segmentation with controlled perturbations of thresholds and samples.

An edge/zone that repeatedly becomes a boundary receives a high `BoundaryPersistence` value.

This is intended to separate:

- true structural transitions that remain stable under perturbation;
- accidental boundaries caused by one arbitrary threshold.

### 5.4 Consensus region graph

Persistent boundaries create initial micro-regions. Assets4All then builds a graph where each region stores a descriptor (“Region DNA”):

- centroid and relative position
- area / volume estimate
- oriented bounding box / PCA
- aspect ratios
- thickness statistics
- curvature statistics
- normal distribution
- ground proximity/contact
- symmetry group
- repetition signature
- neighboring regions
- boundary strength by neighbor
- source object/material/UV provenance

Profile-specific semantics operate on this graph, not directly on polygons.

### 5.5 Semantic hypotheses

The selected AssetProfile proposes possible region roles. Example for a chair:

- seat
- backrest
- leg/support
- frame
- armrest
- upholstery
- hardware

The profile contributes priors; geometry remains evidence. The engine emits confidence for every role assignment.

### 5.6 Ambiguity-only review

The user reviews only regions that remain unresolved **after automatic repair and semantic inference**. A correction applies to a whole region and may propagate to symmetry/repetition peers.

Manual face selection is Expert-only fallback.

### 5.7 Region quality outputs

- RegionStability
- BoundaryPersistence
- SemanticConfidence
- SymmetryConfidence
- UnresolvedRegionCount
- EstimatedReviewSeconds

These outputs feed CSE but do not directly redefine PVS.

## 6. Ground Integrity Gate

All floor-standing furniture and equipment must be visibly and geometrically above the Blender ground plane and must not penetrate it.

Canonical Blender contract for floor profiles:

- ground plane: `Z = 0`
- intended support/contact: `Z = 0 ± tolerance`
- no meaningful geometry below `-penetrationTolerance`
- no unintended floating support above `+floatTolerance`

### 6.1 Robust support detection

The minimum vertex alone is not sufficient because a single bad spike can corrupt grounding.

Grounding analysis therefore distinguishes:

- absolute minimum Z
- robust low percentile Z
- connected low-contact clusters
- estimated support clusters (feet/base)
- total contact area / contact-point count
- below-ground outliers vs structural penetration

### 6.2 Automatic ground repair

For a valid floor-standing source, Assets4All translates WORK vertically so the intended support level sits at Z=0.

If one or more localized Meshy artifacts extend below the robust support plane, Assets4All first treats them as repair candidates. It must repair/trim/reconstruct the anomalous local geometry and re-run Ground Integrity rather than simply lifting the whole asset and leaving valid feet floating.

If the anomaly is more complex, the deeper repair layer may test multiple candidates (local clamp/trim, region-local reconstruction, symmetry-derived support reconstruction, etc.) and keep only a candidate that passes post-repair validation.

Possible intermediate outputs:

- `PASS_GROUNDED`
- `AUTO_GROUNDED`
- `AUTO_REPAIRED_SUPPORT`
- `DEEP_REPAIR_REQUIRED`
- `REVIEW_UNRESOLVED`

### 6.3 Preview requirement

For floor-standing profiles the large preview always shows a ground/grid plane. Geometry below the plane can be overlaid/highlighted in Review mode.

Ground Integrity is an export-blocking quality gate for floor-standing assets.

## 7. Profiles

Initial static profiles:

- GenericProp
- Chair
- Table
- Sofa
- Cabinet
- Lamp
- Plant
- Decoration
- KitchenEquipment
- ServiceEquipment
- Structural

Profiles supply ranges, semantic expectations, placement strategy, budgets and adapter hints. They do not own the common pipeline.

## 8. Repair risk classes and Repair Tournament

- SAFE: automatic deterministic cleanup/normalization.
- CONDITIONAL: automatic when confidence and post-validation are sufficient.
- DESTRUCTIVE HYPOTHESIS: may run automatically only on disposable transactional WORK candidates; it is never committed directly.

For uncertain defects, Assets4All may create several temporary repair candidates. Each candidate is independently re-analysed. A destructive candidate can become the new WORK only when it wins the Repair Tournament and passes hard invariants such as dimensional envelope, Ground Integrity, topology, profile plausibility and geometry-loss limits.

SOURCE remains immutable. If no candidate clearly wins, Assets4All escalates to Review. Only after the repair system has exhausted viable hypotheses may it recommend source regeneration.

## 9. Quality gates

At minimum:

- Source
- Geometry
- Topology
- Dimensions
- Orientation
- GroundIntegrity (for floor profiles)
- Repair
- Regions
- Materials
- Optimization
- CollisionPreparation
- Export

States:

- PASS
- REVIEW
- FAIL
- N/A

FAIL blocks export, but a pre-repair FAIL is a trigger for automatic repair rather than an immediate user failure. REVIEW is surfaced only after automatic attempts cannot resolve the ambiguity safely.

## 10. Canonical manifest

Target single source of truth:

`<AssetId>.asset4all.json`

It eventually supersedes `.bbbridge.json`, `.bbasset.json` and Asset Studio manifests through controlled migration.

Minimum sections:

- schema
- identity
- source
- session
- profile
- orientation
- dimensions
- functionalMeasurements
- geometry
- grounding
- regions
- semanticRoles
- materials
- variants
- optimization
- lod
- collision
- placement
- viability (PVS + CSE + disagreement)
- quality
- processing metrics
- exports

## 11. v0.1 acceptance priorities

1. Immutable SOURCE / persistent WORK session.
2. Large preview-oriented UI foundation.
3. PVS implementation.
4. Independent CSE implementation.
5. Robust Ground Integrity Gate and automatic grounding/repair.
6. Transactional Self-Healing Repair Cycle with automatic rollback.
7. Region Consensus Engine prototype.
8. No face selection required for the reference fused Meshy chair.
9. Canonical manifest foundation.
10. Unity adapter remains separate from Bistro Builder adapter.

## 12. Reference regression fixture

`A4A_FIXTURE_001` = current fused Meshy chair (`BB_Chair_Master_002`).

It is a regression fixture, not an architectural special case.

Success means the pipeline can analyse, repair, ground, segment, prepare and export it without requiring the normal user to manually select thousands of faces.
