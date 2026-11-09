# Carta — definición funcional por secciones (Modo Juego)

**Fecha de inicio:** 09/11/2026.
**Estado:** «Cartas y reglas de activación» y «Platos» **APROBADAS FUNCIONALMENTE el 09/11/2026**. La conveniencia de mantener o eliminar como subopción independiente «Edición de cartas, recetas y escandallos» sigue PENDIENTE de aprobación. No confundir aprobación funcional con validación visual ni implementación en Unity.
**Regla de trabajo:** revisar código existente; aprobar funcionalidad sección por sección; **no diseñar, programar ni cambiar el estilo visual hasta cerrar el alcance funcional**. Al diseñar, aplicar a TODOS los paneles el sistema visual global obligatorio descrito en `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Separación entre sistemas

- **Carta (jugador):** administra las cartas comerciales del restaurante, el catálogo de platos disponibles para ofrecer y la configuración jugable de cada carta.
- **Catálogo Maestro de Platos (herramienta de autoría de Unity):** define platos, recetas base, fotos, ingredientes y escandallos de referencia. No sustituirlo por un segundo catálogo ni duplicarlo dentro de la UI del jugador.
- **Editor de carta:** modifica los platos ofertados, precios y condiciones de cada carta; es distinto de crear reglas automáticas que eligen qué carta está activa.
- **Carta V3:** utilizar la pantalla/código ya desarrollado como punto de partida funcional; el rediseño visual posterior debe respetar la identidad gráfica común.

## Código existente comprobado

- `BistroBuilderMenuPortfolioService`: portfolios de varias cartas por restaurante, carta efectiva, carta de reserva, forzado manual, crear desde la carta activa, duplicar, renombrar, eliminar con restricciones, reglas de activación.
- `BistroBuilderMenuActivationContextService`: calendario, reloj, servicio concreto y señales de evento/promoción.
- `BistroBuilderMenuActivationRuleRuntimeState`: cinco tipos de reglas, `Schedule`, `Season`, `Event`, `Promotion` y `Composite`, con prioridad, fechas, días de semana, servicio, horas, evento y promoción.
- `BistroBuilderMenuPortfolioRuntimeView`: vista existente para gestión de cartas y reglas; incluye operaciones básicas. Es código anterior y no supone aprobación de su diseño.
- `BistroBuilderMenuEditorService` y `BistroBuilderMenuEditSessionService`: edición de artículos y cambios aplicados/descartados; mantener separación del portfolio.
- Selección actual: si existe forzado manual, prevalece; en caso contrario se aplica la regla coincidente de mayor prioridad/especificidad; sin coincidencia se usa carta base.
- No se ha verificado la integración de esta experiencia con la barra V21, Carta V3 ni una build final de Unity.

## Cartas y reglas de activación — FUNCIONALIDADES APROBADAS

**Objetivo:** organizar varias cartas comerciales y definir **cuál se ofrece automáticamente** según el día o las circunstancias del restaurante, sin editar aquí recetas/escandallos.

1. **Consultar todas las cartas** del restaurante: nombre, situación (base, activa, programada, forzada) y número de platos, cuando ese dato exista en la fuente de verdad.
2. **Saber cuál está activa y por qué:** mostrar «carta base», regla que se cumple o activación manual.
3. **Crear una carta** partiendo de la activa, **duplicar** otra y **cambiar nombres** sin crear platos nuevos ni duplicar el Catálogo Maestro.
4. **Eliminar cartas** con confirmación y restricciones: conservar al menos una, no eliminar la activa/base/forzada ni una referenciada por reglas sin resolver primero esas dependencias.
5. **Elegir carta base:** se utiliza cuando no coincide ninguna regla de activación.
6. **Forzar temporalmente una carta:** decisión manual que prevalece sobre las reglas; debe ofrecerse una forma clara de **volver a la activación automática**.
7. **Definir reglas automáticas:** por comida/cena, día de la semana, fechas/temporada, horario, evento y promoción; combinar condiciones cuando corresponda.
8. **Dar prioridad a las reglas** y explicar conflictos: una sola carta operativa; si dos reglas coinciden, gana la de mayor prioridad y después la más específica. Evitar la ambigüedad de resultados.
9. **Comprobar la programación:** previsualizar qué carta se serviría para una fecha y servicio futuros; no cambiar la carta real al hacer la comprobación. Esta herramienta de simulación de escenario puede requerir una ampliación de la presentación.
10. **Proteger las comandas durante el servicio:** APROBADO: los cambios que alteren la carta operativa durante un servicio en curso **no deben aplicarse de inmediato**; deben diferirse al siguiente servicio cuando la arquitectura lo permita, o bloquearse con una explicación hasta que el restaurante cierre. Esta salvaguarda es un **requisito de implementación futura**, no está verificada en el runtime existente.

**Límites de alcance aprobados:**
- En esta sección NO se editan ingredientes, recetas ni escandallos; tampoco los precios/platos individuales: eso se definirá después en «Platos» y «Edición de cartas».
- Los eventos y promociones deben proceder de sus sistemas de juego, no de un botón de prueba que permita al jugador inventarse una señal de evento.
- No crear otro gestor de cartas ni otra autoridad de disponibilidad.
- Respetar las ofertas/comandas y guardado de los servicios en curso.

## Platos — FUNCIONALIDADES APROBADAS

**Objetivo:** decidir qué platos puede ofrecer el restaurante y a qué condiciones comerciales y operativas, usando únicamente los platos válidos del catálogo de juego, sin duplicar el Catálogo Maestro interno.

1. **Consultar platos disponibles** para incorporar a una carta, diferenciando los **incluidos**, los **no incluidos**, los **activos** y los **temporalmente no disponibles**. No ofrecer como jugables registros solo fotográficos ni borradores incompletos del Catálogo Maestro.
2. **Buscar, filtrar y ordenar** platos por categoría, nombre, estado, precio, preparación y rentabilidad, sin alterar el orden jugable de la carta al ordenar la lista.
3. **Seleccionar un plato** para ver nombre, categoría, descripción, fotografía real (o indicar que falta), precio de venta, coste de elaboración estimado, margen calculado, tiempo de preparación y disponibilidad.
4. **Añadir o retirar** platos de la carta que se está gestionando, sin borrar su definición del Catálogo Maestro y sin afectar otras cartas del restaurante.
5. **Activar o desactivar** un plato incluido y **marcarlo agotado manualmente**, respetando las restricciones reales del inventario/receta. Mostrar por qué no se puede pedir un plato, cuando el servicio dispone de ese dato.
6. **Modificar el precio de venta del plato en esa carta** y mostrar cómo cambia el margen comercial calculado usando los costes pertinentes del juego. Los costes y márgenes se calculan; no son campos libres que el jugador teclea.
7. **Configurar la disponibilidad por servicio** (comida/cena y desayuno solamente si forma parte del restaurante), respetando las modalidades de servicio y los permisos jugables existentes.
8. **Marcar un plato como especial o plato firma**, si corresponde a la política comercial ya programada.
9. **Establecer el orden en su categoría** para la presentación operativa; ordenar visualmente la lista de gestión no modifica este orden.
10. **Guardar o descartar** los cambios del editor, con confirmación si se intenta salir con modificaciones pendientes, y proteger el servicio activo de cambios inconsistentes.

**Frontera funcional aprobada:** «Platos» se centra en qué ofrece y a qué precio el jugador. La definición técnica original (ingredientes, receta base, mermas y técnica) pertenece al Catálogo Maestro de autoría; no duplicar editores ni bases de datos. El editor V3 anterior incluye controles para crear o editar recetas durante la partida, pero esas capacidades NO se consideran automáticamente aprobadas: se requiere una decisión explícita de alcance.

**Estado comprobado en código y diseño anterior (sin aprobar su aspecto):** el editor de Carta dispone de selección, filtros, añadido/retirada, precio, disponibilidad, agotado manual, plato firma, dificultad/preparación y escandallo/margen calculados. La rama Carta V3 contiene una pantalla «Platos» con estas operaciones y un formulario de recetas, así como pruebas históricas documentadas. **No se ha auditado su integración con V21 actual ni con el rediseño global.**

## Edición de cartas, recetas y escandallos — RECOMENDACIÓN PENDIENTE DE APROBACIÓN

### Recomendación: suprimir la tercera subopción independiente

**No crear una pantalla nueva** con estas tres funciones. La gestión ya queda cubierta por:

- **«Cartas y reglas de activación»:** crear, duplicar, renombrar, eliminar y programar cartas; elegir carta base y forzado temporal, visualizar cuál está activa.
- **«Platos»:** añadir y retirar platos de la carta, activar o desactivar, fijar precio y servicios, organizar y guardar/descartar cambios.
- **«Detalle de plato» dentro de Platos:** **consultar sin editar el original** la receta base, ingredientes, cantidades, merma, preparación, coste de elaboración, coste real asociado a suministros cuando exista y margen calculado. Los datos de cálculo proceden de los sistemas canónicos y proveedores/inventario, no de campos ficticios.
- **«Catálogo Maestro» (herramienta interna Unity, fuera de las pantallas del jugador):** administrar y validar definiciones, fotografías, ingredientes, recetas base, estaciones, tiempos y escandallos de referencia. No exponer herramientas de publicación del administrador al jugador.

### Por qué requiere una decisión explícita

**El código existente sí soporta creación/edición de platos y recetas en runtime.** `BistroBuilderMenuEditorService.TryCreateOrUpdateDishRecipe` delega en `BistroBuilderDishRecipeAuthoringService.TryCreateOrUpdate`, que puede crear un `DishId` nuevo con `IsPlayerCreated=true`. Además, `TryApplyAndContinue` confirma de forma atómica cambios en carta, platos y recetas. Por tanto, simplificar la interfaz es una **decisión funcional nueva**: no asumir que el código actual ya limita esa capacidad. No eliminar el sistema sin auditoría ni pruebas; para primera versión se recomienda **no exponer esos controles al jugador** si se aprueba el modelo de Catálogo Maestro cerrado.

### Alternativa que podría aprobarse más adelante

Permitir que el jugador cree **recetas propias de la partida**, separadas por ID y persistencia de las definiciones canónicas, con ingredientes reales, costes y validación; sería una mecánica avanzada de creación de platos y necesitaría un diseño funcional específico. **No queda aprobada** con la aprobación de «Platos».

### Resultado propuesto de la navegación (no aprobado todavía)

Bajo la barra superior «Carta», únicamente dos secciones: **1. Cartas y reglas de activación; 2. Platos**. La consulta de receta/escandallo es detalle de «Platos», no una tercera sección. Se evita duplicar acciones entre menús.

## Próximo paso

Solicitar aprobación o ajuste sobre **eliminar «Edición de cartas, recetas y escandallos» como subopción independiente**, manteniendo la lectura de receta/escandallo dentro de «Platos» y el Catálogo Maestro como herramienta interna. Aclarar expresamente si se quieren recetas propias creadas por el jugador en fases posteriores.
