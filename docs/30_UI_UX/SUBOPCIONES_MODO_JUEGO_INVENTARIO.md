# Inventario — definición funcional por secciones (Modo Juego)

**Fecha de inicio:** 09/11/2026.
**Estado:** **Existencias — propuesta pendiente de aprobación**. Las demás secciones (Alertas de stock, Movimientos y Recepciones) están pendientes de definir.
**Regla de trabajo:** trabajar SOLO funcionalidad por pantalla, documentar aprobaciones antes de avanzar y dejar el rediseño visual global para una fase posterior. Identidad vinculante en `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Secciones previstas

1. **Existencias** (actual propuesta).
2. **Alertas de stock** (siguiente revisión).
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

## Existencias — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

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

### Límites y decisiones pendientes de aprobación

- Las **entradas reales de compras** se validan en Recepciones y su servicio correspondiente, no mediante «Aumentar stock» de Existencias.
- Los consumos, reservas y caducidades normales los gestiona el gameplay; el jugador solo corrige discrepancias reales.
- **Propuesta:** confirmación de corrección manual para evitar errores accidentales; el código revisado hoy ejecuta el ajuste directamente, de modo que esta confirmación requeriría adaptación de UI.
- No agregar por defecto fichas de proveedor, negociación de compra ni pronósticos inventados a la pantalla de Existencias.
- El sistema de previsión procede de la planificación existente; aún no se ha inspeccionado de forma integral el comportamiento de proveedores y pedidos.
- Rediseñar todos los controles con la identidad premium común **después** de aprobar la funcionalidad de las pantallas.

## Próximo paso

Obtener aprobación o cambios para **Inventario → Existencias**. Después continuar con **Alertas de stock** y guardar cada decisión en el registro general.
