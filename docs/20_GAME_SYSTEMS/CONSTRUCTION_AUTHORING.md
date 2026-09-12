# Bistro Builder — Construction Authoring / hardening de Bloque 18

## Estado reconciliado
El núcleo lógico de Bloque 18 llegó a cerrar una batería reportada de 84/84 autotests y se declaró V1 cerrado en un punto del desarrollo. Playtests posteriores demostraron que **la experiencia de construcción todavía no estaba lista para jugador**, por lo que Construction Authoring/18N continúa como hardening y UX activa.

## Núcleo ya cubierto
- paredes con IDs estables y uniones T/X;
- habitaciones automáticas, split/merge y `RoomId`;
- openings/puertas sobre segmentos;
- geometría y casos cóncavos;
- Undo/Redo y modelo Draft/Baseline;
- persistencia y restauración;
- integración con Economía, BBSIS y Navigation.

## Problemas reales detectados en playtest
- mover o eliminar mesas/sillas podía dejar la aplicación pensando;
- `Confirmar`, `Validar` y `Guardar` no daban feedback perceptible suficiente;
- la pantalla inicial de diseño podía permanecer visible/bloqueando;
- no era evidente cómo crear una pared o una habitación desde la UI;
- tener infraestructura correcta no bastaba para explicar al jugador qué podía hacer.

## Decisiones de hardening
- preservar núcleo autoritativo existente; no reconstruir BBACS/BBSIS/Nav/SaveGame desde cero;
- previews son no autoritativos y nunca hacen commit por sí solos;
- selección directa, snapping visible, recálculo/recentrado topológico y Undo por gesto;
- puertas/ventanas usan `Openings` + segmentos, no CSG como autoridad;
- V1 no incluye múltiples pisos, tejados, paredes curvas ni CAD avanzado;
- auditar cambios locales y ramas antes de integrar; nunca descartar trabajo ajeno automáticamente.

## Gate player-ready
Un usuario debe poder abrir Modo Edición y, sin conocer el código, crear una habitación/pared, añadir una puerta, colocar/mover/eliminar mobiliario, entender validez/coste, confirmar, guardar, cargar y continuar editando con respuesta fluida y feedback inequívoco.