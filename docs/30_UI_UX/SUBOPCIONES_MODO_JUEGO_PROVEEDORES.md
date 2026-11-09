# Proveedores — definición funcional por pantallas (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** propuesta **Proveedores → Proveedores PENDIENTE DE APROBACIÓN**. Catálogo, Compra inteligente y Pedidos pendientes de definir en pasos siguientes.
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

## Propuesta: PROVEEDORES → Proveedores

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

### Límites recomendados (pendientes de aprobación)

- **No** permitir que el jugador cree, edite o borre proveedores comerciales; esos registros pertenecen a herramientas internas de autoría.
- **No** realizar pedidos desde la ficha de Proveedores: el jugador selecciona ofertas en **Catálogo** o usa **Compra inteligente**, y confirma las compras mediante el sistema **Pedidos**.
- **No** añadir negociación, contratos exclusivos, multas, valoración manual de proveedores ni nuevas reglas de reputación en esta fase sin una mecánica aprobada aparte.
- **No** ocultar proveedores bloqueados: mostrar su existencia y las condiciones de progresión genera objetivos de juego.
- Reutilizar `SupplierId` y los servicios canónicos; no crear una segunda tabla, fichas permanentes ni snapshots jugables paralelos.

## Próximo paso

Solicitar aprobación funcional de **Proveedores → Proveedores**. Después definir **Proveedores → Catálogo**, continuando con Compra inteligente y Pedidos. No comenzar el diseño visual hasta terminar la definición de qué hace cada pantalla.
