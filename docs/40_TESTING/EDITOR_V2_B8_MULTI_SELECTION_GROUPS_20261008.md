# Editor V2 — B8 Multiselección y grupos

**Fecha:** 2026-10-08
**Rama:** `feature/editor-v2`
**Estado:** PASS

## Objetivo

B8 convierte la selección múltiple en una capacidad transaccional de Editor V2, no en una agrupación visual. Una operación sobre varios colocables debe conservar identidades y relaciones, validarse como conjunto, confirmar todo o nada y aparecer como una única acción en el historial global.

## Implementación validada

- `BistroBuilderEditorV2SelectionCoordinator` mantiene Selection Set, selección primaria, deduplicación, orden estable e intersección de capacidades.
- Shift+selección permite añadir o retirar miembros sin convertirlos en una jerarquía temporal.
- `BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider` proyecta la selección explícita sobre `RestaurantPlacementLinkedGroupService`.
- Linked Groups realiza cierre transitivo, deduplicación y corte natural de ciclos entre providers semánticos.
- Movimiento y rotación calculan poses relativas desde el snapshot inicial, evitando deriva acumulativa.
- `RestaurantLinkedGroupPlacementConstraintRule` valida los seguidores con las autoridades de Placement/BBSIS antes de confirmar.
- `BistroBuilderEditorV2CompoundPlaceableHistoryCommand` encapsula operaciones grupales y revierte los miembros ya aplicados si falla un hijo.
- Duplicación y eliminación por lote reutilizan Lifecycle, Finance e History y aplican rollback si una fase falla.
- La implementación no crea padres temporales ni modifica la jerarquía persistente de los objetos.
- La capa visual definitiva de multiselección queda fuera de B8 y se resolverá en el diseño UI/UX de Editor V2.

## Gate B8

Self-test real:

`BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest.RunFromCommandLine`

Resultado final:

**59 OK / 0 fallos**

La batería fue ejecutada tres veces consecutivas, las tres con exit 0.

Cobertura relevante:

- Selection Set válido, deduplicado y con selección primaria.
- Orden determinista independientemente del orden de entrada.
- Rechazo de mezcla de autoridades.
- 1.000 miembros sin duplicados.
- Undo/Redo compuesto.
- fault injection de Undo y Redo con rollback.
- grafo semántico transitivo, determinista y resistente a ciclos.
- 10.000 transformaciones sin NaN/Infinity.
- cancelación exacta de raíz + 24 seguidores.
- integración real con colocables de `Prototype_Restaurant`.
- clic + Shift+clic, toggle y recuperación del primario.
- movimiento rígido del conjunto.
- cancelación exacta.
- commit real con un solo comando histórico.
- Ctrl+Z/Ctrl+Y global sobre el grupo.
- eliminación grupal + Undo/Redo con identidad y registro restaurados.
- editabilidad individual después de restaurar.
- duplicación con varios fallos previos sin estado parcial.
- IDs nuevos y geometría relativa preservada.
- Undo/Redo de duplicación en una sola acción.

## Rendimiento

- `SELECTION_1000_MS`: ~8 ms.
- `GROUP_10000_MS`: ~40 ms.
- `GROUP_10000_ALLOCATED_BYTES`: 0 bytes tras warmup.

El presupuesto del self-test es < 3 s y < 1 MB de asignación; B8 queda ampliamente por debajo.

## Regresión

Después de B8 se ejecutó la cadena real de regresión:

- B1: **20/20**.
- B2: **22/22**.
- B3: **28/28**.
- B4: **63/63**.
- B5: **48/48**.
- B6: **37/37**.
- B7: **41/41**.
- Edit Block 18 Core: **84/84**.
- Edit Runtime Lifecycle: **20/20**.
- Scene Validator: **77/77**.
- Queen Test: **PASS**, incluyendo Finance, BBSIS, Navigation, Save/Load, servicio y rollback.

Todas las ejecuciones finalizaron con exit code 0.

## Gate de cierre

B8 queda cerrado porque la operación grupal es atómica, Undo/Redo representa una sola acción, las relaciones semánticas se conservan/reconstruyen mediante sus providers, los elementos continúan siendo editables individualmente y las regresiones permanecen verdes.

**Siguiente bloque habilitado:** B9 — Catálogo escalable.
