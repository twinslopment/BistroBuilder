# Editor V2 — B10 Sustitución inteligente (08/10/2026)

## Estado / autoridad

**B10 NÚCLEO CERRADO (PASS técnico) en la rama aislada `feature/editor-v2`.** Save/Load real en Play Mode, regresión B4–B10 y build de Windows 64 bits superados. La conexión con el botón definitivo del catálogo y el nuevo inspector continúa en el proyecto visual Editor V2: no está incluida en el cierre de las autoridades de B10. No se ha mezclado con la rama maestra.

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

1. **Save/Load real: PASS.** Batería `BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest.RunFromCommandLine` (Unity Editor Play Mode real; sin `-quit`): sustitución de silla, SaveGame -> carga real -> borrado de slot diagnóstico (950–969), 38 colocables recuperados, 28 vínculos de plazas-mesas consistentes, ID nuevo presente y antiguo ausente. Reporte: `EditorV2_B10_SaveLoadPlay_Report.txt`; log: `Logs/B10_SaveLoadPlay6.log`. El test estabiliza primero la topología de asientos; no altera una partida existente.
2. Pendientes fuera del núcleo validado: prueba visual interactiva de jugador final y estrés B10 masivo en diferentes perfiles BBSIS, incluidos cambios grandes de dimensiones; los casos que no caben se rechazan mediante validación canónica.
3. El backend B10 está disponible como API; **no** se ha cableado a los botones de UI porque la selección visual se define y aprueba en otro chat.
4. No integrar en `integration/master-current-20260918` hasta enlazar y aceptar visualmente la operación en el Editor V2. La ejecución real sin UI está disponible como API en el servicio de grupos.

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

## Compilación PC y reparación de catálogo

**Windows Standalone 64 bits PASS**, Unity 6000.3.19f1: `Logs/B10_WindowsBuild.log` contiene `Build Finished, Result: Success` y `BB_PLAYTEST_BUILD_PASS`, 181348903 bytes reportados, 13 warnings, modo `FullScreenWindow` a resolución nativa. Salida: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`. Las advertencias no son errores de compilación.

El Save/Load real descubrió un problema preexistente de catálogo: el archivo de escena sólo tenía cuatro definiciones serializadas aunque había nueve definiciones de colocables válidas (incluida `pf_bb_chair_master_001_olive`). Se añadieron exactamente las cinco referencias omitidas a `Assets/Scenes/Prototype_Restaurant.unity` sin cambiar otros objetos. La autoridad existente para sincronizar futuras definiciones sigue siendo el instalador canónico `BistroBuilderUniversalSaveFoundationInstaller` (no se ha añadido un segundo sincronizador). Durante la ejecución headless inicial Unity bloqueó el renombrado temporal del archivo de escena, por lo que se aplicó y verificó un parche YAML acotado con copia recuperable en `Library/B10_SceneCatalogBackup_20261008.unity`. Después de esa actualización, Save/Load completó correctamente el ciclo completo. No atribuir este fallo a una pérdida de datos ni ocultarlo en auditorías.

## Reglas de mantenimiento

- No tocar `docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md` durante las pruebas de lógica B10.
- Mantener la rama aislada sin mezclar otras ediciones de assets/UI ni cambios de terceros.
- No utilizar precio simulado en runtime: el caso de 250 EUR es *únicamente* un fixture efímero de tests y llama al bridge real de Finance y a PurchaseOrder real.
