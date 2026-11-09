# Economía — definición funcional por secciones (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Economía → Resumen y Resultados APROBADAS FUNCIONALMENTE (09/11/2026)**. **Economía → Caja: PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN**. Históricos y Financiación siguen pendientes. Las aprobaciones son funcionales: no suponen rediseño ni implementación de mejoras en Unity.
**Regla de trabajo:** se definen las funciones, no el diseño visual ni la implementación. El aspecto futuro de TODAS las pantallas seguirá `docs/30_UI_UX/SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`, respetando las barras V21 y E5.5 aprobadas.

## Secciones verificadas en el código de Finanzas 3J

`BistroBuilderFinanceRuntimeView` define estas cinco pestañas:
1. **Resumen** (`Overview`).
2. **Resultados** (`Results`).
3. **Caja** (`Cash`).
4. **Históricos** (`History`).
5. **Financiación** (`Financing`).

**Arquitectura canónica:** `BistroBuilderFinanceService` (caja y movimientos reales), `BistroBuilderFinancialResultsService` (resultados), `BistroBuilderFinancialHistoryService` (series), `BistroBuilderFinancingService` (deuda, riesgo y liquidez), y `BistroBuilderFinanceDashboardService` (fachada de lectura efímera). **No crear un nuevo libro contable, saldos, históricos ni sistema de préstamos.**

La documentación histórica `docs/FINANCE_3J_UI.md` describe las cinco pestañas y pruebas de 2026. No acredita por sí sola que el nuevo sistema visual V21/E5.5 esté ya integrado ni validado.

## Economía → Resumen — FUNCIONALIDADES APROBADAS

**Objetivo:** ofrecer una fotografía rápida y fiable de la situación económica del restaurante durante el día de juego, con enlaces a las secciones especializadas.

1. **Mostrar el saldo de caja global actual**, procedente de la autoridad financiera. No confundirlo con el indicador de **Caja del servicio** de la barra inferior del Modo Juego: si representan periodos distintos, mostrar claramente su significado; ambos deben basarse en los mismos hechos monetarios canónicos.
2. **Mostrar el dinero disponible tras compromisos con proveedores**, separado del saldo bruto en caja. No presentar como gastable el importe comprometido en pedidos aún no debitados.
3. **Mostrar las ventas del día de juego** y el **resultado operativo del día** como dos conceptos distintos. Ventas no equivalen a beneficio ni a cobros disponibles.
4. **Mostrar riesgo financiero y estado de liquidez** con sus estados reales. Si la proyección está incompleta o es desconocida, explicarlo claramente y **nunca etiquetar como sana** una situación sin datos suficientes.
5. **Desglosar brevemente la liquidez:** compromisos con proveedores, próximos gastos operativos, próximas cuotas de deuda, deudas vencidas, horizonte de cálculo, liquidez proyectada y cobertura de obligaciones cuando el sistema aporte estos valores.
6. **Mostrar margen bruto y coste de producto del día**, incluyendo los avisos de cobertura COGS pendiente/estimada. No mostrar una rentabilidad como definitiva si faltan datos de coste reconocidos.
7. **Resumir los gastos operativos del día** y el resultado final correspondiente. El desglose por conceptos pertenece a **Resultados**, sin duplicar su pantalla.
8. **Mostrar contribución por servicio** (Desayuno/Comida/Cena solamente si existen en la partida): ingresos, margen y número de cuentas pagadas cuando estén registrados. **No asignar artificialmente gastos generales a cada servicio.**
9. **Accesos a detalle**: navegar a Resultados para desgloses, Caja para entradas/salidas, Históricos para evolución y Financiación para préstamos y obligaciones. Estas conexiones deben reutilizar las secciones aprobadas en lugar de crear nuevos paneles financieros aislados.
10. **Actualizar con los eventos económicos reales** (ventas, pagos, compras, nóminas, gastos y deuda) utilizando el snapshot del dashboard existente, sin recalcular cifras con fórmulas alternativas en la UI.
11. **Indicadores comprensibles y sin saturación:** dar prioridad a saldo, dinero comprometido, ventas, resultado y riesgo; ampliar el detalle solo al entrar en la sección pertinente. Esta es una norma de contenido; el diseño de tarjetas, colores e iconos se decidirá en el chat de diseño visual.
12. **Solo lectura y sin acciones económicas de riesgo:** desde Resumen no se crean préstamos, no se modifican balances ni se tramitan compras; cualquier financiación requiere la sección Financiación y confirmación específica.

### Contraste con el código existente

- `BistroBuilderFinanceRuntimeView.BuildOverviewSection` ya muestra cinco indicadores: caja, disponible tras compromisos, ventas hoy, resultado hoy y riesgo financiero; tres paneles adicionales de liquidez, resultado y servicios.
- `BistroBuilderFinanceDashboardService.TryBuildDashboard` compone datos de caja, resultado diario, históricos, posición de liquidez y riesgo. El dashboard **no persiste por su cuenta**.
- La vista usa `BistroBuilderLiquidityPosition.projectionComplete` para señalar información incompleta, y `BistroBuilderDayFinancialResult.IsCostCoverageComplete` / `costCoverageGapCents` para la cobertura de costes.
- `BistroBuilderFinanceRuntimeView.BuildResultsSection` aporta los gastos desglosados que no deben repetirse íntegramente en Resumen; `BuildCashSection` registra movimientos financieros de caja.
- `BistroBuilderFinanceRuntimeView` tiene navegación interna de cinco pestañas. **La navegación profunda desde cada indicador de Resumen, la sincronización visual exacta con la barra inferior E5.5 y el nuevo diseño global son mejoras previstas**, no se han implementado ni probado en este paso.
- El origen de los importes es financiero y está expresado en céntimos canónicos; **no usar float en cálculos de dinero ni introducir cifras decorativas**.

### Exclusiones y fronteras aprobadas

- No confundir **caja**, **liquidez disponible**, **ventas**, **margen bruto** y **resultado operativo**.
- No inventar pronósticos de ingresos futuros, rentabilidades garantizadas, comparativas con restaurantes reales ni métricas históricas inexistentes.
- No crear avisos críticos paralelos al sistema financiero; reutilizar indicadores y eventos canónicos y decidir su relación con Actividad cuando se aborde la integración.
- No permitir editar importes desde Resumen: el saldo y los resultados los calculan otros sistemas del juego.
- El rediseño visual debe resultar coherente con el resto del juego y las barras aprobadas, nunca parecer un CRM independiente.

## Economía → Resultados — FUNCIONALIDADES APROBADAS

**Objetivo:** mostrar cómo se construye el resultado económico del restaurante, separando ventas, coste de producto, margen bruto y gastos; distinguir **resultado operativo** de los movimientos de dinero de Caja.

1. **Consultar las ventas cobradas del día de juego**, el número de cuentas pagadas y la parte de ventas que tiene coste de producto reconocido. No equiparar ventas con beneficio ni incluir en ventas la entrada de un préstamo.
2. **Mostrar el coste de producto vendido (COGS)** real, estimado o mixto que calcula el sistema, acompañado de su **calidad** y del coste teórico de referencia. No sumar el precio de compra de todo el inventario como COGS: la compra es tesorería, y el consumo vendido genera el coste de ventas.
3. **Calcular y mostrar el margen bruto** en importe y porcentaje a partir de los resultados canónicos. Explicar que mide ventas menos coste de producto, antes de gastos generales.
4. **Advertir sobre cobertura incompleta de costes:** mostrar si faltan líneas valoradas, la brecha `costCoverageGapCents` y si el coste refleja importes reales, estimados o ambos. No presentar márgenes incompletos como cifras cerradas ni silenciar posibles datos no disponibles de pagos de proveedor.
5. **Desglosar gastos del día** por categorías realmente registradas: portes de proveedor, gastos operativos recurrentes, nóminas, marketing, baja de activos, pérdidas de inventario por merma/caducidad, intereses financieros y otros gastos reconocidos.
6. **Mostrar total de gastos operativos y resultado operativo** del día, indicando explícitamente cuándo es pérdida. No mezclar amortización del principal, cobros de préstamos, compra de activos ni cobros por reventa con el beneficio operativo del día.
7. **Desglosar la contribución por servicio** (desayuno/comida/cena solo cuando existan datos): ventas de mesa y barra, coste de producto, margen bruto, porcentaje de margen y cuentas pagadas. **No repartir artificialmente alquileres, nóminas ni otros gastos generales entre servicios.**
8. **Identificar qué conceptos explican un mal resultado** mediante sus cifras: ventas bajas, COGS elevado, merma o mayores gastos. Ofrecer lectura comprensible de datos disponibles; no inventar análisis causal avanzado ni recomendaciones no calculadas por los sistemas actuales.
9. **Actualizar automáticamente al cambiar los resultados** de ventas, consumo, cierre de servicio, pagos y gastos, empleando `BistroBuilderFinancialResultsService` y el dashboard canónico, sin guardar otra cuenta paralela.
10. **Ir a Caja para movimientos monetarios concretos** y a Históricos para comparar días y periodos. Esta pestaña describe el resultado económico del **día seleccionado/actual**; la selección de días comparables o periodos se resolverá en Históricos, salvo ampliación expresamente aprobada.
11. **Solo lectura:** el jugador no podrá teclear ingresos, gastos, márgenes o resultados, ni modificar operaciones económicas consolidadas desde esta pantalla. Los cambios reales se originan en los correspondientes sistemas jugables.

### Código y límites verificados

- `BistroBuilderFinanceRuntimeView.BuildResultsSection` ya muestra tres bloques: **Ventas y margen**, **Gastos del día** y **Contribución por servicio**, con cuentas, importes, COGS, margen, cobertura de coste, gastos por concepto y resultado operativo.
- `BistroBuilderFinancialResultsService.TryGetDayResult` agrega resultados reales y costes mediante las autoridades `BistroBuilderFinanceService` y `BistroBuilderProductCostService`, sin persistir una base de resultados independiente.
- `BistroBuilderDayFinancialResult` incluye `revenueCents`, `costedSalesCents`, `productCostCents`, `theoreticalProductCostCents`, `grossProfitCents`, `grossMarginBasisPoints`, costes por naturaleza, `totalPeriodExpensesCents`, `operatingResultCents` y `costCoverageGapCents`.
- `BistroBuilderFinancialResultCostQuality` distingue `None`, `Estimated`, `Mixed` y `Actual`. Las ventas sin coste reconocido y el desglose de pagos de proveedor incompleto se deben tratar con transparencia.
- `BistroBuilderServiceFinancialResult` distingue ventas de **mesa** y **barra**, cuentas y contribución bruta por comida, cena u otro servicio existente; **no atribuye gastos comunes arbitrariamente**.
- El código actual **no incorpora** selección de fechas ni comparación histórica dentro de esta pestaña, ni análisis causal automático por motivos de pérdidas: son posibles mejoras de lectura, no capacidades acreditadas. Los históricos están en otra sección.

### Reglas de frontera aprobadas

- Mantener separación entre **Resultados** (rentabilidad) y **Caja** (tesorería y movimientos). El coste de mercancía comprada no se registra doblemente como gasto de ventas del día.
- Evitar cálculos monetarios nuevos con `float`: todos los importes se leen de céntimos canónicos y se presentan formateados.
- No introducir gráficos históricos redundantes, exportaciones, ajustes contables manuales, impuestos simulados no programados ni nuevas métricas financieras en esta primera fase.
- Aplicar posteriormente la misma identidad visual premium que a las restantes pantallas, preservando la barra superior V21 y la inferior E5.5.

## Economía → Caja — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** consultar la tesorería real del restaurante: **qué dinero hay, cuánto ha entrado o salido y qué compromisos limitan su disponibilidad**, sin mezclar esos movimientos con el resultado económico y sin permitir introducir dinero manualmente.

1. **Saldo global actual:** mostrar la cantidad de dinero disponible en el libro de caja canónico, diferenciándola claramente de las **ventas** y del **beneficio operativo**.
2. **Dinero comprometido y disponible:** visualizar el saldo bruto y los compromisos vigentes con proveedores; mostrar el efectivo disponible después de esos compromisos como cifra distinta. Los compromisos aún no debitados no se presentan como pagos ya efectuados.
3. **Resumen de movimientos del día:** total de entradas, salidas y variación neta de caja, calculados desde las transacciones reconocidas por el sistema financiero.
4. **Distinguir las operaciones de tesorería** presentes en el código: compras a proveedores, inversiones, devoluciones por reventa de activos, recepción de préstamos y devolución del principal de deuda, además de los cobros y otros pagos reales. **Un préstamo recibido no es un ingreso de explotación**, ni la compra de un activo equivale automáticamente a un gasto del día.
5. **Movimientos recientes reales**, en orden de más reciente a más antiguo: importe con signo y moneda, concepto comprensible, tipo entrada/salida, categoría, día y hora del juego, y referencia vinculada cuando esté disponible. Mostrar estado vacío cuando todavía no existan transacciones.
6. **Buscar y filtrar movimientos** por concepto/categoría, sentido (entrada/salida) y día/periodo cuando los datos existentes permitan consultarlo. La vista actual muestra una lista de movimientos recientes, pero **no dispone de buscador ni filtros propios**; son ampliaciones de interfaz/consulta.
7. **Detalle consultable de un movimiento:** conocer importe exacto, motivo, origen, día de juego y vínculo con el pedido, venta, préstamo o inversión que lo generó si hay una referencia resoluble. No mostrar identificadores técnicos como información principal ni inventar transacciones inexistentes.
8. **Consultar compromisos y obligaciones próximas:** importe comprometido con proveedores, obligaciones operativas próximas, cuotas de financiación, vencimientos y deudas atrasadas a partir del modelo canónico de liquidez.
9. **Mostrar proyección de liquidez con transparencia:** indicar horizonte en días de juego y saldo previsto después de obligaciones conocidas; si faltan datos, advertir expresamente que la proyección es **incompleta**, sin etiquetar al restaurante como financieramente sano por defecto.
10. **Accesos de navegación útiles:** desde Caja ir a Resultados para entender rentabilidad, a Financiación para el origen de cuotas/préstamos, a Pedidos para compromisos y a Históricos para evolución de tesorería. Utilizar las pantallas canónicas existentes, sin crear otra gestión de compras.
11. **Coherencia con la barra inferior E5.5:** la métrica de **Caja del servicio** de la barra inferior no es necesariamente el saldo global. Debe quedar claro cuándo se habla del servicio actual, del día de juego o de la caja total, sin cambiar el diseño aprobado de esa barra.
12. **Solo lectura:** no habrá botones para aumentar/reducir efectivo, registrar ingresos o gastos ficticios, eliminar movimientos, editar asientos o pedir créditos desde Caja. El dinero cambia únicamente por acciones reales de otros sistemas del juego.

### Código existente comprobado y ampliaciones pendientes

- `BistroBuilderFinanceRuntimeView.BuildCashSection` ya representa **Caja y obligaciones** y los **movimientos de caja recientes**; enseña saldo, disponible tras compromisos, entradas/salidas/variación diaria, compras, inversiones, devolución de principal, préstamos recibidos, reventa de activos y horizonte de liquidez.
- `BistroBuilderFinanceDashboardService.CopyRecentMovements` lee el ledger `BistroBuilderFinanceService` en orden inverso y permite pedir hasta **500 filas**; la UI tiene una configuración inicial de **80 movimientos visibles**. Esto **no equivale a un buscador histórico completo** ni a paginación ya implementada.
- `BistroBuilderFinanceMovementView` contiene secuencia, identificador de operación/origen, categoría y grupo, signo de entrada/salida (`Credit`/`Debit`), importe en céntimos, **día y minuto del juego**, descripción y etiqueta de presentación. No inventar fechas de calendario real para esos índices.
- `BistroBuilderLiquidityPosition` contiene caja, compromisos con proveedores, disponible tras compromisos, obligaciones próximas, deuda vencida, proyección y `projectionComplete`. Los datos faltantes nunca se sustituyen por estimaciones inventadas en UI.
- `BistroBuilderDayFinancialResult` distingue movimientos de tesorería de resultado del día; las compras de inventario pueden salir de Caja sin convertirse inmediatamente en COGS de Resultados, y los préstamos recibidos no constituyen beneficio.
- **Filtros, búsqueda, detalle interactivo, enlaces profundos a Pedidos/Financiación y visualización exacta conjunta con la barra inferior E5.5 requieren UI/integración nueva y pruebas.** Este paso solo propone funcionalidades: no ejecuta la build.

### Exclusiones propuestas

No crear arqueo físico, ingreso manual, transferencias bancarias, caja por empleado, impuestos o auditorías administrativas nuevos sin definir previamente su mecánica. No permitir que un botón de Caja altere directamente el ledger. No duplicar la pestaña Históricos en esta pantalla; aquí se consultan movimientos concretos y el estado de tesorería, no análisis gráfico de periodos.

## Próximo paso

Solicitar aprobación o ajustes de **Economía → Caja**. Después continuar por **Históricos** y **Financiación**, manteniendo separadas las decisiones funcionales de la fase posterior de diseño visual y Unity.
