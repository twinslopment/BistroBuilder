# Inventario — definición funcional por secciones (Modo Juego)

**Fecha de inicio:** 09/11/2026.
**Estado:** **INVENTARIO CERRADO FUNCIONALMENTE el 09/11/2026**. Las cuatro secciones — Existencias, Alertas de stock, Movimientos y Recepciones — están APROBADAS. El rediseño visual, integración V21 y las ampliaciones técnicas de UI siguen pendientes; documentación no acredita nuevas pruebas de Unity.
**Regla de trabajo:** trabajar SOLO funcionalidad por pantalla, documentar aprobaciones antes de avanzar y dejar el rediseño visual global para una fase posterior. Identidad vinculante en `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Secciones previstas

1. **Existencias** (aprobada funcionalmente).
2. **Alertas de stock** (aprobada funcionalmente).
3. **Movimientos** (aprobada funcionalmente).
4. **Recepciones** (aprobada funcionalmente).

No introducir múltiples autoridades de inventario ni confundir un movimiento de corrección con una recepción de proveedor.

## Sistemas existentes verificados (rama V21)

- `BistroBuilderInventoryWarehouseRuntimeView` ya incorpora cuatro pestañas: **Existencias, Alertas, Movimientos y Recepciones**, con búsqueda, filtros, ordenación, selección de ingrediente, edición de mínimo y ajuste manual.
- `BistroBuilderInventoryWarehouseService` compone datos de inventario canónico, planificación y recepción; permite consultar ingredientes, resumen, movimientos, recepciones y alertas; cambiar mínimo, corregir stock con motivo y evaluar preparación para apertura.
- `BistroBuilderInventoryWarehouseIngredientSnapshot` incluye cantidades físicas, reservadas y disponibles; unidad base, mínimo de stock, estado, frescura/caducidad próxima, previsión de cobertura de días y última recepción.
- Filtros ya existentes: **Todos, Stock bajo, Crítico/agotado, Próxima caducidad**.
- Ordenaciones ya existentes: **Nombre, Stock disponible, Estado y Caducidad**.
- `TryAdjustStock` crea movimiento de corrección en el ledger canónico; **rechaza cantidades negativas y correcciones que reduzcan el disponible por debajo de las reservas existentes**.
- No se ha verificado la integración de esta pantalla con la barra V21, con el futuro rediseño global, ni una nueva build del juego.

## Existencias — FUNCIONALIDADES APROBADAS

**Objetivo:** que el jugador sepa **qué ingredientes tiene realmente, cuánto puede utilizar y qué necesita reponer**, sin alterar las compras ni el consumo automático del servicio.

1. **Consultar todos los ingredientes del almacén** con nombre, unidad aplicable, existencias actuales y estado de stock.
2. **Distinguir el stock físico, reservado y disponible:** no confundir ingredientes existentes con los que pueden utilizarse en nuevas comandas.
3. **Buscar, filtrar y ordenar** por nombre, stock bajo, crítico/agotado o próxima caducidad; ordenar por nombre, cantidad disponible, estado y fecha próxima de vencimiento.
4. **Seleccionar un ingrediente para ver detalle:** cantidades física/reservada/disponible, mínimo, estado, próxima caducidad, cantidad próxima a caducar y última recepción cuando existan datos.
5. **Editar el stock mínimo deseado** por ingrediente; recalcular avisos automáticamente mediante la planificación canónica, sin crear alertas paralelas.
6. **Consultar previsión de consumo y cobertura**, expresada en días solo cuando haya histórico suficiente; si no existe, mostrar ausencia de datos y no inventar una estimación.
7. **Registrar una corrección manual de inventario** (sobrante/faltante) con cantidad, motivo y nota opcional; **confirmar la operación** antes de aplicarla, mostrar el resultado y registrarla en Movimientos. Impedir correcciones negativas, que bajen del stock reservado o que intenten simular una recepción de compras.
8. **Comprobar la preparación de inventario antes de abrir:** informar de ingredientes insuficientes o incidencias; no bloquear de forma automática la apertura por una recomendación de stock, salvo regla general de juego aprobada independientemente.
9. **Mantener cifras actualizadas** tras entregas de proveedores, consumos de cocina, mermas, reservas y caducidades; la pantalla es una vista del mismo inventario canónico.
10. **Acceso funcional a otras secciones:** para investigar una incidencia abrir «Alertas de stock»; para auditar una corrección abrir «Movimientos»; para verificar entregas abrir «Recepciones». Para pedir producto, utilizar «Proveedores»; NO generar pedidos automáticamente desde Existencias.

### Límites funcionales aprobados y advertencias técnicas

- Las **entradas reales de compras** son aceptadas por el servicio canónico de recepción de mercancía desde Proveedores; se **consultan** en la pestaña Recepciones. No se crean mediante «Aumentar stock» de Existencias.
- Los consumos, reservas y caducidades normales los gestiona el gameplay; el jugador solo corrige discrepancias reales.
- **Confirmación aprobada:** exigir confirmación para cada corrección manual antes de aplicarla; el código revisado hoy ejecuta el ajuste directamente, por lo que esta decisión requiere adaptar la UI y probarla.
- No agregar por defecto fichas de proveedor, negociación de compra ni pronósticos inventados a la pantalla de Existencias.
- El sistema de previsión procede de la planificación existente; aún no se ha inspeccionado de forma integral el comportamiento de proveedores y pedidos.
- Rediseñar todos los controles con la identidad premium común **después** de aprobar la funcionalidad de las pantallas.

## Alertas de stock — FUNCIONALIDADES APROBADAS

**Objetivo:** mostrar los problemas de inventario que requieren atención, facilitar la causa y conducir a la acción correcta, sin crear una segunda autoridad de alertas.

1. **Consultar alertas activas** de los ingredientes con datos reales del sistema canónico, actualizadas tras compras, consumo, reservas, correcciones y caducidad.
2. **Distinguir cuatro tipos:** sin stock, stock crítico, stock bajo y próximo a caducar. Priorizar sin stock/crítico sobre bajo y caducidad próxima; las categorías pueden coexistir para un ingrediente si se trata de problemas diferentes.
3. **Ver la gravedad y un resumen:** número de incidencias activas por tipo y gravedad, sin inventar diagnósticos de gameplay ni estados que no existan.
4. **Filtrar y ordenar** alertas por gravedad, tipo e ingrediente; mostrar primero las críticas sin eliminar las de menor prioridad.
5. **Seleccionar una alerta y consultar su causa:** ingrediente, stock disponible, mínimo configurado, próxima caducidad y cobertura prevista cuando exista; una acción «Ver existencias» abrirá la ficha canónica del ingrediente. **La selección/navegación desde la alerta requiere ampliar la UI actual**, cuyas filas de alertas son informativas, sin clic.
6. **Resolver mediante acciones reales:** ajustar el mínimo desde Existencias, corregir errores de inventario con confirmación o dirigirse a Proveedores para comprar. No aumentar stock mediante un botón ficticio ni recibir compras por esta pantalla.
7. **Desaparición automática:** una alerta desaparece cuando el estado real deja de justificarla; no permitir marcarla como resuelta si sigue faltando stock ni mantener avisos falsos.
8. **Evitar duplicados y notificaciones insistentes:** reutilizar `AlertKey` estable por ingrediente/tipo y los eventos `AlertActivated`/`AlertCleared`; no añadir una segunda lista independiente. No mostrar modales recurrentes.
9. **Comprobación informativa antes de abrir:** presentar los avisos relevantes cuando se consulte la preparación para apertura; su presencia por sí sola no bloquea abrir.
10. **Integración futura con Actividad:** permitir que los avisos críticos pertinentes alimenten el panel izquierdo de Actividad y su indicador de crítico pendiente mediante **una proyección de los mismos eventos de Inventario**, no un nuevo estado de alertas. La conexión con Actividad V21 **no está verificada en esta rama** y queda pendiente de desarrollo/pruebas.

### Comportamiento actual verificado y diferencias con la propuesta

- `BistroBuilderInventoryPlanningService` conserva alertas activas deduplicadas `BistroBuilderInventoryAlertSnapshot`; tipos `LowStock`, `CriticalStock`, `OutOfStock` y `NearExpiry`, con gravedad `Warning` o `Critical`.
- Publica `AlertActivated` y `AlertCleared` cuando aparece o desaparece un tipo para un ingrediente. No posee un estado independiente de «leído» ni un botón de reconocimiento manual.
- Cuando **el mínimo configurado es cero**, el código actual no crea alertas automáticas de reposición (aunque Existencias todavía muestre «sin stock»): respetar esta política salvo decisión futura sobre su cambio.
- La alerta de caducidad próxima se deriva de lotes y días del juego; no de una fecha inventada por la UI.
- `BistroBuilderInventoryWarehouseRuntimeView.RefreshAlerts` presenta las alertas como filas solo informativas y oculta las acciones. **Filtros específicos de alertas, selección interactiva, enlaces de acción e integración con Actividad son ampliaciones propuestas, no funcionalidades ya implementadas.**

### Límites

No crear compras, devoluciones ni pedidos automáticos desde Alertas. No exponer al jugador operaciones de depuración ni permitir que oculte críticos sin solucionar su causa. No reutilizar esta sección para la auditoría histórica de movimientos o recepciones.

## Movimientos — FUNCIONALIDADES APROBADAS

**Objetivo:** ofrecer al jugador un historial claro y fiable de **por qué han aumentado o disminuido las existencias**, aprovechando directamente el registro de transacciones del inventario canónico (ledger).

1. **Consultar los movimientos recientes:** listar entradas y salidas reales ordenadas de más reciente a más antiguo, y permitir revisar el historial conservado sin crear un registro paralelo. Mostrar un estado vacío comprensible cuando no existan movimientos.
2. **Distinguir los motivos:** stock inicial, recepción de compras, consumo de cocina, mermas, correcciones manuales y caducidad. Reservas/liberaciones internas se omiten en la vista básica para evitar ruido, aunque su efecto sobre disponible/reservado sigue reflejado en Existencias.
3. **Mostrar los datos esenciales:** ingrediente, tipo de movimiento, cantidad positiva o negativa con unidad adecuada, motivo cuando exista y orden/fecha de registro **solo si el dato está disponible**. Los identificadores técnicos no se presentan como sustitutos de información comprensible.
4. **Buscar y filtrar por ingrediente y tipo**, distinguir entradas y salidas y ofrecer un filtro por periodo solamente cuando la fuente permita acotarlo de forma verificable. La UI actual no incluye búsqueda ni filtros específicos de movimientos: esto sería ampliación de presentación/consulta.
5. **Consultar el detalle de un movimiento** para ver tipo, cantidad, ingrediente, motivo/nota original, fuente u operación relacionada y marca temporal si consta. No inventar precios ni atribuir costes económicos donde la transacción no los contiene.
6. **Ir al ingrediente** en Existencias para consultar la situación actual del stock, sin modificar el movimiento histórico.
7. **Relacionar con Recepciones** cuando el origen sea una compra, accediendo a la recepción real; no procesar ni duplicar la compra desde Movimientos.
8. **Historial inalterable desde esta pantalla:** el jugador no puede editar, eliminar ni cancelar transacciones pasadas. Una diferencia de inventario se corrige desde Existencias y crea **un nuevo movimiento trazable**, nunca borrando el anterior.
9. **Mostrar la información relevante sin sobrecargar:** límite de filas visible, paginación/carga progresiva si fuera necesaria; mostrar claramente el alcance disponible. No prometer historial ilimitado ni exportación de datos sin soporte y decisión adicionales.
10. **Actualización con el juego:** al recibirse compras, consumirse ingredientes, caducar lotes o registrarse correcciones, el historial reflejará las transacciones persistidas por el sistema canónico, sin inventar entradas para eventos que no hayan ocurrido.

### Verificación de código y límites

- `BistroBuilderInventoryWarehouseService.CopyMovementsTo` devuelve los movimientos recientes del ledger por orden inverso, con máximo entre 1 y 1000 filas (según petición), y permite excluir operaciones internas de reserva. No implementa filtros/búsqueda específicos por tipo/ingrediente ni paginación.
- `BistroBuilderInventoryWarehouseRuntimeView.RefreshMovements` muestra actualmente hasta 160 filas en formato informativo y oculta las acciones; la selección y el detalle navegable aún requieren adaptación.
- `BistroBuilderInventoryWarehouseMovementSnapshot` contiene `Sequence`, `TransactionId`, `OperationId`, `IngredientId`, nombre, unidad, tipo, delta físico, delta reservado, fuente, motivo y `TimestampUtcTicks`. Esta marca temporal es de registro UTC; **no equivale necesariamente al día del calendario jugable**. No inventar el día de juego si no consta.
- El historial no es un segundo registro de auditoría ni se generan transacciones ficticias; no crear operaciones de compra, devolución o corrección directamente en esta pantalla.
- **La búsqueda, los filtros específicos, el detalle seleccionado y el enlace a Existencias/Recepciones son funcionalidades propuestas, no verificadas en la UI actual.**

## Recepciones — FUNCIONALIDADES APROBADAS

**Objetivo:** comprobar **qué compras han llegado efectivamente al restaurante, qué ingredientes se han incorporado al almacén y a qué pedido corresponden**, sin crear un segundo sistema de recepción ni contabilizar dos veces las existencias.

1. **Consultar recepciones confirmadas** de compras, ordenadas de más reciente a más antigua; estado vacío cuando todavía no haya llegado ninguna mercancía.
2. **Ver resumen por recepción:** referencia del pedido/recepción, origen o proveedor cuando esté disponible, número de ingredientes y cantidades recibidas. No inventar precios, importes o datos de proveedor que no estén en la fuente real.
3. **Consultar detalle de cada recepción:** lista completa de ingredientes con cantidades y unidades; marca temporal de registro si existe. La fecha UTC del ledger no debe presentarse como día de juego si no está disponible ese dato en la lectura.
4. **Buscar y filtrar** por ingrediente, origen/proveedor y referencia; permitir seleccionar el registro y consultar su detalle. El código actual no incorpora estos filtros, ni filas de Recepciones seleccionables: son una ampliación funcional.
5. **Relacionar con Proveedores:** ofrecer «Ver pedido» cuando se pueda resolver el pedido de origen. Los pedidos pendientes o en tránsito se consultan en **Proveedores**, no aparecen falsamente como recibidos.
6. **Acceder a Existencias:** seleccionar un ingrediente recibido y consultar sus existencias actuales, distinguiéndolas de la cantidad histórica que llegó en aquella recepción.
7. **Relacionar con Movimientos:** permitir consultar las entradas de inventario asociadas a la recepción utilizando `ReceiptId` / `OperationId`, sin generar registros duplicados.
8. **Evitar duplicados:** una misma recepción no debe sumar mercancía dos veces, aunque se repita un evento, se vuelva a cargar una partida o se reproduzca la animación del repartidor. Reutilizar la idempotencia canónica por `ReceiptId`.
9. **No editar recepciones ya confirmadas:** el jugador no puede borrarlas, cambiar sus cantidades ni revertir una compra desde esta pestaña. Las discrepancias físicas posteriores se corrigen desde Existencias, con movimiento nuevo y trazable.
10. **No introducir confirmación manual de entrega en esta primera fase:** el circuito existente de Proveedores completa la recepción automáticamente cuando se valida su entrega. Recepciones permite **consultar y verificar**, pero no volver a aceptar la mercancía. Una mecánica posterior de entrega parcial/rechazo/incidencia requeriría definir reglas nuevas antes de programarla.

### Código verificado y límites técnicos

- `BistroBuilderSupplierReceivingBridge23L.TryAcceptHandoff` recibe una entrega de un pedido real en estado `InDelivery`, llama a `BistroBuilderGoodsReceivingService.TryReceiveGoods` y marca el pedido como `Delivered` si se completa el flujo.
- `BistroBuilderGoodsReceivingService.TryReceiveGoods` valida y registra la recepción en inventario de forma atómica usando `ReceiptId` idempotente; si ya se registró, no repite el aumento de existencias. La animación del repartidor se dispara **después** del registro y no es el acto de recepción contable.
- `BistroBuilderInventoryWarehouseService.CopyReceiptsTo` compone una vista de las operaciones `Purchase` del ledger, agrupadas por `OperationId`/identificador de recepción; devuelve los últimos registros sin una segunda persistencia.
- `BistroBuilderInventoryWarehouseRuntimeView.RefreshReceipts` presenta filas informativas; no hay aún detalle seleccionable, búsqueda ni enlaces operativos. Las acciones descritas son **propuestas pendientes de implementación**.
- El snapshot de recepción expone `ReceiptId`, `SourceId`, `TimestampUtcTicks`, `Lines` y secuencias. No garantiza importe, proveedor legible ni fecha en calendario del juego: cruzar datos con Proveedores cuando sea posible y mostrar ausencia de información en otro caso.

### Frontera con Proveedores

**Proveedores** se ocupa de pedidos, estados logísticos y entregas pendientes. **Inventario → Recepciones** consulta únicamente entregas realmente aceptadas en inventario. No agregar aquí un segundo botón de compra ni una confirmación manual que ya contradiga el flujo automatizado.

## Próximo paso

**Inventario queda cerrado funcionalmente.** Continuar la definición funcional de **Proveedores → Proveedores**, sin empezar aún el rediseño visual global ni modificar el runtime Unity.
