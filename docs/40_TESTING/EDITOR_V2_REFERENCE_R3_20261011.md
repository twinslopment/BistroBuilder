# Editor V2 — R3 y pantalla completa — 11/10/2026

## Petición y estado de aceptación

El usuario solicita continuar hasta igualar la imagen aprobada y probar el editor a pantalla completa. La referencia completa de 1672 × 941 sigue siendo vinculante. R2 se conserva como entrega anterior; R3 se entrega en una carpeta distinta. No se declara igualdad por píxel ni aceptación gráfica del usuario.

## Cambios de R3

Catálogo nativo medido en 557 × 730 unidades, sin el escalado adicional del detalle antiguo de 535 × 719. Cabecera, pestañas, categorías, destacado, relacionados y cuadrícula pasan a las medidas de la referencia completa. Fuente SDF Times New Roman Bold para encabezados y Arial Regular/Bold para texto de paneles y controles V2, sin negrita ni tracking sintético adicionales. Las fuentes siguen teniendo fallback a los recursos existentes si el sistema no dispone de ellas.

Marcos superiores y placa central recuperados del original, pie con segmentos vacíos y esquinas grabadas. Se descarta el borde inferior del catálogo que contenía precios y nombres dibujados de la referencia; no se conservan fragmentos de interfaz como fondo de controles reales. Iconos nativos de silla, giro circular, cubo delineado, lupa con signo +, bolsa del destacado y chevrones. Separadores ornamentales del inspector. El resaltado del catálogo sigue al objeto primario de B8 sin alterar recientes ni la propuesta de sustitución B10.

`-bb-editor-v2-fullscreen` inicia la revisión Unity en `FullScreenWindow` con `Display.main.systemWidth/systemHeight`, sin forzar una resolución inferior ni una resolución virtual 4K. F11 alterna ventana/pantalla completa en las sesiones explícitas de revisión; el menú V2 ofrece la misma acción. El ajuste solo afecta a la presentación y no crea, carga o guarda una partida nueva. El lanzador usa `-bb-gallery-viva-review`, conserva la escena y omite la pantalla inicial anterior.

La revisión HTML incorpora entrada/salida real mediante Fullscreen API, con gesto de usuario; el lanzador abre un aviso con el botón «Probar en pantalla completa». Marcos y uniones del pie sin rótulos raster duplicados, divisores del inspector, fotos de relacionados de sus zonas originales y ocho tarjetas con nombre/precio completos. Se mantiene el mismo conjunto de datos de revisión independiente y los filtros combinados.

## Evidencia

- Play Mode: 106 comprobaciones PASS, `Logs/gallery_viva_r3_qa_5.log`, informe `Logs/GalleryViva/functional.txt`.
- Navegador: 16 comprobaciones PASS en `Tools/EditorV2ReferencePreview/evidence/browser-checks-r3.json`, con interacciones reales. Incluye filtros, favoritos, recientes, zoom, vacío, plegado, exterior, entrada/salida a pantalla completa, textos completos y geometría 4K.
- Compilación final: `Logs/gallery_viva_r3_build_6.log`; informe reproducido al final.
- Player GPU: `Logs/gallery_viva_r3_player_6.log`, salida 0 / CAPTURE_COMPLETE. `Fullscreen_Native.png` de 1920 × 1080 en el monitor disponible; informe `FULLSCREEN_NATIVE=True`, modo FullScreenWindow y retorno a ventana True.
- Captura HTML completa de 3840 × 2160. `WorldSelection_4K.png` nativa usa supersampling 2× de 1920 × 1080; no se confunde con resolución física 4K.
- Un único inspector en todas las capturas de ficha, construcción, superficies y selección. Cero capturas negras.

Se mantiene la excepción del índice UnityEditor.Search observada al arrancar el Editor; no se atribuye a la prueba visual del Player. El resultado de compilación incluye sus advertencias, no se presenta como ejecución sin avisos.

## Límites que siguen pendientes

La vista HTML conserva el restaurante central como ilustración y utiliza ocho artículos de la imagen como datos de revisión. No es una escena 3D interactiva ni escribe en una partida Unity. La escena Unity conserva los nueve artículos publicados y los modelos de prototipo. Se ha comprobado el inventario local de FBX/GLB: hay modelos de sillas, paredes y puertas, pero las ocho mesas ornamentales y el restaurante de la ilustración no están publicados como modelos jugables.

Todavía hay diferencias de acabado, iconos y ornamentos respecto de la imagen aprobada. Las pruebas funcionales y el modo pantalla completa están verificados; la identidad gráfica absoluta sigue abierta. No se publica una foto o un precio de referencia sobre un prefab diferente. No se declara cerrado Halo de Huella/Proyector Inteligente ni el contenido artístico del restaurante.

## Entrega

`Builds/Windows/EditorV2_R3_20261011/` y `BistroBuilder_EditorV2_R3_20261011.zip`. Lanzador principal `ABRIR_EDITOR_V2_R3_PANTALLA_COMPLETA.cmd`; se añade lanzador del panel HTML a pantalla completa. Incluye fuentes de la intervención, capturas R3, informes y manifest de integridad. Extraer la carpeta completa antes de ejecutar; no mezclar EXE/datos de R2. Proyecto completo en el worktree `codex/editor-v2-galeria-viva`, sin integración ni push a principal.

## Arranque de revisión probado

El lanzador abre directamente Seleccionar activo, Mesas y la ficha de la primera mesa publicada, con un solo inspector y sin inventar una selección de tres objetos. Se valida por separado con el mismo Player y los mismos flags de pantalla completa: `Logs/gallery_viva_r3_startup_3.log`, `ReviewStartup_Fullscreen.png` y `startup.txt` (CAPTURE_COMPLETE / StartupTool=select). Cambiar filtros/selección después conserva los comportamientos canónicos.

El manifiesto registra el hash SHA256 del código `Assembly-CSharp.dll`, además del EXE, y lo compara con R2: el EXE es el runtime común de Unity y por sí solo no distingue los cambios de aplicación.

## Ajustes finales de revisión

Se fijan los márgenes de los paneles a 9/10 unidades de referencia, altura de 730 y pie de 101. El artículo publicado sin precio conserva el estado canónico «Disponible», con cuerpo compacto para evitar tocar el botón de detalles. En HTML se corrigen el corazón dibujado por duplicado, las cuatro etiquetas de relacionados y el orden de teclado de las variantes. La estrella decorativa deja pasar el clic real al botón de favorito: verificado también en la mesa destacada.

Las 16 comprobaciones del navegador se repiten sobre estos archivos; Play Mode final conserva 106 comprobaciones PASS. Se repiten las capturas GPU y el arranque de revisión tras la última compilación.

## Informe de build

```
BUILD=Succeeded
ERRORS=0
WARNINGS=13
UNITY=6000.3.19f1
UTC=2026-10-11T21:43:38.1139810Z
```
