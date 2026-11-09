# Economía — definición funcional por secciones (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Economía → Resumen: PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN**. Quedan pendientes de definir **Resultados, Caja, Históricos y Financiación**.
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

## Economía → Resumen — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

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

### Exclusiones y fronteras propuestas

- No confundir **caja**, **liquidez disponible**, **ventas**, **margen bruto** y **resultado operativo**.
- No inventar pronósticos de ingresos futuros, rentabilidades garantizadas, comparativas con restaurantes reales ni métricas históricas inexistentes.
- No crear avisos críticos paralelos al sistema financiero; reutilizar indicadores y eventos canónicos y decidir su relación con Actividad cuando se aborde la integración.
- No permitir editar importes desde Resumen: el saldo y los resultados los calculan otros sistemas del juego.
- El rediseño visual debe resultar coherente con el resto del juego y las barras aprobadas, nunca parecer un CRM independiente.

## Próximo paso

Solicitar aprobación o ajustes de **Economía → Resumen**. Después seguir por **Economía → Resultados**, **Caja**, **Históricos** y **Financiación**; registrar cada aprobación en el libro general de decisiones.
