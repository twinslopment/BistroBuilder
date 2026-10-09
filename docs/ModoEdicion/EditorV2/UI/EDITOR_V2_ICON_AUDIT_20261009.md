# Editor V2 — Inventario de iconos · 09/10/2026

## Resultado de auditoría (revisión real de la rama feature/editor-v2)
- Biblioteca SVG: `Assets/BistroBuilder/UI/Iconography/Icons/`, **82 archivos** (12 con prefijo `bb-options-`, destinados a opciones generales). Otros iconos proceden de trazados genéricos de Lucide; el nombre existente no es prueba de que el acabado artístico aprobado esté cerrado.
- Logo oficial: asset aprobado en `Assets/Resources/BistroBuilder/UI/EditorV2/BB_Logo_Aprobado_Referencia.png`.
- No declarar el conjunto de iconos **definitivo**: aún falta comprobar por familias el grosor, relieves, sombras, contraste, coherencia de latón/miel, tamaños y estados hover/deshabilitado en Unity.
- **Existentes que cubren conceptos:** `table-2`, `armchair`, `wrench`, `move`, `rotate-cw`, `copy`, `trash-2`, `layout-grid`, `lamp-desk`, `flower-2`, `cooking-pot`, `warehouse`, `umbrella`, `star`, `settings`, `pencil`, `arrow-left`, `arrow-right`.
- **Iconos específicos pendientes de diseñar/aprobar en el acabado BB:** Seleccionar (puntero propio), Superficies (capas/materiales), Snapping (imán inteligente), Vistas (cubo/vistas de cámara), Cámara/captura, Día/noche, badges propios de construcción/demolición, contadores de multiselección y los iconos ilustrados de las subfamilias que lo requieran. Una posible analogía SVG no basta para declarar el arte final. Las herramientas Snapping/Vistas continúan deshabilitadas hasta tener acciones reales.
- Categorías visibles a revisar con su icono **en build**: Todos, Mesas, Asientos, Barra, Cocina, Almacenamiento, Iluminación, Decoración y Exterior. Reutilizar la biblioteca original cuando el resultado pase inspección; ilustrar específicamente cuando no coincida.

## Regla vinculante
Se aprueba estética mediante vista real en build. Conservar iconos funcionales validados; no introducir glifos Unicode, emojis o SVG genéricos como sustitutos artísticos definitivos. La revisión y el inventario no cambian las autoridades de catálogo, inspector ni selección B8.
