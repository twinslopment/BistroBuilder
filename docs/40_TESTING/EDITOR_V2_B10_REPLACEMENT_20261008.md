# Editor V2 — B10 Sustitución inteligente (08/10/2026)

## Estado / autoridad

**Implementación funcional en la rama aislada `feature/editor-v2`.** No trasladar a la maestra hasta disponer de verificación final, incluido el round-trip completo de carga real y la integración visual aprobada en su chat propio.

Referencias vinculantes: `docs/20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md`, BBSIS/Placement, `RestaurantPlaceableLifecycleService`, `RestaurantPlacementHistoryService`, `BistroBuilderPlaceableFinanceBridge` y `restaurant.structure`. B10 no implementa un segundo motor de colocación, finanzas ni guardado.

## Contrato funcional implementado

- `RestaurantPlaceableCreationService.TryQuoteReplacement`: coste real = compra de destinos − ingresos netos por retiradas según Finance/ledger; aritmética de 64 bits comprobada.
- `RestaurantPlaceableCreationService.TryReplaceBatch`: sustituye una o varias instancias preservando sus anclajes, orientaciones y jerarquía cuando la nueva geometría es válida; sólo entre categorías compatibles; rechaza jerarquías padre/hijo seleccionadas a la vez.
- Prevalida la sustitución completa sobre la escena, aparta las instancias originales, prepara las nuevas, valida y publica el resultado únicamente cuando las operaciones espaciales superan la autoridad existente.
- Reutiliza lifecycle/registry, validación espacial, regeneración de relaciones, Finance y el historial central. Rechaza modo edición inactivo, transacciones concurrentes y autoridad financiera no enlazada.
- En caso de error intenta compensación económica, destrucción de provisionales y restauración de objetos anteriores; comunica explícitamente si la recuperación se queda incompleta.
- `BistroBuilderEditorV2ReplaceHistoryCommand` representa retirada + compra como una sola operación. Undo/Redo aplica fases en orden seguro e invierte las fases completadas si una falla.
- `BistroBuilderPlaceableFinanceBridge.Replacement` invierte conjuntamente los asientos de compra/venta en Undo/Redo sin reutilizar erróneamente una única categoría contable para ambas.
- `BistroBuilderEditorV2GroupOperationService` publica cotización y sustitución desde la selección (sin modificar visuales pendientes de aprobación).
- Refuerzo general de liberación de recursos del historial: las entradas de Redo descartadas no destruyen objetos que ya pertenecen a una nueva operación; cobertura en eliminación y creación individual y operaciones compuestas.

## Evidencia obtenida

Comando de prueba: `BistroBuilderEditorV2B10ReplacementSelfTest.RunFromCommandLine` mediante Unity 6000.3.19f1, escena real `Assets/Scenes/Prototype_Restaurant.unity`.

**Resultado B10: 40 OK / 0 fallos** (`EditorV2_B10_Replacement_Report.txt`; log de Unity `Logs/B10_PaidLoop2.log`). Comprueba:

- historial compuesto, orden de Undo/Redo, reintentos y fault injection con rollback;
- rechazo de categoría incompatible sin alterar caja, objetos ni historial;
- sustitución individual en escena, misma posición de anclaje, nuevas identidades y una única operación;
- captura efectiva por `RestaurantStructureSaveSectionProvider`, JSON round-trip y conservación del ID/itemId del reemplazo;
- Undo/Redo real de Finanzas y colocables;
- sustitución simultánea de dos mesas, un único historial y Undo/Redo con recuperación de caja;
- rama nueva tras Undo previo: descarte seguro de recursos compartidos del historial;
- fixture financiero **temporal e in-memory** (no se modifica el catálogo): compra a **250 EUR**, gasto **25 000 céntimos**, reventa al **50 %** con recuperación de **12 500 céntimos**, varias inversiones Undo/Redo sin duplicación de movimientos.

## Limitaciones verificadas / pendientes

1. El test de persistencia prueba **captura y serialización de estructura**, **no** una carga completa a través del orquestador Save/Load en modo de juego. No afirmar que el round-trip completo esté certificado.
2. No se ha pasado todavía una sesión visual de aceptación de un jugador final con cambios de tamaño, asociaciones complejas mesa-silla y todos los perfiles BBSIS, ni una prueba masiva de rendimiento B10.
3. El backend B10 está disponible como API; **no** se ha cableado a los botones de UI porque la selección visual se define y aprueba en otro chat.
4. No está autorizada la integración en `integration/master-current-20260918` hasta resolver estas verificaciones.

## Regresiones

Lanzador reproducible: `Tools/BistroBuilder/RunEditorV2B10Regression.ps1`. Ejecutado en serie con Unity 6000.3.19f1; cada subproceso devolvió **exit code 0** y se verificó su informe:

| Bloque | Pruebas | Resultado |
|---|---:|---|
| B4 Historial global | 63 | PASS |
| B5 Reforma/finanzas | 48 | PASS |
| B8 Multiselección | 59 | PASS |
| B9 Catálogo escalable | 42 | PASS |
| B10 Sustitución | 40 | PASS |
| **TOTAL** | **252** | **0 fallos** |

Logs reproducibles: `Logs/EditorV2_B4_Regress_B10.log` a `Logs/EditorV2_B10_Regress_B10.log`. La prueba B10 incluye los datos financieros de 250 EUR y 50 % de reventa. Esta batería es una regresión de Editor en modo `-batchmode`, no sustituye una prueba de jugador ni una compilación Windows posterior a B10.

## Reglas de mantenimiento

- No tocar `docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md` durante las pruebas de lógica B10.
- Mantener la rama aislada sin mezclar otras ediciones de assets/UI ni cambios de terceros.
- No utilizar precio simulado en runtime: el caso de 250 EUR es *únicamente* un fixture efímero de tests y llama al bridge real de Finance y a PurchaseOrder real.
