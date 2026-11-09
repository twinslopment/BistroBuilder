# Economía — definición funcional por secciones (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Economía → Resumen APROBADA FUNCIONALMENTE (09/11/2026)**. **Economía → Resultados: PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN**. Caja, Históricos y Financiación siguen pendientes. La aprobación no supone rediseño ni implementación de mejoras en Unity.
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

## Economía → Resultados — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

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

### Reglas de frontera propuestas

- Mantener separación entre **Resultados** (rentabilidad) y **Caja** (tesorería y movimientos). El coste de mercancía comprada no se registra doblemente como gasto de ventas del día.
- Evitar cálculos monetarios nuevos con `float`: todos los importes se leen de céntimos canónicos y se presentan formateados.
- No introducir gráficos históricos redundantes, exportaciones, ajustes contables manuales, impuestos simulados no programados ni nuevas métricas financieras en esta primera fase.
- Aplicar posteriormente la misma identidad visual premium que a las restantes pantallas, preservando la barra superior V21 y la inferior E5.5.

## Próximo paso

Solicitar aprobación de **Economía → Resultados**. Después continuar por **Caja**, **Históricos** y **Financiación**; registrar cada paso y mantener el rediseño visual como trabajo independiente.
