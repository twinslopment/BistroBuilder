# Bistro Builder — Bloque 04 · Proveedores — V1 aprobada

**Fecha de aprobación:** 10 de noviembre de 2026.

**Estado:** Diseño visual y flujo funcional del prototipo HTML/CSS/JavaScript **aprobados por el usuario**. Se conserva esta versión como referencia congelada para futuras iteraciones. **No se ha integrado en Unity**, ni se ha validado el comportamiento dentro de la partida.

## Cuatro apartados vinculantes
- Proveedores: listado, filtros, fichas, fiabilidad/calidad, estado de bloqueo y desbloqueo.
- Catálogo: ofertas, formatos, disponibilidad, búsqueda/filtros, cantidades y generación de borradores.
- Compra inteligente: estrategias Ahorrar/Equilibrado/Urgente y generación de borradores sin confirmación automática.
- Pedidos: consulta de borradores/pedidos, revisión, confirmación explícita y cancelación conforme al estado.

## Diseño vinculante
- Identidad heredada de Inventario V7 aprobado y del sistema visual global de Bistro Builder (panel lateral, tipografía, filas crema, botones bronce, densidad responsive).
- Barra superior V21 y microanimación V30 del prototipo. Barra inferior E5.5 presente **como imagen estática de referencia** en el HTML, no como controlador jugable.
- Movimiento reducido / brillo al 50% en patrones compartidos, sin alterar las barras maestras de Unity.

## Alcance de la aprobación
- El HTML usa proveedores, ofertas, importes y pedidos **simulados**. No hay conexión con los servicios canónicos de Proveedores 2.3, Inventario, Economía ni guardado/carga.
- Los estados y acciones de la maqueta se basan en `BistroBuilderSupplierPlayerRuntimeView.cs`, en los estados del modelo de órdenes y en los contratos del motor de Compra Inteligente.
- El archivo solicitado `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_PROVEEDORES.md` no se encontró en las ramas consultadas; reconciliarlo con el diseño cuando esté disponible antes de implementación real.

## Pruebas del prototipo
- 16/16 comprobaciones funcionales en HTML, cero errores JavaScript registrados.
- 7 resoluciones examinadas, desde 1024×768 hasta 3440×1440, sin desbordamientos ni solapamientos detectados.
- Evidencias: `INFORME_PRUEBAS_FUNCIONALES.json`, `INFORME_RESPONSIVE.json`, capturas y `LEEME_Proveedores_Bloque04.md`.

## Conservación y modificaciones futuras
Esta V1 es un punto de referencia **no reemplazable**: futuras correcciones deben hacerse en una V2 o en una rama nueva, conservando la V1. No mezclar todavía en la rama principal de integración.