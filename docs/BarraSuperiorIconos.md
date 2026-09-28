# Barra superior del juego

## Versión vigente — 25/09/2026
La barra del modo normal conserva el logo y los diez iconos originales de la preview V3 aprobada por el usuario. La adaptación a Unity usa un marco marfil y latón dibujado en UI, arte con siluetas geométricas independientes y etiquetas Recoleta. Evita estirar la imagen completa, los recortes cuadrados que cortaban los iconos y las superficies oscuras superpuestas por el tema genérico.

La altura se limita a 76–144 píxeles físicos según el alto de pantalla (aproximadamente 96 px en 1080p), con margen lateral y distribución flexible. Se conserva el selector Normal/Edición procedente de la implementación previa, separado de la barra y oculto durante Nueva partida. La barra de edición mantiene su diseño existente.

Hover inmediato con ajuste de 90 ms, iluminación cálida localizada y movimiento individual definido en `Resources/BistroBuilder/UI/TopBar/Parts/catalog.json`; animación independiente de la velocidad del juego y compatible con movimiento reducido. Los PNG son los originales de la preview sin alteración de píxeles; el recorte de siluetas se realiza como geometría UI. El catálogo describe dimensiones, contornos y parámetros de movimiento.

## Verificación
`BistroBuilderTopBarResponsiveTest.Run` (también accesible desde `BistroBuilderTopNavigationPlayTest.RunBatch`): 77 comprobaciones PASS. Resoluciones 800×600, 1024×768, 1280×720, 1920×1080, 2560×1440, 3440×1440 y 3840×2160. Comprueba límites, altura, ancho, proporciones de logo/iconos, contraste y ausencia de texto cortado, ausencia de estilos genéricos duplicados, píxeles realmente renderizados, hover y navegación Personal → Inventario → Actividad → Opciones.

Capturas de Unity en `Logs/TopBarResponsive/bar-*.png`; resultado en `Logs/TopBarResponsive/result.txt`. Revisadas visualmente las de 1280 y 1920 píxeles. La build se entrega en `Builds/Windows/BistroBuilder_Playtest/`.

## Histórico
La V3 anterior usaba una placa raster completa con hotspots; no satisfacía el tamaño compacto solicitado. El intento posterior de recortar cada icono en un cuadrado cortaba partes del arte y recibía estilos oscuros automáticos. Estas implementaciones quedan sustituidas por la composición responsive actual. La autoridad vigente de diseño se recoge en `docs/30_UI_UX/UI_UX_DEFINITIVE.md`.
## Corrección de nitidez — 26/09/2026
La prueba real detectó preferencias antiguas de Unity a 1280×720 con resolución nativa desactivada: el player ampliaba ese framebuffer al monitor completo. `defaultIsNativeResolution` en la build no sobrescribe esas preferencias existentes. `BistroBuilderDisplaySettings` resuelve el monitor del player al arrancar y solicita su resolución nativa al usar pantalla completa sin bordes. Opciones reutiliza esa autoridad al volver a pantalla completa; el modo ventana y las dimensiones explícitas de línea de comandos se respetan. No se modifica la partida ni se cambia el arte aprobado.

El panel IMGUI antiguo de Construcción se oculta cuando existe el shell definitivo, tanto en modo normal como en edición. La prueba responsive comprueba también esa ausencia.

## Revisión de detalle y respuesta — 28/09/2026
Los iconos se encuadran por los límites de su silueta, eliminando el espacio vacío del recorte sin modificar los PNG ni deformar el dibujo. Ocupan el 59 % de la altura disponible, manteniendo la altura de la barra. El filtrado trilineal con mipmaps Kaiser evita el muestreo inestable al reducir y animar el arte; las texturas UI no heredan reducciones globales de mip. Etiquetas Recoleta más oscuras y de mayor peso/tamaño.
El hover produce respuesta en el propio evento de entrada (también presión y liberación), se asienta en 90 ms y mantiene después un movimiento pequeño. No espera al primer máximo de una onda lenta. Movimiento reducido conserva iluminación/foco sin mover el icono. Prueba ampliada: 77 comprobaciones PASS, con entrada/pulsación/liberación inmediatas y filtrado en las siete resoluciones. Capturas 1280 y 1920 revisadas.
La corrección nativa anterior se verificó en el player: FROM=1280x720, TO=1920x1080, RESOLVED=1920x1080.
