# Bistro Builder — sistemas de gestión

## Inventario / Almacén (2.2)
Un único almacén jugable. Lotes internos no se gestionan manualmente; FEFO, caducidad/deterioro lento y realista, recepciones, mínimos, alertas y previsión. Desperdicio/mermas profundas quedan descartados del alcance actual.

## Proveedores (2.3)
Datos maestros separados de mercado dinámico y estado de partida. Pedidos con ciclo `Draft → Confirmed → PendingDelivery → InDelivery → Delivered / Cancelled`. Formatos comerciales y ofertas desde V1. Editores de Proveedores e Ingredientes mantienen logos/imágenes, Undo/Redo, Dirty y validación. La recomendación automática se denomina **Inteligente**.

## Economía (3)
`BistroBuilderFinanceService` / `finance.runtime` es la única autoridad monetaria. Caja, ledger, ventas, costes, gastos, nóminas, resultados, históricos y financiación convergen en esa autoridad; otros dominios proyectan hechos económicos sin wallets paralelos.

## Personal (4)
`EmployeeId` persistente no es `WaiterId`. Personal posee identidad laboral, salario, experiencia, habilidades y estado del empleado; el binding de sesión conecta empleados con agentes operativos. `WaiterTaskCoordinator` conserva autoridad de tareas.

## Horarios (5)
`staff.schedule` planifica por día/servicio, cobertura y coste proyectado. Filtra quién es elegible para el binding de sesión; no crea empleados ni Waiters. Las ediciones de planificación se realizan con restaurante cerrado.

## Reservas (6)
Gestiona capacidad y disponibilidad temporal/lógica de reservas de clientes, integrada con seating y servicio sin apropiarse de las reservas espaciales BBSIS.

## Marketing, Reputación y Progresión (7–9)
Marketing modifica demanda mediante acciones/costes definidos; Reputación refleja experiencia acumulada; Progresión gobierna avance/desbloqueos. Deben integrarse con Finanzas y servicio mediante contratos, no mediante estado monetario o satisfacción duplicado.

## Regla transversal
Presentation ofrece consulta y comandos. Ninguna pantalla escribe directamente en snapshots de estos dominios.