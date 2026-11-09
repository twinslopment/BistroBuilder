# Carta — definición funcional por secciones (Modo Juego)

**Fecha de inicio:** 09/11/2026.
**Estado:** **PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN** para «Cartas y reglas de activación». Otras secciones de Carta aún sin definir en este documento.
**Regla de trabajo:** revisar código existente; aprobar funcionalidad sección por sección; **no diseñar, programar ni cambiar el estilo visual hasta cerrar el alcance funcional**. Al diseñar, aplicar a TODOS los paneles el sistema visual global obligatorio descrito en `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Separación entre sistemas

- **Carta (jugador):** administra las cartas comerciales del restaurante, el catálogo de platos disponibles para ofrecer y la configuración jugable de cada carta.
- **Catálogo Maestro de Platos (herramienta de autoría de Unity):** define platos, recetas base, fotos, ingredientes y escandallos de referencia. No sustituirlo por un segundo catálogo ni duplicarlo dentro de la UI del jugador.
- **Editor de carta:** modifica los platos ofertados, precios y condiciones de cada carta; es distinto de crear reglas automáticas que eligen qué carta está activa.
- **Carta V3:** utilizar la pantalla/código ya desarrollado como punto de partida funcional; el rediseño visual posterior debe respetar la identidad gráfica común.

## Código existente comprobado

- `BistroBuilderMenuPortfolioService`: portfolios de varias cartas por restaurante, carta efectiva, carta de reserva, forzado manual, crear desde la carta activa, duplicar, renombrar, eliminar con restricciones, reglas de activación.
- `BistroBuilderMenuActivationContextService`: calendario, reloj, servicio concreto y señales de evento/promoción.
- `BistroBuilderMenuActivationRuleRuntimeState`: cinco tipos de reglas, `Schedule`, `Season`, `Event`, `Promotion` y `Composite`, con prioridad, fechas, días de semana, servicio, horas, evento y promoción.
- `BistroBuilderMenuPortfolioRuntimeView`: vista existente para gestión de cartas y reglas; incluye operaciones básicas. Es código anterior y no supone aprobación de su diseño.
- `BistroBuilderMenuEditorService` y `BistroBuilderMenuEditSessionService`: edición de artículos y cambios aplicados/descartados; mantener separación del portfolio.
- Selección actual: si existe forzado manual, prevalece; en caso contrario se aplica la regla coincidente de mayor prioridad/especificidad; sin coincidencia se usa carta base.
- No se ha verificado la integración de esta experiencia con la barra V21, Carta V3 ni una build final de Unity.

## Propuesta a aprobar: Carta → Cartas y reglas de activación

**Objetivo:** organizar varias cartas comerciales y definir **cuál se ofrece automáticamente** según el día o las circunstancias del restaurante, sin editar aquí recetas/escandallos.

1. **Consultar todas las cartas** del restaurante: nombre, situación (base, activa, programada, forzada) y número de platos, cuando ese dato exista en la fuente de verdad.
2. **Saber cuál está activa y por qué:** mostrar «carta base», regla que se cumple o activación manual.
3. **Crear una carta** partiendo de la activa, **duplicar** otra y **cambiar nombres** sin crear platos nuevos ni duplicar el Catálogo Maestro.
4. **Eliminar cartas** con confirmación y restricciones: conservar al menos una, no eliminar la activa/base/forzada ni una referenciada por reglas sin resolver primero esas dependencias.
5. **Elegir carta base:** se utiliza cuando no coincide ninguna regla de activación.
6. **Forzar temporalmente una carta:** decisión manual que prevalece sobre las reglas; debe ofrecerse una forma clara de **volver a la activación automática**.
7. **Definir reglas automáticas:** por comida/cena, día de la semana, fechas/temporada, horario, evento y promoción; combinar condiciones cuando corresponda.
8. **Dar prioridad a las reglas** y explicar conflictos: una sola carta operativa; si dos reglas coinciden, gana la de mayor prioridad y después la más específica. Evitar la ambigüedad de resultados.
9. **Comprobar la programación:** previsualizar qué carta se serviría para una fecha y servicio futuros; no cambiar la carta real al hacer la comprobación. Esta herramienta de simulación de escenario puede requerir una ampliación de la presentación.
10. **Evitar cambios bruscos durante el servicio:** propuesta de seguridad jugable: las modificaciones que afecten al menú efectivo durante un servicio activo deben bloquearse o posponerse para el siguiente servicio; **criterio funcional por aprobar**, no está verificado como salvaguarda del código actual.

**Límites de alcance propuestos:**
- En esta sección NO se editan ingredientes, recetas ni escandallos; tampoco los precios/platos individuales: eso se definirá después en «Platos» y «Edición de cartas».
- Los eventos y promociones deben proceder de sus sistemas de juego, no de un botón de prueba que permita al jugador inventarse una señal de evento.
- No crear otro gestor de cartas ni otra autoridad de disponibilidad.
- Respetar las ofertas/comandas y guardado de los servicios en curso.

## Próximo paso

Solicitar aprobación funcional de **«Cartas y reglas de activación»**. Después definir **Carta → Platos** y **Carta → Edición de cartas / recetas / escandallos** sin mezclarlos con la herramienta de autoría.
