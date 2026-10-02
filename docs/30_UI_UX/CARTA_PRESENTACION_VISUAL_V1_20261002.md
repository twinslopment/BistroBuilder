# CARTA — reconstrucción de presentación V1 · 02/10/2026

**ESTADO: PROPUESTA VISUAL / IMPLEMENTADA EN RAMA / PENDIENTE DE APROBACIÓN DEL USUARIO.**
No equivale todavía a la aprobación de Personal V8. Mantener `feature/bb-presentation-interaction-quality-v1` hasta revisión comparativa; no integrar automáticamente en master.

## Referencia
Personal V8 es el lenguaje gráfico canónico: marfil, papel crema, latón, contornos discretos, relieve fino, jerarquía Recoleta + Inter, botones miel y rojo exclusivamente para acciones destructivas. La captura anterior `docs/Images/UIAudit20260929/Carta-1280.png` describe el estado oscuro que se sustituye.

## Tres pantallas reales (no tres implementaciones web)
1. `BistroBuilderMenuPortfolioRuntimeView`: MIS CARTAS / REGLAS DE ACTIVACIÓN / DETALLE DE LA REGLA. Mantiene duplicar, renombrar, eliminar, activar manual, fijar base, reglas automáticas y acceso al editor activo.
2. `BistroBuilderMenuEditorRuntimeView`: lateral de categorías/filtros; tabla central de platos; detalle del plato con precio, disponibilidad, modalidad, servicios, preparación, posición y economía. Mantiene los tres scrolls correctos y los mismos callbacks de dominio.
3. `BistroBuilderDishRecipeAuthoringRuntimeView`: datos/servicios/modalidades a la izquierda; receta, rendimiento, merma, ingredientes, cantidades y notas a la derecha. Conserva sus controles de creación, edición y guardado en el borrador.

## Cambios implementados
- `BistroBuilderMenuEditorUiFactory.cs` es la autoridad de la paleta, tipografía legacy uGUI y creación de controles para las tres vistas. No se introduce un WebView ni una segunda autoridad funcional.
- Marcos principales con `StylePlate` idempotente (Outline + Shadow), paneles marfil y fondos de scroll claros. En `CreateButton` la tintura se aplica una sola vez, sin oscurecerla multiplicando dos colores.
- Imágenes originales del catálogo `BBIconCatalog` para cabeceras y los tres apartados del gestor. No se descargan iconos o fuentes externos.
- Títulos limpios ASCII con el Recoleta importado del propio proyecto. El OTF es **DEMO**: los textos con tildes, eñe/euro y metadatos dinámicos usan la familia oficial Inter para evitar glifos de marca, no Georgia. El título del editor permanece en `CARTA Y PLATOS`; `En carta: X/Y · Restaurante: ...` queda en Inter. `SetButtonDisplay()` también actualiza la fuente al modificar textos dinámicos de botones. Los datos y el contenido de la partida no se modifican.
- `BistroBuilderUiDesignSystem.IsCartaModalChild` evita que el rescan gráfico global sobrescriba fuentes y colores únicamente dentro de `MenuPortfolioModal`, `MenuEditorModal` y `DishRecipeAuthoringModal`. El HUD, lanzadores, Personal y los otros módulos mantienen el tratamiento global habitual.
- Se reemplazaron los controles que mantenían colores oscuros de la antigua UI, incluso los de confirmación, service mode y filas de ingredientes.

## Validación real
- Unity **6000.3.19f1**, escena `Assets/Scenes/Prototype_Restaurant.unity`.
- Nuevo gate reversible: `BistroBuilderMenuVisualV1RuntimeProbe.RunBatch` en `Assets/Editor/BistroBuilder/Menu/`. Abre secuencialmente portfolio, editor y autoría de nueva receta sin aplicar commits al estado de partida; inspecciona controles, iconos, fondos, marco y servicios.
- **17 PASS / 0 FAIL** en la regresión final con iconografía, Recoleta segura y contador dinámico en Inter. Se pasó también `BistroBuilderMenuEditorRuntimeView.TryValidateVisibleContent`.
- `ScreenCapture.CaptureScreenshot` no está garantizado en batchmode y está deshabilitado expresamente ahí; **no se presentan imágenes como capturas de Game View Unity**.
- Regresión cruzada ejecutada: `BistroBuilderStaffApprovedRuntimeProbe.RunBatch` **23 PASS / 0 FAIL**, con compilación correcta. El cambio acotado del diseño global no altera Personal V8.

## Previsualización para aprobar
Artefacto autocontenido de esta conversación: `Carta_BistroBuilder_PREVIEW_V1.html` con capturas PNG independientes de las tres vistas y montaje conjunto, programado con HTML/CSS/JavaScript.
- Es una maqueta de comparación, no autoridad del dominio ni integración HTML dentro de Unity.
- Los datos de plato ilustrados incluyen únicamente nombres/precios visibles del catálogo para dar contexto; márgenes y otros estados de partida no se inventan como hechos.
- La vista 2 muestra seis columnas alineadas; la edición de precio, las categorías, los filtros y las opciones del plato siguen siendo controles del runtime real.
- El archivo de preview incorpora el gráfico de la barra superior de referencia y el emblema de Bistro Builder, pero **no incluye fuentes tipográficas**; el ajuste definitivo de glifos depende de los recursos locales.

## Fuera de alcance deliberado
No se cambian reglas de negocio, Save/Load, precios persistidos, recetas canónicas, ofertas, inventario, empleados, spawns ni la barra operativa del restaurante. La coincidencia visual final se decide comparando una captura interactiva normal de Game View con la preview aprobada, no por un PASS automático.
