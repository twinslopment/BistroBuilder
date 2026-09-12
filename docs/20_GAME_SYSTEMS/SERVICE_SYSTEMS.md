# Bistro Builder — servicio, clientes, comandas, cocina y sala

## Clientes y mesas
El servicio debe mantener grupos/clientes, seating, consumo individual y compartido, cuenta y limpieza. La ficha contextual del cliente/mesa expone información básica y acciones operativas como `Disculpa`, `Explicar demora` o `Agilizar cuenta` cuando proceda.

## Comandas
La comanda canónica soporta líneas, consumidores múltiples y pases. En compartidos, una línea puede permanecer `Served` hasta que todos los consumidores hayan reclamado/consumido; los pases se liberan según política. La autoridad de estados de línea no pertenece a Kitchen ni a UI.

## Cocina
Estados operativos visibles: **Fluida / Cargada / Saturada / Bloqueada**. Acciones de gestión aprobadas: **Reducir entrada**, **Pausar nuevas comandas** por plato y **Priorizar comanda** con máximo 3 prioridades simultáneas.

## Camareros
Gestión por zonas: comedor interior, terraza, barra y apoyo. Asignación automática considera cercanía, carga y prioridad; jefe de sala opcional. No existen descansos manuales como minijuego. Personal laboral y agentes runtime siguen siendo dominios separados.

## Entrada, sala y barra
La espera general se representa mediante lista virtual, evitando masas físicas innecesarias. Solo la barra mantiene espera física cuando corresponde. El HUD puede mostrar `Espera: X clientes`.

## Asignación de mesa
Algoritmos de política previstos/aprobados: **Equilibrado**, **Priorizar satisfacción**, **Rotación**, **Proteger reservas** y **Equilibrar zonas**. La política selecciona; BBSIS/Navigation resuelven viabilidad y desplazamiento.

## Fin de servicio/día
El cierre debe agotar o resolver trabajo operativo, consolidar resultados económicos/experiencia y dejar un estado persistible coherente para el siguiente ciclo. El Bloque 15 V1 está cerrado; extensiones deben integrarse sin crear un segundo cierre de día.

## Regla de experiencia
`Caja` y `Satisfacción` en HUD se refieren al servicio actual. Las métricas históricas pertenecen a sus sistemas de gestión/reputación correspondientes.