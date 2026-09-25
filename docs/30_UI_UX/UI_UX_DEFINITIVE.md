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
### Catálogos contextuales del modo edición — 22/09/2026

Las siete opciones de la barra inferior abren secciones independientes: Construir, Superficies, Paredes, Decoración, Iluminación, Servicios y Otro. Se conservan las barras aprobadas. Cada sección tiene título, búsqueda y filtros propios; cambiar de sección cancela la colocación provisional y limpia el inspector anterior.

- Construir conserva los artículos integrados y su inspector real. Decoración, Iluminación, Servicios y Otro filtran por categoría y subcategoría autorada; una categoría vacía lo indica explícitamente.
- Paredes ofrece pared interior/exterior, puerta, ventana, separador bajo y módulo. Crear, Editar y Abrir hueco usan las herramientas existentes; habitación, orientación/longitud de módulo, ajustes de huecos y zonas siguen accesibles.
- Superficies muestra los materiales existentes del kit. El suelo de caliza se aplica a una habitación cerrada mediante el borrador canónico, con tarifa por área, deshacer y confirmación. El enlucido se puede consultar; su aplicación independiente aún no está disponible.
- Las miniaturas de arquitectura se renderizan desde el kit del proyecto. Los modelos adicionales de las imágenes de referencia no se incorporan como artículos ficticios.
- El diseño inicial conserva guardar recuperación y validar/continuar. Los paneles antiguos de construcción se ocultan para evitar superposiciones.

Límite actual: los catálogos de iluminación, servicios y otros elementos necesitan assets autorados. Los controles específicos de lámparas y funciones de servicio requieren sus definiciones y comportamiento; no se presentan como funciones operativas sin esa integración.
Validación 22/09/2026: siete rutas y títulos, miniaturas, puertas/ventanas, filtros, cerrar/reabrir y mallas de iconos: PASS. Capturas 1920×1080, 1280×720 y 3440×1440. Núcleo de construcción en escena aislada: 84 OK / 0 fallos. Build Windows: PASS (13 warnings).

Corrección visual del modo edición: se retira el overlay IMGUI duplicado de diseño inicial; permanece el panel compacto con guardar recuperación y validar. Eliminar/Rotar/Duplicar conservan texto e iconos opacos, borde visible y estado desactivado legible. Suavizado SMAA High limitado a la cámara durante edición, restaurando su configuración al salir. Cuadrícula con mayor separación del suelo y líneas menores más robustas. Solo se regeneran miniaturas automáticas a 512 px; se preservan las manuales y la resolución global.


### Ajuste de paredes y módulos — 2026-09-22
Decisión del usuario: las paredes nuevas se trazan horizontal o verticalmente, sin diagonales, también con Shift/Alt. Los módulos aceptan orientaciones de 0/90/180/270 grados. Rotar en la barra inferior (o R) gira la pared seleccionada 90 grados alrededor de su centro, conservando identidad, longitud, acabados y huecos asociados; usa el historial de construcción para deshacer/rehacer. En modo módulo gira la vista previa antes de colocar. El trazado encadenado continúa desde el extremo restringido mostrado.


### Prohibición de cruces de paredes — 2026-09-22
Decisión posterior del usuario: paredes y módulos no pueden atravesarse. Las uniones en esquina y T siguen permitidas. Crear, duplicar, girar y mover usan validación atómica; el trazado de paredes, habitaciones y módulos indica el cruce en rojo antes de confirmar. Una operación rechazada no cambia documento ni historial. Se conservan diseños antiguos para poder repararlos sin borrado automático; su validación final bloquea los cruces pendientes. La topología puede representar cruces históricos, pero esto ya no autoriza nuevos cruces en el modo edición.


### Nueva partida y continuidad de paredes — 2026-09-23
Nueva partida usa una tarjeta opaca de 640 × 544, con escala ajustada a pantalla, títulos Recoleta, controles Inter y cuatro iconos vectoriales de distribución (Vacío, Compacto, Equilibrado, Amplio). Mantiene el servicio existente de creación/carga. El catálogo ofrece una sola opción Pared; se retira Pared exterior y se conserva el identificador interno existente para compatibilidad. Las paredes se representan mediante mallas continuas con remates derivados de las paredes vecinas: esquinas a inglete y encuentro de ramales en T contra la cara del tramo principal. El borrador y la materialización definitiva comparten geometría, sin cambiar las identidades ni posiciones relativas de puertas/ventanas. El cambio sustituye la repetición visual de prefabs de módulo que producía juntas y bordes visibles.


### Simplificación de herramientas de paredes — 2026-09-23
Por petición del usuario se elimina la pestaña Abrir hueco. Quedan Crear y Editar. Puertas y ventanas continúan como artículos del catálogo de paredes.
