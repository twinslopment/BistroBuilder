# Barra superior V21 + V30 rápida — DISEÑO APROBADO

**Estado de diseño: VINCULANTE.** Diseño V21 y movimiento V30 rápido aprobados por el usuario.

**Estado de integración: FUENTE VISUAL/PROTOTIPO, NO INSTALADO EN LA BUILD DEL JUEGO.**

## Fuente única y archivos

El prototipo aprobado, su código editable y la marca original están en:

`DesignReferences/TopBar/V21_V30/`

- `index.html`: estructura real de 10 botones y SVG originales de la versión aceptada.
- `styles.css`: acabado, tipografía, dimensiones, estados, adaptación y keyframes V30.
- `app.js`: selección, clics, teclado, repetición y controles de prueba.
- `assets/Logotipo_Bistro_Builder_Original.png`: logotipo original EXACTO, sin tratamiento ni modificación.
- `V21_V30_1920_reference.png` / `V21_V30_1280_reference.png`: capturas del prototipo.
- `ESPECIFICACION_V21_V30.md`: especificación técnica original íntegra.
- `test_v30.py`: prueba original del prototipo; utiliza rutas temporales de autoría, por lo que se conserva como evidencia, no como runner CI listo para ejecutar.

**No usar otras exploraciones de barra ni imágenes generadas como fuente de verdad.** No reconvertir los iconos a otros dibujos sin aprobación.

## Barra superior, izquierda a derecha

1. Marca Bistro Builder: visual, no lanza menús operativos.
2. Actividad
3. Personal
4. Carta
5. Inventario
6. Proveedores
7. Reservas
8. Economía
9. Marketing
10. Reputación
11. Opciones

Son **10 botones de navegación**, además del logotipo. La rueda de servicio, en estudio, es independiente y no se incluye aquí.

## Identidad V21

- Negro satinado y fondo discreto.
- SVG blancos y trazos nítidos; no neón azul, no iconos arcade ni mezcla azul-dorado.
- Logotipo dorado original intacto.
- Separadores finos, selección iluminada controlada con línea inferior.
- Diseñar y verificar al menos a 1920×1080 y 1280×720.
- No cambiar el orden ni el nombre de los accesos.

## V30 rápida obligatoria

- Duración normal: 280 ms (la cámara lenta 840 ms del prototipo es **solo una herramienta de prueba**).
- Presión 100 % → 94 %, pequeño rebote al 103 %, vuelta a 100 %.
- Destello blanco breve, sin colores vivos.
- Se reinicia en clics consecutivos sobre el mismo botón.
- Respetar movimiento reducido, teclado y estados interactuable/deshabilitado.
- Este efecto se aplica a los botones presentes y futuros del juego. **Modo Edición queda excluido hasta decisión posterior.**

## Integración en Unity

La rama que contiene esta documentación **hereda** el trabajo en C# de `feature/ui-v30-global-button-feedback-20261104` (commit `b2fe71f7`):

- `Assets/Scripts/Presentation/UI/BistroBuilderV30ButtonMotion.cs`
- `Assets/Scripts/Presentation/UI/BistroBuilderV30ButtonInstaller.cs`

Para construir la barra jugable en Unity, utilizar `BistroBuilderUiShell.TopNavigation.cs` y el sistema de navegación existente. Asociar cada opción a su pantalla real. No crear una segunda barra ni rutas de negocio paralelas. El HTML es una referencia editable, no un HUD de Unity instalado.

## Criterios de aceptación de la futura integración

- [ ] Correspondencia visual comprobada con la referencia a 1920×1080 y 1280×720.
- [ ] Los diez botones abren sus vistas reales, sin acciones de prueba ni navegación ficticia.
- [ ] Logotipo original sin alteraciones.
- [ ] Animación V30 rápida en todos, con clic repetido y movimiento reducido.
- [ ] Ausencia de solapamientos, errores de Unity y regresiones en Carta V3 y demás sistemas.
- [ ] Modo Edición sin cambios.

## Pruebas ejecutadas al preparar esta referencia

- Verificados los SHA-256 del HTML, CSS, JS, prueba original, especificación original y logotipo: coinciden con el paquete exacto previamente aprobado.
- Prueba con Chromium/Playwright a 1280×720 y 1920×1080: **PASS** en 10 accesos, animaciones, clics repetidos, teclado, tamaño, logo y ausencia de errores JS.
- Movimiento reducido: **PASS**.
- **No** se ha validado la barra V21 dentro de Unity ni en una build jugable.
