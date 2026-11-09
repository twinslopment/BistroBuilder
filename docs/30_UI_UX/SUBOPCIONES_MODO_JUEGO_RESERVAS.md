# Reservas — definición funcional (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **RESERVAS CERRADO FUNCIONALMENTE el 09/11/2026** con una única pantalla aprobada: **Agenda y gestión**. Las ampliaciones UI, integración con Actividad/V21, rediseño visual y pruebas actuales de Unity siguen pendientes.
**Alcance del trabajo actual:** definir qué hace la interfaz jugable a partir de código real. NO modificar Unity ni diseño visual. En una fase posterior aplicar la identidad gráfica global obligatoria a todos los paneles, respetando las barras definitivas de Modo Juego V21 y E5.5; el Modo Edición no comparte esta navegación.

## Arquitectura comprobada

- `BistroBuilderReservationPlayerScreen` ofrece hoy **una sola pantalla** con agenda por día y ficha lateral para crear, editar y cancelar; no incorpora pestañas independientes.
- `BistroBuilderReservationPlayerFacade` orquesta agenda, creación, edición, reubicación y cancelación usando las autoridades canónicas de Reservas y Disponibilidad, con reversión cuando falla una fase.
- `BistroBuilderReservationService` mantiene la colección `reservations.state`, transiciones de estado y persistencia por snapshot.
- `BistroBuilderReservationAvailabilityService` comprueba mesas reales, capacidad y choques horarios, aplicando el margen entre reservas del perfil.
- `BistroBuilderReservationServiceIntegration` conecta las reservas activas con la llegada de grupos de clientes y el servicio 3D. Los estados de llegada/sentados/completados/no presentados se gestionan según hechos del juego, no mediante botones ficticios.
- `BistroBuilderReservationAvailabilityProfile` tiene **14 días como valor de horizonte inicial configurable** (validación 1–60) y margen de rotación inicial de **15 minutos configurable** (0–120). No establece horarios de apertura.
- `BistroBuilderReservationEngine` admite **1–12 comensales por reserva**, duración **30–240 min**, nombre y notas; el horario de llegada debe quedar dentro del día. No confundir estos límites de V1 con una preferencia estética.
- Los estados canónicos son: `Booked` (reservada), `Due` (en hora), `Arrived`, `Seated`, `Completed`, `Cancelled`, `NoShow`.
- La integración exacta de esta UI histórica en el nuevo sistema global V21/E5.5 y su funcionamiento en una build actual NO están probados en este paso.

## Estructura APROBADA: UNA pantalla «Reservas → Agenda y gestión»

**Objetivo:** que el jugador organice reservas de su restaurante de forma comprensible, compruebe su encaje en las mesas y gestione cambios, sin convertirlo en un sistema aparte de clientes o de distribución manual del salón.

1. **Agenda por día de juego**, comenzando en el día actual y permitiendo navegar por el horizonte configurado (14 días por defecto, no un valor fijo). Mostrar total de reservas y reservas activas del día elegido.
2. **Listado ordenado de reservas** con hora, nombre, número de comensales, duración prevista, mesa asignada y estado. Distinguir reservas pasadas, canceladas y no presentadas sin borrarlas del registro.
3. **Consultar ficha de una reserva** con los mismos datos y notas; mostrar el motivo de imposibilidad de edición cuando el estado ya lo prohíba.
4. **Crear reserva manual del jugador** indicando nombre, comensales, día, hora, duración y notas opcionales, con rangos validados por la capa canónica. Evitar campos libres no soportados por el sistema.
5. **Buscar automáticamente una mesa compatible** con capacidad suficiente, sin solapar reservas previas ni el margen de rotación establecido. Mostrar la mesa asignada y ofrecer localizarla visualmente en el restaurante cuando sea viable.
6. **Informar con precisión cuando no haya disponibilidad** para la franja o el tamaño del grupo. No crear reservas que vulneren la capacidad real ni sobreescribir asignaciones existentes; el jugador podrá ajustar fecha, hora o comensales y volver a intentar.
7. **Modificar reserva todavía editable** (nombre, tamaño, hora, duración o notas) y recalcular/reasignar mesa si fuera necesario. Bloquear cambios para mesas ya ocupadas y estados terminales conforme al motor de Reservas; no permitir que la UI ignore el servicio real.
8. **Cancelar con confirmación explícita**, conservando la reserva en el historial como cancelada. No cancelar reservas ya sentadas, completadas, no presentadas o previamente canceladas. La UI actual requiere segundo clic para confirmar.
9. **Estados automáticos ligados al gameplay**: Reservada → En hora → Llegada → Sentados → Completada, o No presentado si supera la tolerancia sin llegar. No generar clientes duplicados por abrir/cerrar la pantalla.
10. **Integración con sala y Actividad**: la reserva que se aproxima o un no presentado podrán aparecer como acontecimiento relevante en el panel de Actividad a partir de los estados existentes, sin duplicar persistencia ni inventar un monitor de reservas nuevo. Este enlace específico todavía no está verificado con el panel Actividad rediseñado.
11. **Persistencia de las reservas y sus mesas lógicas** en `reservations.state` al guardar/cargar, sin guardar objetos visuales ni crear una segunda base de datos.
12. **Búsqueda y filtros útiles** por nombre, estado y tramo horario, solo como **mejora de presentación futura**; la pantalla actual contiene agenda diaria y selección, pero no filtros/buscador avanzado. Priorizar sencillez y lectura sobre un CRM.

### Reglas y límites APROBADOS

- **No dividir sin motivo** Reservas en «Agenda», «Nueva reserva», «Cancelaciones», etc.: crear/editar/cancelar será parte de una misma pantalla, con formulario contextual.
- **No editar manualmente mesas individuales por defecto**: el planificador ya asigna una mesa compatible. La eventual selección manual/traslado de mesa se estudiará como mecánica aparte si el jugador la solicita.
- **No aceptar reservas para días fuera del horizonte** ni grupos que excedan la capacidad de las mesas disponibles.
- **No crear una plataforma externa de reservas, cobros de señal, listas de espera, depósitos, penalizaciones o CRM** en este primer alcance sin definir sistemas jugables y reglas concretas.
- **No confundir reservas con Personal → Horarios**: aquí se reserva disponibilidad de mesas para comensales; allí se programan empleados.
- Mantener intacta la decisión de rediseño visual premium coherente con todas las pantallas. La UI histórica solo se reutiliza como funcionalidad; su aspecto NO es referencia aprobada.

## Próximo paso

**Reservas queda cerrado funcionalmente con una pantalla única**, agenda y formulario contextual. Continuar por **Economía → Resumen**. La aprobación funcional no implica que todas las mejoras ya estén programadas ni que se haya aprobado el diseño visual.
