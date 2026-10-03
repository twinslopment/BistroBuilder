# Carta · Gestor V3 · propuesta CSS/JS igualada a Personal V8 (03/10/2026)

## Alcance exacto
Se reutiliza la preview HTML interactiva V2.1 del Gestor. Esta versión **únicamente cambia CSS, JavaScript de presentación y las referencias a las fuentes oficiales**. No cambia la estructura funcional, las tres columnas, los campos, los datos ilustrativos ni los handlers; no toca ninguna clase C# o asset de Unity.

## Referencia visual
Imagen de Personal V8 aprobada por el usuario (lado derecho de la comparación del 03/10): patrón de tipografía, jerarquía, paleta marfil/latón, filas color miel, relieves, botones y separación.

- Títulos y nombres de cartas/reglas, botones y datos destacados: Recoleta del proyecto.
- Ayudas, contadores, descripciones, valores editables y notas explicativas: Inter oficial.
- Recoleta DEMO: los segmentos ASCII seguros se dibujan con Recoleta; los caracteres que pueden producir marcas DEMO (acentos/€) se dibujan con Inter dentro de un wrapper que no introduce huecos. Las aclaraciones «(vacío = cualquiera)» van enteramente en Inter.
- La preview usa rutas relativas al proyecto: los archivos de fuentes **no se copian ni empaquetan**.
- Se conserva el tick centrado V2.1 y la interactividad original.

## Entregables
- `docs/30_UI_UX/previews/Carta_Gestor_PersonalV8_V3.html`: preview autocontenida en HTML/CSS/JS salvo referencias a los OTF/TTF instalados.
- `docs/30_UI_UX/previews/Carta_Gestor_PersonalV8_V3_1440x814.png` y `Carta_Gestor_PersonalV8_V3_1280x720.png`: capturas reales en Chrome Windows con las fuentes oficiales.
- `C:\Users\mruperez\Downloads\Carta_Gestor_PersonalV8_V3.html`: versión lista para abrir desde Descargas con las rutas adaptadas al directorio.

## Verificación
Pruebas de JavaScript/DOM de la preview a 1440x814, 1313x740, 1280x720, 900x600 y 1920x1080: 3 columnas y listas correctas, viewport sin scroll horizontal, confirmaciones, nuevo/guardar/eliminar regla, casillas y navegación sin errores de JS; formulario y botones visibles. Captura con fuentes reales inspeccionada en 1440 y 1280.

**Estado: preview visual propuesta.** Pendiente de aceptación del usuario antes de trasladar esta equivalencia a la pantalla nativa de Unity. No declarar Unity implementado por el mero hecho de tener esta preview.
