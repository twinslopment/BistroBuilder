# Bistro Builder — Familias de iconos de ACTIVIDAD

Estado: DISEÑO CERRADO
Versión: 1.0
Fecha: 2026-09-23
Relacionado: `ActivityEventCatalog.md`

## Objetivo

Definir el lote visual mínimo necesario para cubrir el catálogo completo sin crear un icono distinto para cada frase.

Regla: el icono identifica el concepto; el color/insignia identifica estado o gravedad.

## Lenguaje visual

- Misma familia estética que los iconos aprobados de la barra superior.
- Volumen 3D contenido, no caricaturesco.
- Base crema, madera, grafito, dorado suave y colores semánticos puntuales.
- Sin fondos cuadrados propios; deben funcionar sobre las tarjetas crema del panel.
- Lectura clara a tamaño pequeño.
- Siluetas distintas entre familias.
- Evitar texto dentro del icono salvo elementos naturales del objeto.
- Mantener coherencia de iluminación y perspectiva.

## Semántica de color

- Info: dorado/crema neutro.
- Positive: verde controlado.
- Opportunity: dorado vivo.
- Attention: ámbar.
- Critical: coral/rojo.
- Reservation: coral suave o dorado según contexto.

El color no sustituye al símbolo: todas las variantes deben seguir siendo reconocibles sin depender únicamente del color.

## Lote V1 — 28 familias

1. `activity.people.arrival` — grupo llegando.
2. `activity.table.seated` — mesa/grupo sentado.
3. `activity.table.attention` — mesa necesita atención.
4. `activity.wait.alert` — espera excesiva.
5. `activity.table.bill` — cuenta solicitada.
6. `activity.table.complete` — mesa finalizada.
7. `activity.order.new` — nueva comanda.
8. `activity.order.ready` — pedido listo.
9. `activity.order.priority` — comanda prioritaria.
10. `activity.order.error` — error de pedido.
11. `activity.dish.problem` — plato con problema.
12. `activity.dish.trending` — plato destacado.
13. `activity.kitchen.state` — estado cocina.
14. `activity.kitchen.equipment` — avería/bloqueo cocina.

15. `activity.flow.state` — entrada/sala: fluida, espera, saturada, ritmo reducido.
16. `activity.wait.group` — grupo en lista de espera.
17. `activity.table.available` — mesa disponible/preparada.
18. `activity.bar.state` — espera en barra / barra saturada.
19. `activity.staff.state` — falta, saturación o apoyo de personal.
20. `activity.reservation` — reserva próxima/llegada/sentada.
21. `activity.reservation.group` — grupo grande / pico de reservas.
22. `activity.guest.special` — habitual/importante/VIP.
23. `activity.stock.state` — bajo/crítico/agotado/bloqueo.
24. `activity.supplier` — entrega/problema de suministro.
25. `activity.reputation` — reseña positiva/negativa/recuperación.
26. `activity.marketing` — campaña/demanda/riesgo.
27. `activity.opportunity` — bebida/postre/upsell/barra.
28. `activity.trend.up` — gran afluencia/tendencia positiva.

## Variantes por familia

Las variantes se resuelven con insignias pequeñas y color semántico, no rehaciendo el icono desde cero.

Ejemplos:
- `kitchen.state`: gorro/cocina base + punto o aura de estado.
- `stock.state`: caja base + flecha abajo / ! / X.
- `reservation`: calendario o cartel base + reloj / llegada / check.
- `reputation`: estrella/bocadillo base + sonrisa / alerta.
- `staff.state`: persona base + ! / apoyo / saturación.
- `bar.state`: barra/copa base + espera / alerta.

## Iconos que NO necesitan familia propia

No crear iconos separados para:
- cada número de mesa;
- cada plato;
- cada ingrediente;
- cada proveedor;
- cada empleado;
- cada nivel de cocina;
- cada minuto de espera;
- cada tamaño de grupo;
- cada campaña;
- cada texto de reseña.

Esos datos son variables de la entrada, no conceptos visuales nuevos.

## Prioridad de producción gráfica

Primera tanda:
- table.bill
- order.new
- reservation
- reputation
- stock.state
- dish.trending
- wait.alert
- kitchen.state
- staff.state
- trend.up

Segunda tanda:
- people.arrival
- table.seated
- table.attention
- table.complete
- order.ready
- order.priority
- order.error
- dish.problem
- kitchen.equipment

Tercera tanda:
- flow.state
- wait.group
- table.available
- bar.state
- reservation.group
- guest.special
- supplier
- marketing
- opportunity

## Exportación recomendada

Para cada familia aprobada:
- PNG con transparencia para referencia y fallback.
- SVG cuando la forma lo permita sin perder el acabado.
- 256×256 master.
- 128×128 runtime de alta densidad.
- 64×64 runtime estándar.
- nombre estable según `IconKey`.

Convención:
`BB_Activity_<Family>_<Variant>`

Ejemplo:
`BB_Activity_Stock_Critical.png`

## Regla canónica

No diseñar decenas de iconos redundantes. Toda frase nueva debe intentar reutilizar primero una familia existente. Solo se añade una nueva familia cuando el concepto no pueda leerse correctamente con las 28 actuales.

## Registro de aprobaciones

Las selecciones visuales aprobadas se registran en `ActivityApprovedIcons.md`.
Ese documento es la autoridad para saber qué variante exacta de cada familia está cerrada.

Estado a 2026-09-23: **25 de 28 familias aprobadas**.
