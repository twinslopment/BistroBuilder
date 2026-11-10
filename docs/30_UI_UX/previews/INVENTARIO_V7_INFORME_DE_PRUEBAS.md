# Bistro Builder — Inventario · V7 responsive

**Fecha:** 10 de noviembre de 2026  
**Estado:** prototipo interactivo para revisión visual. **No se integra en Unity.**

## Adaptaciones realizadas

- Composición de gestión a pantalla completa como Personal; barra lateral y área principal comparten el espacio sin desbordamiento de ventana.
- Escalado real por tramos de anchura y altura, con márgenes, tamaños de fuente, botones, filas, títulos, filtros y decoración adaptados.
- Listados con desplazamiento interior; la cabecera y el pie permanecen visibles cuando no caben todas las filas.
- Barra de filtros y búsqueda adaptativa; en formatos estrechos se priorizan los filtros y la búsqueda, ocultando el selector de orden para evitar saturación.
- Panel de detalle y formulario de ajuste dimensionados dentro de la zona de trabajo, incluso en 1024×768.
- 2560×1440 y 3440×1440 con escalado adicional; las **imágenes raster de referencia** de las barras se mantienen proporcionadas, por lo que puede verse una banda oscura lateral en formatos ultrapanorámicos.
- Preferencia CSS `prefers-reduced-motion` respetada en el panel propio de Inventario.

## Pruebas automatizadas en Chromium

Se ejecutó un recorrido de diseño y de funcionalidad en las **9 resoluciones**: 1024×768, 1280×720, 1366×768, 1440×900, 1600×900, 1904×910, 1920×1080, 2560×1440 y 3440×1440.

En **cada** resolución se verificó:

- Existencias, Alertas, Movimientos y Recepciones, con datos visibles y sin errores de JavaScript.
- Barra superior y barra inferior encajadas en los límites del contenido central.
- Sin desbordamiento horizontal ni vertical de página.
- Título completo, sin recorte lateral.
- Tabla con altura operativa y desplazamiento interno.
- Apertura del detalle del ingrediente dentro de los límites de la ventana.
- Apertura/cierre del diálogo de ajustes dentro de los límites de la ventana.

Pruebas funcionales adicionales a 1280×720:

- Búsqueda de ingrediente y filtros de urgencia correctos.
- Validación que impide reducir más stock que el disponible.
- Aumento de stock +5 simulado, actualización y movimiento de ajuste generado.
- Navegación de recepción a movimientos, reinicio y movimiento reducido.

**Resultado:** 9/9 resoluciones verificadas; ninguna excepción JavaScript observada. El script `test_responsive_v7.py` registra los detalles en `test_report.json`.

**Cambio dinámico de tamaño:** con Alertas → Caducidad seleccionado, se redimensionó una misma sesión de 1920×1080 a 1280×720, 3440×1440, 1024×768 y de vuelta; selección y 4 resultados se conservaron.

## Limitaciones conocidas

- Los datos, filtros y ajustes son simulados en HTML. No invocan el inventario canónico de Unity.
- Las barras superior e inferior son **capturas estáticas de la referencia aprobada de Personal**, no las implementaciones originales animadas V21/V30 y E5.5. No se han modificado esos sistemas.
- La tipografía caligráfica exacta de la última referencia de Personal todavía no está identificada. La cabecera lateral usa `Segoe Script` con fallback; el cuerpo declara `Inter` con fallback. **No se afirma equivalencia exacta de fuentes con Personal.**
- Esta validación es de renderizado Chromium y funcionalidad del prototipo, no de Unity, GPU, ni jugabilidad.

## Cómo probarlo

1. Abrir `BistroBuilder_Inventario_Responsive_V7.html` con Chrome o Edge.
2. Pulsar F11 si se desea pantalla completa. Cambiar tamaño de ventana para comprobar la adaptación.
3. Probar Existencias, Alertas, Movimientos y Recepciones; seleccionar filas para abrir el detalle.
4. En Existencias, abrir «Ajustar existencias» y confirmar un ajuste simulado; comprobar la aparición del movimiento y «Restablecer».

El prototipo es autocontenido y no depende de Internet, de librerías externas ni de archivos tipográficos distribuidos.