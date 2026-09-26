# Barra superior del juego

## Versión vigente — 25/09/2026
La barra del modo normal conserva el logo y los diez iconos originales de la preview V3 aprobada por el usuario. La adaptación a Unity usa un marco marfil y latón dibujado en UI, arte con siluetas geométricas independientes y etiquetas Recoleta. Evita estirar la imagen completa, los recortes cuadrados que cortaban los iconos y las superficies oscuras superpuestas por el tema genérico.

La altura se limita a 76–144 píxeles físicos según el alto de pantalla (aproximadamente 96 px en 1080p), con margen lateral y distribución flexible. Se conserva el selector Normal/Edición procedente de la implementación previa, separado de la barra y oculto durante Nueva partida. La barra de edición mantiene su diseño existente.

Hover de 170 ms, iluminación cálida localizada y movimiento individual definido en `Resources/BistroBuilder/UI/TopBar/Parts/catalog.json`; animación independiente de la velocidad del juego y compatible con movimiento reducido. Los PNG son los originales de la preview sin alteración de píxeles; el recorte de siluetas se realiza como geometría UI. El catálogo describe dimensiones, contornos y parámetros de movimiento.

## Verificación
`BistroBuilderTopBarResponsiveTest.Run` (también accesible desde `BistroBuilderTopNavigationPlayTest.RunBatch`): 66 comprobaciones PASS. Resoluciones 800×600, 1024×768, 1280×720, 1920×1080, 2560×1440, 3440×1440 y 3840×2160. Comprueba límites, altura, ancho, proporciones de logo/iconos, contraste y ausencia de texto cortado, ausencia de estilos genéricos duplicados, píxeles realmente renderizados, hover y navegación Personal → Inventario → Actividad → Opciones.

Capturas de Unity en `Logs/TopBarResponsive/bar-*.png`; resultado en `Logs/TopBarResponsive/result.txt`. Revisadas visualmente las de 1280 y 1920 píxeles. La build se entrega en `Builds/Windows/BistroBuilder_Playtest/`.

## Histórico
La V3 anterior usaba una placa raster completa con hotspots; no satisfacía el tamaño compacto solicitado. El intento posterior de recortar cada icono en un cuadrado cortaba partes del arte y recibía estilos oscuros automáticos. Estas implementaciones quedan sustituidas por la composición responsive actual. La autoridad vigente de diseño se recoge en `docs/30_UI_UX/UI_UX_DEFINITIVE.md`.