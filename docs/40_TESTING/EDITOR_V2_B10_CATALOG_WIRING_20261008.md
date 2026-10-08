# Editor V2 — B10 «Sustituir selección» en catálogo e inspector (08/10/2026)

## Estado de integración

**Conexión funcional implementada y verificada en Unity.** El catálogo de artículos existente y el inspector derecho invocan la fachada B8 `BistroBuilderEditorV2GroupOperationService`, que delega en las autoridades B10. No se ha implementado una segunda operación de sustitución ni se ha alterado la maqueta visual Galería Viva en el archivo `docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md`.

**Límite expreso:** el diseño artístico Galería Viva (catálogo con Destacado, relacionados, marco y distribución definitivos) está aprobado como referencia, pero todavía **no existe en Unity de forma íntegra**. Esta entrega conecta el flujo funcional al inspector/catálogo de Unity actualmente materializados y deja un botón reutilizable para trasladarlo sin duplicar negocio al aspecto visual final. No se deben presentar capturas HTML como capturas del Unity runtime.

## Recorrido de jugador

1. Entrar en modo edición y seleccionar un mueble o varios de la misma familia mediante B8.
2. Pulsar una tarjeta del catálogo: cuando hay selección editable, el clic prepara una **candidatura de sustitución**; no inicia un ghost de colocación ni modifica el restaurante.
3. El inspector muestra cantidad de artículos elegidos, coste **neto** autoritativo (o «Recuperas…», o «Sin coste adicional»), y el botón miel «Sustituir selección».
4. Si la combinación no es válida, explicar el rechazo y deshabilitar el botón, manteniendo intacta la selección. No interpretar fondos insuficientes como bloqueo de progresión.
5. Al pulsar se recalcula el presupuesto en Finance; si ha cambiado, se exige revisar el nuevo importe y pulsar otra vez. Si permanece válido, se ejecuta una sola transacción B10, con Undo/Redo agrupado y restauración canónica de relaciones y contabilidad.
6. Tras ejecutar se limpia la selección y no queda una colocación pendiente. Si no hay ningún mueble seleccionado, pulsar catálogo sigue utilizando el flujo anterior de crear/colocar.

El control de sustitución reside en el inspector derecho y se genera una sola vez, sin búsquedas costosas por fotograma; sólo reevalúa el coste al cambiar la selección o el estado de la colocación. Utiliza superficie marfil y acción color miel, coherente con la dirección aprobada.

## Archivos

- `Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalogPanel.cs`: bifurcación de tarjeta según selección, sin colocación accidental.
- `Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/RestaurantPlaceableInspectorPanel.cs`: puntos de enlace al inspector ya existente.
- `Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/RestaurantPlaceableInspectorPanel.Replacement.cs`: render y cotización, invalidaciones de selección, confirmación idempotente de interfaz.
- `Assets/Editor/BistroBuilder/EditMode/BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest.cs`: ejercita mediante Play Mode el clic de tarjeta y botón real, rechazo de categoría incompatible, más el Save -> Load -> Delete de partida QA.

## Evidencias

- Prueba en escena `Prototype_Restaurant.unity`: `Logs/B10_CatalogWiring_FinalPlay.log`; evidencia versionada `docs/40_TESTING/EDITOR_V2_B10_UI_SAVELOAD_PASS.txt`. PASS: click de catálogo, botón habilitado para la cotización válida y deshabilitado para categoría incompatible; el botón crea la sustitución, quita la selección original y no deja colocación activa; Save/Load reconstruye 38 colocables, 28 relaciones y el ID nuevo.
- Batería canónica independiente B10: 40/40 PASS antes de enlazar la UI.
- La validación de arte final e interacción visual a 1920×1080 y 1280×720 sigue pendiente al materializar Galería Viva. No presentar esta batería batchmode nographics como ensayo visual con monitor.

## Regresión y comportamiento no determinista de QA

La batería B4/B5/B8/B9/B10, repetida después de conectar la UI, acabó con **252/252 PASS**, exit code 0 para cada proceso (63+48+59+42+40). Se ejecutó con `Tools/BistroBuilder/RunEditorV2B10Regression.ps1`.

**Incidencia transparente:** la primera ejecución conjunta arrojó un fallo en **B8** (`68 OK / 1 fallo`), al no hallar en los primeros 100 pares un hueco para duplicación grupal (`FootprintOutsideCandidateArea`). Una repetición independiente de B8 obtuvo **74/74 PASS** y la siguiente batería conjunta **59/59 PASS** en B8. Esta variación de escenario/prueba de estrés debe examinarse si reaparece; no se modificó ni desactivó el motor ni el caso para forzar éxito. El enlace UI B10 y el Save/Load real no fallaron.

## Build Windows de cierre

**Windows 64-bit PASS tras la conexión UI y protección frente a cambio de selección**, Unity 6000.3.19f1: `Logs/B10_CatalogWiring_FinalBuild.log` confirma `Build Finished, Result: Success` y `BB_PLAYTEST_BUILD_PASS`; exit code 0, tamaño de build informado 181357799 bytes y 13 warnings. Ejecutable PC: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`. La compilación no sustituye una inspección gráfica presencial de la futura Galería Viva.

## Convivencia

No alterar ni reemplazar el diseño aprobado del otro hilo ni mezclar con la rama maestra hasta que se complete Galería Viva y su aceptación en pantalla. `BistroBuilderEditorV2GroupOperationService` es el contrato público de la futura presentación sin tocar Finance, BBSIS, historial o Save/Load.
