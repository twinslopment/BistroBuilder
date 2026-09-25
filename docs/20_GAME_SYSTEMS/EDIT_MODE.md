# Bistro Builder — Modo Edición / Construcción

**Estado reconciliado:** arquitectura/core V1 cerrados en su alcance; **Construction Authoring/UX hardening activo** hasta que el flujo completo sea player-ready.

## Objetivo de producto
El jugador debe poder partir de un local y comprender cómo **crear habitaciones/paredes**, modificar estructura, colocar puertas y mobiliario, reorganizar elementos y confirmar cambios sin conocer herramientas técnicas internas.

## Reglas vinculantes
- Solo fuera de servicio.
- Construcción instantánea: no hay obreros construyendo ni tiempos artificiales.
- Barra superior y panel inferior cambian a contexto de edición con coste/presupuesto/aforo y acciones confirmar/cancelar.
- Edición consume contratos de BBSIS, Navigation, Interaction/Reservation, Economía y persistencia; no duplica sus autoridades.
- Habitaciones, paredes, puertas, mobiliario y equipamiento comparten un pipeline de validación/guardado coherente.
- Preview no hace commit; Draft/Baseline, snapping y Undo por gesto son no destructivos.

## Catálogo de artículos — UX vinculante
- El catálogo de mobiliario y equipamiento es un **panel vertical compacto en el lateral izquierdo** durante Modo Edición; sustituye temporalmente a `Actividad`.
- El viewport del restaurante permanece en el centro, el inspector contextual a la derecha y la franja inferior se reserva para herramientas/acciones de edición. No usar una barra horizontal inferior como catálogo principal.
- Estados del panel: abierto, compacto (solo categorías/iconos) y oculto temporalmente. V1 no requiere redimensionado libre.
- Cabecera con título **Catálogo de artículos**, búsqueda en tiempo real y filtros. La búsqueda opera sobre nombre legible, categoría, subtipo y etiquetas; nunca muestra IDs técnicos.
- Categorías base: Todos, Mesas, Asientos, Barra, Cocina, Almacenamiento, Iluminación, Decoración y Exterior. Puertas, ventanas, paredes y habitaciones pertenecen a Construcción, no al catálogo de artículos.
- Filtros V1: precio, desbloqueado/todos, interior/exterior y estilo cuando exista masa crítica de assets. Ordenación: relevancia, precio ascendente/descendente y nombre.
- Tarjetas en dos columnas cuando el ancho lo permita. Cada tarjeta muestra miniatura coherente, nombre legible, precio y estado especial. Variantes de color/material se agrupan bajo un mismo artículo.
- Artículos bloqueados: miniatura desaturada + etiqueta **No disponible** y explicación del requisito. Fondos insuficientes se muestran como estado económico distinto, no como bloqueo de progresión.
- Un clic en la tarjeta activa colocación mediante ghost/preview en el restaurante. Clic en viewport coloca; Esc cancela. La colocación se mantiene activa para repetición hasta cancelar.
- Favoritos y Recientes forman parte del comportamiento objetivo del catálogo para acelerar reutilización de assets.
- El scroll del catálogo es vertical y debe virtualizar/reutilizar tarjetas cuando el volumen lo requiera; no instanciar todo el catálogo simultáneamente.
- Cuando el puntero está sobre el catálogo, la UI tiene prioridad: no edge-pan, zoom accidental, colocación ni selección del mundo detrás del panel.
- El ghost comunica validez y motivo de rechazo, y muestra coste. Los cambios permanecen en Draft hasta Aplicar; seleccionar/preview no descuenta dinero ni hace commit.
- Al salir de Modo Edición, el catálogo desaparece y el lateral izquierdo vuelve a `Actividad`.
- Responsive: en 1920×1080 se priorizan dos columnas; en resoluciones más bajas puede reducirse a una columna o compactarse sin comprometer el viewport.

## Dirección gráfica aprobada — Propuesta C revisada
La referencia visual vinculante para el Catálogo de artículos y su inspector es la **Propuesta C revisada**. Sustituye las propuestas A, B y D como dirección principal.

- Mantener una estética clara, cálida y premium coherente con Bistro Builder: superficies marfil/crema, texto carbón, acentos verde oliva y sombras suaves. Evitar apariencia de herramienta CAD fría, interfaz arcade o paneles excesivamente oscuros.
- El catálogo izquierdo se presenta como panel claro de esquinas redondeadas, jerarquía limpia y respiración visual. Cabecera con título, cierre, búsqueda; debajo categorías con icono + texto, filtros secundarios y cuadrícula de dos columnas.
- Las tarjetas usan miniatura grande y homogénea, nombre y precio. Selección activa: contorno verde oliva y confirmación/check discreto. Favorito: estrella/acento cálido. Bloqueado: desaturado, candado y motivo legible.
- El viewport central sigue siendo el protagonista. La cuadrícula debe leerse integrada con el suelo. El ghost usa transparencia y contorno/huella verde de validez sin ocultar el asset ni el pavimento.
- El inspector derecho es un panel claro y vertical. Orden visual: nombre del artículo → preview grande → descripción breve → precio/ámbito → variantes de color → dimensiones → **Reglas de colocación** → estado final de colocación.
- **Reglas de colocación** se muestran como checklist con iconos verdes y, para una silla estándar de suelo, incluye como mínimo: `Se puede colocar en suelos`, `Requiere espacio libre` y `Apto para interior y exterior`. Las reglas reales dependen de los metadatos del artículo; la UI no inventa capacidades.
- El estado final del inspector usa una caja verde suave, por ejemplo `Listo para colocar`, acompañada por una explicación breve (`No hay obstrucciones en este espacio`). Un estado inválido debe conservar la misma jerarquía y explicar el motivo.
- La barra inferior mantiene fondo claro y separa **contextos/herramientas de edición** de **acciones sobre el objeto**. Puede exponer Construir/Superficies/Paredes/Decoración/Iluminación/Servicios/Otro según contexto y acciones como Eliminar/Rotar/Duplicar; no debe convertirse en un segundo catálogo de artículos.
- La barra superior conserva la identidad global de Bistro Builder, el estado `Modo Edición`, controles de cámara/edición compatibles, fecha/hora, caja y acceso a continuar/salir sin competir con el viewport.
- Recoleta se reserva para identidad/títulos cuando corresponda e Inter/sans equivalente para controles, datos y microcopy.
- Espaciado, iconografía y estados deben reutilizar el Design System del juego; no crear un lenguaje visual paralelo exclusivo del editor.

## Feedback universal aprobado
Preview/ghost, snapping suave y visible, medidas útiles, materialización breve, pulsos/reveal discretos, estados de validez y Undo/Redo. El feedback es abstracto: no simula obra física.

## Requisitos de interacción
- mover, rotar, colocar y retirar elementos sin pausas perceptibles injustificadas;
- `Confirmar`, `Validar` y `Guardar` producen feedback visible y resultado inequívoco;
- overlays/pantallas iniciales no permanecen bloqueando tras confirmar;
- crear pared/habitación es descubrible desde la UI;
- cancelar restaura estado anterior de forma segura;
- puertas/ventanas usan openings/segmentos; V1 excluye pisos múltiples, tejados, curvas y CAD avanzado.

## Gate player-ready
Local vacío → crear estancia → paredes/puerta → mobiliario → mover/eliminar → validar → confirmar → guardar → cargar → volver a editar. Debe conservar geometría/IDs, no dejar residuos y responder con fluidez.
El catálogo contextual de siete secciones está descrito en UI_UX_DEFINITIVE.md (22/09/2026). La aplicación del suelo utiliza BistroBuilderApplySurfaceFinishCommand; las mallas de acabado son únicamente una proyección visual del documento/borrador.


### Ajuste de paredes y módulos — 2026-09-22
Decisión del usuario: las paredes nuevas se trazan horizontal o verticalmente, sin diagonales, también con Shift/Alt. Los módulos aceptan orientaciones de 0/90/180/270 grados. Rotar en la barra inferior (o R) gira la pared seleccionada 90 grados alrededor de su centro, conservando identidad, longitud, acabados y huecos asociados; usa el historial de construcción para deshacer/rehacer. En modo módulo gira la vista previa antes de colocar. El trazado encadenado continúa desde el extremo restringido mostrado.

Uso: Paredes → Editar → seleccionar pared → Rotar/R. La selección de arquitectura conserva su herramienta propia. Validación: WallActionsTest PASS (botón real, ventana asociada, deshacer/rehacer, cuatro giros, módulos y trazado encadenado con Alt+Shift); core 84/84; siete pantallas y resoluciones 1280/1920/ultrawide PASS; build Windows PASS. Evidencia en Logs/WallActionsBuild3.log y Logs/WallActionsTest.txt.


### Prohibición de cruces de paredes — 2026-09-22
Decisión posterior del usuario: paredes y módulos no pueden atravesarse. Las uniones en esquina y T siguen permitidas. Crear, duplicar, girar y mover usan validación atómica; el trazado de paredes, habitaciones y módulos indica el cruce en rojo antes de confirmar. Una operación rechazada no cambia documento ni historial. Se conservan diseños antiguos para poder repararlos sin borrado automático; su validación final bloquea los cruces pendientes. La topología puede representar cruces históricos, pero esto ya no autoriza nuevos cruces en el modo edición.

Validación del bloqueo: WallCrossingFinalBuild PASS; pruebas reales de módulo, trazado, habitación, giro, movimiento y duplicación rechazados sin modificar el borrador; uniones T válidas y reparación de cruces históricos con deshacer/rehacer; core 84/84. Revisión general previa de siete pantallas y resoluciones PASS en WallCrossingBuild.log.


### Nueva partida y continuidad de paredes — 2026-09-23
Nueva partida usa una tarjeta opaca de 640 × 544, con escala ajustada a pantalla, títulos Recoleta, controles Inter y cuatro iconos vectoriales de distribución (Vacío, Compacto, Equilibrado, Amplio). Mantiene el servicio existente de creación/carga. El catálogo ofrece una sola opción Pared; se retira Pared exterior y se conserva el identificador interno existente para compatibilidad. Las paredes se representan mediante mallas continuas con remates derivados de las paredes vecinas: esquinas a inglete y encuentro de ramales en T contra la cara del tramo principal. El borrador y la materialización definitiva comparten geometría, sin cambiar las identidades ni posiciones relativas de puertas/ventanas. El cambio sustituye la repetición visual de prefabs de módulo que producía juntas y bordes visibles.

Validación 2026-09-23: OpeningAndJoinsFinalBuild PASS; menú e iconos comprobados a 1280/1920/ultrawide; esquinas interiores/exteriores coincidentes, orientación inversa, continuidad recta, uniones T, hueco de puerta y misma geometría en borrador/definitiva; core 84/84. Capturas revisadas en docs/Images/NewGame_1920.png y WallJoins_1920.png. Nueva partida usa también fondo opaco para ocultar la escena durante la elección.
