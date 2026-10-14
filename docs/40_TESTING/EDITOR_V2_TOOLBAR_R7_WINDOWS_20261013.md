# Editor V2 — R7 Windows: botones de barra inferior — 13/10/2026

## Petición y resultado

El usuario rechaza el aspecto poco profesional de Eliminar y pide mejorar también los demás botones de la barra. Referencia del estado anterior: `codex-clipboard-a413446e-8fe1-4cf1-afdf-7070feba8d7f.png`.

Los once botones tienen una superficie marfil de relieve fino, borde de latón discreto, estados de hover/pulsación específicos y una selección ámbar. Eliminar usa burdeos con texto e icono claros cuando está disponible y un tono rosado suave con tinta legible cuando está desactivado. El rojo saturado anterior con icono/texto grises no se utiliza en la barra.

La papelera se redibuja con geometría original de líneas uniformes, tapa, asa y dos ranuras. `ToolbarTrashR7.ttf` lleva ese trazado a TMP SDF y se utiliza exclusivamente en Eliminar de la barra; los otros controles conservan sus siluetas SDF. El SVG fuente se conserva como trazabilidad, no como una imagen de un botón completo.

Iconos centrados en sus cajas, altura 30, posición superior 3 y separación de la etiqueta. Etiquetas de tamaño 13 y fuente semibold común. Las superficies de estados se generan una vez y se reutilizan, con nueve cortes y esquinas suavizadas. No se generan texturas, materiales o controles cada frame. Pulsar desplaza solamente el contenido una unidad hacia abajo; el rectángulo de clic conserva su posición. La navegación por teclado conserva un anillo de foco diferenciado.

Esta petición actualiza el acabado interno de los botones. Conserva el marco aprobado, los once controles, grupos MODO/EDITAR/AYUDAS, títulos, cajas de 54 unidades, pie de 85 unido al borde inferior y papel opaco. La cabecera R6 conserva su unión superior y el texto de iluminación. No modifica el restaurante central.

## Autoridad y comportamiento

La disponibilidad y las acciones siguen procediendo del shell y las autoridades de selección existentes. Eliminar, Mover, Girar y Duplicar no se habilitan artificialmente. Snapping conserva su indisponibilidad actual. Los eventos de apariencia no ejecutan acciones de juego. Perder la selección mientras se pulsa elimina el estado de presión y restaura el contenido.

## Verificación

- `Logs/editor_v2_r7_toolbar_qa_4.log`: **293 comprobaciones nativas Play Mode PASS**, salida 0. Incluye regresiones de filtros, búsquedas, favoritos, recientes, precios, ámbito, orden, rebind, colocación, prioridad de inspector, superficies, cabecera/luz y pie. Añade once controles nativos, etiquetas sin desbordamiento, centrado y separación de iconos, opacidad y contraste mínimo de 4,5:1 en ambos extremos del degradado de los once botones; Eliminar no invoca su callback desactivado. Selección real habilita Eliminar; los botones disponibles responden a entrada, pulsación, liberación y salida, reutilizan sus sprites y recuperan la posición exacta. El botón derecho no produce estado pulsado y perder la selección cancela una pulsación sostenida. El PASS corresponde a las comprobaciones funcionales; no representa ausencia de la excepción preexistente del indexador del editor que se detalla abajo.
- `Logs/editor_v2_r7_toolbar_build_2.log`: Unity 6000.3.19f1, Windows x64, **Build Succeeded, 0 errores, 13 advertencias**. La configuración de producto/pantalla se restaura tras compilar esta revisión.
- `Logs/editor_v2_r7_toolbar_states.log`: Player GPU, salida 0, `CAPTURE_COMPLETE`, FullScreenWindow físico 1920 × 1080. Capturas de Eliminar desactivado, disponible mediante selección de un objeto real, hover y pulsado. El recorrido visual no invoca clic sobre Eliminar. Recortes de barra sin reescalado, revisados visualmente; `Toolbar_Available_4K` tiene 3840 × 2160 por supersampling 2×, no prueba de monitor físico 4K.
- `Logs/editor_v2_r7_toolbar_player.log`: Player GPU, salida 0, `CAPTURE_COMPLETE` en catálogo, asientos, búsqueda, vacío, filtros, ficha, construcción, superficies y selección. Ventanas 1280 × 720, 1672 × 941 y 1908 × 1042, pantalla completa nativa y regreso a ventana.

Se conservan los intentos previos, sin atribuirles PASS:

1. Fallo al comparar el estado inicial de Vistas con la salida del puntero. La prueba ahora establece expresamente un reposo mediante pointer-up/pointer-exit antes del ciclo completo.
2. La comprobación de perder selección esperaba solo cinco frames. El shell actualiza sus read models cada 0,35 segundos; la prueba ahora observa la indisponibilidad real antes de comprobar el estado de la vista.
3. Una lectura de Python con la codificación predeterminada de Windows alteró los acentos de los archivos de QA/captura. Se restauró UTF-8, se repitió la batería completa y se recompiló el Player. La prueba final de búsqueda con acentos y mayúsculas está incluida en las 293 comprobaciones.
4. La primera captura de estados empezó a 1920 × 1080 pero terminó en ventana 1670 × 940; por ello produjo `CAPTURE_FAILED`, aunque las imágenes del acabado eran útiles como avance. El informe y el log se conservan como intento fallido. La captura automatizada controla ahora sus cambios de pantalla sin consumir F11; las sesiones interactivas conservan F11. Se repite hasta obtener `CAPTURE_COMPLETE` a resolución nativa.

Las cuatro ejecuciones de QA del editor registran una excepción de `UnityEditor.Search.SearchDatabase` durante indexación. También está en `Logs/editor_v2_r6_header_qa_2.log`, anterior al ajuste. No se ha cambiado ni cerrado ese subsistema. Es una limitación del entorno de editor; los registros finales del Player y la prueba funcional se conservan por separado. Los guards de escena pasan tras las ejecuciones.

## Entrega

`Builds/Windows/EditorV2_R7_20261013/BistroBuilder_EditorV2_R7.exe` inicia directamente en Editor V2 a pantalla completa. F11 alterna ventana. Conservar la carpeta completa de datos/runtime junto al EXE.

ZIP diferenciado R7, fuentes del ajuste, fuente vectorial original, capturas y registros. `MANIFEST_SHA256.json` verifica los archivos y que `Assembly-CSharp.dll` difiere de R6. No modifica la entrega R6. Rama sin integrar; este ajuste no declara aceptación gráfica global de todo el editor.
