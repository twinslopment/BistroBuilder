# CARTA V3 - Step16 | Nitidez, detalle del plato e iconografía

**Fecha:** 26/10/2026. **Rama:** `codex/carta-v3-step15-fidelity-20261026`.

## Referencia
Comparación del usuario (26/10/2026): primera imagen, referencia aprobada con texto nítido y jerarquía visual fuerte; segunda imagen, implementación Unity anterior. Esta entrega es código uGUI/TMP real, sin recrear la pantalla como imagen estática.

## Cambios de implementación
- Componente `BistroBuilderCartaV3SdfMirror`: representa con atlas SDF oficiales de Recoleta/Inter/Inter SemiBold el texto de detalle, botones inferiores y columnas de platos; conserva Text nativo, enlaces de datos y eventos.
- Tipografía del detalle: encabezado/título/subtítulo superiores, ocho etiquetas, valores, badge de estado y aviso de fotografía inexistente con mayor jerarquía/contraste.
- Inputs y fichas: caracteres más legibles, iconos de los campos 35x32, márgenes izquierdos reservados y conmutador Disponible de mayor dimensión.
- Botonera: pictogramas reales 36x36, iconos blancos dedicados de añadir, eliminar y guardar, duplicación marrón y nueva flecha de deshacer en Descartar. Acciones existentes intactas.
- Filas: Nombre, subtítulo, precio, modalidad, preparación y margen con SDF, manteniendo selección, ordenación, desplazamiento y datos.
- Navegación y modelos de negocio sin alterar; los platos sin fotografía muestran un placeholder veraz, sin inventar imágenes.

## Validación
- `Logs/CartaV3Step16_FinalRegression.log`: **78 PASS / 0 FAIL** en Play Mode; incluye nuevas comprobaciones explícitas de SDF e iconos en uGUI.
- `Logs/CartaV3Step16_FinalBuild.log`: **BB_PLAYTEST_BUILD_PASS** Windows; incluye cambios de scripts e iconos.
- Compilación de ensamblados Unity OK. Las 15 advertencias de compilación/build reportadas por Unity no son errores bloqueantes.
- Ejecutable: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

## Limitación pendiente
No atribuir paridad visual píxel a píxel a la captura de referencia: la sesión gráfica completa de Platos, con el contenido ilustrativo del usuario, no ha sido capturada después de esta build. Los datos de la demo y los platos reales difieren. Revisar en PC la composición a 1920x1080 y 1280x720 antes de cerrar definitivamente la equivalencia visual.
