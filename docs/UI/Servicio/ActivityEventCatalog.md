# Bistro Builder — Catálogo canónico de eventos de ACTIVIDAD

Estado: DISEÑO CERRADO
Versión: 1.0
Fecha: 2026-09-23
Documento padre: `PanelActividad.md`

## Propósito

Este catálogo define qué frases puede generar el panel ACTIVIDAD, qué datos variables utiliza, qué icono corresponde, cómo se prioriza y qué ocurre al pulsar cada entrada.

Regla principal: ningún sistema de gameplay debe escribir frases completas en la UI. Los sistemas emiten eventos estructurados y ACTIVIDAD compone el texto mediante plantillas localizables.

## Modelo de datos

Cada definición contiene:
- `EventId`: identificador estable y único.
- `Category`: Incident, Opportunity, Event o Reservation.
- `Severity`: Info, Positive, Attention o Critical.
- `IconKey`: familia visual canónica.
- `TitleKey` y `BodyKey`: claves de localización.
- `TargetType`: entidad que se selecciona al pulsar.
- `Lifetime`: Transient, StickyUntilResolved o ServiceSession.
- `Aggregation`: regla para evitar spam.
- `FeatureGate`: sistema requerido, si procede.

## Pipeline de runtime

1. El sistema de gameplay detecta un cambio real.
2. Emite un evento semántico con IDs y datos, nunca texto final.
3. `ActivityFeedService` resuelve la definición en `ActivityEventCatalog`.
4. Se valida deduplicación, prioridad, agrupación y vigencia.
5. `ActivityTemplateFormatter` resuelve título y cuerpo desde Unity Localization.
6. `ActivityPanelController` inserta o actualiza la fila.
7. Al pulsar, `ActivityTargetRouter` centra/selecciona el objetivo y alimenta Contexto.

La UI no puede modificar el estado de gameplay directamente por mostrar un evento.

## Presentación

- Máximo visual simultáneo: 8 filas en la referencia aprobada.
- El resto permanece disponible mediante scroll.
- Orden: Critical > Attention > Opportunity relevante > cronología.
- Un Critical no resuelto permanece fijado.
- El filtro de cabecera ofrece: Hoy, Incidencias, Oportunidades y Reservas.
- La hora procede del reloj de juego, no del reloj real.
- Título de fila: Inter SemiBold.
- Texto secundario y hora: Inter Regular.
- Cabecera ACTIVIDAD: Recoleta.
- Cada entrada puede mostrar un marcador semántico adicional a la derecha.

## Reglas anti-spam y determinismo

- Un mismo `EventId + TargetId` no crea duplicados dentro de su ventana de deduplicación.
- Los eventos de estado solo se generan cuando cambia el estado.
- Stock bajo/crítico/agotado se vuelve a emitir únicamente tras abandonar y volver a entrar en ese estado.
- Reservas generan un único evento por transición.
- Eventos positivos repetitivos tienen rate-limit.
- Incidencias resueltas dejan de estar fijadas; su registro histórico puede permanecer.
- Eventos agregables conservan los TargetIds afectados para poder navegar entre ellos.
- Guardar/cargar no vuelve a ejecutar efectos ni recompensas.
- Tras cargar, las referencias inválidas se descartan de forma segura.

## Catálogo — Mesas y clientes

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `table.group_arrived` | **Nuevo grupo** · {guests} personas | Info | `activity.people.arrival` | Group |
| `table.group_seated` | **Mesa {table}** · Grupo sentado | Info | `activity.table.seated` | Table |
| `table.attention_needed` | **Mesa {table}** · Necesita atención | Attention | `activity.table.attention` | Table |
| `table.waiting_excessive` | **Mesa {table}** · Espera demasiado sus platos | Critical | `activity.wait.alert` | Table |
| `table.bill_requested` | **Mesa {table}** · Ha pedido la cuenta | Info | `activity.table.bill` | Table |
| `table.finished` | **Mesa {table}** · Servicio finalizado | Info | `activity.table.complete` | Table |
| `table.customer_unhappy` | **Cliente descontento** · Mesa {table} | Attention | `activity.reputation.negative` | Table |
| `table.customer_recovered` | **Incidencia resuelta** · Mesa {table} recuperada | Positive | `activity.reputation.recovered` | Table |

## Catálogo — Comandas, platos y cocina

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `order.created` | **Nuevo pedido** · Mesa {table} · {lines} platos | Info | `activity.order.new` | Order |
| `order.ready` | **Pedido listo** · Mesa {table} | Info | `activity.order.ready` | Order |
| `order.priority_set` | **Comanda prioritaria** · Mesa {table} | Attention | `activity.order.priority` | Order |
| `order.error` | **Error de pedido** · Mesa {table} | Attention | `activity.order.error` | Order |
| `dish.cold_or_poor` | **Problema de plato** · Mesa {table} | Attention | `activity.dish.problem` | Order |
| `dish.blocked_stock` | **Plato no disponible** · {dish} | Critical | `activity.stock.blocked` | Dish |
| `dish.trending` | **Plato destacado** · {dish} · {count} pedidos | Positive | `activity.dish.trending` | Dish |
| `kitchen.state_loaded` | **Cocina cargada** · Aumenta la cola | Attention | `activity.kitchen.loaded` | Kitchen |
| `kitchen.state_saturated` | **Cocina saturada** · {pending} comandas pendientes | Critical | `activity.kitchen.saturated` | Kitchen |
| `kitchen.state_blocked` | **Cocina bloqueada** · Requiere actuación | Critical | `activity.kitchen.blocked` | Kitchen |
| `kitchen.state_recovered` | **Cocina fluida** · Ritmo recuperado | Positive | `activity.kitchen.recovered` | Kitchen |
| `kitchen.equipment_issue` | **Incidencia de cocina** · {equipment} | Critical | `activity.kitchen.equipment` | Kitchen |
| `kitchen.priority_limit` | **Prioridades completas** · Máximo alcanzado | Attention | `activity.order.priority` | Kitchen |

## Catálogo — Entrada, sala, barra y espera

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `foh.state_waiting` | **Entrada con espera** · {groups} grupos | Attention | `activity.wait.queue` | Entrance |
| `foh.state_saturated` | **Sala saturada** · Requiere ajuste | Critical | `activity.zone.saturated` | Zone |
| `foh.state_slowed` | **Ritmo reducido** · Entrada controlada | Attention | `activity.flow.slowed` | Entrance |
| `foh.state_recovered` | **Entrada fluida** · Ritmo recuperado | Positive | `activity.flow.recovered` | Entrance |
| `waitlist.group_added` | **Nuevo grupo en espera** · {guests} personas | Info | `activity.wait.group` | WaitTicket |
| `waitlist.long_wait` | **Espera elevada** · {minutes} min | Critical | `activity.wait.alert` | WaitTicket |
| `waitlist.table_available` | **Mesa disponible** · Grupo en espera puede sentarse | Opportunity | `activity.table.available` | WaitTicket |
| `waitlist.sent_to_bar` | **Espera en barra** · {guests} personas | Info | `activity.bar.wait` | WaitTicket |
| `waitlist.group_left` | **Grupo perdido** · Abandona la espera | Attention | `activity.wait.left` | WaitTicket |
| `bar.saturated` | **Barra saturada** · No absorbe más espera | Critical | `activity.bar.saturated` | Bar |
| `zone.staff_shortage` | **Falta personal** · {zone} | Critical | `activity.staff.shortage` | Zone |
| `table.needs_reset` | **Mesa pendiente** · Limpieza/preparación | Attention | `activity.table.reset` | Table |
| `table.ready` | **Mesa preparada** · Disponible de nuevo | Positive | `activity.table.available` | Table |

## Catálogo — Reservas

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `reservation.arriving_soon` | **Reserva próxima** · {guests} pax · {minutes} min | Reservation | `activity.reservation.soon` | Reservation |
| `reservation.arrived` | **Reserva llegada** · {guests} pax | Reservation | `activity.reservation.arrived` | Reservation |
| `reservation.seated` | **Reserva sentada** · Mesa {table} | Reservation | `activity.reservation.seated` | Table |
| `reservation.large_group` | **Grupo grande próximo** · {guests} pax | Attention | `activity.reservation.group` | Reservation |
| `reservation.special_guest` | **Cliente especial** · Reserva próxima | Opportunity | `activity.guest.special` | Reservation |
| `reservation.peak_window` | **Pico de reservas** · {count} entradas próximas | Critical | `activity.reservation.peak` | ReservationGroup |

Regla vigente: no se generan eventos de no-show ni de retraso de clientes reservados.

## Catálogo — Inventario y proveedores

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `inventory.low` | **Inventario bajo** · {ingredient} | Attention | `activity.stock.low` | Ingredient |
| `inventory.critical` | **Stock crítico** · {ingredient} | Critical | `activity.stock.critical` | Ingredient |
| `inventory.out` | **Stock agotado** · {ingredient} | Critical | `activity.stock.out` | Ingredient |
| `inventory.updated` | **Inventario actualizado** · {source} | Info | `activity.stock.updated` | Inventory |
| `supplier.delivery_received` | **Entrega recibida** · {supplier} | Positive | `activity.supplier.delivery` | Supplier |
| `supplier.issue` | **Problema de suministro** · {supplier} | Attention | `activity.supplier.issue` | Supplier |

## Catálogo — Personal y operación

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `staff.overloaded` | **Empleado saturado** · {employee} | Attention | `activity.staff.overloaded` | Employee |
| `staff.zone_uncovered` | **Zona sin cobertura** · {zone} | Critical | `activity.staff.shortage` | Zone |
| `staff.support_needed` | **Apoyo requerido** · {zone} | Attention | `activity.staff.support` | Zone |
| `staff.upsell_opportunity` | **Venta sugerida** · Mesa {table} | Opportunity | `activity.opportunity.upsell` | Table |

## Catálogo — Reputación, marketing y oportunidades

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `reputation.good_review` | **¡Buena reseña!** · “{excerpt}” | Positive | `activity.reputation.positive` | Reputation |
| `reputation.bad_review` | **Reseña negativa** · “{excerpt}” | Attention | `activity.reputation.negative` | Reputation |
| `reputation.word_of_mouth` | **Boca a boca** · Demanda orgánica al alza | Positive | `activity.reputation.word_of_mouth` | Reputation |
| `marketing.campaign_started` | **Campaña activa** · {campaign} | Info | `activity.marketing.campaign` | Marketing |
| `marketing.demand_spike` | **Demanda al alza** · +{percent}% prevista | Opportunity | `activity.trend.up` | Marketing |
| `marketing.capacity_risk` | **Demanda excesiva** · Capacidad en riesgo | Critical | `activity.marketing.risk` | Marketing |
| `opportunity.regular_guest` | **Cliente habitual** · Mesa {table} | Opportunity | `activity.guest.regular` | Table |
| `opportunity.special_guest` | **Cliente importante** · Mesa {table} | Opportunity | `activity.guest.special` | Table |
| `opportunity.drink` | **Oportunidad de bebida** · Mesa {table} | Opportunity | `activity.opportunity.drink` | Table |
| `opportunity.dessert` | **Oportunidad de postre** · Mesa {table} | Opportunity | `activity.opportunity.dessert` | Table |
| `opportunity.walkin_group` | **Mesa aprovechable** · Grupo espontáneo de {guests} | Opportunity | `activity.opportunity.group` | Group |
| `opportunity.bar_wait_sale` | **Espera rentable** · Grupo puede pasar a barra | Opportunity | `activity.opportunity.bar` | WaitTicket |
| `opportunity.high_demand` | **Día de gran afluencia** · {guests} comensales (+{percent}%) | Opportunity | `activity.trend.up` | Restaurant |

## Eventos condicionados por sistemas futuros

Estos IDs quedan reservados pero no forman parte del lote de iconos V1 mientras el canal correspondiente no esté activo:
- `online.order_problem` — pedido online problemático.
- `online.paused` — canal online pausado.
- `online.capacity_risk` — online agravando saturación de cocina.

## Agrupaciones canónicas

- Varias `table.waiting_excessive` en 60 s → **Espera elevada · {count} mesas necesitan atención**.
- Varias `zone.staff_shortage` simultáneas → **Falta de personal · {count} zonas afectadas**.
- Varias reservas próximas en una misma ventana → `reservation.peak_window`.
- Varias oportunidades de upselling de la misma familia pueden agruparse por zona.
- Las incidencias Critical nunca se ocultan dentro de una agrupación sin conservar acceso a cada objetivo.

## Persistencia

ACTIVIDAD es una proyección de UI, no autoridad de gameplay.
Al guardar un servicio activo se conservan:
- eventos visibles recientes;
- eventos StickyUntilResolved;
- timestamp de juego;
- EventId;
- TargetRef;
- parámetros de plantilla.

Al cargar:
- no se repiten efectos;
- se validan referencias;
- se rehidratan únicamente entradas todavía relevantes.

## Contrato de implementación

Tipos previstos:
- `ActivityEventDefinition`
- `ActivityEventInstance`
- `ActivityEventCatalog`
- `ActivityFeedService`
- `ActivityFeedAggregator`
- `ActivityTemplateFormatter`
- `ActivityTargetRouter`
- `ActivityPanelController`

Los productores de gameplay solo conocen un contrato de emisión, por ejemplo:
`Publish(ActivityEventId.TableBillRequested, payload)`.

No deben depender de prefabs, TextMeshPro, sprites ni jerarquías UI.

## Criterios de aceptación

- Un evento idéntico no se duplica por polling o reentrada.
- Las transiciones de estado son deterministas.
- Critical permanece visible hasta resolución.
- El filtro no altera ni destruye eventos.
- Cargar partida no duplica mensajes.
- Un clic siempre resuelve a un Target válido o falla de forma segura.
- Las frases se localizan sin recompilar gameplay.
- Cambiar un icono no modifica la lógica del evento.
- El panel sigue siendo secundario respecto al restaurante.

Este documento es la fuente canónica para implementar las frases del panel ACTIVIDAD.
