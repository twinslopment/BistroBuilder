# Editor V2 — R4 nativo Windows — 12/10/2026

## Destino y alcance

El usuario aclara que el Editor V2 se ejecutará como un ejecutable normal de Windows. La entrega es el Player Unity existente, con sus controles nativos y sus autoridades B8/B9/B10. El HTML/CSS/JavaScript queda únicamente como banco de comparación; sus 31 comprobaciones no se cuentan como validación del juego.

Referencia vigente: la imagen completa de 1672 × 941 aportada por el usuario (`ApprovedFullReference.png`). Se trabaja sobre paneles, filtros, cabecera, ficha y herramientas; el restaurante central conserva el prototipo, conforme a la petición de no centrar esta revisión en él.

## Cambio nativo

- Siluetas aprobadas independientes: 28 trazados convertidos a una fuente vectorial propia (`ApprovedGlyphsR4.ttf`), renderizada con TMP SDF. No sustituye la tipografía del texto ni introduce fuentes comerciales redistribuidas. Las métricas conservan el margen izquierdo original de cada trazado; los iconos usan sus cajas medidas.
- Pie: posiciones explícitas de los once botones, leyendas y uniones ornamentales de la referencia. Retirados los divisores y diamantes adicionales de grupo. Continúan las reglas reales de disponibilidad de las operaciones.
- Catálogo: una composición nativa de 557 × 730; búsqueda y categorías medidas, indicador activo del carrusel, tarjeta seleccionada de interior crema. Cuadrícula con primera fila de 117 y segunda de 85 unidades, conservando virtualización y scroll. La captura de la primera iteración detectó recorte de precios; se corrigió el alto variable heredado que lo producía y se repitieron QA/build/capturas.
- Ficha: marco ornamental del preview, caja de imagen de 286 × 172, vínculo visual del preset dimensional, material original desde el prefab y variantes reales. El vínculo indica el preset publicado; no finge una operación de redimensionado que el backend no ofrece.
- El producto de la build R4 abre directamente V2 a pantalla completa, incluso si existen preferencias de ventana de una sesión anterior. F11 alterna ventana y pantalla completa. Se oculta la apertura previa sin crear ni guardar una nueva partida.

Los ornamentos muestreados contienen únicamente bordes y papel; rótulos, campos, iconos, botones y consultas son controles nativos. El catálogo sigue mostrando definiciones, imágenes, dimensiones, materiales y precios reales. No se publica una fotografía de mesa de referencia sobre un prefab distinto.

## Resultados comprobados

- `Logs/editor_v2_r4_native_qa_5.log`: **134 comprobaciones Play Mode PASS**, salida 0. Búsqueda, categorías, favoritos, recientes, precio y ámbito combinados, orden, clics mediante raycaster, favorito de tarjetas recicladas, colocación provisional canónica, ficha, zoom, selección y regreso desde arquitectura. Un inspector y una presentación por contexto; glifos SDF presentes; geometría de botones y filas comprobada.
- `Logs/editor_v2_r4_b9_regression.log`: **42 OK / 0 fallos**, con 10.000 definiciones, filtros y pool acotado. Informe `EditorV2_B9_ScalableCatalog_Report.txt`.
- `Logs/editor_v2_r4_native_build_2.log`: Unity 6000.3.19f1, Windows x64, **Build Succeeded, 0 errores, 13 advertencias**. La configuración de producto/pantalla del proyecto se restaura después de generar la build específica.
- `Logs/editor_v2_r4_native_player_2.log`: Player GPU, salida 0 / `CAPTURE_COMPLETE`. Galería, búsqueda, vacío, filtros, ficha, construcción, superficies, selección y pantalla completa; ningún frame negro ni inspectores superpuestos. Resolución física del monitor: **1920 × 1080**; entrada a FullScreenWindow y regreso a ventana confirmados.
- `Logs/editor_v2_r4_native_startup.log`: salida 0 / `CAPTURE_COMPLETE`, Seleccionar, Mesas, ficha del artículo publicado, un inspector, FullScreenWindow de 1920 × 1080.
- `WorldSelection_4K.png` es una captura con supersampling 2× desde 1920 × 1080; no se presenta como prueba de monitor físico 4K.

Las primeras dos ejecuciones de la QA R4 fallaron por una comprobación auxiliar que intentaba leer una textura de estilo con la copia CPU liberada. Se retiró esa lectura y se volvió a ejecutar la batería completa; no se declaran PASS esas ejecuciones. La prueba GPU se ejecutó en una ventana visible porque la ventana oculta no produjo capturas. Las advertencias/errores de cierre del Editor se conservan en los logs; no se confunden con fallos del Player.

## Entrega y límites

`Builds/Windows/EditorV2_R4_20261012/BistroBuilder_EditorV2_R4.exe` es la revisión de Windows. Extraer siempre la carpeta completa. Incluye runtime/datos, evidencias finales, informes, fuentes de la intervención y manifiesto SHA256. El manifiesto compara `Assembly-CSharp.dll` con R3; el EXE de Unity por sí solo no identifica la versión del código.

No se declara igualdad por píxel ni aceptación gráfica final. Persisten diferencias de acabado de controles/fondo del destacado y de contenido del catálogo respecto a la ilustración. La referencia contiene ocho mesas y variantes aún no publicadas; el proyecto actual conserva sus nueve artículos y modelos de prototipo. No se ha integrado ni publicado esta rama. R2/R3 permanecen como historia y no son la entrega R4.
