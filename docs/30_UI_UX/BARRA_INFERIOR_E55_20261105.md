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

## Pruebas pendientes dentro de Unity

- Compilación de scripts en Unity 6000.3.19f1 y ejecución de Play Mode. Desde el menú del Editor: **Bistro Builder > Pruebas > Validar barra inferior E5.5** para comprobar jerarquía y controles 1×/2×/3×/pausa sin alterar el estado final del reloj.
- Confirmar ingresos del servicio, satisfacción de visitas activas, clima y cambios reales de velocidad/pausa.
- Comprobar que no reaparece el dock 368B ni queda ningún elemento de la antigua barra.
- Validación visual a 1920×1080 y 1280×720, tanto con panel Servicio abierto como con actividad y menú de gestión.
- Confirmar que el Modo Edición conserva intacta su barra propia.

**No afirmar PASS de compilación o captura visual hasta ejecutar estas pruebas en Unity.**
