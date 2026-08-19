# Bistro Builder — 3J UI jugable de Finanzas y Caja

## Estado

Implementación en rama `feature/3j-finance-cash-ui`.

**No debe cerrarse 3J hasta que la fase de endurecimiento 3A–3I y su Queen Test financiera global hayan sido validadas en Unity.** La rama 3J se desarrolla sobre esa base para no detener el trabajo mientras la validación presencial queda pendiente.

## Principio arquitectónico

3J es Presentation + una fachada de lectura. No introduce una nueva autoridad económica.

Cadena de dependencias:

- 3A `BistroBuilderFinanceService` → caja y ledger canónicos.
- 3G `BistroBuilderFinancialResultsService` → resultado por servicio/día.
- 3H `BistroBuilderFinancialHistoryService` → históricos, KPIs y comparativas.
- 3I `BistroBuilderFinancingService` → liquidez, riesgo y contratos de deuda.
- 3J `BistroBuilderFinanceDashboardService` → read-model efímero para la UI.
- 3J `BistroBuilderFinanceRuntimeView` → presentación e interacción del jugador.

`BistroBuilderFinanceDashboardService` no implementa `IBistroBuilderSaveSectionProvider`, no conserva saldo, no posee ledger, no duplica históricos y no persiste deuda.

La única acción monetaria iniciada por 3J es aceptar una oferta de financiación. Se canaliza así:

`UI 3J → FinanceDashboardService → FinancingService 3I → FinanceService 3A`

La vista nunca publica directamente en el ledger.

## Pantallas jugables

### Resumen

Presenta de forma compacta:

- caja actual;
- disponible tras compromisos con proveedores;
- ventas del día;
- resultado operativo del día;
- estado de liquidez;
- riesgo financiero;
- obligaciones próximas;
- deuda vencida;
- liquidez proyectada;
- margen y COGS del día;
- contribución de Desayuno / Comida / Cena.

### Resultados

Separa expresamente:

- ventas;
- COGS reconocido y teórico;
- margen bruto;
- gastos operativos;
- nóminas;
- marketing;
- portes;
- deterioro/caducidad de inventario;
- bajas de activos;
- intereses de financiación;
- resultado operativo.

Los gastos generales no se reparten artificialmente entre servicios.

### Caja

Presenta tesorería sin confundirla con beneficio:

- caja actual;
- entradas y salidas del día;
- variación neta;
- compras a proveedores;
- inversiones;
- principal de deuda;
- préstamos recibidos;
- reventa de activos;
- liquidez proyectada;
- movimientos recientes del ledger 3A en orden inverso.

### Históricos

Ventanas disponibles:

- 7 días;
- 30 días;
- 90 días;
- todo el histórico permitido por 3H.

Métricas gráficas:

- ingresos;
- resultado operativo;
- variación de caja.

El gráfico es uGUI nativo, sin texturas ni dependencias externas, y agrega históricos largos a un máximo de 180 buckets visuales.

También muestra KPIs y comparación con el periodo anterior de igual duración cuando existe.

### Financiación

Muestra exclusivamente las ofertas y contratos publicados por 3I:

- principal;
- plazo;
- interés total;
- total a devolver;
- elegibilidad e impedimento real;
- deuda pendiente;
- próxima cuota;
- estado del préstamo.

Aceptar financiación exige un modal de confirmación. La UI genera un token estable para esa confirmación y 3I conserva la idempotencia de la operación.

## Integración de input

Al abrir Finanzas:

- se deshabilita temporalmente la cámara profesional;
- se deshabilita temporalmente la interacción de edición;
- los otros accesos globales se ocultan mediante `BistroBuilderFinanceUiModalCoordinator`;
- al cerrar se restauran los estados anteriores.

El coordinador existe como puente aditivo porque la capa transversal UI 2.3JKL-B2 fue creada antes de Finanzas y no ofrece actualmente registro público de módulos nuevos. No toca ninguna autoridad de dominio.

## Seguridad visual

- ScrollViews usan `RectMask2D`, nunca `Mask` clásico transparente.
- Botones persistentes usan `Transition.None` y `Navigation.None` para evitar flashes/estado Selected de uGUI.
- La selección visual es determinista.
- Los importes usan céntimos autoritativos y formato `es-ES`; la UI no recalcula dinero con `float`.
- `Unknown` de liquidez se muestra como información incompleta, nunca como estado sano.

## Herramientas de validación

### Instalación

`Tools → Bistro Builder → Finanzas → 3J - Instalar + validar + autotest`

Instalador idempotente y transaccional. Hace backup byte a byte de la escena y revierte si falla cualquier gate.

### Validador

Comprueba:

- base 3A–3I estructuralmente limpia;
- una única fachada 3J;
- una única vista 3J;
- un único coordinador modal;
- referencias exactas a las autoridades canónicas;
- root único `BB_3J_FinanceUI` bajo el HUD canónico;
- ausencia de una sección Save 3J o ledger paralelo;
- persistencias 3A/3D/3I siguen siendo únicas.

### Autotest

Cubre contratos puros de:

- rangos 7/30/90/Todo;
- formato monetario/porcentajes/estados;
- deep clone del read-model;
- gráfico y límite de buckets;
- token de confirmación;
- ausencia de persistencia propia.

### Prueba runtime real

`Tools → Bistro Builder → Finanzas → 3J - Prueba runtime real`

Automática. Entra en Play Mode, recorre las cinco pantallas, prueba periodos y gráficos, valida el aislamiento modal, abre una confirmación de financiación sin mover caja, acepta después una financiación real por 3I, verifica ledger/deuda, restaura exactamente los snapshots iniciales 3A/3I y sale de Play Mode.

Gate final esperado:

`PRUEBA RUNTIME 3J SUPERADA`

con `Error/Exception/Assert: 0`.

## Gate de cierre

3J solo podrá declararse cerrado cuando se cumplan todos estos puntos:

1. compilación Unity: 0 errores;
2. endurecimiento 3A–3I: validación/autotest limpios;
3. Queen Test financiera global endurecida: SUPERADA;
4. instalador/validador/autotest 3J: limpios;
5. prueba runtime real 3J: SUPERADA;
6. revisión visual funcional aceptada por el usuario;
7. commit de instalación de escena subido y verificado.
