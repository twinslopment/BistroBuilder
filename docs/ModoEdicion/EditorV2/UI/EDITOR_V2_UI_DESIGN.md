# Bistro Builder — Editor V2 — Diseño UI/UX

**Estado del documento:** diseño en curso; la distribución general se acepta como base revisable. Ninguna propuesta visual no ratificada se considera definitiva.
**Inicio:** 2026-10-08
**Rama de trabajo:** `feature/editor-v2`
**Alcance:** especificación visual y UX, sin implementación.
**Autoridades técnicas:** `docs/20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md`; `docs/ModoEdicion/EditInteractionDesign.md`.

## 0. Principios visuales vinculantes aportados por el usuario
- PC exclusivamente; 1920×1080 y 1280×720.
- Elegante, sobrio, comercial; marcos marfil/latón, paneles crema, relieves discretos.
- Miel para selección; rojo reservado a acciones destructivas.
- Recoleta en títulos e Inter en textos secundarios.
- Iconografía original de Bistro Builder, no iconos genéricos.
- No asumir los menús actuales ni los mockups anteriores como UI final del Editor V2.
- Mostrar previews e imágenes al usuario antes de aprobar cada componente; no implementar hasta aprobar la UI/UX completa.
- Restaurante/viewport protagonista y edición visual inmediata, comprensible, reversible.

## 1. Estructura general — BASE ACEPTADA, REVISABLE (2026-10-08)
La persona usuaria ha respondido: «por ahora me convence las 5 zonas y como las has definido. Avancemos».

Cinco zonas:
1. **Superior:** identidad de Editor V2, estado de la reforma, historial y acceso a Aplicar / Descartar.
2. **Izquierda:** catálogo de artículos y acceso a herramientas de construcción sin mezclar contenido.
3. **Centro:** restaurante 3D, selección y manipulación directas, preview y feedback.
4. **Derecha:** inspector contextual del elemento seleccionado, ocultable cuando no es necesario.
5. **Inferior:** herramientas de trabajo, snapping, controles de visualización y acciones permitidas.

Criterios de composición aceptados como orientación, no medidas finales:
- Se prioriza el viewport; los paneles laterales no bloquean innecesariamente el trabajo.
- Catálogo compacto/ocultable; inspector plegable.
- En 1920×1080, los laterales pueden convivir. En 1280×720 se evalúa compactar el inspector de inicio.
- El panel Actividad no ocupa el lateral izquierdo mientras funciona el editor.
- No confundir operación provisional, cambios pendientes de la reforma y restaurante confirmado.
- Las barras son específicas del contexto de edición pero comparten identidad gráfica con el juego.

## 2. Barra superior — ESTRUCTURA DE MODOS APROBADA; DISEÑO VISUAL PENDIENTE
**Decisión aprobada 2026-10-08:** al entrar en Editor V2, desaparecen de la barra superior las diez secciones de navegación habituales del modo normal. En su lugar aparece una barra específica de reforma y edición, con una identidad de modo inequívoca. Al salir se restaura la navegación habitual. No debe confundirse visualmente el modo edición con el servicio/normal, pero mantiene la identidad gráfica del mismo juego (marfil, latón, Recoleta, Inter, iconos BB).
La aprobación alcanza la sustitución de navegación y la diferenciación clara de modos; **no** alcanza todavía la posición, dimensión, tratamiento, iconos, colores secundarios ni distribución exacta de controles.
Diseñar y presentar preview antes de fijar: composición, jerarquía de controles, distribución a 1920×1080 y 1280×720, estados sin cambios y con reforma pendiente, ubicación y redacción de Aplicar / Descartar / Salir y Undo / Redo, estado económico y tratamiento de iconos.
Referencias verificadas: `Assets/Resources/BistroBuilder/UI/TopBar/BistroBuilder_NormalTopBar_v3.png`; `Assets/Resources/BistroBuilder/UI/TopBar/Parts/`; `docs/UI/NewGame/approved-reference.png`; `docs/30_UI_UX/UI_UX_DEFINITIVE.md`.
Las referencias del modo normal indican identidad visual; no fuerzan composición ni navegación idénticas en Editor V2.

## 2.1 Propuesta V2 de distribución — A REVISAR (2026-10-08)
Esta composición se muestra al usuario para revisión; ninguna posición, medida o icono queda aprobada por el mero hecho de aparecer en el mockup.

De izquierda a derecha:
1. Marca original «Bistro Builder» y distintivo visible «MODO EDICIÓN» con tratamiento algo más marcado de latón.
2. Restaurante activo: nombre legible; evitar menú de cambio de restaurante sin confirmar su pertinencia.
3. Economía: «Caja disponible», «Coste de la reforma» (estimado, aún sin aplicar); valorar saldo previsto sin redundancias.
4. Estado: número de cambios pendientes y señalización de diagnósticos, no equivalente a guardado definitivo.
5. Historial global: Deshacer / Rehacer con disabled visible.
6. Acciones: Descartar (destructivo, confirmación), Aplicar reforma (primaria miel/latón, solo habilitada cuando proceda), Salir (diferenciada, aviso de cambios pendientes).

Responsive propuesto:
- 1920×1080: etiquetas completas, información económica desglosada, sin sacrificar legibilidad.
- 1280×720: misma jerarquía y acciones accesibles, abreviar información secundaria e iconos con tooltips; no ocultar Aplicar, Descartar ni Salir.
- Mostrar ejemplo sin cambios / con cambios / con errores; no usar rojo en estados neutros.

Correcciones necesarias al mockup antes de cerrar: usar realmente los recursos originales del logo y los iconos de Bistro Builder; no convertir la maqueta en una promesa de guardado automático; no inventar acciones de cámara/CAD; cifras y nombre de restaurante son ejemplos ilustrativos, nunca datos de partida.
Las piezas de imagen generada son solo referencias conceptuales del chat; la referencia vectorial de jerarquía está en `References/BarraSuperior_V2_Layout_DRAFT.svg`.

## 2.2 Identidad oficial y elección del distintivo — APROBADO EN SU ALCANCE (2026-10-08)
- **Identidad verificada en archivos aprobados del juego:** emblema tridimensional de fachada de restaurante con tejado y tenedor superpuesto, junto a rotulación `BISTRO BUILDER` en dorado/latón sobre placa marfil. No es un logotipo tipográfico plano ni una placa nueva con la palabra «Bistro Builder» sin el edificio.
- **Fuente gráfica canónica en la barra superior actual:** `Assets/Resources/BistroBuilder/UI/TopBar/BistroBuilder_NormalTopBar_v3.png`. La documentación `docs/30_UI_UX/UI_UX_DEFINITIVE.md` declara aprobado ese recurso visual y especifica reutilizar las partes sin repintarlas. El recorte `Assets/Resources/BistroBuilder/UI/TopBar/Parts/asset-0.png` es una textura fuente de la barra y tampoco constituye un PNG del emblema aislado.
- **Referencias recortadas para el diseño, SIN declarar nuevas marcas oficiales:** `References/BB_Emblema_CasaTenedor_Referencia.png` y `References/BB_Logo_Aprobado_Referencia.png` (extraídas directamente del original, sin reinterpretación).
- **Elección confirmada explícitamente:** distintivo «MODO EDICIÓN», **Opción 1 de la primera serie**, placa de latón con icono de lápiz y regla cruzados, independiente del logotipo oficial. Confirmación del usuario: «opcion 1 de la primera serie».
- El logotipo oficial del juego se conserva sin cambios en el editor; la insignia «MODO EDICIÓN» es una pieza independiente. El arte final, proporciones, hover y responsive aún requieren preview y visto bueno específico.
- No se han modificado componentes Unity, ni se ha añadido un sistema visual en runtime.

## 3. Barra inferior — DIRECCIÓN VISUAL ACEPTADA (2026-10-08)
**Aprobación del usuario:** «me gusta muchisimo esa barra inferior». Se consolida el tratamiento visual mostrado en la última serie de tres previews (vista completa, vista compacta y detalle ampliado): marco marfil y latón con perfiles biselados, relieve elegante, tornillería y ornamentación mínima integrada, iconos de aspecto ilustrado, rótulos muy legibles, áreas bien delimitadas. La distribución previamente aceptada mantiene tres grupos. **No se extrapola esta aprobación a las demás zonas de las previews, ni a textos técnicos o efectos no confirmados**.
La distribución aprobada propone 3 grupos de herramientas, sin duplicar las familias de artículos del catálogo izquierdo:
1. **Modo:** Seleccionar, Colocar, Construir, Superficies. Cambia el contexto principal y la herramienta disponible; no crea un segundo catálogo. La familia Construir abre/subordina las herramientas estructurales pertinentes, aún por diseñar.
2. **Editar selección:** Mover, Girar, Duplicar, Eliminar. Capacidad contextual determinada por Editor V2 Selection; botones no aplicables desactivados/ocultos según se valide más adelante. Eliminar es destructiva; rojo únicamente aquí.
3. **Ayudas:** Snapping, Cuadrícula, Vistas. Indicador activo evidente en miel. El snapping contextual B7 propone, la validación espacial decide. Cámara existente 369, sin sistema paralelo.

**Dirección artística consolidada:** marco marfil/latón, biseles metálicos, profundidad de botones y relieves proporcionados al tamaño; fondo crema, miel/dorado en seleccionados, texto oscuro legible, iconos originales ilustrados siguiendo el acabado de la preview favorita. Recoleta para encabezados y rótulos de grupo; Inter para botones y ayudas. Rojo solo en Eliminar. El detalle técnico de iconos y tamaños exactos se resolverá al diseñar el resto de UI; no reintroducir botones planos ni verdes como sustitución de la selección miel.

**Responsive a probar:**
- 1920×1080: etiquetas e iconos visibles, tres grupos separados, barra inferior compacta (mockup 90 px).
- 1280×720: reducción de anchos, mantener títulos breves y blancos de clic utilizables (mockup 76 px); sin esconder herramientas primarias. Si no caben por textos reales, reconsiderar distribución, no sobreponer elementos.
- No replicar dinero/fecha/reloj/acciones Aplicar/Descartar en la barra inferior.
- La escena y el catálogo/inspector incluidos en los mockups son ilustrativos y no constituyen aprobación de esas zonas.

**Previews generadas para revisión del usuario en este chat** (no derivadas de Unity): `EditorV2_BarraInferior_Preview_1920.png` y `EditorV2_BarraInferior_Preview_1280.png`. La referencia del layout que queda en este worktree es `References/BarraInferior_V2_Layout_DRAFT.svg` (vector esquemático), no el PNG del chat. Ni el código C# ni la UI Unity se han modificado.

## 4. Catálogo lateral e inspector derecho — CONCEPTO VISUAL ELEGIDO (2026-10-08)
**Estado:** elegido expresamente por el usuario el concepto «Catálogo Galería Viva» junto al inspector de la imagen facilitada el 2026-10-08, y ratificado de nuevo tras revisar varias alternativas («me quedo con este, hablando solo del catálogo y panel derecho»). Esto **aprueba la referencia artística y la estructura**, no el aspecto de las barras ni la escena 3D de fondo ni las funciones aún no confirmadas.

Las siguientes notas conservan la primera propuesta como registro histórico; la decisión de referencia definitiva visual es la especificada en 4.1 y las afinaciones aún pendientes están en 4.2. Se conserva la identidad aprobada de la barra inferior, no se reutilizan decisiones visuales obsoletas del editor V1.

### Función y límites
- El **Catálogo de artículos** ocupa el lateral izquierdo y sustituye al panel Actividad durante edición; catálogo de mobiliario y equipamiento, no lista de comandos de construcción.
- **Construcción / Superficies** se acceden por la barra inferior aprobada y abren sus flujos propios; no incorporar habitaciones, paredes, puertas o ventanas como tarjetas de mobiliario sin una decisión explícita posterior.
- Autoría e inventario de assets procedentes de SAVIC; las miniaturas y precios de los mockups son **solo ejemplos**, no catálogo real aprobado ni contratos de gameplay.
- La capa gráfica consume las categorías, desbloqueos, costes y contratos reales; nunca deduce permisos por nombres o modelos.

### Primera propuesta visual
- Marco tallado suave marfil/latón, panel crema, sombra contenida, botones ligeramente elevados, bordes de miel para activo, Recoleta en título y sección, Inter para búsqueda, filtros, nombres y precios.
- Cabecera **«Catálogo de artículos»** con control de compactar/ocultar y una descripción secundaria opcional. Se prioriza el espacio disponible.
- Búsqueda con texto legible **«Buscar artículos…»**, acceso a filtros, ordenación por relevancia/precio/nombre.
- Accesos rápidos **Todos / Favoritos / Recientes** y categorías oficiales objetivo: Todos, Mesas, Asientos, Barra, Cocina, Almacenamiento, Iluminación, Decoración, Exterior. Categorías de contenido, no duplicaciones de modos.
- Tarjetas con miniatura nítida sobre fondo coherente, nombre, precio y favorito. El artículo activo usa borde miel y tic dentro del margen.
- Estados diferenciados: **Seleccionado** (miel), **No disponible por progresión** (desaturado con candado/requisito), **Fondos insuficientes** (mensaje económico; no tratarlo como bloqueo de progresión), **Sin resultados**, **Cargando miniatura**, **Error de recurso** (a diseñar).
- El clic en artículo activa la operación de colocación provisional / universal preview aprobada, pero no hace commit. Esc cancela la operación actual, no descarta la reforma global. BBSIS/Placement decide validez; el catálogo no la inventa.
- Vertical scrolling eficiente con virtualización/caché para muchos artículos; el puntero sobre el panel bloquea interacción de mundo, zoom y edge-pan.
- No incluir controles técnicos de posición XYZ ni escalado arbitrario en el catálogo.

### Responsive a comprobar
- **1920×1080:** panel desplegado ~400–430 px de ancho para mostrar categorías a un lado y tarjetas en **2 columnas**; el viewport sigue siendo predominante.
- **1280×720:** panel ~295–305 px con categorías en chips compactos, resultados en **1 columna**; inspector derecho preferentemente plegado; mantener búsqueda, favoritos y acceso a filtros.
- Compacto/oculto no altera la selección ni pierde los filtros del usuario. Scroll con listas largas; acciones principales accesibles por teclado.
- Anchos, alturas, imágenes e iconos exactos sujetos a validación en vista real.

### Referencias y alcance del mockup
- Referencia visual en la rama: `References/CatalogoV2_Distribucion_DRAFT.svg` (esquema de composición).
- Previews de esta conversación: `EditorV2_Catalogo_Preview_1920.png`, `EditorV2_Catalogo_Preview_1280.png`, `EditorV2_Catalogo_Detalle.png`. Estos PNG fueron generados como imágenes de revisión fuera de Unity; **no están copiados a la rama**. No se deben presentar como capturas del juego ni assets SAVIC integrados.
- La barra inferior en las previews reproduce visualmente la referencia favorita; otras secciones alrededor son contexto, **no nuevas aprobaciones**.

## 4.1 Galería Viva — REFERENCIA ARTÍSTICA Y ESTRUCTURA APROBADAS
**Fuente:** imagen aportada y elegida expresamente por el usuario el 08/10/2026; no sustituir por «Biblioteca Viva», «Laboratorio de Estilo», otra galería o los menús viejos.
**Catálogo lateral izquierdo:**
- Marco marfil/latón, biseles y relieve discretos, título Recoleta «Catálogo Galería Viva», textos Inter e iconos propios de BB.
- Cabecera con búsqueda y filtros; pestañas Todos / Favoritos / Recientes.
- Columna vertical de categorías de artículos con selección miel; construcción arquitectónica queda fuera del catálogo de mobiliario.
- Cuerpo de arriba abajo: fotografía comercial «Destacado» con precio y acceso a detalles; carrusel «Artículos relacionados»; cabecera «Todos los artículos» con orden; rejilla de tarjetas con miniatura, nombre legible, precio y favorito.
- El destacado es oscuro solo por la fotografía; los controles y el resto de tarjetas mantienen superficies claras. Scroll independiente, acciones accesibles.
**Inspector derecho:**
- Panel claro con marco equivalente, nombre y favorito, preview grande y descripción breve.
- Secciones Dimensiones / Rotación / Materiales / Variantes, visibles únicamente cuando el objeto y sus metadatos lo justifiquen.
- Las reglas/estado de colocación proceden de los sistemas de validación y deben ser accesibles sin romper la jerarquía.
**Alcance:** se aprueban el catálogo y panel derecho exclusivamente. Nada en la imagen implica aprobar barras superiores, guardado automático, precios de ejemplo, capacidades XYZ/CAD ni escenario renderizado.

## 4.2 Refinamiento V3 — ESTADOS Y CRITERIOS DE RESPONSIVE ACEPTADOS COMO DIRECCIÓN; DETALLE PENDIENTE
**Confirmación de avance:** tras explicar que las vistas de estados e inspector son complementarias, el usuario respondió «adelante, avancemos». Se conservan ambas líneas de diseño y las correcciones indicadas (miel para selección, aviso económico no destructivo sin rojo, controles únicamente cuando existen capacidades reales). No se fija por ello cada píxel, interacción o dimensión de los mockups.
**Responsive propuesto**
- 1920×1080: catálogo desplegado con categorías laterales y varias tarjetas según el ancho; inspector desplegado al existir selección; restaurante/viewport sigue siendo el foco.
- 1280×720: catálogo más estrecho; búsqueda visible, destacado reducido/plegable, relacionados desplazables, rejilla adaptable a 1–2 columnas; inspector normalmente plegado y desplegable en la selección. Sin quitar funciones ni superponer barras.
- Sin forzar medidas exactas de una imagen generada: probar escala real, contraste, legibilidad y hit targets.

**Estados de tarjeta propuestos**
- Normal: miniatura, nombre, precio, favorito.
- Hover: iluminación miel sutil y breve elevación; nunca una selección persistente.
- Seleccionado: borde/fondo miel y tic legible; permanece al mover el puntero.
- Favorito: estrella de latón; acción independiente del clic de colocación.
- Reciente: distintivo reloj discreto; no altera el precio ni bloquea el artículo.
- Bloqueado por progresión: miniatura desaturada + candado + requisito; impide colocar.
- Fondos insuficientes: información de coste y aviso ámbar/carbón, NO rojo; Finanzas decide la disponibilidad económica. Nunca confundir con progresión bloqueada.
- En colocación: marca de gesto provisional y preview/ghost del BB Universal Preview System. Esc cancela solo ese gesto, no la reforma completa.
- Vacío, miniatura cargando o fallida: explicación y placeholder útiles, sin silencio ni modales rutinarios.

**Datos y visibilidad del inspector**
- Siempre que exista artículo seleccionado: nombre, imagen/placeholder, categoría, precio cuando proceda, descripción si existe y estado de disponibilidad.
- Dimensiones: leer dimensiones autoradas en centímetros, NO ofrecer escalado/redimensionado arbitrario solo por mostrarlas.
- Rotación/Mover/Duplicar/Eliminar: visibles o habilitados conforme a las capacidades reales de BistroBuilderEditorV2Selection, no automáticamente por familia gráfica.
- Materiales/acabados: aparecen solo con FurnitureFinishProfile u otro perfil válido y opciones realmente cargadas.
- Variantes: solo cuando haya variantes reales compatibles; una mesa cuadrada no se presupone variante de una redonda.
- Reglas y alcance espacial: proceden de InspectorRules, PlacementScope y validación del candidato; no mostrar «Listo para colocar» si no se ha validado.
- Cada familia tiene contexto propio: mobiliario, equipamiento, arquitectura y superficies. No inventar controles de luz, gas/agua, coordenadas o escala.
- La selección, el gesto provisional y la aplicación de reforma son estados diferentes; la UI no promete guardado automático sin respaldo técnico.

**Filtros:** precio, disponibilidad, interior/exterior y estilo cuando existan datos; ordenación por relevancia, precio, nombre. Bloquear interacciones de mundo detrás de paneles, conservar selección/filtros al plegar; navegación teclado, foco visible y tooltip en iconos abreviados.
**Pendiente de visto bueno:** tamaños definitivos, microinteracciones, comportamiento plegable, estados finales y composiciones 1920/1280. Las previews son conceptos, no pruebas de Unity.
**Referencias presentadas en el chat:** GaleriaViva_V3_VistaAmplia.png, GaleriaViva_V3_VistaCompacta.png, GaleriaViva_V3_Estados_Articulo.png y GaleriaViva_V3_Inspector_Variantes.png. PNG no copiados a Git. Referencia vectorial versionada: References/GaleriaViva_Responsive_States_V3.svg.

## 5. Herramientas de construcción — ORGANIZACIÓN APROBADA, ACABADO PENDIENTE (2026-10-08)
**Respuesta del usuario a la propuesta de Taller:** «sí, avancemos». Se ratifica la **organización funcional/visual de áreas**: pulsar Construir sustituye temporalmente Galería Viva por Taller de construcción, el inspector cambia a contexto arquitectónico y la escena permanece protagonista. Esta aprobación no cierra iconografía, medidas, transiciones ni controles exactos de cada herramienta.

### 5.1 Ámbito y fundamento técnico
- El núcleo Construction Authoring existente tiene acciones de tipo Furniture/Select/Wall/Room/Door/Window/WallModule; las interfaces de contexto NO vuelven a implementar transacciones, coste, BBSIS, Navigation ni validación.
- Arquitectura activa a través de **Construir** en la barra inferior ya aceptada. Construcción sustituye temporalmente al catálogo de artículos izquierdo con un panel específico denominado de forma **provisional** «Taller de construcción»; al volver a Colocar/Mobiliario reaparece «Catálogo Galería Viva». No incluir puertas/ventanas/segmentos como muebles del catálogo.
- **Superficies** es otro contexto de la barra inferior, no una pestaña de muebles; se diseñará con el mismo sistema gráfico en su propio subbloque.

### 5.2 Composición visual propuesta
**Izquierda (Taller):** marco marfil/latón coherente con Galería Viva y barra inferior, Recoleta para encabezados, Inter para controles; acceso «Volver a artículos», alternancia Crear/Editar, cinco familias con icono específico: Habitación, Pared, Puerta, Ventana, Módulo. Herramienta activa en miel. Descripción breve y cómo iniciar el gesto; acciones inexistentes no se representan como funcionales.
**Centro (viewport):** se conserva el restaurante como protagonista; una pared muestra volumen translúcido tenue, línea de apoyo al suelo y extremos/snapping; una habitación muestra contorno en suelo y volumen provisional de paredes. BBSIS/Placement/Construction informa qué es válido; las guías no confirman nada. Feedback contenido, sin teñir objetos enteros ni cuadrícula siempre encendida.
**Derecha (inspector):** cambia de mobiliario a selección/trazado estructural. Presenta nombre/estado provisional, esquema breve, dimensiones informativas o calculadas, compatibilidad, zonas, conexiones, costes SOLO si Finance los publica, y diagnóstico localizado. Si faltan datos muestra pendiente, nunca un valor inventado. No duplicar Aplicar/Descartar global.
**Inferior:** se conserva la barra aprobada MODO/EDITAR/AYUDAS; se señala Construir como modo activo. Deshacer/Rehacer globales no se sustituyen por historiales particulares. «Esc» abandona el gesto actual sin descartar la reforma.

### 5.3 Reglas concretas de cada familia
- **Pared:** marcar inicio y final del segmento. Preview de longitud y conexión, altura/grosor solo como datos si están expuestos. Snapping en vértices/segmentos según algoritmo existente. Falso que una línea provisional ya esté construida.
- **Habitación:** delimitar el rectángulo o forma admitida por el tool. Mostrar área, perímetro y zona solo si existen datos calculables/autorados; permitir revisar antes de aceptar. Split/merge respaldados por Construction Authoring, no nuevos botones improvisados.
- **Puerta/Ventana:** se sitúan en un segmento anfitrión válido mediante opening canónico. No permitirlos flotando; vista previa sobre pared; inspector de tipo y dimensión según datos reales.
- **Módulo de pared:** únicamente familias/formatos publicados; no inventar curvas, cubiertas o multilayer CAD.
- **Superficies:** herramienta separada que leerá materiales publicados y destinos válidos; la estética concreta se revisará al diseñarla.

### 5.4 Responsive y estados (propuestos)
- **1920×1080:** panel izquierdo ~455 px; inspector estructural derecho ~354 px al seleccionar/trazar; viewport amplio. Una columna de herramientas a la izquierda y estados/medidas sobre el inspector, sin popup central pesado.
- **1280×720:** panel izquierdo ~314 px con herramientas en dos columnas; inspector plegado como pestaña vertical que se puede desplegar bajo demanda; viewport no queda atrapado entre paneles. Reservar espacio a ambas barras horizontales.
- Activo = miel; hover = iluminación sutil; deshabilitado = crema atenuado + explicación; advertencia económica = ámbar neutral (no rojo destructivo). Conflicto geométrico se explica sin un UI de error rojo genérico. Si no hay selección ni gesto, inspector puede permanecer plegado.
- Preview no es commit ni garantiza validez. Cancelar un gesto no equivale a Descartar reforma.
- La tipografía Recoleta/Inter, iconos BB originales, relieve y ornamentos discretos seguirán pendientes de arte final; los símbolos vectoriales usados en la prueba son placeholders, no assets de iconografía aprobados.

### 5.5 Referencias de trabajo y limitaciones de las previews
- Previews visuales realizadas en el chat (fuera de Unity): `EditorV2_Construccion_Pared_1920.png`, `EditorV2_Construccion_Habitacion_1920.png`, `EditorV2_Construccion_Pared_1280.png`. Todavía no se han integrado como archivos PNG en Git.
- Plano vectorial versionado y editable: `References/ConstruccionV2_Layout_DRAFT.svg`. Este plano no sustituye a los PNG de revisión.
- El escenario usado como fondo procede de una referencia ilustrativa y conserva un gizmo de selección de mobiliario previo; **no** define el estado del editor durante construcción ni constituye una prueba de simulación de construcción.
- Los ejemplos de 4,2 m, 12,4 m², 2,50 m y 0,12 m no fijan especificaciones de gameplay; lo que muestra Unity debe derivarse de la sesión real.
- La parte superior y la escena de las imágenes son contexto; **no** se aprueban con esta propuesta, ni se prometen autosave, nuevas herramientas de CAD o valores económicos ficticios.
- **Estado final de este subbloque:** organización Taller / contexto Construir **aprobada** el 08/10/2026; arte final, iconos, mediciones, microinteracciones y todos los gestos específicos permanecen pendientes de aprobación visual. No programar todavía.

## 6. Selección y multiselección — ESTÉTICA MULTISELECCIÓN APROBADA; ANIMACIÓN EN PROPUESTA (2026-10-08)
**Fundamento técnico comprobado:** B8 PASS sobre Selection Coordinator / Selection Set con selección primaria, Mayús+clic aditivo/toggle, restricciones por autoridad (no mobiliario+arquitectura en la misma selección), capacidades comunes como intersección, operaciones de grupo atómicas, rollback, geometría relativa conservada e historial único. No duplicar este núcleo. Fuente técnica: docs/20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md (B8) y Assets/Scripts/Application/Restaurant/EditMode/EditorV2/BistroBuilderEditorV2SelectionSet.cs.

**Propuesta A — selección individual:**
- El objeto elegido recibe contorno fino miel y una indicación de foco, sin teñir el modelo completo ni dibujar una caja CAD permanente.
- El inspector derecho mantiene el concepto Galería Viva, con foto, nombre, propiedades con soporte real, materiales cuando proceda y acciones compatibles. La barra inferior conserva su diseño aprobado.
- Selección ≠ inicio de arrastre: levantar/asentar solo durante manipulación real, de acuerdo con EditInteractionDesign.

**Multiselección — DIRECCIÓN VISUAL APROBADA (sustituye la propuesta anterior):**
- Referencia exacta: imagen seleccionada explícitamente por el usuario en el chat el 08/10/2026, `image(20261008-120359).png`, con mesa y dos sillas, **segunda alternativa de la última terna**.
- **Esquinas breves de latón/miel**, ligeramente luminosas alrededor de cada artículo seleccionado. Sin recuadros completos, círculos en el suelo, perímetros ornamentales ni etiquetas 1/2/3 sobre las piezas. El modelo 3D mantiene su material y posición.
- **Pastilla superior flotante marrón oscuro con filete fino dorado**, icono de multiselección, texto dinámico «3 artículos seleccionados» y chevrón. No ocupa todo el ancho del viewport.
- Mayús+clic añade o retira objetos de la misma autoridad, según B8; la última selección puede ser primaria. El gizmo de mover/rotar solo aparece cuando corresponde a la herramienta o gesto, no es parte de la selección pasiva.
- **Inspector conjunto pendiente de definición:** la imagen elegida conserva el inspector individual de la mesa; NO se interpreta como aprobación de la propuesta anterior de panel «Selección múltiple». Abrir la pastilla podría permitir ver miembros/capacidades comunes, pero exige una nueva validación visual.
- Transformaciones de grupo solo si están soportadas por todos los miembros; conservar geometría relativa, Undo/Redo y selección individual.
- Al transformar el conjunto, se preserva su disposición relativa. La operación es única para Undo/Redo; cada pieza sigue pudiendo seleccionarse y editarse individualmente.
- No forzar en un grupo cambios simultáneos de materiales o dimensiones si los objetos tienen perfiles diferentes.

### 6.1 Microanimación al seleccionar — PROPUESTA, NO APROBADA
**Intención del usuario:** introducir una pequeña animación al pulsar sobre un objeto. Diseñar y enseñar preview animada (o secuencia de fotogramas) antes de marcarla como definitiva.

**Propuesta de tiempos iniciales para evaluar (240 ms en total):**
- 0–80 ms: aparecen con suavidad las esquinas cortas de selección doradas; no modificar escala, rotación, posición ni materiales reales del objeto.
- 80–180 ms: discreto brillo miel recorriendo el borde de esas esquinas; sin holograma, destello fuerte ni capa fluorescente.
- 180–240 ms: la iluminación se asienta en estado estable persistente, sin bucles.
- Al deseleccionar: desvanecer las marcas en ~120 ms.
- Al añadir una pieza con Mayús+clic, animar SOLO esa pieza; las previamente seleccionadas mantienen su estado.
- Al retirar de la selección, desvanecer solo la pieza retirada; si queda una, debe seguir habiendo selección individual sin alteración de pose.
- Si empieza un arrastre inmediatamente, no retrasar el gesto para esperar la animación: la interacción tiene prioridad.
- Un clic repetido sobre el mismo objeto ya seleccionado no reinicia la animación. Desactivar o reducir movimiento si hay una opción de accesibilidad para ello.

**Límites vinculantes:** se animan las marcas visuales, NO se eleva ni hace saltar el mueble por seleccionarlo; cualquier elevación visual suave solo corresponde al inicio real de la operación de transporte/movimiento, siguiendo EditInteractionDesign. La UI no cambia física, selección real B8, snapping, validación ni historial.
**Pruebas visuales pendientes:** 1920×1080 y 1280×720; uno y varios muebles; clic repetido, Mayús+clic rápido, deselección; coste gráfico insignificante en PC de especificación mínima; comparar brillo con la iluminación de un restaurante oscuro y uno claro.
**Estado:** duraciones, curva easing, intensidad de brillo y posible respuesta de la pastilla superior permanecen PENDIENTES de aprobación.

**Propuesta C — intento no compatible:**
- Si se intenta Mayús+seleccionar arquitectura con una selección de mobiliario, no alterar el conjunto. Mostrar aviso ámbar/crema localizado: «No puedes combinar mobiliario y arquitectura en la misma selección».
- Ofrecer continuidad sin modal obligatorio ni error destructivo en rojo. No inventar operaciones cruzadas ni grupos persistentes entre autoridades.

**Responsive / interacción:**
- 1920×1080: inspector de selección múltiple puede desplegarse y muestra lista y operaciones disponibles; viewport como protagonista.
- 1280×720: inspector plegado inicialmente a rail fino, apertura contextual cuando el jugador la solicite; resumen de selección compacto sobre viewport sin cubrir búsqueda ni barra inferior.
- El menú Galería Viva permanece disponible durante selección de mobiliario. El taller de construcción se ocupa del contexto arquitectónico; nunca mostrar ambos como catálogos principales al mismo tiempo.
- El panel izquierdo y el inspector bloquean input de mundo bajo ellos. Esc en una manipulación cancela el gesto (no descarta reforma). Salida de selección sin gesto se definirá en revisión de microinteracciones.
- Distinguir selección, preview de movimiento provisional y aplicación global. Miel para selección, azul/cian solo para guías justificadas de snapping; rojo reservado a acciones destructivas.

**Previews para aprobación visual, generadas fuera de Unity:**
- EditorV2_Seleccion_single_1920.png: único artículo.
- EditorV2_Seleccion_multi_1920.png: 3 muebles e inspector conjunto.
- EditorV2_Seleccion_conflict_1920.png: mezcla de autoridades rechazada.
- EditorV2_Multiseleccion_Compacta_1280.png: pantalla compacta con inspector plegado.
Los PNG se entregan en el chat; **no están almacenados dentro de Git**. Fondo 3D y demás barras de una referencia seleccionada del usuario; no se ratifican ni se prueban con estas capturas.
Referencia esquemática versionada: References/Seleccion_Multiseleccion_Layout_DRAFT.svg.
**Estado:** esquinas de latón/miel + pastilla oscura de contador superior APROBADAS. Aún pendientes el inspector de conjunto, microanimación, selección simple y composición responsive exacta. NO programar.

## 7. Resto de componentes — SIN DISEÑO APROBADO
Orden: superficies (detalle); Apply/Discard; feedback/snapping/errores/estados; responsive/coherencia final.

## 8. Registro de aprobación
- 2026-10-08: cinco zonas de distribución general aceptadas **provisionalmente** como punto de partida. No implica aprobación de la imagen conceptual al detalle, tamaño exacto, microinteracciones o componentes particulares.
- 2026-10-08: **aprobado** que la barra de Editor V2 sustituya completamente la navegación superior normal de diez secciones y se diferencie de modo inequívoco; la apariencia y los controles definitivos siguen pendientes de preview y revisión.
- 2026-10-08: **confirmada Opción 1** del distintivo «MODO EDICIÓN», lápiz y regla cruzados, manteniendo el logotipo oficial sin reinterpretarlo.
- 2026-10-08: **dirección artística y distribución de la barra inferior aceptadas expresamente** («me gusta muchisimo esa barra inferior»). No incluye aprobación de todas las zonas que aparecen en esa escena de ejemplo.
- 2026-10-08: **Galería Viva elegida expresamente** como concepto artístico y estructural del catálogo y el inspector derecho, exclusivamente. Sustituye la anterior propuesta genérica; las imágenes de los otros menús y el restaurante no se consideran aprobadas.
- 2026-10-08: **estados e inspector V3 complementarios aceptados como dirección de diseño** al continuar hacia construcción; responsive y microinteracciones finales sujetos a pruebas visuales. Mantener controles regidos por capacidades reales y avisos económicos no destructivos sin rojo.
- 2026-10-08: **organización del Taller de construcción aprobada** tras las previews de pared/habitación (usuario: «si, avancemos»). Galería Viva se sustituye por Taller al pulsar Construir; inspector en contexto estructural. Acabados específicos y microinteracciones pendientes.
- 2026-10-08: **estilo de multiselección aprobado expresamente** («me quedo con esta forma de seleccion»): esquinas cortas doradas en cada elemento y pastilla superior oscura con contador; quedan descartadas como diseño base las otras variantes de círculos/contornos completos/números.
- 2026-10-08: **microanimación de selección propuesta por el usuario** («quiza cuando pulses sobre un objeto podría hacer una pequeña animación»). Timing y detalles de efecto por aprobar con preview; no altera la posición ni escala del asset.
- No implementar C#/Unity ni sustituir UI heredada antes del cierre visual explícito.
