# Proveedores — definición funcional por pantallas (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Proveedores → Proveedores y Catálogo APROBADAS FUNCIONALMENTE (09/11/2026)**. **Compra inteligente: propuesta funcional pendiente de aprobación**. Pedidos queda pendiente de definir. El rediseño visual y las ampliaciones técnicas continúan pendientes.
**Criterio global:** aprobar exclusivamente qué hace cada pantalla. No rediseñar ni programar Unity hasta después. Al diseñar TODOS LOS PANELES aplicar la identidad gráfica común obligatoria (`SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`) y respetar las barras V21/E5.5 aprobadas.

## Secciones comprobadas en el código existente

`BistroBuilderSupplierPlayerRuntimeView` contiene:
1. **Proveedores** (lista y ficha comercial).
2. **Catálogo** (ofertas y formatos del proveedor seleccionado).
3. **Compra inteligente** (análisis y estrategias).
4. **Pedidos** (borradores, confirmados, en tránsito y entregados).

No crear un segundo gestor de proveedores, ni mezclar compra con la edición de registros internos de autoría.

### Fuentes existentes verificadas

- `BistroBuilderSupplierAuthoringDatabase` y `BistroBuilderSupplierAuthoringRecord`: proveedor y atributos comerciales canónicos.
- `BistroBuilderSupplierProgressionService.TryGetSupplierAccess` y `IsSupplierUnlocked`: disponibilidad/bloqueo y condiciones de progreso de desbloqueo.
- `BistroBuilderSupplierPlayerRuntimeView.BuildSupplierRows`: enumera proveedores **activos**, indica disponibles/bloqueados, fiabilidad, plazo de entrega, promociones activas y progreso de desbloqueo.
- `BistroBuilderSupplierPlayerRuntimeView.RefreshSupplierDetail`: descripción, fiabilidad, plazos, pedido mínimo, portes y umbral de portes gratuitos, promociones, catálogo, modelo comercial y condiciones de desbloqueo. Botones de acceso a Catálogo, Compra inteligente y Pedidos.
- `BistroBuilderSupplierCommercialIntelligenceService`, `BistroBuilderSupplierMarketService`: cotizaciones/promociones/ofertas cambiantes. No confundir con el registro fijo de proveedores.
- `BistroBuilderSupplierPurchaseOrderService`: autoridad de pedidos; la ficha de proveedor no escribe pedidos ni inventario.
- La integración con la futura interfaz global rediseñada y el estado de las builds V21 requiere validación posterior.

## Proveedores → Proveedores — FUNCIONALIDADES APROBADAS

**Objetivo:** elegir de quién abastecerse valorando disponibilidad, especialidad y condiciones comerciales.

1. **Listado de proveedores activos** del mercado de juego, diferenciando **disponibles** y **bloqueados**. No mostrar entradas de autoría inactivas como comercios jugables.
2. **Buscar, filtrar y ordenar** proveedores por nombre, tipo/especialidad, disponibilidad y fiabilidad; ordenar por plazo de entrega y otras condiciones existentes cuando haya datos comparables. La búsqueda/filtros específicos son ampliaciones de presentación pendientes, no funciones ya confirmadas en la vista actual.
3. **Ficha de proveedor seleccionado:** nombre y descripción, especialidades, fiabilidad, plazo habitual de entrega, pedido mínimo, gastos de envío, umbral de portes gratuitos y promociones vigentes si corresponden a datos reales.
4. **Mostrar claramente los bloqueados y cómo desbloquearlos** de acuerdo con el sistema de progresión, sin inventar un botón de pago ni desbloqueos manuales.
5. **Comparar condiciones esenciales entre proveedores** sin confundir esa comparación con la de precios de productos: la comparación detallada de ofertas se realizará en Catálogo y Compra inteligente.
6. **Consultar promociones vigentes**, indicando duración real y condiciones cuando estén disponibles; no inventar ofertas ni permitir modificar precios desde la ficha del jugador.
7. **Acceso directo al catálogo** del proveedor escogido si ya está desbloqueado; al bloqueado se le muestran los requisitos y no se habilitan compras.
8. **Acceso a Compra inteligente** para sugerir abastecimiento utilizando los sistemas actuales de análisis de inventario, precios y compras.
9. **Acceso a Pedidos**, con posibilidad de consultar los pedidos relacionados con el proveedor elegido si el vínculo puede resolverse, sin generar un nuevo registro de pedidos.
10. **Actualizar al avanzar la partida:** cambios de progreso, bloqueo, fiabilidad, promociones o condiciones cuando el sistema los modifique se reflejan en la misma ficha y lista canónicas.

### Límites aprobados de la primera fase

- **No** permitir que el jugador cree, edite o borre proveedores comerciales; esos registros pertenecen a herramientas internas de autoría.
- **No** realizar pedidos desde la ficha de Proveedores: el jugador selecciona ofertas en **Catálogo** o usa **Compra inteligente**, y confirma las compras mediante el sistema **Pedidos**.
- **No** añadir negociación, contratos exclusivos, multas, valoración manual de proveedores ni nuevas reglas de reputación en esta fase sin una mecánica aprobada aparte.
- **No** ocultar proveedores bloqueados: mostrar su existencia y las condiciones de progresión genera objetivos de juego.
- Reutilizar `SupplierId` y los servicios canónicos; no crear una segunda tabla, fichas permanentes ni snapshots jugables paralelos.

## Proveedores → Catálogo — FUNCIONALIDADES APROBADAS

**Objetivo:** consultar qué ingredientes y formatos comercializa cada proveedor, comparar las ofertas reales y preparar compras sin confirmar el gasto de inmediato.

1. **Consultar catálogo del proveedor seleccionado** y sus ofertas activas. Los proveedores bloqueados muestran requisitos, pero **no permiten añadir ofertas al pedido**. No presentar registros inactivos de autoría como productos comprables.
2. **Buscar y filtrar productos** por ingrediente, nombre, categoría/tipo, disponibilidad y promociones; ordenar por precio, precio normalizado y plazo cuando los datos sean comparables. Esta búsqueda/filtros de Catálogo son una **ampliación futura de UI**, no parte verificada de la lista actual.
3. **Comparar ofertas del mismo ingrediente** entre proveedores disponibles y formatos de venta, especialmente **precio normalizado por unidad** y condiciones comerciales. La UI actual se centra en el catálogo de un proveedor; una comparación transversal requiere mejorar el flujo de consulta, sin crear cotizaciones paralelas.
4. **Consultar ficha de oferta:** ingrediente, fotografía del ingrediente cuando exista (si no, usar icono/ausencia explícita), proveedor, presentación/paquete, cantidad neta, unidad, precio de mercado, precio efectivo y coste normalizado.
5. **Ver disponibilidad real y promociones vigentes**, con descuento y fin de campaña cuando se disponga de datos; las ofertas no disponibles no pueden incorporarse a nuevas compras.
6. **Consultar condiciones:** cantidad mínima de paquetes, incrementos permitidos, plazo estimado de entrega, pedido mínimo monetario, portes y portes gratuitos cuando correspondan al proveedor.
7. **Seleccionar cantidad de paquetes** cumpliendo mínimo e incrementos; calcular subtotal estimado de la línea. No convertir paquetes en unidades arbitrarias ni ignorar cantidades canónicas del formato.
8. **Añadir o actualizar producto en el borrador de pedido** del proveedor elegido. Crear el borrador a través del servicio de pedidos cuando no exista. El clic no compra ni recibe existencias de inmediato.
9. **Revisar y confirmar desde Pedidos:** allí se valida el carrito completo, promociones vigentes, pedido mínimo, portes, total y entrega, con confirmación explícita. Las cotizaciones pueden cambiar antes de confirmar; en ese instante se recalculan y se congelan condiciones de la compra confirmada.
10. **Navegar entre secciones**: volver a ficha de Proveedores, pasar a Compra inteligente o revisar el pedido/borrador, manteniendo `SupplierId`, `SupplierOfferId` y `PurchaseOrderId` de los sistemas existentes.

### Límites técnicos verificados

- `BistroBuilderSupplierPlayerRuntimeView.BuildCatalogRows` lista ofertas activas de un proveedor desbloqueado, lee cotizaciones efectivas y muestra precio normalizado, paquete, promoción y disponibilidad.
- `RefreshCatalogDetail` incluye precio de mercado, efectivo, normalizado, descuento, duración, condiciones, plazo, cantidad mínima/incrementos y subtotal de paquetes.
- `AddSelectedOfferToDraft` comprueba acceso del proveedor, crea el borrador mediante `BistroBuilderSupplierProgressionService.TryCreatePlayerDraft` si hace falta y añade o sustituye línea mediante `BistroBuilderSupplierPurchaseOrderService.TrySetDraftLine`; **no confirma el pedido**.
- `ConfirmPendingOrder` se ejecuta en la sección Pedidos y **revalida la cotización** al confirmar para evitar condiciones caducadas.
- El mercado/autoridad comercial mantiene precios/disponibilidad y promociones; el catálogo del jugador no los modifica. Las funciones de **búsqueda avanzada, filtros y comparación entre proveedores** todavía **no están implementadas en esa pantalla** y requerirán integración y pruebas.

### Exclusiones aprobadas

- No editar precios, proveedores, formatos de envase, promociones ni ingredientes originales desde la interfaz del jugador.
- No crear un segundo carrito o catálogo de productos: reutilizar exclusivamente los borradores de `BistroBuilderSupplierPurchaseOrderService` y las ofertas vigentes.
- No realizar la confirmación de pago ni dar por recibida la mercancía desde Catálogo. La confirmación pertenece a Pedidos y la entrega/recepción a sus sistemas canónicos.
- No prometer que una oferta/importe mostrado será el precio final hasta confirmar: las condiciones dinámicas se revalidan al confirmar.

## Proveedores → Compra inteligente — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** ayudar al jugador a planificar la reposición de ingredientes según las necesidades reales y los proveedores disponibles, **sin comprar automáticamente**.

1. **Analizar necesidades reales** de ingredientes a partir de stock físico/disponible/reservado, mínimos configurados, consumo previsto cuando haya histórico, próximas caducidades y pedidos ya en camino. No inventar previsiones si faltan datos.
2. **Detectar riesgo de desabastecimiento** por ingrediente (sin riesgo, bajo, medio, alto, crítico), teniendo en cuenta el stock que quedará cuando llegue un nuevo pedido. El informe y los riesgos se derivan del motor existente.
3. **Proponer tres estrategias existentes:** **Ahorrar** (menor gasto relativo), **Equilibrado** (coste, plazo, fiabilidad y merma) y **Urgente** (priorizar evitar roturas de stock). Son preferencias de optimización, **no promesas** de coste mínimo global ni entrega garantizada.
4. **Comparar planes:** total estimado (producto y portes), proveedores utilizados, número de ingredientes recomendados, riesgos altos/críticos y si se cumplen los importes mínimos de compra. Identificar la estrategia recomendada y explicar brevemente el motivo.
5. **Desglosar cada plan:** ingrediente, proveedor/oferta, cantidad de paquetes, precio estimado, plazo, riesgo y justificación. Permitir consultar alternativas identificadas por el motor cuando existan; la interfaz actual solo presenta un resumen de hasta siete ingredientes y esta consulta completa requerirá adaptación.
6. **Tener en cuenta restricciones comerciales reales:** proveedores desbloqueados, ofertas disponibles, formatos, cantidad mínima, incrementos, portes y pedido mínimo por proveedor. Marcar como no confirmable cualquier cesta que no supere la validación requerida en lugar de forzar una compra desproporcionada.
7. **Alertar de sobrecompra y merma** si una oferta/paquete genera exceso previsto o riesgo de caducidad. Las comparaciones se basarán en los datos que realmente calcula el motor.
8. **Actualizar análisis bajo demanda** para incorporar nuevas existencias, consumos, cambios de oferta, promociones, pedidos en camino y progreso del jugador; señalar fecha/día de cálculo y diagnósticos de datos insuficientes, sin mantener como actual una propuesta obsoleta.
9. **Crear borradores a partir del plan seleccionado:** generar los borradores agrupados por proveedor mediante los servicios actuales de pedidos, solo cuando el plan sea válido. Es una **propuesta de compra**, no un cargo en Caja ni la recepción de ingredientes.
10. **Revisar y modificar en Pedidos** las cantidades y líneas propuestas antes de confirmar, revalidando precios, promociones, portes y plazos en la confirmación final. El jugador conserva siempre la decisión de comprar.

### Estado técnico comprobado

- `BistroBuilderSupplierSmartPurchaseService.TryBuildRecommendations` reúne datos del inventario canónico, previsión y política de stock, pedidos pendientes y ofertas comerciales para producir un informe.
- `BistroBuilderSupplierSmartPurchaseEngine.BuildReport` genera **Ahorrar, Equilibrado y Urgente**, recomienda una estrategia y ofrece alternativas para determinados ingredientes; valora coste, plazo, fiabilidad, desabastecimiento y merma.
- `BistroBuilderSmartPurchaseReport` diferencia datos resueltos de inventario, previsión y políticas, incluye diagnósticos y registra el día de juego del análisis.
- `BistroBuilderSupplierPlayerRuntimeView` permite actualizar análisis, seleccionar una estrategia, consultar resumen y crear borradores de ese plan para después revisar en Pedidos.
- `BistroBuilderSupplierSmartPurchaseService.TryCreateDraftFromPlan` agrupa líneas por proveedor y usa la autoridad de pedidos existente. Comprueba que los proveedores estén desbloqueados, sin saltarse el sistema de progresión.
- **Aún requieren UI adicional** el detalle íntegro de todos los ingredientes recomendados y alternativas, avisos operativos claros por datos incompletos y navegación granular a la ficha de cada propuesta. No se afirma que estén integrados en la build V21.

### Reglas de alcance

- No activar compras automáticas recurrentes ni aceptar gastos sin confirmación del jugador.
- No prometer previsiones exactas, disponibilidad futura garantizada o mejor precio absoluto. Mostrar incertidumbre y problemas cuando faltan datos.
- No crear un motor de compra, cartera, sistema de proveedores o base de stock paralelos.
- No obligar al jugador a aceptar la estrategia sugerida: puede elegir cualquiera de las viables o comprar manualmente en Catálogo.
- No inventar límites de presupuesto, negociación, reglas de financiación o compra periódica que no hayan sido definidos como mecánica aparte.

## Próximo paso

Solicitar aprobación o ajustes para **Proveedores → Compra inteligente**. Después continuar con **Proveedores → Pedidos**, y solo tras cerrar las funcionalidades pasar al diseño visual global.
