# Editor V2 — R6 Windows: cabecera y selector de luz — 12/10/2026

## Petición y resultado

El usuario pide aplicar el cierre de fondo y la unión al borde de pantalla también a la barra superior, y corregir el texto que se descuadra al cambiar Día, Atardecer y Noche. Referencia del fallo: `codex-clipboard-862f40c9-46b3-457d-9158-02eb5ba3cc29.png`.

La cabecera ocupa todo el ancho y empieza en posición superior 0, sin margen de 8. Conserva 74 unidades de alto (aproximadamente 85 píxeles a 1920 × 1080). El papel es opaco hasta sus bordes y esquinas. El marco ornamental es una capa por encima de ese papel, para que cerrar el fondo no oculte el borde dorado. El logo se adapta dentro de la barra y evita el desplazamiento negativo que lo recortaría al tocar el borde superior.

La barra inferior compacta R5 conserva sus 85 unidades y su posición inferior 0. No se altera el restaurante central ni las autoridades/filtros del catálogo.

## Causa del texto duplicado y corrección

`EditDay.GetComponentInChildren<TMP_Text>()` tomaba el primer texto descendiente, el glifo SDF del sol. Al seleccionar un preset, escribía su nombre en ese glifo grande; la etiqueta auténtica permanecía en Día. Esto producía texto duplicado, de tamaño y ubicación incorrectos, como muestra la captura del usuario.

La vista ahora enlaza expresamente `EditDay/Label`. Su fuente tiene tamaño 14, sin autoajuste, y una caja fija de 63 × 48 a posición 64/0. Atardecer ocupa 60,7 unidades, cabe sin desbordar y conserva separación antes de la flecha. Día y Noche comparten exactamente la misma caja y estilo. Seleccionar un preset o restaurar la vista actualiza esa etiqueta y preserva el glifo del sol.

La iluminación sigue siendo previsualización por las acciones existentes; no se introduce otra autoridad ni un cambio de hora del juego.

## Verificación

- `Logs/editor_v2_r6_header_qa_2.log`: **184 comprobaciones Play Mode PASS**, salida 0. Mantiene las pruebas de filtros, búsqueda, favoritos, recientes, orden, reciclado, colocación, ficha, selección, arquitectura y pie R5. Añade cabecera al borde superior/ancho completo, papel continuo, marco sobre papel, logo contenido y ciclo Día → Atardecer → Noche → Día mediante clics comprobados con raycaster. En cada estado valida etiqueta, glifo del sol, tamaño, posición, ancho de texto y separación de flecha.
- `Logs/editor_v2_r6_header_build.log`: Unity 6000.3.19f1, Windows x64; **Build Succeeded, 0 errores, 13 advertencias**. Configuración general de producto/pantalla restaurada tras compilar la revisión.
- `Logs/editor_v2_r6_header_states.log`: Player GPU, salida 0; `CAPTURE_COMPLETE`, FullScreenWindow **1920 × 1080**, un inspector, Seleccionar/Mesas. Capturas nativas de Día, Atardecer, Noche, regreso a Día y menú abierto. En los cuatro cambios: `FontSize=14`, `SunPreserved=True`.
- Recortes de la cabecera obtenidos de esas capturas, sin reescalado, e inspeccionados visualmente: papel y borde dorado completos, sol conservado y etiqueta en su celda.
- `GalleryVivaQA/header_pixel_comparison.json`: comparación de los recortes 1920 × 85 de Día/Atardecer/Noche. Cero píxeles diferentes fuera de la región de la etiqueta y cero diferencias en la región del sol. Se conservan las regiones y SHA256 de los PNG comparados.
- `Logs/editor_v2_r6_header_player_2.log`: Player GPU, salida 0; `CAPTURE_COMPLETE` en galería, asientos, búsqueda, vacío, filtros, ficha, construcción, superficies y selección. Tamaños de ventana 1280 × 720, 1672 × 941 y 1908 × 1042; pantalla completa 1920 × 1080 y regreso a ventana verificados. `WorldSelection_4K.png` es supersampling 2× y no una prueba de monitor físico 4K.

La primera ejecución R6 falló en la prueba de clic a un desplegable recién activado, antes de que sus gráficos se registraran en el canvas; la rutina anidada tampoco propagó la excepción al informe principal y se detuvo únicamente ese proceso de QA. Se corrigió el control de la rutina y se esperan tres frames después de abrir el menú. Segunda ejecución completa: 184 PASS. Se conserva el primer log como evidencia fallida, sin atribuirle PASS. El guard de escena pasó tras ambas ejecuciones.

El primer recorrido general del Player (`editor_v2_r6_header_player.log`) cerró con salida 0 antes de producir capturas/informe; no se cuenta como validación. Se conservó el registro y se repitió el recorrido hasta obtener `CAPTURE_COMPLETE`.

## Entrega

`Builds/Windows/EditorV2_R6_20261012/BistroBuilder_EditorV2_R6.exe` abre directamente el Editor V2 a pantalla completa. F11 alterna ventana. Mantener la carpeta completa de datos/runtime junto al ejecutable.

Incluye capturas de los estados, recortes de las barras, fuentes de este ajuste, informes y registros. `MANIFEST_SHA256.json` compara `Assembly-CSharp.dll` con R5 para verificar que el código corresponde a la corrección. R5 queda conservada como historia; esta entrega no modifica una build previa. Rama sin integrar y aceptación gráfica global no declarada.
