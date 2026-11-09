# Inventario — definición funcional por secciones (Modo Juego)

**Fecha de inicio:** 09/11/2026.
**Estado:** **Existencias — funcionalidades aprobadas para avanzar** (09/11/2026, confirmación del usuario de continuar). **Alertas de stock — propuesta pendiente de aprobación.** Movimientos y Recepciones siguen pendientes. Aprobación funcional, no visual ni de implementación Unity.
**Regla de trabajo:** trabajar SOLO funcionalidad por pantalla, documentar aprobaciones antes de avanzar y dejar el rediseño visual global para una fase posterior. Identidad vinculante en `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Secciones previstas

1. **Existencias** (aprobada funcionalmente).
2. **Alertas de stock** (propuesta actual).
3. **Movimientos** (después).
4. **Recepciones** (después).

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

- Las **entradas reales de compras** se validan en Recepciones y su servicio correspondiente, no mediante «Aumentar stock» de Existencias.
- Los consumos, reservas y caducidades normales los gestiona el gameplay; el jugador solo corrige discrepancias reales.
- **Confirmación aprobada:** exigir confirmación para cada corrección manual antes de aplicarla; el código revisado hoy ejecuta el ajuste directamente, por lo que esta decisión requiere adaptar la UI y probarla.
- No agregar por defecto fichas de proveedor, negociación de compra ni pronósticos inventados a la pantalla de Existencias.
- El sistema de previsión procede de la planificación existente; aún no se ha inspeccionado de forma integral el comportamiento de proveedores y pedidos.
- Rediseñar todos los controles con la identidad premium común **después** de aprobar la funcionalidad de las pantallas.

## Alertas de stock — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

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

## Próximo paso

Solicitar aprobación o ajustes para **Inventario → Alertas de stock**. Después continuar con **Movimientos**.
