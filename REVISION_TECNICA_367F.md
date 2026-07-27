# BistroBuilder 367F — Revisión técnica

## Objetivo

Cerrar la ejecución funcional de platos compartidos y varios pases sobre las bases validadas de 367D1 y 367E.

## Autoridades

- `BistroBuilderCanonicalOrderService`: única autoridad transaccional de estados de línea.
- `BistroBuilderOrderCompositionService`: compositor puro de peticiones a partir de un perfil de datos.
- `BistroBuilderCourseAndSharingService`: autoridad operativa de liberación de pases y proyección persistible.
- `BistroBuilderCustomerDiningService`: autoridad de consumo por cliente y reclamaciones parciales de líneas compartidas.
- `KitchenSystem`: consumidor de líneas `Queued`, incluidas liberaciones posteriores.

## Flujo canónico

1. La comanda se crea con todas sus líneas en `Draft`.
2. Al enviarla a cocina, 367F somete todas las líneas a `Submitted` y libera únicamente el pase inicial a `Queued`.
3. La cocina procesa solo líneas liberadas.
4. Un plato compartido pasa a `Served` una sola vez y cada consumidor registra su propia reclamación.
5. La línea permanece `Served` mientras falte algún consumidor.
6. Tras completar todos los consumidores, la línea pasa atómicamente a `Consumed`.
7. Según la política, 367F libera el siguiente pase de `Submitted` a `Queued`.
8. La cuenta permanece bloqueada hasta que todos los clientes y todas las líneas estén resueltos.

## Políticas publicadas

- `PerTable`: libera un pase cuando todas las líneas de pases inferiores están `Consumed` o `Cancelled`.
- `PerCustomer`: libera las líneas del siguiente pase cuyos consumidores hayan resuelto sus pases inferiores.
- `Hybrid`: líneas individuales por consumidor; líneas compartidas coordinadas por mesa.
- `Manual`: no libera automáticamente; queda preparado para una acción operativa posterior.

La instalación piloto utiliza `PerTable`.

## Protección frente a reentrada

Los eventos canónicos y de consumo son síncronos, pero los manejadores de 367F solo encolan `OrderId`. La evaluación se drena bajo una guardia explícita después de terminar la mutación que originó el evento. La cocina aplica el mismo patrón para descubrir líneas liberadas posteriormente. Esto evita repetir la reentrada de corrutinas encontrada en 367D.

## Operaciones atómicas nuevas

- `TrySubmitOrderAndReleaseCourse(...)`
- `TryReleaseSubmittedLines(...)`

Ambas trabajan sobre una copia profunda, validan el agregado completo y solo sustituyen la comanda cuando toda la operación es válida.

## Compatibilidad legacy

La fachada `RestaurantOrder` continúa existiendo para el flujo actual, pero ya no exige que el número de líneas sea igual al número de clientes. La validez del enlace se comprueba por cobertura exacta de `CustomerId`, permitiendo relaciones muchos-a-muchos entre clientes y líneas.

## Persistencia futura

El snapshot de esquema 1 conserva:

- `OrderId` y `LegacyOrderId`;
- política de coordinación;
- pase inicial;
- pases liberados;
- líneas liberadas;
- revisión.

Las reclamaciones parciales de consumidores permanecen en el snapshot 367E/367F de consumo individual. La integración definitiva con guardado de servicio abierto se realizará en `service.runtime`.

## Alcance diferido

- selección jugable detallada de quién comparte cada plato;
- UI final de pase para cocina y sala;
- disparo manual desde camarero/jefe de sala;
- preparación anticipada y conservación térmica;
- bandejas y capacidad de transporte: 367G;
- modalidades de barra: 367H;
- recetas e inventario: 368.
