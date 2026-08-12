# Assets4All — Large Workspace UX

## Goal

The normal Assets4All experience must feel like a focused production application inside Blender, not a stack of technical panels.

The 3D preview is the primary surface. Controls surround it and are intentionally shallow.

## Target composition

```text
+------------------------------------------------------------------------------------------------+
| Assets4All | Asset: BB_Chair_Master_002 | Chair | AUTO | Unity | Session: WORK                |
+--------------------------+---------------------------------------------------+-----------------+
| ASSET                    |                                                   | DECISION        |
| Source                   |                                                   | PVS 86          |
| Profile                  |              LARGE 3D PREVIEW                    | CSE 81          |
| Dimensions               |                                                   | Δ 5             |
| Source/Work revision     |          ground plane / grid visible             | REVIEW          |
|                          |                                                   |                 |
| PIPELINE                 |          overlays toggled independently          | QUALITY GATES   |
| 1 Analyse                |       - Regions                                  | Geometry  PASS  |
| 2 Prepare                |       - Symmetry                                 | Ground    PASS  |
| 3 Review                 |       - Materials                                | Regions REVIEW  |
| 4 Approve                |       - Below-ground geometry                    | Export    WAIT  |
| 5 Export                 |       - Collider                                 |                 |
+--------------------------+---------------------------------------------------+-----------------+
| [ ANALYSE ] [ PREPARE AUTOMATICALLY ] [ REVIEW 2 ISSUES ] [ APPROVE ] [ EXPORT TO UNITY ]     |
+------------------------------------------------------------------------------------------------+
```

## Primary-button design

Only one primary action is visually dominant at a time.

States:

1. No analysis -> `ANALYSE ASSET`
2. Analysis complete and processable -> `PREPARE AUTOMATICALLY`
3. Ambiguities -> `REVIEW N ISSUES`
4. Gates pass -> `APPROVE ASSET`
5. Approved -> `EXPORT`
6. Both viability estimators reject -> `REGENERATE SOURCE` recommendation, not a destructive button

Secondary actions remain smaller: restore WORK revision, toggle overlays, show diagnostics, open Expert mode.

## Preview rules

For `FLOOR` profiles:

- Z=0 ground plane is always visible in the Assets4All workspace.
- Ground contact can be displayed as a heat/diagnostic overlay.
- Geometry below the allowed plane is highlighted in Review mode.
- `Ground Integrity = FAIL` prevents Approve/Export.

Region review must work from object-space clicks on regions, not polygon selection.

## Viability presentation

PVS and CSE are intentionally shown separately.

Example:

```text
PVS  91  TECHNICALLY VERY VIABLE
CSE  66  AUTO CONVERSION UNCERTAIN

Decision: REVIEW
Reason: region/semantic ambiguity
Expected human review: 18 s
```

The UI must never hide a large disagreement by averaging the scores.

## Modes

### AUTO

Default. Minimal controls, automated processing and issue-only review.

### REVIEW

Adds region/material/ground/collision overlays and concept-level corrections.

### EXPERT

Exposes technical Blender tools and face-level fallback. Expert mode is not the normal production path and its use is measured as manual intervention time.

## Performance UX

Every pipeline phase reports elapsed time. The user should always know whether Assets4All is working and which stage is active:

`Analysing geometry -> Detecting regions -> Ground check -> Materials -> Optimisation -> Validation`

The workspace records automatic processing time and human review time separately.
