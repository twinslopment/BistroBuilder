# Editor V2 — Galería Viva nativa — 10/10/2026

## Alcance y referencia vinculante

Implementación autorizada expresamente por el usuario después de recuperar y leer las referencias de «Diseño visual Editor V2» y «Analizar Bistro Builder» y el bundle documental completo. La referencia de detalle suministrada el 10/10/2026 es `docs/ModoEdicion/EditorV2/ReferenciasRecuperadas/Imagenes_Originales/EditorV2_GaleriaViva_Detalle_Usuario_20261010.png` (535 × 719). Prevalece la última instrucción del usuario: nada de la presentación anterior en el modo edición.

Trabajo aislado en `codex/editor-v2-galeria-viva`, sobre `feature/editor-v2` y su snapshot de trabajo visual vigente. No se modifica la rama principal ni el checkout de origen. El snapshot anterior se conserva en `.tmp/inherited-v2-wip.json`; los archivos anteriores a esta intervención se preservan en `.tmp/gallery-viva-authored-baseline/`.

## Implementación

- `BistroBuilderGalleryViva`: única presentación nativa del catálogo de objetos. Composición de 535 × 719, iconos originales recuperados de la referencia, papel marfil, tipografías Recoleta/Inter, buscador, tres pestañas, nueve categorías laterales, destacado, relacionados y cuadrícula de cuatro columnas. Se escala entre las barras V2 reales, evitando que tapen el título o la cuadrícula.
- Controles reales de Unity, TMP y EventSystem. La referencia no se usa como pantalla plana con zonas clicables. Producto, nombre, precio y miniatura proceden de las definiciones canónicas; los recortes de la referencia solo aportan ornamentos y papel.
- B9 mantiene el índice y la virtualización. Búsqueda insensible a acentos y mayúsculas, favoritos, recientes en orden de uso, categoría, ámbito y límites de precio combinables. Ordenación por relevancia, precio ascendente/descendente y nombre. El destacado y los relacionados siempre respetan la misma consulta.
- Corazones de tarjetas recicladas resuelven la definición actual, evitando referencias capturadas de otro artículo. Las categorías y favoritos tienen receptores de clic propios.
- Las tarjetas siguen llamando al controlador canónico del panel: sin selección, colocación provisional; con selección B8, candidato de sustitución B10. No se duplica colocación, selección, economía, persistencia, construcción ni SAVIC.
- Se desactivan los renderizadores previos ApprovedSkin/PreviewSkin/GalleryShowcase/PremiumLayout. Todos los hijos anteriores de `CatalogContent` permanecen ocultos, incluidos los creados o reactivados después. El diagnóstico IMGUI provisional se excluye del bootstrap; se conserva su servicio B11. El panel IMGUI de construcción permanece excluido por el shell V2.
- Taller de paredes y superficies e inspectores con paleta nativa marfil/latón y Recoleta, sin el acabado blanco/verde previo. La ficha de producto y la de selección B8 protegen sus colores ante escaneos del tema; las barras no tapan sus encabezados. Se conserva la construcción canónica y su economía.
- El corazón de la ficha de producto y los corazones del catálogo actualizan la misma consulta de favoritos. El encabezado de la ficha permanece fuera del área desplazable, con cierre accesible. Las filas de selección B8 dejan el espacio flexible al scroll, no a las pestañas.
- Favoritos y recientes son de la sesión actual; no se introduce un nuevo formato de guardado.

## Validación comprobada

- Unity **6000.3.19f1**. Play Mode sobre `Prototype_Restaurant.unity`: **61 comprobaciones PASS**, `Logs/GalleryViva/functional.txt`, ejecución `gallery_viva_functional_final.log`.
- La batería comprueba los clics mediante `EventSystem.RaycastAll`, incluido el desplegable, campos de precio y botón de aplicar, ámbito exterior, limpiar filtros, categorías y corazones. Comprueba también rebind del pool, colocación provisional y ausencia de presentaciones antiguas después de refrescos de tema.
- B9: **42 OK / 0 fallos**, con 10.000 definiciones, pool acotado y 300 actualizaciones de scroll. Reporte `EditorV2_B9_ScalableCatalog_Report.txt`.
- Windows x64: build independiente, cero errores de compilación; conserva las advertencias previas del proyecto. Reporte incluido en `BUILD_REPORT.txt`.
- Render GPU real del Player, Intel Iris Xe, a **1920 × 1080 y 1280 × 720**, captura completa y recorte del catálogo. Se verifican catálogo completo, asientos, búsqueda de «básica», estado vacío, desplegable de filtros, ficha de producto, Taller de paredes y superficies, y selección del mundo B8. No se equipara la prueba headless a una captura gráfica.
- Correcciones derivadas del render: altura del título Recoleta, insets reales de las barras, anchura de contenido virtualizado, círculos de flechas independientes del recurso interno `UI/Skin/Knob.psd`, y cambio efectivo de resolución para evitar capturas negras de una ventana oculta.

## Entrega y límites materiales

Entrega separada: `Builds/Windows/EditorV2_GaleriaViva_20261010/`, con ejecutable y lanzadores `ABRIR_GALERIA_VIVA_1280.cmd` y `ABRIR_GALERIA_VIVA_1920.cmd`. Los lanzadores abren directamente el modo V2 de esta revisión. No utilizar las builds anteriores para evaluar este cambio.

El catálogo publicado tiene nueve definiciones reales (dos mesas de prueba, seis sillas y una planta). Las ocho mesas fotografiadas y sus precios en la maqueta no están publicadas en este catálogo; no se asignan esas fotos o importes a otros prefabs. El ambiente fotográfico del destacado y el contenido final requieren esos recursos de producto. La interfaz nativa está implementada; la identidad gráfica exacta de todo Editor V2, Halo de Huella y aceptación visual del usuario no se declaran cerradas con esta entrega.
