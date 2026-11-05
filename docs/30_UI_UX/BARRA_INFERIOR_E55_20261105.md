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

## Pruebas aún pendientes

- **Verificación visual real** de fidelidad a la imagen E5.5, sin solapamientos a 1920×1080 y 1280×720, incluyendo apertura de menús.
- **Prueba con datos variables de servicio**: ingresos de caja, satisfacción de visitas activas y cambios meteorológicos; su origen canónico está conectado, pero no hay prueba numérica integrada registrada.
- **Prueba interactiva del modo edición**: comprobar que su barra independiente permanece intacta.
- La implementación sigue **provisional** hasta que se validen estos puntos y se apruebe su aspecto final.
