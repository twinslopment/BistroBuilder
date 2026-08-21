# BB Living Architecture — Estado de implementación V1

Estado global: **LA1–LA11 IMPLEMENTADOS / AUDITADOS ESTÁTICAMENTE / PENDIENTES DE UNITY REAL**.

> En Bistro Builder no editas un conjunto de paredes. Reformas un restaurante que entiende qué estás intentando conservar y qué consecuencias tendrá cada cambio.

Este archivo es el índice vivo de estado. La especificación vinculante está en `LIVING_ARCHITECTURE_V1_SPEC.md`; los hitos LA6–LA11 disponen además de documentación específica en `docs/`.

## Estado por hito

### LA1 — Kernel topológico
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

IDs estables, snapshot canónico, Building/Level/Vertex/Wall/Opening, DeepClone, fingerprint SHA-256 determinista y gate de invariantes. GameObject/Transform no son autoridad.

### LA2 — Regiones emergentes
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Grafo planar, detección determinista de caras cerradas, RegionId derivado, área/centroide, consultas espaciales y rechazo de cruces no topológicos. **10 casos puros**.

### LA3 — Operaciones transaccionales
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Flujo A→propuesta B→commit atómico, stale fingerprint, rollback, Undo/Redo semántico y primitivas crear/mover/dividir/eliminar pared y mover vértice. **12 casos puros**.

### LA4 — Restricciones e intención
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Restricciones hard/advisory para anclas, longitud, ángulo, apertura y área; correcciones locales deterministas sin solver global que rediseñe por el jugador. **9 casos puros**.

### LA5 — Snap inteligente
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Candidatos read-only de vértice/proyección y semánticos paralelo, perpendicular, igual longitud y continuidad, con ranking y confianza deterministas. **10 casos puros**.

### LA6 — Impacto Bistro Builder
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Reporte previo estructurado, adaptadores aislados a colocables/seating/circulación, consecuencias de regiones, corrección mínima opcional, deduplicación y fail-safe ante excepción o mutación externa. **12 casos puros**.

### LA7 — Runtime/Mesher
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Materialización Snapshot→MeshData→Unity, paredes con espesor/altura/elevación y huecos de aperturas. La representación visual nunca reconstruye la verdad del Domain. **10 casos puros**.

### LA8 — Persistencia
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

DTO versionado dentro del SaveGame universal, Capture/Restore/Migrate, orden determinista, rechazo de esquema futuro y conservación de IDs/topología. **10 casos puros**.

### LA9 — Herramienta jugable V1
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Sesión de edición con selección, preview, crear/mover/eliminar, longitud numérica, snap, impacto, Confirmar/Cancelar y Undo/Redo; controlador runtime desacoplado de la autoridad canónica. **12 casos puros**.

### LA10 — Sistema Universal de Feedback del Modo Edición
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Estados Idle/Valid/Warning/Invalid, ghost, cues de snap/confianza, impactos, correcciones sugeridas y señales Confirm/Cancel/Undo/Redo. Feedback reacciona; arquitectura decide. Sin obreros/NPCs de construcción. **10 casos puros**.

### LA11 — Queen Test y hardening final V1
**IMPLEMENTADO / PENDIENTE UNITY REAL**.

Queen Test reversible de 12 casos con flujo completo: construcción → región → apertura → intención → impacto → commit → Save/Load → Undo/Redo → rollback. Incluye stale proposal, aislamiento de adaptadores, determinismo y barrido property-style de 64 geometrías.

Runner acumulativo LA2–LA11: **107 casos esperados**. LA1 conserva su runner Editor histórico independiente.

## Gates finales obligatorios en Unity real

La V1 **NO está validada ni cerrada** hasta superar todos estos gates:

1. Compilación completa del proyecto: **0 errores**.
2. `Bistro Builder/Living Architecture/LA1/Run Self Test`: PASS.
3. `Bistro Builder/Living Architecture/LA11/Run Accumulated LA2-LA11`: **107/107**.
4. `Bistro Builder/Living Architecture/LA11/Run Queen Test`: **12/12**.
5. Composición efectiva del modo Edición con StateService, persistencia, RuntimePresenter, herramienta LA9 y feedback LA10.
6. Play Mode: dibujar recinto → región → apertura → reforma → impacto → Confirmar/Cancelar → Undo/Redo.
7. Save real → Load real con IDs/fingerprint/topología coherentes y reconstrucción runtime correcta.
8. Operación inválida: rollback integral, sin geometría parcial ni GameObjects huérfanos.
9. Revisión visual de openings, snaps, ghost, estados válido/warning/inválido y materialización.
10. Medición de latencia de preview para operaciones V1 normales.
11. Console final: **0 errores**.

## Regla de cierre

Completar código, documentación y auditoría estática permite declarar un hito **IMPLEMENTADO**. Solo Unity real puede elevarlo a **VALIDADO/CERRADO**.

La rama de trabajo de esta V1 es `feature/living-architecture-v1`.
