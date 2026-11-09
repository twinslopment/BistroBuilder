# Reputación — definición funcional (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **REPUTACIÓN CERRADA FUNCIONALMENTE el 09/11/2026** con UNA sola pantalla aprobada: **Valoración y opiniones**. El rediseño, las mejoras de UX y su implementación/verificación en una build Unity siguen pendientes.
**Alcance:** definición funcional en una sola pantalla. Sin modificar código Unity ni diseño visual en esta fase. El futuro diseño seguirá `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`, respetando la barra superior V21 y barra inferior E5.5 de Modo Juego.

## Arquitectura contrastada

- `BistroBuilderReputationPlayerScreen` implementa hoy **UNA pantalla consultiva**, sin pestañas ni acciones que muten reputación: muestra valoraciones, satisfacción, cinco aspectos, experiencias, orígenes de clientes y reseñas recientes.
- `BistroBuilderReputationPlayerFacade.TryBuildSnapshot` consulta exclusivamente `BistroBuilderReputationService`, `BistroBuilderGuestRelationsService`, `BistroBuilderCustomerExperienceTrackingService` y `BistroBuilderGeneralGameStateService`, sin inventar métricas de rendimiento.
- `BistroBuilderReputationEngine` calcula las puntuaciones por evidencia de experiencias jugables, genera reseñas derivadas y puntúa satisfacción; el sistema no representa una plataforma externa real ni mensajes libres de jugadores.
- `BistroBuilderCustomerExperienceTrackingService` vincula los resultados de visitas del restaurante con la reputación, con seguimiento de visitas activas.
- `BistroBuilderReputationSnapshot` es la autoridad persistente `reputation.state`; el seguimiento de visitas en curso utiliza `reputation.runtime` y la relación de clientes sus propios servicios.
- El UI actual muestra **las ocho reseñas más recientes** y el motor conserva hasta **128 reseñas** y **256 experiencias recientes** en su estado acotado. **No afirmar que exista una navegación histórica completa ni un buscador**.
- La UI histórica no acredita integración visual o build vigente con la barra V21 y E5.5; solo se aprueban funciones hasta nueva orden.

## Reputación → Valoración y opiniones — FUNCIONALIDADES APROBADAS

**Objetivo:** que el jugador interprete la percepción real de los comensales, sus causas principales y el impacto reputacional en demanda/retorno, **sin poder manipular opiniones ni puntuaciones**.

1. **Mostrar una valoración global del restaurante** procedente del servicio canónico, en escala comprensible y diferenciada de las reseñas de 1 a 5 estrellas. Evitar convertir mecánicamente los porcentajes globales en estrellas sin una regla aprobada.
2. **Mostrar el nivel de satisfacción** (muy mala, mala, correcta, buena o excelente), su relación con la experiencia jugable y, cuando exista, la última satisfacción registrada. Señalar expresamente cuando todavía no exista evidencia suficiente; el valor neutro inicial no constituye automáticamente satisfacción contrastada de clientes.
3. **Desglosar cinco aspectos de calidad percibida**: **Servicio**, **Tiempos de espera**, **Calidad de la comida**, **Relación calidad/precio** y **Ambiente**. Distinguir claramente mejores y peores aspectos con sus valores reales, sin asignar causalidad artificial.
4. **Contabilizar experiencias**: total de visitas evaluadas, positivas, negativas y visitas aún en curso. No asumir que toda experiencia es positiva o negativa; existen resultados intermedios.
5. **Consultar reseñas recientes** en orden cronológico inverso, indicando día de juego, puntuación de 1–5 estrellas y una frase corta que represente el aspecto más destacado o criticado. No inventar párrafos libres ni nombres de clientes no presentes en el dato.
6. **Conocer origen de los clientes** desde categorías reales: descubrimiento orgánico, Marketing, boca a boca, habituales y Reservas, contando los eventos reconocidos por `BistroBuilderReputationService`.
7. **Consultar efecto reputacional en el negocio**: boca a boca, efecto persistente en demanda y potencial orgánico de repetición de visita. Presentarlos como **modificadores del motor**, no como nuevos clientes garantizados ni ventas monetarias atribuidas.
8. **Mostrar relación con clientes habituales** a partir de las cohortes de `BistroBuilderGuestRelationsService` y su potencial de retorno; no construir fichas personales/CRM, ni confundir cohorte con cliente individual identificable.
9. **Orientar mejoras mediante datos reales:** destacar los aspectos comparativamente peor valorados y explicar, de forma sencilla, que mejorar atención, esperas, cocina, precio o ambiente afecta a experiencias futuras. El motor no ofrece un diagnóstico causal avanzado: cualquier consejo será descriptivo, no una predicción cuantificada.
10. **Actualizar datos cuando cambien experiencias, reputación, relaciones de clientes o día de juego**, leyendo el snapshot desde los servicios actuales. Al guardar/cargar, no duplicar reseñas ni aplicar un mismo resultado dos veces.
11. **Conservar independencia respecto a Marketing:** las campañas pueden modificar reputación y demanda por sus mecanismos, pero el jugador no puede comprar opiniones ni ajustar directamente reputación desde esta pantalla. Dirigir a Marketing para contratar campañas y a los sistemas operativos del restaurante para corregir problemas.
12. **Una única pantalla de consulta con apartados internos**, sin pestañas independientes de reseñas/satisfacción/análisis en V1. El diseño futuro debe dejar respirar al restaurante 3D y mantener coherencia con otras pantallas del juego.

### Verificación de código y ampliaciones pendientes

- `BistroBuilderReputationPlayerScreen.Refresh` ya muestra valoración global, satisfacción, boca a boca/demanda/repetición, puntuación de cinco aspectos, experiencias, cohortes, orígenes y reseñas.
- `BistroBuilderReputationPlayerFacade.TryBuildSnapshot` extrae de fuentes canónicas estos campos y selecciona **hasta ocho reseñas recientes**; `BistroBuilderReputationPlayerScreen.ReviewLabel` presenta mensajes cortos basados en `summaryKey`.
- `BistroBuilderReputationEngine.TryApplyExperience` utiliza experiencias reconocidas y genera una reseña con puntuación y aspecto, mientras `BistroBuilderReputationService.TryApplyExternalReputationCredit` administra créditos reputacionales externos a la visita mediante su propia autoridad.
- `BistroBuilderReputationAspect`: `Service`, `WaitingTime`, `FoodQuality`, `ValueForMoney`, `Ambience`. `BistroBuilderRestaurantDiscoverySource`: `Organic`, `Marketing`, `WordOfMouth`, `ReturningGuest`, `Reservation`.
- **No existen actualmente controles de búsqueda/filtro de reseñas, gráfica histórica de valoración, explicación interactiva del origen de cada puntuación, selección de reseña o acciones de respuesta**. No se declaran capacidades implementadas.
- La presentación inicial usa porcentajes incluso sin experiencias; **la distinción «sin datos suficientes» es un requisito funcional nuevo de UX**, y las sugerencias descriptivas basadas en aspectos y cualquier navegación a otros paneles también necesitan adaptación.
- **No existen reseñas de texto libre escritas por personas reales**: las reseñas visibles son construidas por el sistema a partir de experiencias del juego, con estrellas y una clave corta de resumen.

### Límites aprobados

- No permitir editar puntuaciones ni crear/borrar reseñas, responder ficticiamente a ellas, pagar para eliminarlas o premiar arbitrariamente satisfacción.
- No mostrar valoraciones como reseñas verificadas de servicios de internet ni inventar perfiles, nombres, fotos, citas textuales, fechas reales o comparaciones con restaurantes ajenos.
- No construir otro motor de reputación, analítica de marketing o persistencia paralela.
- No duplicar el indicador **Satisfacción** de la barra inferior: este debe continuar mostrando el valor y alcance que tenga aprobado, y el panel explicará sus causas con la misma autoridad de datos, sin rediseñar la barra.
- La identidad visual global premium, efectos de hover y enlaces entre pantallas se decidirán y programarán posteriormente; **no hay cambios en la build Unity por esta aprobación funcional**.

## Próximo paso

**Reputación queda cerrada funcionalmente con una única pantalla.** Continuar por **Opciones → Partida**, primera de las nueve categorías existentes de configuración. Diseño visual e implementación Unity siguen por separado.
