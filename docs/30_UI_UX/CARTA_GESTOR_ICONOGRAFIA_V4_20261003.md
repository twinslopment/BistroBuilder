# Carta Gestor V4 — iconografía de encabezados (03/10/2026)

## Especificación acordada
Cambiar **solo cuatro posiciones de encabezado** en Carta:
- CARTA: libro marrón «MENÚ» recuperado de la imagen aportada por el usuario, sin reinterpretar su motivo
- Mis cartas: colección de cartas/libros apilados
- Reglas de activación: libro de menú con reloj
- Detalle de la regla: nueva tablilla/portapapeles con tres comprobaciones (sustituye expresamente el anterior icono rechazado)

**Prohibido reutilizar estos iconos decorativos en las filas de las listas, los botones o las demás pestañas.** Conservar los iconos de filas BBIconCatalog / SVG ya programados. Sin alteraciones en dominio, reglas, cartas, persistencia ni Save/Load.

## Fuentes de recursos
Cuatro PNG transparentes optimizados, almacenados como:
`Assets/Resources/BistroBuilder/UI/MenuHeaderIcons/{carta_main,mis_cartas,reglas_activacion,detalle_regla}.png`.
Los recursos se importan sin alterar los archivos tipográficos del proyecto. Los cuatro PNG transferidos a Windows se verificaron individualmente por SHA256.

## Presentación web
`docs/30_UI_UX/previews/Carta_Gestor_Iconografia_V4.html` deriva directamente de la V3 aprobada con cuatro sustituciones estáticas de encabezado y seis reglas CSS específicas de tamaño/sombra. Se mantienen JS, interactividad, filas, formularios, casillas centradas, Recoleta e Inter. Previews de Chrome Windows con recursos reales en `Carta_Gestor_Iconografia_V4_1440x814.png` y `Carta_Gestor_Iconografia_V4_1280x720.png`.
Copia para abrir en la máquina del usuario: `C:\Users\mruperez\Downloads\Carta_Gestor_Iconografia_V4.html`, con URLs de recursos ajustadas a esa carpeta.

## Unity
`BistroBuilderMenuEditorUiFactory` reutiliza un único cargador/caché de sprites `Resources/BistroBuilder/UI/MenuHeaderIcons`. `AddMenuHeaderIcon` aplica el libro original; `AddPortfolioSectionIcon` selecciona una de las tres imágenes por título. Cuando faltan recursos se mantiene la iconografía anterior como fallback seguro. Los botones/filas se crean por vías independientes y no están modificados.

## Validación
- Unity 6000.3.19f1, escena real Prototype_Restaurant, `BistroBuilderMenuVisualV1RuntimeProbe.RunBatch`: **26 PASS / 0 FAIL, EXIT 0**.
- Gates nuevos: cuatro imágenes distintas presentes en los encabezados, sin raycast y manteniendo aspecto; icono de fila deliberadamente diferente al decorativo del encabezado.
- Verificaciones anteriores de Carta: navegación de Gestor/Editor/Recetas, confirmación destructiva, toggles centrados, listas reales, escandallo y scrolls; siguen pasando.
- Capturas Chrome revisadas: la V4 conserva las tres columnas y solo cambia los cuatro iconos del encabezado.
- Esta aceptación de iconos NO implica fusión a master. La rama de presentación sigue aislada.
