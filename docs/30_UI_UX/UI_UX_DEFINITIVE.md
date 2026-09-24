# Bistro Builder — UI/UX definitiva

**Estado:** diseño vinculante; implementación/hardening en `feature/21a-ui-ux-definitive`.

## Composición base
- Restaurante como protagonista visual; HUD ligero y contextual.
- Navegación de secciones **horizontal en la parte superior**: Actividad, Economía, Personal y demás secciones globales.
- `Actividad` funciona como feed compacto a la izquierda.
- Panel contextual a la derecha, compacto y expandible según selección.
- Franja operativa inferior para acciones del contexto actual.
- Velocidad, `Caja` y demás indicadores globales operativos se integran en la zona superior; no crear un menú lateral permanente.

## Interacción
- Seleccionar una mesa recentra suavemente la cámara **sin zoom automático**.
- Doble clic en mesa abre detalle/comanda cuando corresponda.
- Clic en evento de Actividad centra y selecciona su objetivo.
- Clic en camarero, cocina o barra centra y abre su panel contextual.
- `Esc` o clic en vacío limpia la selección/cierra contexto apropiado.
- Cambiar selección debe transicionar el contexto sin reconstruir visualmente toda la interfaz.

## Estados visuales
HUD operativo por estados **Normal / Atención / Crítico / Resolución**. Verde = correcto; ámbar = atención; rojo solo para crítico; azul/gris = neutro. Notificaciones agrupadas, sin spam ni modales rutinarios. `Actividad` muestra aproximadamente 5–8 eventos útiles.

## Tipografía y tono
Recoleta para títulos/encabezados cuando encaje con la identidad visual; sans limpia tipo Inter para interfaz. Estética elegante, sobria y legible; evitar barroquismo y ornamentación que compita con el restaurante.

## Modo Edición — composición visual
Durante Modo Edición, `Actividad` deja temporalmente su lateral al **Catálogo de artículos**. La dirección visual aprobada es la **Propuesta C revisada**:

- catálogo claro y vertical a la izquierda;
- restaurante/viewport como área dominante;
- inspector contextual claro a la derecha;
- herramientas y acciones en una franja inferior;
- superficies marfil/crema, tipografía carbón, acentos verde oliva y sombras suaves;
- tarjetas de artículo en dos columnas cuando haya ancho suficiente;
- ghost y validez en verde sin tapar la lectura del suelo;
- inspector con preview, variantes, dimensiones, **Reglas de colocación** y estado final explicado;
- la barra inferior no duplica el catálogo y separa navegación de edición de operaciones sobre el objeto.

La propuesta B no define el estilo general; solo se incorpora de ella el patrón de checklist de `Reglas de colocación` dentro del inspector claro de la propuesta C.

## Principios
UI contextual y progresiva, PC como referencia. Presentation lee snapshots y emite comandos: no duplica lógica ni se convierte en autoridad de dominio. La cámara ayuda a comprender el restaurante y nunca se convierte en protagonista de la experiencia.

## Nueva partida — decisión 24/09/2026
La composición aprobada es marfil clásico panorámico, con tres opciones de preparación del restaurante y botones Al pase. Véase [diseño aprobado y alcance](NEW_GAME_APPROVED.md). Maqueta interactiva disponible; integración de esta pantalla en Unity pendiente.
