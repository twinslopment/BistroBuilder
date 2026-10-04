# CARTA — Gestor: especificación visual vinculante (04/10/2026)

## Fuente de verdad

Captura aprobada por el usuario: **Carta_Gestor_PersonalV8_V3_1280.png** (1280 × 720). Coincide con el gestor `docs/30_UI_UX/previews/Carta_Gestor_PersonalV8_V3.html`. Se programa la **vista nativa Unity del Gestor**, no una sustitución por una imagen plana ni un webview.

Si la iconografía decorativa V4 difiere de esta captura, el Gestor adopta la V3: libro abierto plano en CARTA y Mis cartas, reloj sólido en Reglas de activación, documento plano en Detalle de la regla. La barra de navegación superior conserva sus propios iconos oficiales y el menú CARTA de cuero; no cambiar globalmente `BistroBuilderMenuEditorUiFactory` para el resto de pantallas.

## Implementación

- `BistroBuilderCartaReferenceV3Style.cs`: estilos específicos de la vista Gestor aplicados al árbol uGUI real: gradientes marfil y crema, borde latón redondeado, remaches discretos, fondos con nine-slice, selección miel, acciones destructivas rojas, inputs marfil, botonera en latón y status verde/gris
- Recursos SVG exactos exportados de los `<symbol>` de la preview V3 (no similares), almacenados en `Assets/Resources/BistroBuilder/UI/CartaReferenceV3Icons`; incluye variantes blancas para destructivas
- `BistroBuilderMenuPortfolioRuntimeView`: tres columnas con la anchura V3, formulario de regla con etiquetas visibles encima de sus campos; los siete días conservan sus Toggle reales y check centrado; scroll en listas, metadatos y nombres en textos diferentes (Recoleta/Inter)
- Recoleta oficial en títulos, nombres y botones; Inter para descripciones, cifras/inputs y caracteres incompatibles con la Recoleta DEMO. Los nombres y encabezados con acentos usan runs tipográficos mixtos tal como hace la preview HTML
- No se modifican `BistroBuilderMenuPortfolioService`, horarios/evaluación, Save/Load, datos de cartas ni acciones de usuario: únicamente la presentación y el texto resumen visible de las filas. Se conserva el cierre vía botón discreto y Escape
- El marco respeta el espacio de las barras del juego y mantiene anclas responsivas. 1280×720 y 1920×1080 son targets de verificación

## Verificación y límites

Se han compilado **Assembly-CSharp** y **Assembly-CSharp-Editor** con el SDK local y referencias reales de Unity: **0 errores**. Las advertencias de otros módulos preexistentes no implican fallo de esta implementación. El segundo proceso Unity batch no pudo inicializar su Package Manager mientras una instancia de Editor estaba abierta; el resultado de una compilación C# independiente no debe anunciarse como un Play Mode PASS ni como una certificación visual pixel-perfect.

La preview HTML V3 y sus capturas son la referencia visual; una captura real de Game View a ambas resoluciones queda como control final de similitud al abrir Unity, antes de declarar paridad absoluta.
