# Editor V2 — Paneles R4 — 12/10/2026

**Destino de entrega: el ejecutable nativo de Windows de BistroBuilder, en Unity.** Este HTML es únicamente un banco de comparación del diseño. La aclaración del usuario del 12/10/2026 descarta el navegador como destino de ejecución.

Para comparar el diseño HTML, abrir `ABRIR_PANELES_V2_R4_PANTALLA_COMPLETA.cmd` y pulsar **Probar en pantalla completa**. La rueda de opciones permite volver a ventana. Esta prueba no sustituye la verificación del ejecutable nativo.

## Composición

Esta revisión se centra en los paneles y filtros solicitados. La referencia vinculante es `assets/reference-full.png`, de 1672 × 941. El restaurante central conserva la ilustración aprobada como contexto visual.

Una única hoja CSS define el catálogo de 557 × 730, la ficha de 318 × 730, la cabecera y la barra de herramientas. No se cargan estilos de R3, paneles anteriores, marcos de grupo superpuestos ni textos de diagnóstico dentro de la interfaz. Cada recurso de código lleva una versión explícita en la URL para evitar recuperar estilos anteriores desde la caché.

Los títulos, etiquetas, botones, campos y filtros son HTML real. Los 28 iconos se dibujan mediante trazados SVG independientes medidos en la referencia. Los bordes usan exclusivamente segmentos ornamentales sin títulos ni botones rasterizados. Las imágenes de producto y el emblema proceden del PNG original, sin recomprimirlo. El fondo generado usado en R2/R3 deja de formar parte de esta revisión.

## Controles comprobados

Búsqueda sin distinción de acentos o mayúsculas; categorías; Todos/Favoritos/Recientes; ubicación y límites de precio combinados; ordenación; validación del rango; estado vacío; favoritos persistentes; recientes en orden de selección; carrusel con indicador activo; relacionados; ficha y variantes; ampliación de imagen; plegado del catálogo; estados de modo, snapping y vinculación de dimensiones; valores de materiales y rotación conservados durante el filtrado; entrada y salida de pantalla completa mediante el evento `fullscreenchange`.

Los ocho productos son datos de revisión tomados de la imagen, no productos publicados en Unity. Las categorías sin artículos en la referencia muestran un estado vacío. Los botones que requieren un motor 3D emiten `editor-v2-command` con el artículo y el valor correspondientes: este HTML no modifica una partida ni sustituye la autoridad de Unity. No se muestran mensajes técnicos al pulsarlos.

## Evidencia y límites

`evidence/browser-checks-r4.json`: **31 comprobaciones PASS** con campos y clics reales. Incluye ausencia de paneles duplicados, nombres y precios completos y geometría dentro de una ventana de 3840 × 2160. Se conserva una observación no concluyente de la lectura directa de `fullscreenElement`; la entrada/salida se confirmó con el evento nativo y el cambio del control visible.

Capturas: `EditorV2_PANELES_R4_1672.jpg`, `EditorV2_PANELES_R4_FILTROS.jpg` y `EditorV2_PANELES_R4_4K.jpg`. El proveedor de capturas de la vista integrada devuelve imágenes reescaladas y borrosas: estas capturas documentan el estado y no certifican nitidez física ni igualdad por píxel. La prueba automática de Chrome se interrumpió al no poder confirmar su URL actual. No se automatizó Chrome por otra vía.

Textos y SVG se renderizan a la resolución de la pantalla mediante CSS, sin transformar el editor como una imagen plana. Las fotos y los segmentos ornamentales conservan el detalle de su fuente original de 1672 × 941. No se declara identidad gráfica absoluta ni aceptación final del usuario. El fondo del destacado y algunos acabados de controles aún pueden diferir de la imagen aprobada.

## Archivos

`index.html`, `editor-v2.css`, `editor-v2-glyphs.js` y `editor-v2.js`. El conjunto de datos de revisión está declarado en JavaScript. No se carga código del modo de edición anterior.

Servidor opcional desde la raíz del proyecto: `python -m http.server 8767 --bind 127.0.0.1 --directory Tools/EditorV2ReferencePreview`.
