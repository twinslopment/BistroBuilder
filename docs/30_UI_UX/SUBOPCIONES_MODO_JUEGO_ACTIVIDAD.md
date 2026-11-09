# Actividad — comportamiento definitivo del botón V21 (Modo Juego)

**Estado:** DISEÑO FUNCIONAL APROBADO, 09/11/2026. Esta decisión posterior sustituye la estructura anterior de «subopciones» de Actividad.
**Integración en Unity:** PENDIENTE DE VERIFICACIÓN en la build de juego; no declarar funcional lo no probado.

## Decisión principal: Actividad NO tiene submenú

El botón **Actividad** de la barra superior V21 es un **acceso directo** que muestra u oculta el panel izquierdo **Actividad**, sin pantalla separada y sin desplegar subopciones.

### Comportamiento aprobado

1. **Primer clic desde un panel de gestión abierto:** cerrar el panel de gestión, regresar a la escena 3D principal y mostrar el panel izquierdo de Actividad.
2. **Clic con Actividad visible:** ocultar el panel izquierdo y dejar libre la escena 3D.
3. **Clic con Actividad oculta:** volver a mostrar el panel izquierdo, sin abandonar la escena 3D.
4. El botón no cambia el estado del servicio, la partida ni la velocidad de la simulación.
5. Se conserva el panel contextual **derecho** de los elementos seleccionados; no se crea un tercer panel ni otra pantalla de alertas.

### Contenido del panel izquierdo existente

- Acontecimientos recientes del restaurante con su prioridad e indicación de tiempo del juego.
- Filtros internos: **Hoy, Incidencias, Oportunidades y Reservas**.
- **«Alertas e incidencias» NO es una subopción ni una pantalla independiente**: se consulta con el filtro **Incidencias**.
- Los avisos críticos sin resolver conservan su prioridad y persistencia; el panel se alimenta del sistema de eventos existente, no de otro feed.
- Al pulsar una entrada, localizar/seleccionar el elemento afectado cuando exista un destino válido y ofrecer su información y acciones en el panel derecho según el contexto.
- Mantener agrupación y antispam para evitar notificaciones repetitivas.

### Avisos críticos con Actividad oculta

**Diseño aprobado:** cuando exista una incidencia crítica pendiente y el panel izquierdo esté oculto, el icono **Actividad** de la barra superior debe mostrar un **indicador discreto** de alerta. Al abrir Actividad se accede a los avisos. No imponer una ventana modal rutinaria; no ocultar la incidencia real ni eliminarla por cerrar el panel. La forma exacta del indicador se definirá al diseñar la presentación de V21.

### Responsabilidades

- **Actividad izquierda:** feed con filtros y avisos; se alterna visible/oculto con el botón superior.
- **Contexto derecha:** detalle y acciones del elemento seleccionado; se muestra por selección del jugador, independientemente de si Actividad está abierto.
- **Escena 3D:** área visual principal, siempre visible al usar Actividad.
- **Modo Edición:** excluido; conserva sus barras y controles específicos.

## Relación con decisiones anteriores

- La opción A «Vista del restaurante» queda **integrada** en el primer clic de Actividad desde una pantalla de gestión; deja de ser subopción.
- «Actividad reciente» es el contenido del panel izquierdo, no subopción.
- «Alertas e incidencias» se elimina como subopción; su función permanece dentro del filtro «Incidencias».
- La decisión más reciente **prevalece** sobre el registro anterior D-035; nueva decisión D-036.

## Código y estado revisados

- Rama de documentación/visual V21: `feature/ui-topbar-v21-approved-20261105`.
- Sistema avanzado de Actividad, desarrollado en otra rama: `feature/activity-runtime-current`.
- Archivos de referencia: `BistroBuilderUiShell.cs`, `BistroBuilderUiShell.TopNavigation.cs`, `ActivityPanelController.cs`, `ActivityFeedRuntime.cs` y `ActivityTargetRouter.cs`.
- El filtro `ActivityFilter.Incidents` existe en el sistema avanzado.
- El comportamiento de clic único descrito aquí y el indicador de incidencia crítica **deben implementarse/integrarse y probarse** en Unity; esta actualización documental no equivale a cambiarlos en la build.

## Criterios de aceptación para la implementación

- Un solo botón Actividad, sin desplegable, abre/cierra el panel izquierdo y vuelve a la escena 3D desde pantallas de gestión.
- Los filtros existentes siguen operativos y no se pierden eventos al ocultar el panel.
- El indicador de crítico pendiente funciona con Actividad oculto y desaparece únicamente cuando no hay críticos pendientes.
- Las entradas encaminan al destino disponible; el panel derecho muestra el contexto correspondiente cuando se selecciona un elemento compatible.
- No aparecen menús de Actividad en Modo Edición ni se altera el orden de los 10 botones V21.
- Validación visual 1920×1080 y 1280×720 y pruebas de regresión de menús, persistencia y alertas.
