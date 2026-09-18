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