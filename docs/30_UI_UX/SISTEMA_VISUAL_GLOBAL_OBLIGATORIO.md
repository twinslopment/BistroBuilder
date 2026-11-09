# Bistro Builder — sistema visual global obligatorio

**Estado:** DECISIÓN VINCULANTE DEL USUARIO (09/11/2026).
**Ámbito:** TODAS las pantallas, subopciones, paneles y elementos de interfaz del videojuego, tanto del Modo Juego como del Modo Edición, respetando sus controles específicos.

## Regla principal

**Reutilizar funcionalidad o código existente NO significa aprobar ni conservar su diseño visual anterior.** Aunque los sistemas de Personal, Actividad, Carta, Economía, Proveedores, Inventario, Reservas, Marketing, Reputación, Opciones y Editor tengan implementaciones jugables, **TODAS sus interfaces deben rediseñarse visualmente** para formar un mismo sistema de diseño coherente y de calidad comercial premium.

Esto incluye, sin excepción:
- Paleta de colores y tratamientos de superficies, fondos y paneles.
- Tipografías, tamaños, jerarquías, espaciado y alineación.
- Iconos, tamaño, estilo, nitidez y estados de los iconos.
- Botones, pestañas, filtros, campos de entrada, listas, tarjetas, tablas, scroll y confirmaciones.
- Bordes, separadores, esquinas, sombras, resaltados y estados visuales (hover, focus, pressed, selected, disabled).
- Microanimaciones, transiciones y comportamiento adaptado a 1920x1080 y 1280x720.
- Consistencia entre el panel izquierdo de Actividad, el panel derecho contextual y las pantallas completas de gestión.

## Identidad y excepciones de composición

- Dirección buscada: elegante, sobria, cálida y propia de un videojuego de simulación premium; evitar apariencia de CRM, móvil o iconografía infantil.
- Dirección cromática previamente preferida: grafito cálido, piedra, marfil y acentos discretos de madera o bronce; reducir de forma sustancial el violeta/ciruela. No convertir esto en una nueva aprobación de cada detalle aún no decidido.
- Tipografías de referencia: Recoleta para títulos y una sans limpia como Inter para información/controles.
- **Una sola identidad gráfica**, aplicada de forma coherente, no pantallas clónicas: cada módulo puede tener distribución diferente según su funcionalidad.
- Respetar las decisiones específicas vinculantes: barra superior V21 con fondo negro satinado, iconos SVG blancos y microanimación V30 rápida; barra inferior del Modo Juego según su diseño aprobado; controles específicos del Modo Edición. No deshacer aprobaciones particulares al unificar estilo.
- Establecer componentes, tokens, escalas tipográficas y familias de iconos compartidos para impedir divergencias entre pantallas.

## Proceso obligatorio

1. Recuperar y auditar la funcionalidad ya programada, sin crear lógica de negocio duplicada.
2. Definir y aprobar la experiencia funcional de cada subopción.
3. **Rediseñar toda su interfaz** según el sistema visual global, incluyendo iconos, botones, colores y estados.
4. Validar visualmente la coherencia con las demás pantallas, junto con las resoluciones de PC.
5. Solo después implementar/integrar la presentación aprobada y ejecutar pruebas funcionales y visuales en Unity.
6. No llamar a una pantalla «diseño definitivo» por el simple hecho de que la lógica existente funcione.

## Evidencia y alcance de la decisión

La decisión se refiere al **objetivo obligatorio de diseño y futura implementación**. Esta anotación documental no significa que ya se hayan sustituido las interfaces de Unity. Los diseños particulares continúan pendientes de aprobación individual cuando proceda.
