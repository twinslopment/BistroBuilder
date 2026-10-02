# CARTA — Gestor V2.1 (corrección de casillas) · 02/10/2026

## Alcance
Revisión de la pantalla **Mis cartas / Reglas de activación / Detalle de la regla** bajo la identidad de Personal V8. No se modifican las entidades ni los servicios del dominio.

## Corrección solicitada
- En la preview CSS, se elimina el carácter de texto inline utilizado como marca de checkbox. El tick se dibuja con un pseudoelemento geométrico absolutamente centrado, independiente de la línea base tipográfica.
- Casillas de **18 x 18 px** en la preview; misma alineación para regla activa, desayuno, comida, cena y días D–S. La semana usa 7 columnas regulares.
- El estilo mantiene foco de teclado visible y el estado verificado del input HTML real.
- Se desactivan las ligaduras problemáticas de la Recoleta DEMO de los títulos, sin importar/copiar fuentes.
- En la interfaz nativa Unity, `BistroBuilderMenuEditorUiFactory.CreateToggle` conserva un área de 20 x 20, con un Text `✓` Inter Regular centrado como `Toggle.graphic`. El fondo alterna marfil/miel y no usa ColorTint automático.
- Confirmación de eliminación de carta o regla: no actúa sobre el servicio sin aprobación expresa del jugador.

## Otras condiciones visuales mantenidas
Marco marfil/latón, paneles crema, Recoleta para encabezados ASCII seguros, Inter para copy secundario y glifos defectuosos de DEMO, filas seleccionadas miel, botones destructivos rojos, iconografía oficial BBIconCatalog en Unity. El modal nativo conserva `BistroBuilderManagementSafeArea` con HUD inferior 76 unidades y top inset dinámico de la barra principal; overlays se superponen a Actividad/Contexto. Se conservan los scrolls de listas y formulario.

## Previews (no conectadas a datos de partida)
- `docs/30_UI_UX/previews/Carta_Gestor_Interactivo_V2_1.html` con fuentes originales del propio proyecto mediante referencias locales (ningún binario de fuente empaquetado).
- Capturas reales de Chrome en 1440x814, 1280x720 y 1920x1080. No son capturas de la Game View de Unity.
- Copia local para abrir desde Downloads: `C:\Users\mruperez\Downloads\Carta_Gestor_Interactivo_V2_1.html`.

## Gates
- Unity 6000.3.19f1, prueba reversible `BistroBuilderMenuVisualV1RuntimeProbe.RunBatch` en la escena `Prototype_Restaurant`: **24 PASS / 0 FAIL**.
- Test añadido para tick centrado en el toggle de servicio y el de día, cambio de fondo marfil/miel, confirmación/cancelación destructiva y conservación de los tres módulos de Carta.
- Captura visual de Chrome inspeccionada en 1440 y 1280; 1920 generada para revisión. **No equivale a aprobación visual del usuario ni a una ejecución interactiva de Game View.**
- Antes de fusionar a master siguen pendientes la aceptación visual final de Carta y los gates generales de integración.
