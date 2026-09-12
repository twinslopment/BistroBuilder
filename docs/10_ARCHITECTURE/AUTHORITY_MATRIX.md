# Bistro Builder — matriz de autoridad

| Decisión / estado | Autoridad | No debe decidirlo |
|---|---|---|
| Intención y prioridad de tarea | Gameplay / IA | Animation, Navigation, BBSIS |
| Derecho lógico a usar/asignar/poseer | Interaction & Reservation | BBSIS, Navigation, Animation |
| Viabilidad y reserva espacial | BBSIS | Interaction, Navigation, Animation |
| Ruta, velocidad, heading y llegada | Navigation & Crowd Flow | BBSIS, Animation |
| Pose visual, blending, IK, gaze, recovery | Character Animation | Gameplay, BBSIS, Navigation |
| Estado de comanda y líneas | Order domain | Cocina, UI, Animation |
| Estado/preparación de cocina | Kitchen domain | UI, Animation |
| Empleado, salario, experiencia | Staff | Waiter runtime, Finance |
| Tarea operativa de camarero | WaiterTaskCoordinator | Staff, UI |
| Turno planificado | Staff Schedule | Staff roster, Waiter runtime |
| Caja y ledger | Finance | Staff, Suppliers, UI |
| Stock/lotes/FEFO | Inventory | Suppliers, Kitchen UI |
| Mercado/ofertas/pedidos proveedor | Suppliers | Inventory, Finance |
| Reserva de cliente/capacidad | Reservations | Seating visual, UI |
| Estructura colocada del restaurante | Edit/Structure domain | UI, BBPLFS |
| Condición meteorológica global | Climate | mesa individual, zona exterior individual |

## Reglas de frontera
- `EmployeeId` y `WaiterId` son identidades distintas; el binding de sesión las conecta.
- Wait Ticket se reserva a colas semánticas reales; no sustituye reservas espaciales.
- Claims/Spatial Leases pertenecen solo a BBSIS.
- UI emite comandos y presenta snapshots; no muta estado canónico directamente.
- BBPLFS propone/genera layout, pero la estructura final debe pasar por las mismas reglas de edición y validación.
- La persistencia conserva estados de cada autoridad; no crea un dominio alternativo.