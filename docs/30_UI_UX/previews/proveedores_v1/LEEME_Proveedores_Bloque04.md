# Bistro Builder — Bloque 04 · Proveedores · Previsualización V1

**Estado:** para revisión y aprobación visual. **NO implementado en Unity, ni integrado en Git.**

## Alcance
- Apartados: Proveedores, Catálogo, Compra inteligente, Pedidos.
- Diseño heredado de **Inventario V7** aprobado: composición a pantalla completa, panel lateral, tipografía, botones, filas alternas y fichas contextuales.
- Barra superior **V21 con código real del prototipo y animación V30**; botón Proveedores activo. Los otros nueve accesos no cambian de pantalla en esta maqueta.
- Barra inferior **E5.5: imagen estática de referencia** tomada de la preview aprobada de Inventario V7. No se han implementado controles jugables de caja, satisfacción, reloj o velocidad en la demo.
- Datos de proveedores, productos, precios y pedidos **ilustrativos**; no representan una exportación del contenido de la partida.

## Prueba rápida
Abre `BistroBuilder_Proveedores_Bloque04_Preview_V1.html` en Chrome/Edge. No necesita servidor ni conexión. Prueba estas rutas:

1. **Proveedores:** filtrar «Bloqueados», seleccionar Selección Gourmet, comprobar los datos y el estado de bloqueo. Volver a Todos, abrir otro proveedor y usar «Ver su catálogo».
2. **Catálogo:** filtrar «Promociones», buscar «Merluza», abrir ficha, modificar número de envases y pulsar «Añadir a borrador». Si la oferta está bloqueada o no disponible, la acción aparece deshabilitada.
3. **Compra inteligente:** comparar Ahorrar, Equilibrado y Urgente; abrir una propuesta y generar borradores. No se confirman automáticamente.
4. **Pedidos:** seleccionar un borrador, revisar los importes y el pedido mínimo, confirmar con un segundo diálogo. Probar cancelación en un pedido elegible. El resultado solo altera los datos de la página.
5. **Restablecer:** botón circular superior, que devuelve los datos simulados al punto inicial.

## Reglas de dominio respetadas / pendientes
- Catálogo y mercado son autoridades existentes; la interfaz definitiva deberá alimentarse de sus datos reales, no de las muestras de este HTML.
- El pedido se configura por formato/envase, se incorpora como borrador y requiere confirmación explícita.
- La maqueta bloquea la confirmación por debajo de un mínimo por proveedor ilustrativo, para demostrar el estado de botón deshabilitado; la validación real del pedido pertenece al servicio canónico.
- Las estrategias Ahorrar/Equilibrado/Urgente se muestran con comparaciones simuladas: los costes, plazos y riesgos **no son cálculos del motor 2.3F**.
- Se diferencian los estados Draft, Confirmed, PendingDelivery, InDelivery, Delivered, Cancelled, coherentes con el dominio del repositorio.
- No se alteran las autoridades existentes de compra, finanzas, inventario, recepción ni persistencia.

## Documentación consultada y limitación
Se buscaron el archivo solicitado `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_PROVEEDORES.md` en `main`, `integration/master-current-20260918`, `design/actividad-visual-compacta-20261109` y `design/inventario-responsive-v7-20261110`, así como en la Biblioteca de archivos. **No estaba accesible en esas ubicaciones.** Se ha tomado como contrato funcional existente el código actual del repositorio:
- `Assets/Scripts/Presentation/Suppliers/BistroBuilderSupplierPlayerRuntimeView.cs` (4 secciones, acciones y estados de UI).
- `Assets/Scripts/Domain/Suppliers/Orders/BistroBuilderSupplierPurchaseOrderModels.cs` (estados de pedido).
- `Assets/Scripts/Application/Suppliers/BistroBuilderSupplierSmartPurchaseService.cs` (motor canónico separado).
- `docs/30_UI_UX/BISTRO_BUILDER_DESIGN_SYSTEM_GLOBAL_V1.md` (identidad global) y la previsualización Inventario V7.

Cuando aparezca el Markdown de subopciones específico, debe reconciliarse antes de aprobar la implementación definitiva.

## Pruebas técnicas realizadas
- 16 verificaciones de interacción en navegador, 0 errores JavaScript.
- Capturas de los 4 apartados a 1920×1080 y 1280×720.
- Validación responsive a 1024×768, 1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440 y 3440×1440; no se observaron desbordamientos de página, superposiciones HUD/panel o recortes de título.
- Prueba de cambio de ventana conservando pestaña y búsqueda.

**Nota:** Los resultados son pruebas del HTML, no de Unity Play Mode, no de la partida real ni del servicio de compras real.