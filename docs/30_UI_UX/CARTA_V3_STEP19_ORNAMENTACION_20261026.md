# CARTA V3 — Step19: ornamentación y controles fieles a referencia

**Fecha:** 26/10/2026. **Rama aislada:** `codex/carta-v3-step15-fidelity-20261026`.

## Implementación C# / Unity
- Nuevo componente estático `BistroBuilderCartaV3Ornamentation`: marco de marfil y latón compuesto por múltiples perfiles finos, reflejos y cuatro medallones decorativos calculados en runtime. No es un screenshot ni sustituye la UI.
- Bordes iluminados/rebajados en las tres columnas y los cinco botones principales, sin alterar sus eventos.
- Botón de cerrar con dos trazos geométricos de X; evita un glifo ausente en el runtime Windows.
- Flechas desplegables de Categoría y Modalidad en el Detalle del plato: vinculadas mediante `Button` al editor real de plato/receta, en lugar de simular cambios sin acción.
- Todas las piezas añadidas de ornamentación llevan `raycastTarget=false`; únicamente se habilita entrada en los controles operativos.
- Sin cambios de lógica de negocio, inventario, comidas ni datos de recetas; sin tocar la maestra.

## Pruebas y evidencias
- `Logs/CartaV3Step19_OrnamentationRegression.log`: **81 PASS / 0 FAIL** en el probe Unity; incluye 2 pruebas nuevas de la ornamentación y click targets.
- `Logs/CartaV3Step19_FidelityBuild.log`: **Build Finished, Result: Success** y `BB_PLAYTEST_BUILD_PASS` Windows.
- `Logs/CartaV3Step19_Screens/CartaV3_Unity_1280x720.png` y `CartaV3_Unity_1920x1080.png`: capturas reales del ejecutable, no imágenes diseñadas externamente.
- Ejecutable disponible: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

## Revisión visual
Confirma marcos metálicos de borde, esquinas decorativas, pictogramas legibles, X real y dos flechas en el formulario. Mantiene el diseño responsive y la información real de datos.

La equivalencia no es pixel-perfect: el prototipo de referencia muestra ilustraciones y platos de ejemplo que no son necesariamente los que existen en los datos reales. No se han inventado imágenes de platos. El trabajo de microdecoración de cabecera y proporciones exactas sigue abierto a revisión visual.
