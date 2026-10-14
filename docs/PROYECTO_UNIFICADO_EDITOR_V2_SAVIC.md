# Editor V2 y SAVIC en el mismo proyecto

Fecha: 14 de octubre de 2026.

## Qué proyecto abrir

En Unity Hub, elige **Add / Añadir proyecto desde disco** y selecciona:

`C:\Users\mruperez\ProyectoBB\BB_EditorV2_SAVIC`

Usa Unity **6000.3.19f1**. Esta ruta abre el mismo proyecto de trabajo que usa el chat **Analiza el Editor V2**. Las barras, la galería R9 y SAVIC están juntos. Los cambios que ese chat guarde en este proyecto se reflejan al terminar la recompilación de Unity. Un ejecutable ya generado necesita una compilación nueva para mostrar cambios posteriores.

En Unity, abre **Tools > Bistro Builder > Proyecto conjunto > Abrir Editor V2 y SAVIC**. Se abre la escena `Assets/Scenes/Prototype_Restaurant.unity` y el centro de control SAVIC. Para ver y probar las barras, pulsa **Play** y entra en **Modo edición** desde el juego. Detén Play antes de recibir contenido.

## Dónde guardar el .blend

Guarda el archivo de Blender en una carpeta de trabajo fácil de encontrar, por ejemplo `Documentos\Assets4All\MiMueble.blend`. El archivo `.blend` sigue siendo tu original editable. Assets4All entrega un paquete exportado; no necesitas copiar el `.blend` dentro del juego.

## Entregar desde Assets4All

1. En Blender, analiza el asset y aprueba su versión actual en Assets4All. Si cambias geometría, piezas o materiales, vuelve a analizar y aprobar.
2. Pulsa **Entregar a SAVIC** y elige esta carpeta de destino:

   `C:\Users\mruperez\ProyectoBB\BB_EditorV2_SAVIC\ContentInbox\Deliveries`

3. Mantén abierto este proyecto en Unity, con **Play detenido**. Eso significa aquí «SAVIC activo»: SAVIC funciona dentro de Unity, con su escaneo de entregas inicializado.
4. La entrega crea su propia subcarpeta con modelo y metadatos. SAVIC procesa paquetes completos y guarda el resultado en `SAVIC\Receipts\Assets4All`.
5. Abre **Tools > Bistro Builder > SAVIC > Open Control Center** para revisar el contenido. También puedes usar **SAVIC > Assets4ALL > Import Delivery** y seleccionar la subcarpeta de una revisión concreta. Para actualizar un artículo existente, vincúlalo en su primera entrega; las revisiones posteriores mantienen esa identidad.

**PUBLISHED** significa que el artículo ya se ha publicado en el catálogo. Entra en Play, abre Modo edición y búscalo para colocarlo. Recibir un asset no lo coloca automáticamente en el restaurante.

**NEEDS_REVIEW** significa que hay que resolver la revisión que indica SAVIC. La entrega `BB_GlassCorner_01` conservada en este proyecto sigue necesitando clasificación/publicación compatible; su recepción no equivale a publicación automática.

## Colores y cambios posteriores

La exportación incluye los cambios de material/color aprobados en Assets4All. Una actualización conserva la identidad del artículo, sus referencias de catálogo y los ajustes manuales protegidos en SAVIC. Si SAVIC ya tiene un acabado manual protegido, puede conservarlo frente al color de origen actualizado.

Si modificas el código de SAVIC, trabaja sobre este proyecto conjunto y espera a que Unity compile. Si modificas Assets4All, actualiza también la extensión instalada en Blender. Subir código a GitHub no actualiza por sí solo una extensión instalada ni un ejecutable ya compilado.

## Qué se ha validado

La integración se ensaya en una copia aislada antes de instalarla: exportación real en Blender, recepción y actualización de dos revisiones en Unity, colocación mediante V2, Apply, guardado/carga, reinicio, rollback y regresiones de interfaz y geometría. Los assets y precios usados por los diagnósticos permanecen en esa copia de prueba; la instalación usa el contenido real de SAVIC.

El proyecto conjunto es un entorno de trabajo y revisión. La rama MASTER del juego sigue siendo `integration/master-current-20260918`; esta integración no declara terminados los bloques de Editor V2 aún pendientes.
