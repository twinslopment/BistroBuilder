# BB Procedural Layout & Furnishing System (BBPLFS)

Status: V1 design in progress
Branch: `feature/bbplfs-v1`

## Permanent principles
- BBPLFS assists the existing classic Edit/Build Mode; it never replaces it.
- Global Understanding + Selective Generation: the full premises may be understood, but only the explicit Design Scope may be generated or modified.
- Manual and procedural work can be mixed in any order.
- Once a generated result is accepted, it becomes normal editable content; it is not locked into a procedural mode.
- Tool-First: AI interprets intent and context; deterministic tools generate and validate layouts whenever possible.
- BBPLFS does not duplicate BBSIS, Navigation, Interaction & Reservation, Economy, or Edit Mode authority.

## Block 1 — System contract
BBPLFS is responsible for:
- understanding the purchased/rented premises;
- interpreting the player's requested scope and design intent;
- selecting compatible furnishing/equipment candidates;
- generating, comparing and optimizing layout alternatives;
- requesting spatial validation from BBSIS;
- requesting circulation evaluation from Navigation;
- delivering previews/results to Edit Mode for accept/modify/cancel.

BBPLFS is not responsible for:
- final spatial validity;
- pathfinding or crowd simulation;
- logical reservations/interactions;
- authoritative prices or financial transactions;
- materializing construction outside Edit Mode;
- modifying areas outside the active Design Scope.
## Block 2 — Premises Model
`PremisesModel` is a lightweight working representation of the acquired premises.
It references existing authoritative data instead of duplicating it.

It contains:
- premises identity and revision;
- detected spaces/rooms and their usable contours;
- walls, openings, doors, relevant windows, columns and fixed obstacles;
- connections between spaces and external/internal access points;
- existing objects and their editability/lock state;
- derived potentially usable areas for cheap filtering;
- semantic observations and confidence where interpretation is not structural fact.

`PotentiallyUsable` is never equivalent to BBSIS validity.

`DesignScope` is separate from `PremisesModel` and defines exactly what one BBPLFS operation may touch:
- selection;
- zone;
- room;
- multiple rooms;
- whole premises.

The scope also records objects to preserve, objects that may be moved, and objects that are untouchable.
Relevant Edit Mode structural changes invalidate only the affected premises data where practical.

## Block 3 — AI + LayoutBrief
AI is part of BBPLFS V1 from day one.
Its role is semantic interpretation, not arbitrary spatial placement.
AI may:
- classify the requested function and style;
- interpret natural-language priorities, capacity wishes and restrictions;
- use the Premises Model to explain suitable or unsuitable choices;
- translate the request into a validated `LayoutBrief`.

AI may not:
- emit authoritative coordinates/rotations;
- declare BBSIS or Navigation validity;
- expand the Design Scope without explicit player action;
- assume demolition, construction or relocation of locked/fixed elements;
- silently relax a hard player constraint.

### LayoutBrief — minimum contract
- `ScopeRef` — exact Design Scope to operate on.
- `SpaceFunction` — dining, kitchen, bar, waiting, support, etc.
- `GoalProfile` — Balanced, MaxCapacity, MaxComfort, ServiceEfficient or Premium.
- `CapacityTarget` — preferred value/range when relevant.
- `StyleTags` — compact semantic style request.
- `BudgetLimitRef` — optional authoritative budget/cost context reference.
- `PreserveExisting` — what existing work must remain.
- `RequiredElements` — mandatory functional elements requested by the player.
- `ForbiddenElements` — explicitly excluded elements.
- `HardConstraints` — requirements that may not be relaxed.
- `SoftPreferences` — preferences the scorer may trade off.

The brief contains intent, not placement instructions.
### Interpretation rules
1. Explicit player input overrides AI inference.
2. Structural facts from the Premises Model override AI assumptions.
3. Missing non-critical preferences use deterministic project defaults.
4. Missing critical information is requested only when it changes the meaning or feasibility of the operation.
5. AI uncertainty is never converted into a hard constraint.
6. If the requested result is infeasible, BBPLFS reports the conflict and may offer feasible alternatives; it does not silently violate requirements.
7. The same `LayoutBrief` can also be produced by normal UI controls, so layout generation does not depend on free-text AI being available.

### Examples
`"Comedor contemporáneo para 40 personas con buena circulación"`
→ scope selected by player; function Dining; capacity 40; style Contemporary; circulation/service preference high; remaining fields defaulted.

`"Completa esta sala pero no muevas las mesas que ya puse"`
→ current scope; preserve existing tables; generator may only fill available remainder.

`"Hazme todo el restaurante"`
→ valid only when the player explicitly selects/authorizes whole-premises scope; otherwise the AI must not expand scope.

## Closed decisions so far
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Next design block: Asset Layout Profiles + Furnishing Sets.## Block 4 — Asset Layout Profiles
`AssetLayoutProfile` is the small BBPLFS-facing description of an asset. It does not duplicate full asset, BBSIS or Economy data.

Minimum fields:
- `AssetRef` — canonical catalog asset.
- `LayoutRole` — table, seat, counter, appliance, storage, decor, etc.
- `LayoutFamily` — interchangeable family used to reduce search space.
- `FunctionTags` — functions this asset can fulfil.
- `StyleTags` — compact visual/style classification.
- `QualityTier` — coarse quality/premium level where relevant.
- `PlacementType` — floor, wall, support surface or other supported placement.
- `AllowedOrientations` — only when the asset is not freely rotatable for layout purposes.
- `CompatibilityTags` — semantic compatibility with set slots/other assets.
- `FastFootprintRef` — revisioned derived footprint/envelope used only for cheap candidate generation.
- `BBSISDescriptorRef` — authoritative spatial-validation reference.
- `CostRef` — authoritative economy/catalog price reference.

Rules:
- Asset dimensions, ports, Work Edges, Seat Bays, sweeps and true spatial validity remain authoritative outside BBPLFS.
- `FastFootprintRef` is disposable cache data; if stale, it is rebuilt from canonical data.
- Visual variants that behave identically may share one layout family/profile and differ only at concrete asset selection.
- Assets4All/catalog authoring should populate as much of this profile automatically as reliable metadata permits; uncertain semantic tags remain reviewable rather than silently trusted.
## Block 5 — Furnishing Sets
A `FurnishingSet` is a functional recipe, not a prefab and not a permanent ownership container.

It defines:
- `SetRole` — functional purpose, e.g. DiningSet4, BarRun, PrepStation.
- required slots and quantities;
- optional slots;
- compatibility requirements for each slot;
- simple layout relationships needed to generate the set: around, aligned, attached, repeated or adjacent;
- permitted arrangement families when the function requires them.

Example:
`DiningSet4` = 1 dining table + 4 compatible dining seats arranged around it.

Rules:
- Sets describe what must exist together; BBSIS decides whether the concrete arrangement is spatially usable.
- Sets may be partially satisfied by existing manual objects inside the Design Scope.
- Locked/manual objects can therefore become fixed members of a generated set without being recreated.
- A set may substitute compatible concrete assets without changing its functional meaning.
- The generator first works with a small compatible candidate pool/layout families, then resolves concrete assets before final BBSIS/Navigation validation.
- BBPLFS must never enumerate the entire catalog combinatorially when equivalent families can be collapsed.
- Once the player accepts the result, set membership is not required to keep the objects editable; normal Edit Mode remains authoritative for subsequent manual editing.

## Asset matching rule
Concrete asset selection uses deterministic filtering first:
1. required function/slot compatibility;
2. availability and scope restrictions;
3. hard player requirements;
4. footprint/layout compatibility;
5. style, quality, cost and soft preferences for ranking.

AI may translate player language into tags/preferences, but it may not bypass these filters or invent catalog assets.
## Current closure state
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Block 4 Asset Layout Profiles: CLOSED.
- Block 5 Furnishing Sets: CLOSED.
- Next design block: Layout Generator — candidate positions, room patterns, irregular geometry and candidate generation strategy.
