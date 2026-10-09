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

## 6. Selección y multiselección — HALO DE HUELLA APROBADO COMO CONCEPTO VISUAL; INSPECTOR DE GRUPO EN REVISIÓN
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

### 6.1 Microanimación al seleccionar — APROBADA COMO BASE V1 (2026-10-08)
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
**Preview HTML interactiva realizada (2026-10-08):** `EditorV2_Seleccion_Animada_INTERACTIVA.html`, entregada en este chat como archivo autónomo; no está copiada ni integrada en Unity ni en la rama Git. El prototipo incluye escena de referencia, zonas de clic sobre mesa y dos sillas, entradas animadas en esquinas doradas, contador superior dinámico, Mayús+clic para añadir o retirar, clic en fondo o Esc para deseleccionar, desvanecido de salida, y botón «Reproducir secuencia». La selección individual también recibe la animación; no es un efecto exclusivo de la multiselección.
**Pruebas de la preview:** mediante Chromium/Playwright, 1920×1080 y 1280×720: selección simple, entrada y asentamiento, adición sin reanimar los demás, retirada, Escape, reproducción automática, y errores JS 0; PASS en ambas resoluciones. Estas pruebas validan la **demo HTML**, no el comportamiento runtime Unity ni B8.
**Limitaciones del mockup:** el escenario de fondo es una imagen estática de la maqueta anterior y contiene un gizmo ilustrativo; el único comportamiento interactivo representativo es la selección/microanimación de marcas, contador y actualización del inspector simulado. Sin cambios al pipeline 3D o animaciones reales de assets.
**Decisión expresa del usuario 2026-10-08:** «me convence, no descarto futuros arreglos, pero hoy me vale. avancemos». Queda **APROBADA COMO BASE V1** la microanimación para selección simple, multiselección y deselección tal como se vio en el HTML interactivo, incluidos los tiempos orientativos de entrada ~240 ms y salida ~120 ms, sin alterar escala/posición del asset. Queda abierta a ajustes posteriores de timing, easing, intensidad y acabado. No significa implementación en Unity ni implica aprobación de otros detalles no ensayados.

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
**Estado:** esquinas de latón/miel + pastilla oscura de contador superior APROBADAS; microanimación de clic simple, Mayús+clic y deselección APROBADA COMO BASE V1 tras prueba HTML. Inspector de conjunto, cierre responsive y pulido posterior de la animación pendientes; NO programar Unity hasta cerrar la UI general.

### 6.2 Halo de Huella — CONCEPTO VISUAL ELEGIDO (2026-10-09)
**Decisión vigente y vinculante:** el usuario mostró expresamente como preferido el **«Concepto 2 · Halo de Huella»** (imagen elegida en la conversación el 09/10/2026) y confirmó «así es como quiero que sea». Esta aprobación sustituye los anteriores marcos descentrados, cápsulas genéricas y las alternativas de «Huella de Precisión» y «Marco Adaptativo de Grupo». La imagen de inspiración contiene Galería Viva y el inspector ya aprobados, pero **no constituye aprobación del guardado automático ilustrativo ni de los datos ficticios**.

**Composición aceptada:**
- **Objeto individual**: pequeñas esquinas miel/latón, centradas y ajustadas al rectángulo proyectado del **volumen visible completo** de ese asset; además, halo delgado cálido sobre su **huella real en suelo**. Sillas: recoger respaldo, asiento y patas; mesas redondas: contorno/huella elíptica proyectada, no una caja descentrada; mesas rectangulares: huella cuadrangular ajustada a orientación, no una cápsula.
- **Multiselección**: mantener las esquinas y halos individuales sin reiniciarlos para cada nuevo clic. Añadir un **perímetro de grupo sutil calculado** con las huellas proyectadas de sus miembros y la pastilla superior oscura existente con contador. El grupo no fusiona muebles ni cambia sus relaciones o autoridad de selección.
- **Animación**: conservar entrada ~240 ms y salida ~120 ms aprobadas, sin levitar ni reescalar objetos por seleccionar. La huella/halo debe ser menos brillante que las esquinas y no persistir como halo gigante o círculo decorativo.
- **Sin obstrucciones**: no colocar etiquetas blancas encima del mueble ni gizmos de movimiento en selección pasiva. Un clic de selección no mueve ni altera ninguna instancia.
- **Precisión**: no calcular offsets fijos manuales a partir de una captura. El futuro renderer debe transformar bounds/silueta útil y huella espacial desde datos del asset (Renderer bounds, pivote/collider e información SAVIC/Construction Authoring cuando proceda) a proyección de cámara en cada fotograma necesario. Corregir mallas descentradas y pivotes incorrectos mediante metadatos legítimos; no esconderlos detrás de offsets dibujados a ojo.
- **Rotación y cámara**: actualizar al rotar, girar cámara, cambiar zoom o mover. Minimizar visitas a los meshes y evitar recálculos exhaustivos continuos: caché/invalidación por cambio de objeto, pose, encuadre o definición; integrar sin crear otro núcleo que compita con BB Universal Preview/B8.
- **Jerarquía**: proyección del volumen visible para esquinas; huella sobre superficie de apoyo para halo; envolvente del conjunto calculada de huellas compatibles. El perímetro del grupo se oculta en selección individual o si no se puede calcular de forma válida; nunca inventar medidas.
- **Responsive**: 1920x1080 y 1280x720, proyección ajustada al viewport real; no atar marcas a un diseño HTML con ancho fijo.

**Prueba interactiva de geometría (NO Unity):** archivo de esta conversación `EditorV2_HaloDeHuella_SeleccionPrecisa_INTERACTIVO.html`. Renderiza cinco muebles geométricos en SVG y usa las mismas medidas locales para dibujar sus marcas; ofrece sillas, mesa circular, mesa rectangular y mesa + dos sillas. Incluye Mayús+clic, pestañas Principal/Conjunto, contador, Esc, deselección, Undo/Redo de la demo y entrada animada. Los modelos de la demo son esquemáticos, NO los GLB definitivos.
**Verificación web:** Chromium/Playwright a 1920x1080 y 1280x720, PASS en selección individual, multiselección, vuelta automática de Conjunto a Principal al quedar un miembro, contador sin pérdida de selección, Escape, Undo/Redo, no errores JS/no scroll horizontal. La medición de cajas SVG confirma que las marcas envuelven el dibujo completo de silla, mesa redonda y rectangular. **No se han probado aún los Renderers, bounds ni oclusión en Unity**.
Capturas: `EditorV2_HaloHuella_Grupo_1920.png`, `EditorV2_HaloHuella_Grupo_1280.png`, `EditorV2_HaloHuella_Redonda_1920.png`, `EditorV2_HaloHuella_Redonda_1280.png`. La referencia geométrica versionada se denomina `References/HaloDeHuella_Proyeccion_V1.svg`.
**Estado:** concepto estético APROBADO; fórmula de proyección 3D/render, arte y rendimiento pendientes de un prototipo Unity + aceptación visual en build. El inspector doble contexto de 6.3 sigue siendo una propuesta que requiere visto bueno separado.

### 6.3 Inspector de doble contexto — PROPUESTA VISUAL V1 (2026-10-09; NO APROBADA AÚN)
**Motivo:** la imagen exacta elegida por el usuario para multiselección contiene 3 muebles marcados y mantiene abierto el inspector individual de la mesa. No reemplazarlo unilateralmente por una ficha exclusiva de grupo.

**Propuesta mostrada en HTML:** `EditorV2_Inspector_DobleContexto_INTERACTIVO.html` (archivo autónomo de esta conversación). La foto de fondo pertenece a la maqueta elegida; los controles HTML superpuestos, no Unity, constituyen la parte interactiva. No se han modificado código, materiales ni escenas Unity.
- **Vista «Principal» por defecto:** sigue mostrando ficha, imagen, medidas y acabados del artículo principal cuando varios objetos están seleccionados. En la demo, mesa bistró clásica. Al elegir una silla como principal, cambian los datos y la imagen de referencia de ese mueble; no se modifica la selección del resto.
- **Vista «Conjunto» a demanda:** al pulsar la pestaña o el contador flotante, mostrar 2/3/... miembros, principal distinguido en miel, lista con miniaturas/categoría y solo **acciones realmente comunes** obtenidas de `BistroBuilderEditorV2SelectionCoordinator.AggregateCapabilities`. El botón no soportado permanece deshabilitado o desaparece; no inventar cambio de materiales masivo sobre familias incompatibles. Preservar la estrategia actual de `B10` para Sustituir selección, con presupuesto real y confirmación.
- **Pastilla flotante aprobada:** continúa encima del viewport con contador dinámico; clic abre desplegable corto para seleccionar el principal o entrar al panel Conjunto. No crear una banda adicional permanente.
- **Interacción:** clic selecciona individualmente, Mayús+clic añade/retira de la misma autoridad, Esc deselecciona o cierra un inspector abierto según contexto; el rechazo de mezcla mobiliario/arquitectura mantiene intacto el grupo y se comunica con aviso ámbar no destructivo. Las esquinas miel y microanimación V1 aprobada se mantienen; la demostración utiliza la foto fija elegida, por lo que **no valida de nuevo el efecto real en 3D**.
- **1280×720:** inspector inicialmente plegado con indicador compacto de selección y pestaña de apertura; al abrir, usa un cajón dentro de la altura disponible, con scroll independiente. Cerrar no pierde la selección. El catálogo Galería Viva sigue visible y el mundo central no queda tapado permanentemente.
- **1920×1080:** inspector visible por defecto junto al catálogo, con contexto Principal/Conjunto y lista si cabe.
- **Copy y datos:** nombres y dimensiones de la preview son ilustrativos; en runtime proceden de definiciones SAVIC y componentes existentes. Materiales, sustitución, movilidad, costes, capacidades, relaciones funcionales y validación nunca se deducen de la maqueta.
- **Regla visual:** conservar marfil/latón, títulos Recoleta, secundarios Inter, miel para principal y rojo solo en Eliminar/Descartar. No introducir cajas CAD, números grandes encima de cada mueble, ni un segundo panel permanente encima del inspector.

**Pruebas de preview HTML (no Unity):** Playwright/Chromium en 1920×1080 y 1280×720; pestañas, selección de 1/2/3 objetos, cambio de principal, Mayús+clic, grupo, rechazo de arquitectura, Esc, apertura responsive del inspector: PASS, 0 errores de JS, sin scroll horizontal de documento. Las capturas de la conversación son `EditorV2_Inspector_Grupo_1920.png`, `EditorV2_Inspector_Grupo_1280.png`, `EditorV2_Inspector_Principal_1920.png`, `EditorV2_Inspector_Principal_1280.png`, `EditorV2_Inspector_Plegado_1280.png`. La foto de la maqueta incluye un gizmo de transformación estático que NO se aprueba como estado pasivo de selección.
**Alcance de aprobación:** exclusivamente una PROPUESTA de diseño del inspector de conjunto y el plegado responsive; el usuario todavía no la ha validado. Referencia esquemática: `References/InspectorDual_Responsive_DRAFT.svg`.
**Pendiente:** aprobación de esta combinación antes de convertirla en UI Unity; después verificar tipografía/iconos definitivos y responsive en build, y decidir el método provisional de «Proyector Inteligente» para superficies tras ensayos de geometría real.

## 7. Aplicar / Descartar reforma — DIRECCIÓN V1 APROBADA, AFINABLE (2026-10-08)
**Decisión del usuario:** «me gusta, sigamos» tras presentar el HTML interactivo de Aplicar / Descartar con capturas y pruebas de los flujos. Se aprueba **el comportamiento y la dirección visual V1 como base afinable**: revisión antes de aplicar, confirmación de descarte y salida protegida. Esta aprobación NO fija importes simulados, guardado automático, dimensiones exactas ni declara integración en Unity.
**Fundamento confirmado:** docs/20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md, B5, y BistroBuilderEditorV2RenovationSession.cs. Los métodos TryApplyChanges y TryDiscardChanges existen. El guard de salida impide abandonar edición mientras queden cambios por resolver. Finance y validación estructural son autoridad del coste/viabilidad; la UI no inventa cifras ni resultados.

### Composición propuesta
- **Barra superior del modo edición:** mostrar «Cambios pendientes» y «Coste estimado» cuando exista un snapshot válido de la reforma; controles principales «Aplicar reforma», «Descartar» y «Salir». La barra antigua queda fuera. Los nombres, tamaño y disposición final siguen sujetos a aprobación de la fase de barra superior.
- **Aplicar** tiene tratamiento miel/latón como acción afirmativa. **Descartar reforma** usa rojo para advertir que revierte TODA la sesión. **Salir** es independiente, con protección de cambios pendientes.
- **Sin cambios:** botones Aplicar y Descartar deshabilitados o no mostrados según jerarquía responsive; Salir disponible.
- El jugador no recibe un modal de reforma después de cada mueble: revisión global solo al pedir Aplicar/Descartar/Salir.

### Flujo «Aplicar»
1. Clic en Aplicar abre un panel de revisión marfil/latón sobre la escena atenuada (la referencia del juego sigue visible).
2. Mostrar número de operaciones, desglose de mobiliario/arquitectura/superficies cuando se pueda derivar de la sesión, coste neto o devolución y caja prevista SOLO si Finance publica valores fiables.
3. Mostrar validación/diagnósticos por nombre comprensible, nunca afirmar «sin incidencias» por ausencia de datos. Validación y coste deben proceder de los contratos reales, no de un cálculo de la UI.
4. «Seguir editando» cierra el panel sin tocar el draft; «Aplicar los cambios» confirma el conjunto solo si el runtime lo autoriza. No eliminar el historial ni declarar éxito hasta que TryApplyChanges devuelva resultado válido.
5. En validación fallida, suprimir/deshabilitar confirmación y ofrecer «Ver incidencia» conservando el draft. Error técnico se explica sin fingir aplicación.
6. Tras éxito, la sesión se rebasa al restaurante recién confirmado; se actualiza el contador a 0, sin confundir Apply con guardado persistente de la partida ni con salida automática.

### Flujo «Descartar»
1. Clic abre confirmación explícita «¿Descartar toda la reforma?»; texto aclara que revierte mobiliario, arquitectura, costes de reforma y relaciones a la baseline.
2. Acción de riesgo «Sí, descartar reforma» en rojo; alternativa «Cancelar / seguir editando» clara y con foco seguro por defecto.
3. Solo cuando TryDiscardChanges informa éxito, mostrar baseline restaurada, contador 0 y estado sin pendientes.
4. Esc o cerrar el modal NO descarta nada. Esc al mover un objeto cancela solo el gesto actual: estas dos operaciones no se equiparan.

### Flujo «Salir»
- Sin cambios: salir de forma normal según navegación de modo.
- Con cambios: presentar opciones «Seguir editando», «Revisar para aplicar» y «Descartar…». No salir ni descartar silenciosamente. B5 sigue siendo autoridad sobre CanExitEditMode.
- Si hay operación provisional activa al iniciar una acción global, B5 debe cancelarla/coordinarla antes de decidir el resultado; no confundir el gesto aún no confirmado con cambios netos de reforma.

### Lenguaje visual y responsive
- Misma familia de marcos marfil/latón, botones con relieve suave y títulos Recoleta/textos Inter. Se reserva el rojo para Descartar; validación geométrica o económica usa señales informativas ámbar según gravedad y accesibilidad.
- 1920×1080: panel centrado de revisión con tres cifras y lista legibles, sin ocupar toda la pantalla.
- 1280×720: anchura máxima acotada al viewport, contenido desplazable si hace falta, acciones visibles sin cortar texto; ESC y botones de cerrar accesibles por teclado.
- Conservar el contexto Galería Viva y la selección vigente detrás, sin cambiar la apariencia de inspector ni catálogo aprobada.

### Preview y estado de prueba
- HTML autónomo de demostración: EditorV2_Aplicar_Descartar_INTERACTIVO.html, entregado en esta conversación, sin dependencias externas. Capturas 1920/1280 de estado inicial, revisión y confirmación para revisar visualmente.
- Incluye escenarios simulados válidos/con incidencias/sin cambios, revisión, confirmación de descarte, guard de salida, cancelar con Escape, activación/desactivación de botones y notificación de resultado. Estos estados son un prototipo de interfaz, **no transacciones reales Unity**; números y restaurante son ilustrativos.
- Pruebas automatizadas Chromium: flujo Apply, Discard, Exit, bloqueo por incidencias, limpieza de estado, Escape, sin errores JavaScript ni desbordamiento horizontal, PASS a 1920×1080 y 1280×720. No son pruebas funcionales de B5 en Unity.
- Referencia de jerarquía almacenada en References/ApplyDiscard_ReviewV1_DRAFT.svg. HTML y PNG residen en la conversación, NO en Git.
- **Estado:** aprobado como dirección V1 de Aplicar/Descartar/Salir con pendientes. Ajustes finos de dimensiones, color, composición responsiva y textos reales aún posibles en el cierre general. No implementar C# hasta cierre del diseño visual.

## 8. Feedback visual, snapping, errores y estados — DIRECCIÓN V1 ACEPTADA PARA CONTINUAR (2026-10-09)

### 8.1 Regla visual canónica y autoridades
- Base vinculante: EditInteractionDesign.md, decisión 005 Universal Preview; EDITOR_V2_MASTER_PLAN.md B6 (PASS) y B7 (PASS). La UI representa información de Placement Validation, BBSIS, Navigation, Construction y Finance sin sustituir sus decisiones.
- Estados separados y visibles: **seleccionado**, **en transporte/colocación provisional**, **snapping sugerido**, **validado**, **conflicto**, **confirmado** y **cancelado**. El preview nunca realiza commit solo por existir.
- Mantener las **esquinas cortas miel/latón y microanimación V1** ya aprobadas al seleccionar. NO volver a cuadros CAD, círculos gigantes u hologramas. Seleccionar no eleva el asset; elevar/asentar suavemente solo al mover/soltar.
- Posición provisional con huella discreta y ghost tenue de la anterior ubicación cuando sea informativo; la geometría 3D mantiene sus materiales. No pintar modelos enteros de verde o rojo.
- Snapping se expresa mediante guía cian corta y pulso breve junto al destino; puede sugerir mesa, pared, superficie, anfitrión de opening o relación de grupo según perfil real. **Sugerencia NO equivale a validación**: confirmar depende de Placement/BBSIS/Construction y la validez final.
- Problemas: localizar el conflicto espacial o ruta comprometida con trama/huella ámbar y explicación concreta en el inspector, sin modal rutinario ni rojo genérico. Reservar rojo UI para acciones destructivas.
- Un problema no provoca cambios ocultos, compras, commit ni cambios de posición automáticos. El jugador puede corregir, cancelar el gesto con Esc o continuar.
- En muros y habitaciones, volumen provisional translúcido limitado a geometría relevante; puertas/ventanas solo sobre segmento anfitrión válido. No levantar muros definitivos ni simular trabajo.

### 8.2 Diseño de pantalla propuesto
- **Centro:** espacio principal del restaurante; la huella y puntos de snap aparecen cerca del objeto; pequeña pastilla contextual informativa por encima o junto a la operación, evitando tapar la zona de trabajo.
- **Inspector derecho contextual:** título «Vista previa», estado compacto, explicación de una frase, resumen de Snapping / Huella / Validación y, solo si hay conflicto, bloque de causa con el objeto o ruta afectada. «Confirmar gesto» únicamente cuando el validador permita; «Cancelar» nunca descarta la reforma entera.
- **Catálogo izquierdo y barras:** se mantienen Galería Viva y tres grupos MODO/EDITAR/AYUDAS. Botón Snapping con miel cuando está activado; sin duplicar sistemas de snapping ni categorías de construcción en catálogo mobiliario.
- **Casos mostrados:** colocación válida; sugerencia de snapping; superposición con otro mueble; ruta de circulación comprometida; construcción de pared provisional. Si una situación carece de diagnóstico real, usar «Comprobando…» / «No se pudo verificar» sin inferir seguridad.
- **Responsive:** a 1920×1080 inspector abierto, sin tapar viewport. A 1280×720 inspector plegado inicialmente y expandible como rail; las ayudas locales continúan visibles sin invadir ambas barras.

### 8.3 Preview HTML real — creada y probada, NO integrada
- Archivo autónomo de esta conversación: `EditorV2_Feedback_Snapping_INTERACTIVO.html`. No se copia a Unity ni se modifica el comportamiento runtime.
- Interacciones de la demo: elegir cinco estados; clic/arrastre de una mesa de prueba; botón de snapping; Comprobar (validador **simulado**) antes de confirmar un snap o pared; Confirmar gesto; Cancelar/Esc; recorrido secuencial animado; inspector desplegable a 1280×720.
- Capturas 1920×1080 y 1280×720: `EditorV2_Feedback_Valido_1920.png`, `EditorV2_Feedback_Snapping_1920.png`, `EditorV2_Feedback_Conflicto_1920.png`, `EditorV2_Feedback_Construccion_1920.png`, y correspondientes vistas 1280. Escena 3D de referencia **estática**, sobreimpresiones HTML dinámicas, valores y lógica de validación ilustrativos.
- Verificación Chromium: selección de 5 casos, snap toggle, separación entre sugerencia/comprobación, bloqueo de confirmar ante colisión/ruta, confirmar/cancelar, drag, Escape, recorrido automático, inspector compacto, errores JS 0 y sin overflow horizontal, PASS en 1920×1080 y 1280×720. Son pruebas de UI web, **NO** del código C#/Unity ni de rendimiento del editor.
- Diagrama vectorial de estados, referencia versionada: `References/FeedbackSnapping_StatesV1_DRAFT.svg`. HTML y PNG solamente como adjuntos del chat.

**Estado:** el usuario indicó «avancemos, superficies y revisión de coherencia todo junto» tras ver la preview HTML; se consolida la dirección visual/UX V1 para seguir al diseño de Superficies y la revisión final. Intensidades, diagnósticos y tiempos podrán pulirse; no implica validación de implementación Unity ni cierre visual global.

## 9. Superficies — PROPUESTA DE DISEÑO V1 (2026-10-09, PENDIENTE DE APROBACIÓN)
**Aprobaciones de referencia:** estilo Galería Viva/inspector, barra inferior MODO/EDITAR/AYUDAS, marco marfil/latón, Recoleta/Inter, selección miel, no alterar assets al seleccionar y transacciones Apply/Discard. El botón «Superficies» pertenece al grupo MODO, no a la categoría de muebles.

### 9.1 Auditoría funcional previa
- Existe la autoridad de dominio de acabados estructurales: BistroBuilderApplySurfaceFinishCommand y registros SurfaceFinishPatch con Undo/Redo, junto con el contrato B5 de reforma transaccional.
- Render de superficies observado en BistroBuilderSurfaceFinishVisuals.cs: soporta explícitamente parche con surfaceRole=floor y finishDefinitionId=finish.floor.default, usando el material floorMaterial del kit. **Esto NO demuestra soporte de cualquier textura elegida por jugador**, acabados de pared, techos, zócalos o exterior.
- La taxonomía antigua RestaurantEditCatalogSections.Tabs(Surfaces) enumera Todas/Suelos/Paredes/Techos/Zócalos/Exterior. La presencia de una pestaña NO demuestra que su aplicación sea funcional. Distinguir el soporte verificado de la UI futura.
- SAVIC/definiciones publicadas proveen materiales y metadatos; Construction Authoring decide geometría/destinos; BBSIS/validación decide viabilidad; Finance calcula el coste; Preview universal muestra candidatos; Apply/Discard publica/revierte la reforma.
- No prometer pintura libre con pincel de pixeles, UV dinámicas, redimensionado CAD, techos jugables ni múltiples plantas: requieren definición técnica aparte.

### 9.2 Composición visual propuesta
- **Izquierda:** «Taller de superficies», mismo tratamiento que el Taller de construcción y Galería Viva. Cabecera, búsqueda, pestañas Todos/Favoritos/Recientes, familias Suelos/Paredes/Zócalos/Exterior; Techos solo si hay contrato real y decisión específica. Muestras de materiales grandes (miniaturas), etiquetas y filtros por ubicación/compatibilidad cuando existan metadatos.
- **Centro:** elegir acabado y destino sobre una superficie autorizada; overlay muy tenue con patrón del acabado y perímetro miel, sin ocultar el pavimento original ni mantener cuadrícula permanente. En el demo existe una región esquemática clicable; no corresponde a geometría real de Unity. Snapping cian breve cuando la herramienta lo requiera, sin confundir sugerencia con validez.
- **Derecha:** «Inspector de superficies», imagen grande de la muestra, material, destino, área calculada por geometría, compatibilidad y coste SOLO si Finance publica valores fiables. Estado visible «sin destino», «vista provisional», «comprobando», «compatible», «no compatible»; acciones «Cancelar gesto» y «Confirmar gesto» separadas de «Aplicar reforma» global.
- **Flujo:** elegir material -> elegir superficie -> previsualizar sin mutación -> verificar con autoridad real -> confirmar gesto en Draft -> Aplicar/Descartar global según B5. Escape cancela un gesto, no toda la reforma. Undo/Redo global conserva orden entre muebles, paredes y superficies.
- **Colores:** miel indica elección/selección; ámbar informa una limitación; cian solo guía contextual; rojo para Eliminar/Descartar. No teñir objetos enteros de verde/rojo.

### 9.3 Responsive y calidad
- **1920×1080:** catálogo a la izquierda con dos columnas de muestras, escena central predominante e inspector derecho visible. Mantener ambas barras y límites de clic legibles.
- **1280×720:** catálogo compacto de dos muestras por fila si hay ancho y scroll propio; inspector como rail plegable inicialmente y desplegado al iniciar una preview o solicitar propiedades; escenario nunca tapado por un modal rutinario.
- La escena de la preview conserva partes de una maqueta previa (marcas de selección de mueble) integradas en la fotografía fuente; no son parte aprobada de la herramienta Superficies y se eliminarán del arte de runtime.
- No se integran materiales, shader ni familia Surface nueva en Unity en este bloque de diseño.

### 9.4 HTML de prueba
- Entregado en esta conversación: EditorV2_Superficies_Coherencia_INTERACTIVO.html, autónomo con fotografía fuente y cinco muestras de textura generadas para la demo. Clic sobre muestras, elección de tipo, previsualización, comprobación simulada, confirmación simulada al Draft, cancelación/Escape, Undo/Redo, revisión general y modales ilustrativos.
- Pruebas de navegador Chromium/Playwright: en **1920×1080 y 1280×720 PASS**, sin errores JavaScript, sin desbordamiento horizontal; selección de acabado, preview que no permite confirmar antes de comprobar, confirmación de suelo, Undo/Redo, rechazo de familia sin soporte verificado, revisión de coherencia, modales y Escape.
- Capturas de la conversación: EditorV2_Superficies_1920.png, EditorV2_Superficies_Preview_1920.png, EditorV2_Superficies_1280.png, EditorV2_Superficies_Preview_1280.png. **No son capturas ni pruebas de Unity**.
- Referencia versionada en Git: References/Superficies_CoherenciaV1_DRAFT.svg. Los PNG/HTML viven en el chat, no en la rama hasta contar con un mecanismo de copia aprobado.
**Estado:** dirección artística/UX propuesta, por aprobar; la fotografía, materiales y cifras de la demo no son activos canónicos.

### 9.5 Decisión provisional — «Proyector Inteligente» (2026-10-09)
**Elección expresada por el usuario:** «creo que voy a elegir Proyector Inteligente pero si luego una vez programado y probandolo dentro de una build no me gusta, tendríamos que cambiarlo».
- **ESTADO: SELECCIONADO COMO CONCEPTO PREFERIDO PARA PROTOTIPAR, APROBACIÓN DEFINITIVA CONDICIONADA A BUILD JUGABLE.** No se considera cerrado ni se modifica aún Unity por esta decisión.
- Se toma como referencia de intención el HTML `EditorV2_Superficies_Concepto03_ProyectorInteligente.html` mostrado en el chat. El HTML se basa en CSS sobre una fotografía: **NO demuestra** mapeado correcto en la geometría real, oclusión de muebles ni materiales PBR. No presentar efectos de la demo como resultados de Unity.
- Requisito visual prioritario: la superficie editada debe mostrar un **reemplazo real del acabado** sobre la geometría/mesh que corresponda, respetando UV/proyección del plano, dirección, escala de piezas, iluminación, uniones, límites y oclusiones. Evitar capas flotantes, z-fighting, mosaicos desalineados, bordes postizos y transparencia que mezcle el acabado anterior.
- «Inteligente» significa **asistencia contextual justificada** para elegir orientación/escala y encaje (cuando haya datos disponibles), más guías efímeras y opción de ajuste manual; no prometer IA externa, estimaciones inventadas de confianza ni autorizar geometría incompatible. No mostrar porcentaje de «confianza» de la maqueta como dato funcional.
- Dividir interfaz/visualización de catálogo, selección de acabado, inspector y Preview Universal respecto del método concreto de proyección/aplicación del material. Respetar SAVIC, Construction Authoring, BBSIS/validación, Finanzas, B5 y Save/Load; no introducir una segunda autoridad de estado. Conservar claves de acabado y parámetros reproducibles para permitir cambios de presentación sin migraciones improvisadas ni romper partidas.
- **Revisión obligatoria tras build:** probar variedad de pavimentos (madera, baldosas, patrones direccionales) en geometrías rectangulares, irregulares, límites y encuentros; objetos encima del suelo; cámara inclinada y zoom; luces variadas; 1920×1080 y 1280×720; uso real de mouse/teclado; frame time/FPS en PC objetivo; Undo/Redo, Cancelar, Aplicar/Descartar, Save/Load. Comprobar especialmente que no parece un parche pegado.
- **Salida de revisión:** si el resultado no convence, iterar el render/UX o sustituir la estrategia de preview (por ejemplo reemplazo directo o comparador), **sin obligar al usuario a quedarse con Proyector Inteligente**. La propuesta alternativa debe validarse visualmente y probarse; nunca mezclar ni publicar soluciones que no superen pruebas técnicas y visuales. No fusionar en `integration/master-current-20260918` por esta sola selección provisional.

## 10. Revisión de coherencia conjunta — DIAGNÓSTICO, NO CIERRE AUTOMÁTICO (2026-10-09)
**Resultado del diseño actual:** estructura de cinco zonas, barra superior diferenciada y badge Opción 1, barra inferior en tres grupos, Galería Viva, inspector adaptativo, Taller de construcción, selección con esquinas doradas/microanimación, reforma Apply/Discard y lenguaje Universal Preview son compatibles como CONCEPTO. No se ha realizado una build Unity por este chat.

| Área | Evaluación | Decisión / trabajo restante |
|---|---|---|
| Identidad visual global | CONSOLIDADA | Mantener marfil/latón, miel activo, tipografía Recoleta e Inter y el logo aportado; no reinterpretar el logotipo ni incluir textos autosave no demostrados |
| Barra inferior | APROBADA EN DIRECCIÓN | Tres grupos MODO/EDITAR/AYUDAS; habilitación real por capacidades; confirmar iconos BB, variantes de hover y proporciones finales |
| Catálogo e inspector | APROBADOS EN DIRECCIÓN | Galería Viva; datos de SAVIC; selección de objeto cambia inspector; sin controles imaginarios de escalado o rotación |
| Taller de construcción | ORGANIZACIÓN APROBADA | Construir reemplaza catálogo de muebles; confirmar acabados/iconos y estados por herramienta |
| Selección y grupos | HALO DE HUELLA APROBADO VISUALMENTE | Esquinas doradas por volumen proyectado + halo de huella de suelo + perímetro común de grupo; contador y microanimación; falta validar proyección en GLB y decidir inspector de grupo |
| Aplicar/Descartar | BASE V1 APROBADA | Revisión, confirmación destructiva, bloqueo de salida; ajustes de copy/medidas/resultado runtime |
| Feedback y snapping | BASE V1 ACEPTADA PARA SEGUIR | Preview universal contextual; snapping sugiere, validación manda; no enseñar ghost como elemento ya creado |
| Superficies | CONCEPTO PREFERIDO PROVISIONAL | «Proyector Inteligente» sujeto a aceptación en build jugable; material realmente integrado en geometría y revisión de todas las familias. Cambio de técnica permitido sin rehacer dominios |
| Responsive 1280×720 | DEMO WEB PASS | Inspector plegable, cabeceras legibles y herramientas a la vista; quedan comprobaciones en Unity, escalas TMP, resolución y cámara |
| Rendimiento / accesibilidad | PENDIENTE EN UNITY | Probar interacción PC, scroll, foco y ratón; contrastes, miniaturas masivas y rendimiento real sobre hardware objetivo |

### 10.1 Reglas para la futura implementación
1. Una sola herramienta/familia activa: Colocar = Galería Viva; Construir = Taller estructural; Superficies = Taller de acabados; Seleccionar = inspector de selección. No colocar categorías de muebles dentro de arquitectura.
2. La selección pasiva no mueve un objeto; solo el gesto de transporte produce elevación. Las marcas de selección y su animación no escriben el estado de dominio.
3. Una vista previa NUNCA aplica reforma. Previsualizar, Confirmar gesto dentro del Draft y Aplicar global son acciones separadas y reversibles.
4. Habilitación de acciones, coste y diagnósticos proceden de SelectionCapabilities, Construction/Placement/BBSIS/Finance. La pantalla no declara soportada una categoría por existir como tab.
5. UX sin móvil: resolver 1920×1080 y 1280×720 con scroll local en catálogos e inspector plegable; conservar barras visibles.
6. Los iconos oficiales de BB son requisito de arte final. No sustituir su identidad por glifos Unicode usados solo como placeholders en esta demo.
7. Estado de aprobación registrado por COMPONENTE; ninguna pantalla completa generada concede automáticamente aprobación a toda la UI.

### 10.2 Preview y resultado de verificación
- El HTML mencionado incluye botón **«Revisión UX»** con 10 apartados (seis sin contradicción de concepto, cuatro abiertos), demostración de cambio de modos, visor de material, inspector y barra inferior.
- Las capturas EditorV2_Coherencia_1920.png y EditorV2_Coherencia_1280.png documentan la jerarquía de referencia. La demo PASS navegador en ambos tamaños no equivale a validación final ni prueba de Unity.
- No se han cerrado nuevas decisiones visuales de Superficies, inspector múltiple, iconografía o responsive final sin la validación del usuario.
- Para cerrar el diseño se requiere revisar esta preview y los bloqueos de la tabla, y registrar el visto bueno explícito.

## 11. Registro de aprobación
- 2026-10-08: cinco zonas de distribución general aceptadas **provisionalmente** como punto de partida. No implica aprobación de la imagen conceptual al detalle, tamaño exacto, microinteracciones o componentes particulares.
- 2026-10-08: **aprobado** que la barra de Editor V2 sustituya completamente la navegación superior normal de diez secciones y se diferencie de modo inequívoco; la apariencia y los controles definitivos siguen pendientes de preview y revisión.
- 2026-10-08: **confirmada Opción 1** del distintivo «MODO EDICIÓN», lápiz y regla cruzados, manteniendo el logotipo oficial sin reinterpretarlo.
- 2026-10-08: **dirección artística y distribución de la barra inferior aceptadas expresamente** («me gusta muchisimo esa barra inferior»). No incluye aprobación de todas las zonas que aparecen en esa escena de ejemplo.
- 2026-10-08: **Galería Viva elegida expresamente** como concepto artístico y estructural del catálogo y el inspector derecho, exclusivamente. Sustituye la anterior propuesta genérica; las imágenes de los otros menús y el restaurante no se consideran aprobadas.
- 2026-10-08: **estados e inspector V3 complementarios aceptados como dirección de diseño** al continuar hacia construcción; responsive y microinteracciones finales sujetos a pruebas visuales. Mantener controles regidos por capacidades reales y avisos económicos no destructivos sin rojo.
- 2026-10-08: **organización del Taller de construcción aprobada** tras las previews de pared/habitación (usuario: «si, avancemos»). Galería Viva se sustituye por Taller al pulsar Construir; inspector en contexto estructural. Acabados específicos y microinteracciones pendientes.
- 2026-10-08: **estilo de multiselección aprobado expresamente** («me quedo con esta forma de seleccion»): esquinas cortas doradas en cada elemento y pastilla superior oscura con contador; quedan descartadas como diseño base las otras variantes de círculos/contornos completos/números.
- 2026-10-08: **microanimación de selección aprobada como base V1 tras preview HTML interactiva**: usuario «me convence, no descarto futuros arreglos, pero hoy me vale. avancemos». Abarca clic simple, multiselección aditiva y deselección; conserva la posibilidad de mejoras posteriores, sin tocar Unity.
- 2026-10-08: **Aplicar / Descartar V1 aprobado como base afinable** («me gusta, sigamos»), tras HTML interactivo y previews 1920×1080 / 1280×720. Se fija la separación de aplicar, descartar y salir con cambios; no se aprueba un guardado automático ni cifras simuladas.
- 2026-10-09: **feedback / snapping / errores y estados V1 adoptado como base de diseño para continuar** («avancemos, superficies y revisión de coherencia todo junto»). HTML presentado con validez, snap sugerido, obstáculo, circulación y pared provisional. Confirmar requiere validación: snapping no la sustituye.
- 2026-10-09: **Superficies + coherencia global V1** presentadas en una única preview HTML interactiva y dos tamaños, con 10 verificaciones de coherencia. Superficies y cierre responsive PENDIENTES del visto bueno; solo el diseño gráfico existente aprobado permanece vinculante. El soporte runtime verificado de superficies no incluye automáticamente todas las familias del catálogo heredado.
- 2026-10-09: **«Proyector Inteligente» elegido provisionalmente** para la preview de superficies; aceptación final SOLO tras programar y probar en build real. Si no convence, cambiarlo manteniendo contratos de materiales, transacciones y datos, y sin publicar versiones fallidas.
- 2026-10-09: **Inspector de doble contexto Principal/Conjunto presentado en HTML**, preservando Galería Viva, foto/propiedades del principal y grupo accesible desde pestaña o contador. Propuesta responsive 1920/1280 con rail en compacto. Demo web probada, pendiente visto bueno; no modifica Unity.
- 2026-10-09: **«Halo de Huella» (Concepto 2) elegido expresamente** con imagen aportada por el usuario («así es como quiero que sea»), supersede las alternativas y los anteriores overlays descentrados. Prueba SVG+HTML 1920/1280 PASS geométrico; pruebas 3D Unity pendientes. Se conserva el criterio de geometría real/huella, sin offsets por pantalla.
- No implementar C#/Unity ni sustituir UI heredada antes del cierre visual explícito.
