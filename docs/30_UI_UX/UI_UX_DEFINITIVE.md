# Bistro Builder — UI/UX definitiva

**Estado:** referencia canónica de diseño. La **barra superior V21 está aprobada** y su implementación definitiva en Unity permanece pendiente de validación; las demás interfaces conservan su propio estado de desarrollo.

## Composición base
- Restaurante como protagonista visual; HUD ligero y contextual.
- Navegación de gestión horizontal superior: **únicamente las diez opciones V21** documentadas en la sección siguiente.
- `Actividad` funciona como feed compacto a la izquierda.
- Panel contextual a la derecha, compacto y expandible según selección.
- Barra inferior **diferenciada del Modo Juego** para los indicadores y controles operativos, incluidos Velocidad, `Caja` y Climatología; su diseño se rige por su propia documentación. No trasladarlos a la barra superior V21.
- El Modo Edición dispone de controles propios y nunca mostrará la rueda de Servicio del Modo Juego.

## Barra superior obligatoria del Modo Juego (V21)

**Diseño aprobado y vinculante desde el 05/11/2026.** La barra superior es exactamente la definida por el prototipo visual V21, sin reinterpretar sus opciones ni añadir accesos nuevos. De izquierda a derecha:

**Logotipo original de Bistro Builder** (marca, no desplegable operativo) y estos **10 botones**, en este orden:

1. Actividad
2. Personal
3. Carta
4. Inventario
5. Proveedores
6. Reservas
7. Economía
8. Marketing
9. Reputación
10. Opciones

- Mantener la composición, proporciones, iconos SVG blancos, fondo negro satinado, selección y marca dorada del diseño V21 aprobado. La microanimación de los botones será **V30 rápida (280 ms)**.
- No añadir a esta barra otras entradas como Restaurante, Cocina, Comandas, Camareros, Sala, Progreso, Edición, Cierre del día, Caja, Climatología, fecha, velocidad o controles de simulación. La decisión de dónde ubicar el resto de funciones se gestiona fuera de esta barra.
- **El logotipo no abre ningún menú**: se utiliza únicamente como identidad de la marca.
- El Modo Edición tiene sus barras y controles propios. El menú de Servicio, todavía en diseño, **no forma parte de la barra V21 y nunca se muestra en Modo Edición**.
- Esta decisión establece el **diseño requerido**, no prueba que la barra ya esté integrada en la build de Unity.

**Fuente visual editable única:** [`BARRA_SUPERIOR_V21_V30_APROBADA.md`](BARRA_SUPERIOR_V21_V30_APROBADA.md) y `DesignReferences/TopBar/V21_V30/` (HTML, CSS, JS, logotipo original y capturas). Una referencia antigua incompatible pasa a ser histórica y no prevalece sobre esta decisión.

## Actividad — botón directo sin submenú (decisión definitiva 09/11/2026)
- Al pulsar **Actividad** desde una pantalla de gestión, se cierran los paneles abiertos, se vuelve al restaurante 3D y se muestra **Actividad** en el lateral izquierdo.
- Con el panel visible, otro clic en **Actividad** lo oculta; si está oculto, lo vuelve a mostrar. No existe un submenú para Actividad.
- El panel izquierdo conserva los filtros **Hoy, Incidencias, Oportunidades y Reservas**. **«Alertas e incidencias» no es una subopción independiente.**
- Con Actividad oculto y una incidencia crítica pendiente, el botón superior debe mantener un **indicador discreto**, sin duplicar avisos ni abrir modales rutinarios.
- El panel derecho continúa siendo contextual según selección. No modificar el flujo del Modo Edición.
- La implementación exacta y el aviso de críticos están pendientes de integración y pruebas en Unity.
- Fuente de verdad: [`SUBOPCIONES_MODO_JUEGO_ACTIVIDAD.md`](SUBOPCIONES_MODO_JUEGO_ACTIVIDAD.md).

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