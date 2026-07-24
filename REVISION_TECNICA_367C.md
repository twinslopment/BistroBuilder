# BistroBuilder 367C — Revisión técnica

## Decisión arquitectónica

El ciclo existente no se sustituye de golpe. `RestaurantOrder` pasa a ser una
fachada operativa temporal y cada instancia queda enlazada a un agregado
`BistroBuilderCanonicalOrder`.

La integración es estricta: una transición legacy no se confirma hasta que la
autoridad canónica la ha aplicado correctamente. Esto evita una sincronización
tardía basada únicamente en eventos, que podría dejar dos estados diferentes si
un listener falla.

## Atomicidad

`TryAdvanceAllLinesToState` clona el agregado, recorre la ruta normal de cada
línea, valida la copia completa y solo entonces sustituye la comanda original y
sus índices. Una línea inválida no produce una actualización parcial.

## Autoridades

- `BistroBuilderCanonicalOrderService`: identidad, líneas, platos, precios y
  estado canónico.
- `RestaurantOrder`: fachada coarse requerida temporalmente por cocina,
  camareros, cuenta y mesa.
- `BistroBuilderCanonicalOrderIntegrationService`: traducción y puerta
  transaccional entre ambos modelos.

La fachada no puede avanzar de forma independiente cuando está enlazada.

## Preparación para service.runtime

Cada comanda conserva:

- Canonical OrderId.
- OrderLineId por plato físico.
- CustomerId lógico por miembro del grupo.
- DishId.
- precio congelado.
- mesa, grupo y referencia legacy.
- pase y servicio.

La futura carga con el restaurante abierto podrá reconstruir primero las
comandas canónicas y después recrear las fachadas operativas mediante el mismo
contrato de registro.

## Limitación consciente

Todos los platos de una comanda siguen avanzando juntos porque la cocina y la
entrega actuales trabajan a nivel de `RestaurantOrder`. No se finge que ya
existe procesamiento individual.

El siguiente bloque deberá migrar `KitchenSystem`,
`WaiterTaskCoordinator` y `FoodDeliveryServiceFlow` para operar con
`OrderLineId`. La estructura creada aquí no tendrá que cambiar.
