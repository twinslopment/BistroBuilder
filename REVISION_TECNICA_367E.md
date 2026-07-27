# BistroBuilder 367E — Revisión técnica

## Objetivo

Sustituir el temporizador único de `CustomerDiningFlow` por una autoridad runtime que controle el consumo de cada `CustomerId`, cada pase y cada `OrderLineId`.

## Autoridad y responsabilidades

`BistroBuilderCustomerDiningService` es la única autoridad de consumo. Mantiene una sesión por comanda canónica activa y un runtime persistible por cliente.

El estado de `CustomerGroup` y `RestaurantTable` se conserva como fachada operativa compatible. Ya no decide cuándo ha terminado de comer cada cliente.

## Flujo individual

Cada cliente sigue esta secuencia:

`WaitingForDish → Eating → Completed`

También existen estados terminales `Cancelled` y `Failed`.

Un cliente comienza un pase cuando todas las líneas que consume en ese `CourseIndex` están `Served`, `Consumed` o `Cancelled`. Otros miembros del mismo grupo pueden continuar en `WaitingForDish`.

Al terminar el tiempo individual, el cliente registra reclamaciones de consumo sobre sus líneas. Una línea pasa de `Served` a `Consumed` únicamente cuando todos sus consumidores la han reclamado.

## Atomicidad

`BistroBuilderCanonicalOrderService.TryConsumeServedLines` opera sobre una copia profunda del agregado. Valida todos los `LineId` antes de sustituir la comanda original.

Un `LineId` inválido, duplicado o no servido rechaza el lote completo. No quedan líneas parcialmente consumidas.

## Protección contra reentrada

Las mutaciones canónicas publican eventos síncronos. 367E no ejecuta reconciliaciones dentro de esos eventos: únicamente encola el `OrderId`.

El drenaje se realiza fuera de la pila de mutación mediante una guardia explícita y un límite de seguridad. Esta decisión evita repetir el defecto de corrutinas reentrantes corregido en 367D1.

## Recuperación transaccional

Si una interrupción ocurre después de persistir todas las reclamaciones de una línea compartida pero antes de aplicar `Served → Consumed`, la reconciliación detecta la línea completamente reclamada y finaliza la transición de forma idempotente.

## Cuenta y pago

`BillServiceFlow` consulta la guardia de consumo:

- antes de entregar la cuenta;
- después de la entrega;
- antes de completar el pago.

La cuenta solo queda autorizada cuando:

- todos los clientes están `Completed` o `Cancelled`;
- todas las líneas están `Consumed` o `Cancelled`;
- el agregado canónico está `Completed`;
- la fachada legacy está `Served`.

## Persistencia futura

`BistroBuilderCustomerDiningRuntimeSnapshot`, esquema 1, conserva:

- `OrderId` y `LegacyOrderId`;
- referencias estables de grupo y mesa;
- `CustomerId`;
- estado individual;
- pase actual;
- tiempo restante exacto;
- reclamaciones de líneas consumidas;
- revisiones y estado de cuenta.

El contrato está preparado para integrarse en `service.runtime`. Este paquete no registra todavía una sección de guardado de servicio abierto.

## Rendimiento

- Sin búsquedas de escena por frame.
- Índices por `OrderId` con comparador ordinal.
- Buffers reutilizables para finalizaciones y reconciliaciones.
- Sin LINQ en el runtime operativo.
- Sin creación de corrutinas por cliente.
- El avance temporal es lineal respecto a los clientes de comandas activas y no genera basura administrada por frame.

## Compatibilidad

Se conservan los GUID de:

- `BistroBuilderCanonicalOrderService.cs`
- `CustomerDiningFlow.cs`
- `FoodDeliveryServiceFlow.cs`
- `BillServiceFlow.cs`

`CustomerDiningFlow` permanece como adaptador pasivo para no romper prefabs ni referencias serializadas.

## Alcance diferido

367E no incorpora todavía:

- representación visual individual de cada comensal;
- reglas jugables completas de platos compartidos;
- secuenciación operativa de varios pases;
- rasgos y velocidades personales de consumo;
- persistencia completa del restaurante abierto.

Esas capacidades se añadirán sobre los contratos de `CustomerId`, consumidores, `CourseIndex` y snapshot creados aquí.
