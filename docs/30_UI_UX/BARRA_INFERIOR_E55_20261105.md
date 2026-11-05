# Barra inferior E5.5 — Bistro Builder (modo juego)

**Estado:** primera implementación técnica. La referencia visual E5.5 sigue seleccionada provisionalmente, no aprobada definitivamente.
**Rama:** `feature/ui-bottom-e55-20261105` (nace de `feature/ui-topbar-v21-approved-20261105`).

## Contenido autorizado

1. **Caja:** ingresos del servicio actual en euros, no saldo total. Procedencia: `BistroBuilderFinancialResultsService.TryGetCurrentServiceResult().revenueCents`. Sin datos, mostrar raya.
2. **Satisfacción:** puntuación dinámica de las visitas activas del servicio, ponderada por clientes, obtenida del registro de visitas de `BistroBuilderCustomerExperienceTrackingService` y el evaluador canónico. Sin visitas activas, mostrar raya; nunca reutilizar la valoración de otro servicio.
3. **Climatología:** condición exterior actual y temperatura Celsius. Procedencia: `BistroBuilderClimateService.CurrentWeather`.
4. **Velocidad:** Pausar/Reanudar y 1×, 2× y 3×. Autoridad: `GameClock.SetPaused` y `GameClock.SetSpeedMultiplier`, respetando `IsRuntimeSuspended`.

## Especificación visual

- Diseño seleccionado: **E5.5 negro satinado**, borde exterior en latón/dorado fino, módulos individuales oscuros, relieve discreto. Módulo Satisfacción con matiz verde oscuro; barra de progreso semántica; controles activos en crema/dorado.
- Orden, de izquierda a derecha: Caja · Satisfacción · Climatología · Velocidad.
- Superficies ancladas a la parte inferior y adaptativas al Canvas de PC. Verificar 1920×1080 y 1280×720.
- Iconografía generada por código a partir de figuras geométricas (no fuentes externas, atlas ni recursos remotos).
- Fuera de Modo Edición: el componente reaprovecha `BB_UIUX_BottomOperations`, evita crear un segundo Canvas y oculta de forma reversible el dock de tiempo 368B. En Modo Edición no presenta la barra E5.5 y conserva su cromado independiente.
- Las acciones contextuales y otros indicadores no forman parte de esta versión, por decisión expresa del usuario. La acción preexistente de cierre del día no se borra: está accesible en el menú superior `Cierre del día`.

## Archivos

- `Assets/Scripts/Presentation/UI/BistroBuilderBottomE55Hud.cs`
- `Assets/Scripts/Presentation/UI/BistroBuilderUiShell.cs`: instalación idempotente sobre la barra existente.

## Validación ejecutada (05/11/2026)

- **Compilación Unity 6000.3.19f1: PASS / 0 errores C#.** Se observa `Tundra build success` en `BottomE55_Unity_batch6.log`; permanecen únicamente advertencias ajenas a E5.5.
- **Play Mode automatizado: PASS** con `BistroBuilderBottomE55BatchPlayTest.RunBatch`. Evidencia: `Logs/BottomE55_PlayMode_Result.txt` con `PASS E55 Play Mode smoke passed`.
- Comprobados: cuatro módulos y jerarquía, botones 1×/2×/3×, pausa/reanudación real de `GameClock`, restauración de estado tras las pruebas, instalación idempotente, ocultación del HUD inferior antiguo y anulación de interacción del dock 368B.
- Los errores conocidos de `UnityEditor.Search.SearchDatabase` durante arranque batch se filtran exclusivamente en ese caso de Editor, conforme al patrón de otras pruebas del proyecto. No se enmascaran excepciones de gameplay.
- Las primeras ejecuciones detectaron un defecto real en la gestión del `CanvasGroup` del dock 368B; corregido con comprobación explícita de componentes Unity y búsqueda de docks en los Canvas activos. El último Play Mode ya termina en PASS.
- Para volver a comprobarlo: método Editor `BistroBuilderBottomE55BatchPlayTest.RunBatch` en batch o menú **Bistro Builder > Pruebas > Validar barra inferior E5.5** durante Play Mode.

## QA visual y de datos reales del HUD (05/11/2026)

- **PASS — Compilación y Play Mode Unity 6000.3.19f1**: ejecución de `BistroBuilderBottomE55VisualAndDataQa.RunBatch`, registrada en `Logs/E55VisualDataQa.txt` y `BottomE55_VisualDataUnity_v5.log`. Ejecutar con `-batchmode` y dispositivo gráfico; **no** usar `-nographics` para las capturas.
- **PASS — Responsive**: capturas generadas por la cámara y el Canvas reales a 1920×1080 y 1280×720. Altura útil de cada módulo: 72 unidades lógicas, y 4 módulos sin solapamiento geométrico. Reparada la sobrescritura de altura por `EnsureShell()`; ajustados tamaño de texto y alineación de velocidad.
- **PASS — Datos canónicos**: Caja coincide con `TryGetCurrentServiceResult` (en escena de prueba, 0,00 €). Satisfacción refleja estados *controlados a través del servicio de experiencias*, con 90 % en un escenario favorable y 21 % en otro desfavorable. Clima muestra `Lluvia / 8 °C` y `Nieve / -4 °C` usando `SetDebugOverride`. Los snapshots y overrides se restauran al salir de la prueba. Esto verifica las vinculaciones; no equivale a simular un servicio orgánico completo.
- **PASS — Modo edición**: al entrar en modo edición, la barra E5.5 se oculta; vuelve a mostrarse al salir. No se alteran las acciones propias de su barra.
- **PASS — Velocidad y pausa**: sigue vigente `BistroBuilderBottomE55BatchPlayTest`, previamente ejecutada con resultado PASS.
- **LIMITACIÓN EXTERNA IDENTIFICADA — Personal**: al abrir el menú actual de Personal, su pantalla completa tapa tanto la barra superior como la inferior. E5.5 permanece activa, pero oculta detrás del panel. La corrección requiere redimensionar esa pantalla de gestión y otros menús; **no se ha modificado Personal** en esta rama por tratarse de otra interfaz.
- **Caveat del Editor**: persiste una excepción de `UnityEditor.Search.SearchDatabase` en arranque batch, ajena al HUD; el comprobador ignora solo ese caso identificado.

### Evidencias gráficas (capturas Unity, no renders conceptuales)

- `docs/Images/UI/BottomE55/E55_Unity_1920x1080_barra.png`
- `docs/Images/UI/BottomE55/E55_Unity_1280x720_barra.png`
- `docs/Images/UI/BottomE55/E55_Unity_1920x1080_completo.png`

## Pendiente para aprobación definitiva

- Contrastar las capturas del juego con la referencia artística E5.5 seleccionada, incluyendo los efectos sutiles, en revisión visual del usuario.
- Adaptar los paneles de gestión a las zonas ocupadas por barra superior e inferior, especialmente Personal y menú Servicio, en su ámbito de implementación.
- Ensayar un servicio completo generado orgánicamente (ingresos y satisfacción durante pedidos y pagos reales, sin snapshots de prueba).

**Estado:** implementación funcional validada y mejoras visuales comprobadas; diseño E5.5 aún **provisional** hasta aprobación final.
