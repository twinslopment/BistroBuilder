# Resumen de esta rama — Barra superior V21 + animación V30

## Objetivo
Guardar sin modificaciones la **barra superior V21 aprobada** y la **animación V30 rápida** para que Codex pueda implementarlas en Unity sin rediseñar el trabajo aceptado.

## Qué contiene
- En `DesignReferences/TopBar/V21_V30/`: `index.html`, `styles.css` y `app.js`, que forman el prototipo interactivo editable.
- Logotipo dorado **original**, conservado sin alteraciones.
- Diez accesos en este orden: Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación y Opciones.
- Iconos SVG blancos sobre fondo negro satinado, selección iluminada discretamente.
- Animación V30 rápida de 280 ms; el modo lento del prototipo es solo para comparar.
- Capturas de referencia a resoluciones 1280×720 y 1920×1080.
- Especificación original y documento `docs/30_UI_UX/BARRA_SUPERIOR_V21_V30_APROBADA.md`.
- Esta rama se creó sobre la rama V30, por lo que **también incluye el código C# de la microanimación global**.

## Pruebas y estado
- Se verificó que HTML, CSS, JS y logotipo conservan los SHA-256 de los originales.
- Pruebas del prototipo: diez botones, clic repetido, teclado, movimiento reducido, resolución 1280 y 1920, sin errores de JavaScript.
- **Pendiente:** implementar la apariencia V21 en la UI de Unity, conectar los botones a pantallas reales y validar la build. No está integrada en la versión jugable.

## Referencia
Commit del prototipo visual: `092cb7cc`. El logo es únicamente la marca: **no abre un menú operativo**. La rueda de Servicio se desarrolla por separado.
