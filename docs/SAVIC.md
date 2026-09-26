# SAVIC — Sistema de Autoría, Validación e Integración de Contenido

**Proyecto:** Bistro Builder  
**Motor:** Unity 6000.3.19f1  
**Estado:** diseño funcional y técnico V1 cerrado; implementación V1 activa
**Naturaleza:** herramienta interna de desarrollo, principalmente Editor-only  
**Objetivo de escala:** cientos, miles y decenas de miles de recursos a lo largo de la vida del proyecto

## 0. Resumen en cristiano

SAVIC es la fábrica automática de contenido de Bistro Builder.

El flujo normal debe ser: crear o descargar un recurso, copiarlo a "ContentInbox/DropHere" y dejar que SAVIC haga el resto.

SAVIC detecta el recurso, averigua qué es, lo analiza, corrige lo seguro, prepara prefab/materiales/colliders/previews/metadatos, lo registra en los sistemas de Bistro Builder, lo prueba y devuelve uno de estos resultados:

- PASS.
- CORREGIDO AUTOMÁTICAMENTE + PASS.
- REQUIERE REVISIÓN.
- ERROR / NO APTO.

La revisión humana debe ser la excepción. Si una misma revisión aparece repetidamente, se considera una carencia de SAVIC y debe convertirse en una mejora general del sistema.

SAVIC nunca debe caer en bucles infinitos de analizar-corregir-reanalizar. Cada corrección tiene límites, medición de progreso y rollback.

## 1. Misión exacta

[V1] Convertir contenido fuente heterogéneo en contenido válido, trazable, reproducible y listo para producción en Bistro Builder, con la menor intervención manual posible.

[V1] Orquestar sistemas existentes, no reemplazarlos.

[V1] Poder reconstruir qué se hizo con cualquier recurso: fuente, reglas, versión, correcciones, validaciones y resultado.

[V1] Ser usable con miles de recursos sin convertir el Editor en una herramienta lenta.

## 2. Qué resuelve y qué no

SAVIC sí resuelve:

- ingesta automática;
- clasificación;
- análisis;
- normalización;
- preparación de prefabs;
- colliders;
- previews;
- metadatos;
- registro en catálogos;
- contratos de colocación, navegación, BBSIS y Save/Load;
- validación;
- autocorrección;
- mantenimiento y revalidación;
- lotes y trazabilidad.

SAVIC no será:

- Blender ni un editor 3D general;
- el sistema de colocación;
- BBSIS;
- Navigation;
- Save/Load;
- el Sistema de Acabados;
- el sistema de economía;
- una IA que decide sin control;
- un sustituto de AssetDatabase.

## 3. Principios no negociables

[V1] Automatización primero.

[V1] Idempotencia: procesar dos veces lo mismo no crea duplicados ni degrada el resultado.

[V1] Determinismo: misma entrada + mismas reglas + mismas versiones = mismo resultado funcional.

[V1] No destructivo: el original nunca se modifica silenciosamente.

[V1] Trazabilidad completa.

[V1] Separación clara entre fuente, temporal y aprobado.

[V1] Procesamiento por lotes.

[V1] Revisión humana solo para ambigüedad real.

[V1] Validadores y reglas modulares.

[V1] Sin parches por asset individual.

[V1] Integración mediante contratos con sistemas canónicos existentes.

[V1] Funcionamiento offline para todo lo crítico.

[V1] Rendimiento como criterio de PASS.

[V1] Convergencia obligatoria: ninguna corrección puede repetirse indefinidamente.

## 4. Flujo automático completo

1. El desarrollador copia uno o muchos archivos a "ContentInbox/DropHere".
2. SAVIC espera a que cada archivo termine de copiarse.
3. Calcula huella/fingerprint y comprueba duplicados.
4. Retira el archivo de DropHere y archiva el original en ContentSource.
5. Identifica formato y familia probable.
6. Extrae hechos: dimensiones, jerarquía, meshes, materiales, texturas, bounds, etc.
7. Clasifica tipo, categoría y perfiles aplicables.
8. Genera un Processing Plan antes de modificar nada.
9. Ejecuta normalizaciones y autocorrecciones permitidas.
10. Genera artefactos derivados en staging.
11. Ejecuta validación técnica.
12. Ejecuta pruebas de integración.
13. Si todo es válido, publica de forma atómica.
14. Registra el contenido en los sistemas correspondientes.
15. Genera/actualiza previews de UI.
16. Ejecuta post-validación.
17. Guarda historial y deja el asset en PASS, AUTO-CORRECTED, REVIEW o ERROR.

DropHere debe volver a quedar vacía automáticamente cuando SAVIC haya recogido los recursos.

## 5. Estados canónicos

Estados normales:

- NEW
- INGESTED
- ANALYZED
- CLASSIFIED
- PLANNED
- PROCESSING
- VALIDATING
- APPROVED
- PUBLISHING
- PUBLISHED
- PASS

Estados excepcionales:

- AUTO_CORRECTED
- NEEDS_REVIEW
- FAILED_SOURCE
- FAILED_PROCESSING
- FAILED_INTEGRATION
- QUARANTINED
- STALE
- SUPERSEDED

"STALE" significa que el asset era válido, pero una regla, builder o contrato cambió y necesita revalidación o regeneración parcial.

## 6. Estructura de carpetas

Única carpeta que el usuario debe usar normalmente:

"C:/Users/mruperez/ProyectoBB/BistroBuilder/ContentInbox/DropHere"

Estructura propuesta:

~~~
BistroBuilder/
├─ ContentInbox/
│  └─ DropHere/
├─ ContentSource/
│  └─ [originales archivados por SAVIC]
├─ SAVIC/
│  └─ Manifests/
├─ Assets/
│  └─ BistroBuilder/
│     └─ Content/
│        └─ Approved/
│           ├─ Furniture/
│           ├─ Equipment/
│           ├─ Decoration/
│           ├─ Construction/
│           ├─ Materials/
│           ├─ UI/
│           └─ ...
└─ Library/
   └─ BistroBuilder/
      └─ SAVIC/
         ├─ Cache/
         ├─ Index/
         ├─ Jobs/
         ├─ Logs/
         └─ Staging/
~~~

[V1] ContentInbox es entrada, no almacenamiento.

[V1] ContentSource conserva los originales.

[V1] Library/BistroBuilder/SAVIC contiene todo lo reconstruible y no se versiona.

[V1] Assets/.../Approved contiene lo que el juego necesita en runtime/editor.

[R] Los binarios fuente grandes se gestionarán con Git LFS cuando se cierre la política de versionado de fuentes.

## 7. Inventario del contenido que ya existe

[V1] SAVIC no obligará a volver a pasar por ContentInbox todo lo que ya existe en Bistro Builder.

En la primera adopción hará una auditoría del proyecto y clasificará el contenido existente como:

- GESTIONADO POR SAVIC.
- LEGADO ADOPTADO.
- LEGADO PENDIENTE.
- FUERA DE ALCANCE.

Para placeables deberá reconocer como mínimo prefabs, RestaurantPlaceableItemDefinition, catálogo principal, EditableObjectDefinition, previews y dependencias.

[V1] Mover o renombrar un asset no debe convertirlo en un asset nuevo.

[V1] La adopción no puede duplicar IDs ni entradas de catálogo.

## 8. Identidad y procedencia

No se usará un único GUID de Unity como identidad de gameplay.

SAVIC manejará:

- SourceHash: SHA-256 del contenido fuente.
- IngestId: identidad temporal de una ingesta antes de resolver el contenido.
- SavicId: identidad interna estable del registro de autoría.
- CanonicalContentId: ID estable del sistema propietario en Bistro Builder.
- ProcessingRunId: identidad de una ejecución.
- ArtifactRole: prefab, thumbnail, detail-preview, collider-data, manifest, etc.

Para placeables, el CanonicalContentId debe mapear al ItemId estable de RestaurantPlaceableItemDefinition en lugar de inventar un segundo ID runtime competidor.

El manifest registrará también GUID/ruta de Unity como referencia de autoría, pero no como identidad runtime única.

## 9. Manifest o ficha SAVIC

[V1] Cada contenido gestionado tendrá una ficha versionable y legible.

Campos mínimos:

- versión de esquema;
- SavicId;
- CanonicalContentId y IDs de integración;
- SourceHash;
- ruta de fuente archivada;
- familia, tipo, categoría y subcategoría;
- inferencias y confianza;
- dimensiones y orientación;
- piezas y zonas semánticas;
- materiales y texturas;
- artefactos generados;
- perfiles de colocación;
- perfiles espaciales;
- previews;
- dependencias;
- reglas aplicadas;
- fixes aplicados;
- decisiones humanas protegidas;
- validaciones;
- integración;
- estado;
- versiones de pipeline/builders/validators;
- historial mínimo de procedencia.

Los índices rápidos de SAVIC serán reconstruibles y no serán fuente de verdad.

## 10. Cómo reconoce qué tiene delante

SAVIC combinará evidencias, no una sola pista.

Fuentes de evidencia:

- extensión/formato;
- nombre del archivo;
- nombres internos;
- dimensiones;
- proporciones;
- bounds;
- distribución geométrica;
- número y posición de componentes;
- superficie de apoyo;
- materiales;
- texturas;
- jerarquía;
- piezas semánticas;
- procedencia;
- reglas de familia;
- IA opcional en el futuro.

La geometría y estructura pesan más que un nombre poco fiable.

Cada inferencia guarda su propia confianza:

- CERTAIN;
- HIGH;
- MEDIUM;
- LOW;
- UNKNOWN.

La decisión de revisión depende de confianza + criticidad. Una etiqueta secundaria LOW puede tolerarse; una clasificación crítica LOW no.

## 11. Familias 3D V1

Orden inicial:

1. Mesa.
2. Silla.
3. Decoración.
4. Equipamiento.
5. Puerta.
6. Pared/ventana.

Después:

- bancos y sofás;
- taburetes;
- lámparas;
- barras/mostradores;
- otras familias.

Perfiles base:

Mesa:
- suelo;
- pivot inferior;
- huella física;
- tablero/patas/base;
- acabados;
- BBSIS seating.table cuando corresponda.

Silla:
- suelo;
- pivot inferior;
- asiento/respaldo/base/brazos;
- orientación frontal;
- altura de asiento;
- seating.chair;
- Seat Bay/relaciones espaciales según contrato.

### Estado de implementación V1 - Sillas

[V1 IMPLEMENTADO] SAVIC dispone de un perfil geométrico específico de silla que detecta una superficie de asiento en altura intermedia, calcula su altura real y normalizada, mide su cobertura, exige estructura inferior y busca superficie vertical superior compatible con respaldo.

[V1 IMPLEMENTADO] La orientación frontal se infiere geométricamente a partir de la posición del respaldo: se registra eje del respaldo, lado, sesgo hacia el borde y vector frontal local. No depende del nombre del archivo.

[V1 VALIDADO] Las seis sillas canónicas actuales se reconocen como `Chair` usando nombres neutros (`asset_X.glb`) y evidencia exclusivamente geométrica. Cuatro assets canónicos no-silla se rechazan como sillas y un nombre deliberadamente contradictorio `chair_table` queda sin clasificación automática.

[V1 VALIDADO] La silla canónica más conservadora detecta asiento a ~0,453 m; las demás referencias de producción quedan en ~0,453-0,455 m. La calibración actual infiere además correctamente el frontal `+Z` en las referencias conocidas, con respaldo en `-Z`.

[V1 IMPLEMENTADO] La semántica de silla agrupa geometría por función y no por número de meshes. Produce `chair.seat` (`Seat`), `chair.back` (`Backrest`) y `chair.support` (`LegSet` o `BaseSupport`); `chair.arms` (`ArmSet`) aparece solo cuando existe evidencia bilateral suficiente.

[V1 IMPLEMENTADO] El soporte de silla analiza contactos con suelo. Las seis sillas canónicas actuales se resuelven como `MULTI_CONTACT` con cuatro zonas, por lo que sus patas independientes quedan agrupadas en un único `LegSet` conceptual.

[V1 VALIDADO] Las seis sillas canónicas alcanzan `automationReady` con asiento, respaldo y soporte en confianza suficiente. Una prueba sintética de ocho meshes independientes confirma además que cuatro patas se agrupan en un solo `LegSet` y dos brazos se agrupan en un solo `ArmSet` sin depender de nombres de pieza.

[V1 IMPLEMENTADO] El planificador de autoría de sillas normaliza por altura de asiento, no por altura total: acepta el rango seguro de comedor, aplica escala uniforme solo cuando es necesario y detiene la automatización si la corrección requerida es excesiva.

[V1 IMPLEMENTADO] La orientación detectada se normaliza al frente canónico `+Z`. Se han validado fuentes orientadas a `+Z`, `+X`, `-X` y `-Z`, generando automáticamente el yaw visual necesario sin alterar la autoridad runtime de `RestaurantSeat`.

[V1 IMPLEMENTADO] Los colliders de silla dejan de ser una caja de cuerpo completo. SAVIC genera un collider semántico para asiento, uno para respaldo y colliders independientes para las zonas de apoyo; en las seis sillas canónicas actuales el resultado es `1 Seat + 1 Backrest + 4 Supports` (6 colliders). Los brazos, cuando se detectan, se representan como dos colliders laterales y nunca como una caja que cierre artificialmente el hueco central.

[V1 VALIDADO] Las seis sillas canónicas pasan el planificador y la generación/validación de colliders compuestos. Se comprueba además que no queda ningún collider legacy fuera de `SAVIC_Collision` y que el volumen compuesto no degenera en una caja sobredimensionada equivalente al cuerpo completo.

[V1 IMPLEMENTADO] La silla dispone de publicación transaccional completa a prefab, definición colocable, previews y catálogo. La identidad canónica y los GUID de los assets publicados se conservan al reprocesar, los valores manuales de economía permanecen intactos y un fallo revierte al último estado válido.

[V1 IMPLEMENTADO] El módulo de familia `Chair` valida los contratos canónicos antes de publicar: `seating.chair` queda bajo autoridad de BBSIS, Navigation recibe una huella y un punto de aproximación coherentes con el frente `+Z`, y Save/Load resuelve el mismo `CanonicalContentId` desde el catálogo canónico.

[V1 VALIDADO] La prueba vertical desechable de silla recorre clasificación, semántica, planificación, colliders, publicación, previews, catálogo, BBSIS, navegación, persistencia, idempotencia y rollback sin dejar residuos diagnósticos. Mesa y silla publican ahora a través del registro común de módulos de familia, sin bifurcar el kernel por asset.

Decoración:
- suelo, superficie, pared o techo;
- mínima funcionalidad salvo cuando el tamaño afecte colocación/navegación.

Equipamiento:
- visual + mapeo a tipo canónico de gameplay existente;
- SAVIC nunca inventa comportamiento de gameplay.

Puerta:
- marco, hoja, tirador y posible eje/parte móvil;
- arquitectura.door;
- Dynamic Sweep cuando corresponda.

Pared/ventana:
- integración con el sistema constructivo canónico;
- no duplicar alturas, grosores o reglas como constantes SAVIC;
- leer siempre perfiles/contratos actuales del sistema propietario.

## 12. Aprendizajes trasladados desde Assets4All

[V1] Una mesh no equivale necesariamente a una pieza real.

[V1] SAVIC debe poder agrupar varias geometrías que forman una sola pieza conceptual.

Ejemplos:

- varias meshes = un tirador;
- varias meshes = un reposabrazos;
- varias superficies = una pata;
- hoja y marco = partes distintas de una puerta.

[V1] Separar análisis de hechos y decisión semántica.

[V1] Caché e incrementalidad desde el principio.

[V1] Ningún análisis pesado puede bloquear indefinidamente el Editor.

[V1] Nada de excepciones del tipo "si asset == Mesa_47".

[V1] Métricas objetivas: tiempo, cobertura, incidencias, progreso y resultado.

[R] Reutilizar técnicas de agrupación semántica contrastadas en Assets4All, pero no copiar código sin auditarlo y adaptarlo.

## 13. Sistema de reglas

[V1] Núcleo genérico y reglas por capas:

- globales;
- familia;
- tipo;
- categoría;
- origen/perfil.

La regla más específica puede completar o sobrescribir la general.

Cada regla declara:

- RuleId;
- versión;
- entradas;
- salida;
- prioridad;
- ámbito;
- posibilidad de fix;
- dependencias.

No se construirá un lenguaje DSL complejo en V1. Las reglas serán C# + perfiles de datos.

Las excepciones individuales, si alguna es imprescindible, serán ExplicitOverride documentados; nunca ifs ocultos por nombre de asset.

## 14. Autocorrección y convergencia

SAVIC debe intentar arreglar lo máximo posible, pero de forma medible y segura.

Tipos de fix:

- AUTO_SAFE: automático siempre que se cumplan precondiciones.
- AUTO_POLICY: automático porque el perfil de familia lo autoriza.
- SUGGESTED: preparado, pero requiere decisión.
- MANUAL_ONLY: SAVIC no modifica.

Reglas anti-bucle obligatorias:

- un FixId sobre el mismo estado de entrada no se ejecuta dos veces;
- cada corrección registra estado antes/después;
- una corrección debe reducir severidad, cerrar el finding o mejorar una métrica definida;
- si el estado resultante es idéntico: NO_PROGRESS y parada inmediata;
- si aparece un estado ya visto: CYCLE_DETECTED y parada;
- máximo dos rondas automáticas por el mismo finding en V1;
- si una corrección empeora el resultado, rollback;
- el último estado válido se conserva;
- un asset bloqueado no detiene el lote.

Resultado esperado: la mayoría de assets válidos deben terminar en PASS o AUTO_CORRECTED + PASS.

## 15. Materiales, texturas y acabados

SAVIC analizará:

- número de materiales;
- asignación por submesh/pieza;
- color;
- texturas;
- normal;
- metallic;
- roughness/smoothness;
- transparencia;
- resolución;
- duplicados.

Categorías semánticas iniciales:

- Wood;
- Metal;
- Fabric;
- Leather;
- Plastic;
- Glass;
- Stone;
- Ceramic;
- Concrete;
- PaintedSurface;
- Other.

[V1] Distinguir material fuente, material normalizado y slot de acabado.

[V1] Reutilizar materiales/texturas idénticos cuando sea seguro.

[V1] No fusionar materiales solo por nombres parecidos.

[V1] El original se conserva aunque se genere una versión optimizada.

[V1] Las superficies elegibles se conectan al Sistema de Acabados y Variantes mediante su contrato; SAVIC no implementa ese sistema.

## 16. Piezas, zonas semánticas y partes móviles

SAVIC podrá almacenar grupos semánticos como:

- top/tabletop;
- leg/base;
- seat;
- backrest;
- armrest;
- handle;
- door leaf;
- frame;
- drawer;
- cushion;
- work surface.

La detección debe usar geometría, proximidad, conectividad, jerarquía, materiales y reglas de familia.

Si una parte móvil es evidente, SAVIC puede prepararla.

Si no es evidente, se revisa solo esa decisión.

La geometría residual o flotante no se elimina automáticamente salvo evidencia muy alta.

### Estado de implementación V1 — Mesas

[V1 IMPLEMENTADO] SAVIC separa **región física de origen** y **pieza semántica**. Una única malla conectada puede aportar varias zonas funcionales y varias meshes independientes pueden agruparse en una sola pieza conceptual.

[V1 IMPLEMENTADO] Para mesas se obtienen actualmente:

- `table.top` / `Tabletop`;
- `table.support` / `LegSet`, `PedestalBase` o `SupportStructure`;
- patrón de apoyo `MULTI_CONTACT`, `BROAD_BASE` o revisión;
- relaciones `SUPPORTS` y `SUPPORTED_BY`;
- membership trazable hacia las regiones físicas fuente;
- confianza independiente por pieza y evidencia auditable.

[V1 IMPLEMENTADO] La publicación automática exige señales críticas independientes: superficie superior suficientemente horizontal, estructura de soporte fiable, cobertura semántica suficiente y patrón de apoyo coherente. Evidencias fuertes de una zona no pueden ocultar una señal crítica débil de otra.

[V1 IMPLEMENTADO] El análisis de topología física tiene presupuesto explícito. En geometrías patológicas o extremadamente grandes pasa a `BOUNDED_COARSE`: conserva el análisis semántico y de apoyo, limita el detalle físico almacenado y deja registrada la reducción de detalle en el manifest, en lugar de consumir memoria sin límite.

[V1 VALIDADO] La mesa real de Meshy produce `Tabletop` + `LegSet`, cuatro zonas de apoyo y cobertura semántica completa. Una prueba sintética demuestra además que cuatro meshes de patas independientes se agrupan en un único `LegSet`.

[V1 VALIDADO] El agrupado topológico tolera costuras con vértices prácticamente coincidentes incluso cuando caen en celdas distintas de cuantización. Esto evita fragmentar artificialmente una misma pieza física por pequeñas diferencias numéricas de exportación.

[V1 VALIDADO] La calibración actual rechaza como mesa automática las sillas canónicas y la decoración de prueba. Los antiguos `table_basic` de geometría cúbica se consideran placeholders de legado y no son referencias geométricas válidas para calibrar inteligencia semántica de producción.

## 17. Colliders

Objetivo: suficientemente precisos para gameplay y baratos para Unity.

[V1] Preferencia por colliders simples o compuestos.

[V1] Evitar MeshCollider complejo por defecto.

[V1] Generar collider a partir de piezas/volúmenes cuando la familia lo permita.
[V1] Recalcular collider si cambian escala, geometría o pivot relevantes.

[V1] Validar que collider y visual ocupan volúmenes coherentes.

[R] Convex decomposition controlada para formas donde realmente aporte valor.

### Estado de implementación V1 — Mesas

[V1 IMPLEMENTADO] Las mesas ya no usan un único BoxCollider de volumen completo. SAVIC genera un conjunto compuesto basado en semántica: un collider para `Tabletop` y colliders independientes para las zonas de apoyo detectadas.

[V1 IMPLEMENTADO] En patrón `MULTI_CONTACT`, cada zona de contacto con suelo produce un soporte físico independiente. El manifest registra estrategia, versión del builder, número total de colliders, número de soportes y evidencia de procedencia.

[V1 IMPLEMENTADO] La geometría de los colliders se deriva de datos normalizados, por lo que respeta escala y orientación final del asset sin depender del nombre del archivo ni de un prefab concreto.

[V1 IMPLEMENTADO] La actualización de prefabs existentes usa staging y sustitución del payload manteniendo intacto el `.meta` del destino, evitando cambiar el GUID estable aunque Windows mantenga temporalmente un handle de lectura sobre el prefab anterior.

[V1 VALIDADO] La mesa real de Meshy produce actualmente 5 colliders: 1 tablero + 4 apoyos. No queda collider raíz de caja completa. Idempotencia, rollback y republicación siguen en PASS.

## 18. Colocación y snapping

SAVIC no implementa el motor de colocación. Produce/configura los datos que el motor actual necesita.

Para placeables existentes debe alimentar:

- RestaurantEditableObjectDefinition;
- RestaurantPlacementFootprint;
- RestaurantPlaceableObject;
- RestaurantAreaMember;
- placement anchor;
- capacidades requeridas;
- pasos de rotación;
- grid custom solo cuando sea necesario;
- separación mínima;
- bloqueo de otras colocaciones.

Reglas por familia:

- suelo: mesas, sillas, equipamiento, decoración de suelo;

- pared: cuadros, apliques, ventanas y elementos compatibles;
- techo: luminarias de techo;
- superficie: decoración pequeña;
- construcción: puertas/ventanas/módulos mediante el sistema constructivo.

[V1] El snapping se expresa mediante perfiles/contratos del sistema de colocación/construcción, no mediante lógica duplicada en SAVIC.

## 19. Navegación

SAVIC prepara datos; Navigation decide cómo navegar.

Debe determinar si el contenido:

- bloquea circulación;
- no afecta circulación;
- crea paso controlado;
- requiere envelope;
- requiere reconstrucción tras publicación.

La integración respetará el patrón existente de BistroBuilderNavigationEditIntegration: agrupar cambios y reconstruir topología, no ejecutar diagnósticos exhaustivos por cada asset.

[V1] Publicar un lote debe disparar como máximo las invalidaciones necesarias y una consolidación final cuando sea posible.

### Estado de implementación V1 — Mesas

[V1 IMPLEMENTADO] SAVIC valida que la mesa publicada aporta a Navigation la huella canónica correcta, que bloquea topología estática mediante `RestaurantPlacementFootprint` y que sus dimensiones coinciden con las dimensiones normalizadas.

[V1 IMPLEMENTADO] Los puntos de aproximación de cliente y servicio de camarero se validan contra el obstáculo real de la mesa usando el radio de agente y el margen estático canónicos. Un asset no puede considerarse listo si estos endpoints quedan dentro o demasiado cerca del obstáculo.

[V1 VALIDADO] El sandbox de navegación comprueba con el servicio real que el centro de la mesa no es transitable y que los endpoints de cliente y camarero sí lo son. La navegación sigue usando la huella canónica para topología; los colliders compuestos semánticos se mantienen como física del asset y no duplican la autoridad de Navigation.

## 20. BBSIS e interacción espacial

SAVIC asignará perfiles espaciales canónicos existentes.

Familias espaciales ya presentes que pueden aprovecharse:

- generic;
- seating.table;
- seating.chair;
- architecture.door;
- work.kitchen;
- work.bar;
- work.pass;
- logistics.cart.

SAVIC genera/configura Spatial Subject/Contract, traits, ports, work edges, seat bays, sweeps o envelopes solo a través de builders/adapters aprobados.

BBSIS sigue siendo la autoridad espacial.

SAVIC debe validar que los puntos/volúmenes generados no estén en posiciones absurdas y que el contrato sea aceptado por el sistema real.

### Estado de implementación V1 — Mesas

[V1 IMPLEMENTADO] SAVIC no incrusta ni duplica la lógica BBSIS dentro del prefab. Comprueba que la configuración canónica de seating de la mesa resuelva un `Spatial Contract` real de la familia `seating.table` y deja el binding runtime bajo autoridad de BBSIS.

[V1 IMPLEMENTADO] La publicación valida los traits obligatorios `seating.table`, `seat.bays` y `service.table`, así como el número e identidad de los puertos `SeatBay` frente a la capacidad real de la mesa.

[V1 IMPLEMENTADO] El Quality Gate ejecuta el camino real `BistroBuilderSpatialBindingUtility.BindTable`: crea temporalmente el `SpatialSubject`, Adaptive Spatial Proxy y `BistroBuilderTableSpatialAdapter`, valida el subject y exige que el adapter emita exactamente los Seat Bays esperados.

[V1 VALIDADO] Para la mesa real de Meshy de 2 plazas se resuelve `spatial.contract.table.table_basic_2_rectangular`, familia `seating.table`, con 2 puertos SeatBay y 2 volúmenes SeatBay emitidos en runtime. Reprocesado, colliders semánticos, idempotencia y rollback permanecen en PASS.

## 21. Save/Load

[V1] Ningún asset se publica si no puede persistirse correctamente.

Para placeables se exige:

- ID canónico estable;
- prefab/definición resoluble;
- datos de variante/acabado serializables cuando apliquen;
- rehidratación válida;
- ausencia de referencias rotas.

Prueba mínima V1:

1. crear/colocar;
2. guardar;
3. cargar;
4. comprobar mismo ID;
5. comprobar pose;
6. comprobar variante/acabado relevante;
7. comprobar integración espacial/navegación esperada.

SAVIC no introduce otro formato de partida.

### Estado de implementación V1 — integración de persistencia

[V1 IMPLEMENTADO] `BistroBuilderSaveDefinitionCatalog` puede consumir el catálogo colocable canónico del modo edición como fuente dinámica, manteniendo la lista explícita legacy por compatibilidad. Esto evita tener que reescribir la escena por cada asset nuevo que SAVIC publique.

[V1 IMPLEMENTADO] La integración es idempotente: añadir de nuevo el mismo catálogo fuente no duplica entradas, y una definición presente a la vez en la lista legacy y en el catálogo canónico se resuelve una sola vez.

[V1 IMPLEMENTADO] La publicación de una mesa SAVIC valida ya que su `CanonicalContentId` puede resolverse desde el catálogo canónico, que el prefab de carga existe y que su `TableId` funcional es válido. El resultado queda registrado en `tablePersistence` y en la validación `SaveLoad.TableCatalogReadiness`.

[V1 VALIDADO] La mesa real publicada se resuelve correctamente por `ItemId` mediante el catálogo de persistencia, conserva idempotencia tras reprocesado y mantiene el rollback de publicación operativo.

[V1 VALIDADO] `restaurant.structure` acepta un estado sintético que contiene exclusivamente la mesa SAVIC publicada, el payload JSON se serializa/deserializa conservando el `CanonicalContentId` y el estado reconstruido vuelve a superar `ValidateState`. Esta prueba verifica el contrato real de persistencia sin modificar el mundo de juego.

## 22. Previews automáticas

[V1] Cada asset visual publicable genera al menos dos previews:

- Detail Preview: para ficha grande del artículo.
- Catalog Thumbnail: para tarjeta pequeña del catálogo.

Perfil V1 recomendado:

- detalle: 512 x 512;
- catálogo: 256 x 256;
- escena aislada;
- fondo/iluminación coherentes;
- objeto centrado;
- sombra suave;
- framing específico por familia.

La miniatura pequeña no será simplemente la grande reducida; puede tener encuadre propio.

SAVIC probará varios encuadres/perfiles cuando la calidad sea insuficiente antes de mandar el caso a revisión.

Debe reutilizar y evolucionar BistroBuilderCatalogThumbnailService y BistroBuilderCatalogThumbnailQualityService, no crear una segunda solución paralela.

El detalle grande será un builder adicional que comparta escena, framing y quality metrics con el servicio existente.

## 23. Registro en catálogo y menús

SAVIC registra contenido; la UI descubre contenido por datos.

Nunca se añadirá cada asset individualmente al código de un menú.

Para placeables se reutilizarán:

- RestaurantPlaceableItemDefinition;
- RestaurantPlaceableCatalogDefinition;
- RestaurantPlaceableCatalogService.

SAVIC completará:

- ItemId;
- DisplayName inicial;
- Category;
- subcategoría/ruta de catálogo cuando el catálogo canónico la soporte;
- descripción inicial;
- prefab;
- icono de catálogo;
- preview de detalle;
- precio inicial;
- EditableDefinition.

Ejemplo conceptual:

"Silla Bistro"
→ Mobiliario
→ Sillas
→ Comedor
→ aparece automáticamente en el menú que consulta esa categoría.

Si mañana entran 40 sillas válidas, no se editan 40 menús: se registran 40 definiciones y la UI las descubre.

## 24. Campos automáticos y campos protegidos

SAVIC controla normalmente:

- ID;
- tipo;
- categoría técnica;
- dimensiones;
- colocación;
- materiales semánticos;
- acabados;
- prefab;
- colliders;
- previews;
- integraciones;
- validaciones.

El desarrollador puede editar y proteger:

- nombre visible;
- descripción;
- precio;
- orden de catálogo;

- etiquetas comerciales;
- desbloqueo;
- disponibilidad;
- destacados.

Cuando un campo se modifica manualmente queda marcado como DeveloperOverride.

Un reprocesado no puede pisar un DeveloperOverride.

Debe existir "Restaurar control automático" por campo.

El precio sugerido por SAVIC y el precio definitivo del juego son conceptos distintos.

## 25. Actualización de assets existentes

Cuando llega una versión nueva:

- SAVIC compara con inventario;
- determina nuevo asset, actualización o similar;
- conserva identidad canónica si es actualización;
- calcula qué cambió;
- regenera solo lo afectado.

Ejemplos:

- cambia geometría → revalidar pivot, collider, footprint, previews, BBSIS si procede;
- cambian materiales → revalidar materials/finishes/previews;
- cambia tamaño → revalidar placement/navigation/BBSIS;
- cambia solo preview → no tocar gameplay.

Si la nueva versión falla, la versión anterior válida sigue publicada.

## 26. Duplicados y similitud

Tres niveles:

- DUPLICATE_EXACT: mismo contenido/hash, no se importa.
- PROBABLE_UPDATE: muy parecido y compatible con identidad existente.
- SIMILAR_CONTENT: parecido, pero posiblemente recurso diferente.

[V1] Hash exacto.

[V1] Comparación por dimensiones, jerarquía, geometría básica, materiales y nombres.

[R] Huella geométrica avanzada inspirada en Assets4All.

SAVIC nunca elimina/fusiona automáticamente dos assets distintos solo porque se parezcan.

## 27. Arquitectura interna

Capas:

1. Intake.
2. Orchestration Kernel.
3. Analyzers.
4. Classifiers.
5. Rule Engine.
6. Processing Plan.
7. Fix Engine.

8. Artifact Builders.
9. Validation Engine.
10. Publication Transaction.
11. Integration Adapters.
12. Review System.
13. Cache/Dependency Graph.
14. Audit.

El kernel no conoce qué es una mesa. Las familias aportan módulos.

Contrato conceptual de familia:

- Detector;
- Analyzer;
- Classifier;
- RuleProvider;
- FixProvider;
- ArtifactBuilder;
- Validator;
- IntegrationProvider;
- PreviewProfile.

Añadir una nueva familia no debe exigir modificar el kernel.

## 28. Reutilización de herramientas ya existentes

SAVIC debe aprovechar lo que Bistro Builder ya tiene y que ha demostrado valor.

Componentes a integrar/reutilizar:

- BistroBuilderPlaceableFactoryEngine;
- BistroBuilderPlaceableFactoryPlan;
- BistroBuilderPlaceablePrefabConfigurator;
- BistroBuilderCatalogThumbnailService;
- BistroBuilderCatalogThumbnailQualityService;
- BistroBuilderPlaceableMaintenanceService;
- BistroBuilderPlaceablePipelineSelfTest;
- BistroBuilderPlaceableQualityGate;
- BistroBuilderAssetWorkshopCatalogService;
- RestaurantPlaceableCatalogService;
- RestaurantPlacementFootprint;
- RestaurantEditableObjectDefinition;
- BistroBuilderNavigationEditIntegration;
- BistroBuilderSpatialFamilyCatalog;
- BBSIS/Interaction bridges existentes;
- Save/Load canónico.

SAVIC orquestará o refactorizará estas piezas hacia servicios reutilizables cuando convenga. No se creará una segunda fábrica de placeables, un segundo catálogo o una segunda solución de thumbnails.

## 29. Validadores

Los validadores no corrigen. Solo observan y emiten findings.

Resultado:

- PASS;
- FAIL;
- WARNING;

- INFO;
- NOT_APPLICABLE;
- SKIPPED.

Severidad:

- BLOCKER;
- ERROR;
- WARNING;
- INFO.

Ejemplos V1:

- Source.Valid;
- Identity.Unique;
- Classification.SufficientConfidence;
- Geometry.ValidBounds;
- Geometry.ScalePlausible;
- Geometry.Orientation;
- Geometry.Pivot;
- Geometry.PerformanceBudget;
- Materials.Valid;
- Materials.TextureReferences;
- Placement.ValidFootprint;
- Collider.Valid;
- Preview.DetailValid;
- Preview.CatalogValid;
- Catalog.RegisteredOnce;
- BBSIS.ContractSatisfied;
- Navigation.Compatible;
- SaveLoad.RoundTrip;
- Prefab.ValidConfiguration.

BLOCKER y ERROR impiden publicación.

## 30. Pruebas reales antes de publicar

Además de validación estática, SAVIC tendrá sandbox automático.

Pruebas por asset/familia:

- cargar prefab;
- comprobar referencias;
- colocarlo;
- rotarlo;
- validar contacto con suelo/anclaje;
- validar huella;
- validar collider;
- validar material;
- comprobar catálogo;
- comprobar preview;
- comprobar BBSIS cuando aplique;
- comprobar navegación cuando aplique;
- guardar/cargar cuando aplique.

Cada familia añade tests específicos.

SAVIC reutilizará el patrón ya existente de BistroBuilderPlaceablePipelineSelfTest: creación real en sandbox, comprobación y limpieza.

## 31. Publicación atómica

[V1] No se publica parcialmente.

Un asset no puede aparecer en catálogo si todavía tiene roto collider, Save/Load o referencias críticas.

Flujo:

- generar en staging;
- validar;
- preparar ChangeSet;
- aplicar publicación;
- integrar;
- post-validar;
- confirmar.

Si falla:

- rollback al estado anterior;
- conservar original y manifest;
- registrar incidente;
- continuar lote.

## 32. Procesamiento por lotes

Un lote contiene jobs independientes.

Ejemplo:

~~~
Batch 2026-09-22
├─ Job 001
├─ Job 002
├─ Job 003
└─ ...
~~~

Estados de cola:

- waiting;
- analyzing;
- processing;
- validating;
- publishing;
- review;
- failed;
- done.

Un asset fallido no bloquea otros.

SAVIC debe soportar:

- pause;
- resume;
- cancel;
- retry stage;
- retry asset;
- retry failed;
- revalidate affected.

## 33. Rendimiento, caché e invalidación

Regla principal: si nada relevante cambió, no se repite el trabajo.

Clave conceptual:

SourceHash
+ PipelineVersion
+ RuleSetHash
+ BuilderVersion relevante
+ DependencyHash relevante

Usar AssetDatabase.GetAssetDependencyHash y dependencias explícitas cuando corresponda.

No hacer full scan al abrir la ventana.

No recalcular geometría profunda solo para cambiar precio o texto.

No regenerar prefab para cambiar una preview.

No revalidar platos si cambia una regla de mesas.

Las operaciones masivas de AssetDatabase se agruparán cuando aporte valor y siempre con cierre seguro try/finally.

Objetivo UX: añadir 500 assets debe aumentar tiempo de máquina, no trabajo manual.

## 34. Detección de DropHere

La carpeta está fuera de Assets para que Unity no importe antes que SAVIC.

[V1] La detección robusta no dependerá solo de FileSystemWatcher.

Estrategia:

- watcher/evento como aviso rápido;
- escaneo de reconciliación periódico;
- un archivo solo se acepta cuando tamaño y fecha permanecen estables durante comprobaciones consecutivas;
- hash solo cuando el archivo está estable;
- si Unity se cierra, el siguiente escaneo reconstruye la realidad.

Esto evita eventos perdidos, copias incompletas y tormentas de notificaciones.

## 35. Importación FBX y GLB

FBX utilizará el ModelImporter/Asset Pipeline de Unity mediante un adaptador SAVIC.

Para GLB/GLTF, la recomendación V1 es Unity glTFast, paquete oficial "com.unity.cloud.gltfast", porque soporta importación Editor de .glb/.gltf y utiliza ScriptedImporter.

Actualmente ese paquete no figura en Packages/manifest.json del proyecto, por lo que su incorporación debe ser una tarea explícita del primer bloque de implementación antes de procesar los GLB del ContentInbox.

SAVIC debe ocultar el importador concreto detrás de ISourceImportAdapter para que cambiar de implementación no afecte al kernel.

## 36. Processing Plan

Antes de tocar nada, SAVIC genera un plan.

Ejemplo:

~~~
Classify as Furniture/Table
Normalize import settings
Create pivot wrapper
Build semantic material slots
Generate compound collider
Build placeable prefab
Create catalog definition

Create detail preview
Create catalog thumbnail
Register catalog
Validate placement
Validate BBSIS
Validate navigation
Validate Save/Load
Publish
~~~

El plan permite:

- dry-run;
- auditoría;
- comparar antes/después;
- anticipar impacto;
- reproducibilidad.

## 37. Cola de revisión humana

La revisión muestra solo la duda concreta.

Cada ticket contiene:

- preview;
- asset;
- decisión SAVIC;
- evidencia;
- confianza;
- propuesta;
- alternativas;
- impacto;
- validadores bloqueados.

Acciones:

- Aceptar propuesta.
- Cambiar.
- Reprocesar.
- Rechazar asset.
- Aplicar a similares.
- No volver a preguntarme esto en casos equivalentes.

[R] Agrupar tickets equivalentes en Review Clusters.

Una revisión repetitiva es una señal para mejorar SAVIC, no trabajo normal.

## 38. Aprendizaje controlado

SAVIC puede aprender de decisiones humanas, pero no cambiar reglas críticas silenciosamente.

Proceso:

1. guardar override/decisión;
2. detectar patrón repetido;
3. proponer nueva regla;
4. revisar/aprobar;
5. versionar regla;
6. medir resultado.

Clases:

- hard rule;
- learned approved rule;

- suggestion.

Una regla aprendida que empeora resultados se desactiva y puede revertirse.

## 39. UX del Editor

Ruta:

"Tools > Bistro Builder > SAVIC"

Secciones:

- Resumen.
- Cola.
- Revisión.
- Biblioteca.
- Validación.
- Historial.
- Ajustes.

No se crearán pestañas separadas para cada estado.

Resumen:

- total gestionado;
- PASS;
- autocorregidos;
- revisión;
- errores;
- stale;
- últimos lotes.

Biblioteca:

- búsqueda;
- filtros;
- familia;
- categoría;
- estado;
- origen;
- versión.

Panel derecho por asset:

- preview;
- identidad;
- clasificación;
- medidas;
- materiales;
- piezas;
- integraciones;
- validación;
- historial.

Detalles técnicos avanzados estarán colapsados por defecto.

Implementación de UI recomendada: EditorWindow + UI Toolkit con ListView virtualizada para listas grandes.

### Estado de implementación V1 — Centro de Control

[V1 IMPLEMENTADO] `Tools > Bistro Builder > SAVIC > Open Control Center` abre un `EditorWindow` UI Toolkit con las secciones Resumen, Cola, Revisión, Biblioteca, Adopción, Validación, Historial y Ajustes. El panel es una proyección de solo lectura de manifests, jobs e inventario persistido; no introduce otra autoridad ni modifica contenido al navegar.

[V1 IMPLEMENTADO] El modelo de lectura es determinista, tolera datos parciales y ordena con claves estables. Biblioteca ofrece búsqueda y filtros por familia, categoría, estado, origen y versión; Revisión y Validación combinan excepciones del pipeline y del inventario sin ocultar su procedencia.

[V1 IMPLEMENTADO] Las listas de escala usan `ListView` con virtualización `FixedHeight`. Los cambios en manifests, jobs e inventario generan una señal coalescida, mientras que recarga, inventario y escaneo de entrada siguen siendo acciones explícitas y seguras. Pausa, reanudación, cancelación y checkpoints no se simulan en la UI: siguen perteneciendo al bloque 7.

[V1 IMPLEMENTADO] La ficha lateral muestra preview, identidad, clasificación, medidas, materiales, piezas semánticas, integraciones, validaciones y trazabilidad. Los detalles técnicos avanzados permanecen colapsados por defecto y las acciones disponibles se limitan a localizar evidencia o assets existentes.

[VALIDACIÓN PENDIENTE EN UNITY] Existe una prueba de regresión ejecutable por menú o línea de comandos que cubre resumen, filtros, orden determinista, unión de validaciones, señales de refresco, lectura del inventario persistido y contrato de virtualización. Debe ejecutarse en Unity 6000.3.19f1 antes de marcar este bloque como `V1 VALIDADO`.

## 40. Undo/Redo y rollback

Undo/Redo de Unity se utilizará en cambios interactivos de revisión y edición de ScriptableObjects cuando sea fiable.

La creación/eliminación masiva de archivos no dependerá de Undo como única seguridad.

SAVIC usará Publication Transaction + backup/rollback para operaciones de pipeline.

Nunca se prometerá Undo para una operación que técnicamente no pueda revertirse de forma segura.

## 41. Errores y recuperación

Cada error se clasifica:

- transitorio;
- fuente;
- análisis;
- regla;
- fix;
- generación;
- integración;
- publicación.

La cola se checkpointa.

Tras domain reload, recompilación, cierre o crash, SAVIC reconstruye jobs pendientes desde estado persistido.

Si un asset queda inconsistente en staging, se limpia o cuarentena; no contamina Approved.

## 42. Git y ramas

[V1] SAVIC será Git-aware pero no hará merges/push automáticos.

Debe registrar cuando sea posible:

- rama;
- commit base;
- working tree dirty/clean;
- ChangeSet generado.

Se versionan:

- código SAVIC;
- reglas/perfiles;
- manifests;
- contenido aprobado;
- .meta necesarios;
- documentación.

No se versionan:

- cache;
- staging;
- jobs temporales;
- logs reconstruibles.

Política de Bistro Builder: los cambios finales de SAVIC se integrarán en "integration/master-current-20260918" mediante trabajo por ramas SAVIC.

[R] SAVIC podrá preparar un resumen de cambios para commit.

[F] Automatización de branch/stage/commit, siempre explícita y nunca merge automático ciego.

## 43. Versionado y migraciones

Versiones independientes:

- SAVIC version;
- ManifestSchemaVersion;
- PipelineVersion;
- FamilyModuleVersion;
- RuleSetVersion;
- BuilderVersion;
- ValidatorVersion;
- IntegrationAdapterVersion.

Cambio de validator → revalidar.

Cambio de thumbnail builder → regenerar previews.

Cambio de collider builder → regenerar collider + validaciones dependientes.

Cambio de schema → migrar manifest.

Cambio BBSIS → revalidar solo contenido afectado.

No existe "ha cambiado SAVIC, rehacerlo todo" salvo migración excepcional y explícita.

## 44. IA

[F] La IA será un proveedor de evidencias adicional.

Puede ayudar a:

- clasificación visual;
- reconocimiento de piezas;
- semántica de materiales;
- descripción;
- similitud;
- detección de anomalías.

Nunca será dependencia obligatoria para:

- IDs;
- publicación;
- validación crítica;
- integridad;
- Save/Load;
- rollback.

Si no hay Internet, SAVIC sigue funcionando.

## 45. Investigación técnica obligatoria antes de mecanismos delicados

Antes de implementar una parte crítica se revisará:

1. documentación oficial de Unity;
2. APIs actuales de Unity 6000.3;
3. herramientas ya existentes del proyecto;
4. aprendizajes de Assets4All;
5. soluciones externas contrastadas cuando aporten valor.

No improvisar importación, AssetDatabase, batch, geometría, colliders, caché, threading ni dependencias.

Referencias técnicas de partida:

- Unity AssetPostprocessor: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetPostprocessor.html
- Unity ScriptedImporter: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetImporters.ScriptedImporter.html
- AssetImportContext: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetImporters.AssetImportContext.html
- AssetDatabase StartAssetEditing: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.StartAssetEditing.html
- AssetDatabase/GetAssetDependencyHash: https://docs.unity3d.com/ScriptReference/AssetDatabase.GetAssetDependencyHash.html
- EditorWindow: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorWindow.html
- UI Toolkit ListView: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.ListView.html
- Unity Test Framework: https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html
- Unity glTFast: https://github.com/Unity-Technologies/com.unity.cloud.gltfast

## 46. Pruebas automatizadas

[V1] Unit tests:

- IDs;
- hashes;
- reglas;
- planificación;
- confianza;
- convergence guard;
- rollback;
- dependency invalidation.

[V1] Golden assets:

- válido;
- escala errónea;
- pivot erróneo;
- material roto;
- collider complejo;
- duplicado;
- asset ambiguo;
- geometría defectuosa.

[V1] Tests obligatorios:

- determinismo;
- idempotencia;
- rebuild desde Source + Manifest;
- rollback;
- migration;
- integration contracts;
- Save/Load roundtrip;
- preview quality;
- catalog uniqueness.

[V1] Stress:

- 100;
- 500;
- 2.000 assets.

Medir:

- tiempo total;
- tiempo por etapa;
- memoria;
- GC;
- importaciones;
- freezes;
- cache hit rate;
- fallos;
- tickets de revisión.

## 47. Roadmap de implementación

### Bloque 0 — Contrato y baseline
[V1]

- congelar este documento como SAVIC Architecture v1;
- registrar métricas baseline;
- añadir paquete/import adapter GLB;
- decidir .gitignore/LFS para ContentSource;
- preparar primer lote real.

### Bloque 1 — Kernel + Intake
[V1]

- ContentInbox watcher/reconciliation;
- estabilidad de archivo;
- SourceHash;
- ContentSource;
- manifests;
- jobs;
- scheduler;
- estados;
- audit;
- cache base.

### Bloque 2 — Rule/Validation/Fix Engines
[V1]

- reglas;
- confidence;
- validators;
- fixes;
- convergence guard;
- rollback;
- ProcessingPlan.

### Bloque 3 — Adopción del contenido existente
[V1]

- scan de placeables actuales;
- manifest adoption;
- catalog mapping;
- no duplicados;
- legacy status.

[V1 IMPLEMENTADO] SAVIC dispone de adopción no destructiva del contenido canónico existente. La vista previa distingue assets elegibles, bloqueados y contenido de prueba/manual; la adopción individual o por lote recomendado crea identidad y manifest deterministas sin sustituir prefab, GUID, materiales, imágenes, ItemId ni entradas del catálogo.

[V1 IMPLEMENTADO] La adopción conserva un baseline de dependencias y el inventario detecta drift posterior en assets legacy gestionados. La operación es idempotente: un ContentId o fingerprint ya registrado reutiliza su identidad y no genera duplicados.

[VALIDACIÓN PENDIENTE EN UNITY] Ejecutar `Tools > Bistro Builder > SAVIC > Diagnostics > Run Legacy Adoption Self-Test` y validar visualmente la sección `Adopción` del Control Center antes de cerrar el bloque.

### Bloque 4 — Vertical completa Mesa
[V1]

GLB/FBX → análisis → clasificación → scale/orientation → pivot → materiales → acabados → collider → prefab → previews → catálogo → placement → BBSIS → navigation → Save/Load → PASS.

Debe reutilizar PlaceableFactory, ThumbnailService, QualityGate y contratos existentes.

### Bloque 5 — Silla
[V1]

- asiento/respaldo/patas/brazos;
- orientación frontal;
- seat height;
- seating.chair;
- pruebas espaciales.

### Bloque 6 — Editor UX
[V1]

- Resumen;
- Cola;
- Revisión;
- Biblioteca;
- Validación;
- Historial.

Estado actual: implementado en código; pendiente ejecutar la prueba de regresión y la inspección visual en Unity 6000.3.19f1.

### Bloque 7 — Batch, recovery y rendimiento
[V1 antes de declarar estable]

- 100/500/2.000;
- checkpoint;
- resume;
- cancellation;
- incremental invalidation;
- time slicing;
- freeze budgets.

[V1 IMPLEMENTADO — FASE A] La cola persistida dispone de estados de procesamiento, checkpoint por job, pausa/reanudación, cancelación segura y recuperación de operaciones interrumpidas tras domain reload. Los jobs históricos anteriores a este scheduler no se activan automáticamente: solo las nuevas ingestas 3D compatibles quedan marcadas como batch-enabled.

[V1 IMPLEMENTADO — FASE A] El scheduler ejecuta como máximo una operación de asset por tick del Editor y nunca procesa dos assets concurrentemente. Cada operación individual permanece atómica para no dejar una publicación a medias; una cancelación durante una operación se materializa al terminar el tramo atómico. Las operaciones lentas quedan registradas con duración y warning de rendimiento.

[V1 IMPLEMENTADO — FASE A] Existe diagnóstico sintético de 100 / 500 / 2.000 jobs que valida persistencia, reload, orden determinista, pausa, cancelación y recuperación sin procesar assets reales.

[V1 VALIDADO — FASE A] `Run Batch Recovery Self-Test` PASS con 100 / 500 / 2.000 jobs y 2.000-job persistence+reload en 80 ms en la máquina de validación.

[V1 IMPLEMENTADO — FASE B] La reejecución calcula fingerprints separados de estructura y apariencia. Si la estructura cambia, SAVIC hace rebuild completo; si solo cambia material/apariencia con geometría estructural estable, actualiza únicamente el SourceModel visual y previews, conservando colliders, BBSIS/spatial, navegación y persistencia. Si cambian bytes de fuente sin una causa de apariencia demostrable, el sistema falla a rebuild completo en vez de asumir.

[V1 IMPLEMENTADO — FASE B] Clasificación y semantic parts se reutilizan cuando el fingerprint estructural permanece estable y sus versiones siguen siendo actuales. El manifest persiste la decisión incremental, fingerprints, motivo y contadores de reuse/full rebuild/appearance refresh.

[V1 IMPLEMENTADO — FASE B] El scheduler mantiene una sola operación atómica por tick y aplica un presupuesto objetivo de 16 ms entre jobs. Si una operación lo excede, registra el overrun y añade un descanso adaptativo antes del siguiente trabajo; las operaciones de asset siguen siendo atómicas para evitar prefabs/publicaciones a medias.

[V1 VALIDADO — FASE B / NÚCLEO] `Run Incremental Invalidation Self-Test` PASS: reutilización exacta, material-only, invalidación geométrica, fallback seguro, reutilización de colliders/semántica, throttle y 2.000 fingerprints deterministas en 144 ms.

[V1 IMPLEMENTADO — FASE B / PROBE REAL] Existe `Run Incremental Real Asset Probe`, prueba desechable sobre `BB_Chair_Master_002`: publica una silla real, aplica un cambio únicamente visual/material, verifica que conserva colliders + BBSIS/spatial + navegación + persistencia, después aplica un cambio estructural y exige rebuild completo, regeneración de colliders y GUIDs estables. Todo el contenido de prueba se limpia al terminar.

[V1 VALIDADO — FASE B / PROBE REAL] `Run Incremental Real Asset Probe` PASS sobre `BB_Chair_Master_002`: baseline real publicado, cambio material detectado como `APPEARANCE_ONLY`, refresco visual sin reconstruir colliders/BBSIS/navegación/persistencia, cambio estructural detectado como `FULL_REBUILD`, colliders regenerados, GUIDs de prefab/item preservados y sin duplicados de catálogo.

[V1 IMPLEMENTADO — FASE C / INGESTA MASIVA REAL] La cola ya protege trabajo activo al recortar historial: el límite nominal de 5.000 registros solo elimina estados terminales y nunca descarta jobs pendientes o en procesamiento.

[V1 IMPLEMENTADO — FASE C / INGESTA MASIVA REAL] Existe `Run Mass Ingestion Real Probe`, diagnóstico aislado que coloca entradas reales en `ContentInbox/DropHere`, incluye una fuente GLB malformada, tres sillas FBX reales adicionales, un asset de calibración, un duplicado exacto y metadatos JSON. Usa manifests y queue de diagnóstico aislados, simula una interrupción/reload antes del drenado, procesa la cola real de SAVIC y exige que el fallo inicial no impida publicar correctamente el asset válido posterior. Catálogo, fuentes espejo, archivos de prueba y contenido publicado se limpian al finalizar.

[V1 VALIDADO — FASE C / INGESTA MASIVA REAL] `Run Mass Ingestion Real Probe` PASS con 7 entradas en `DropHere`, 5 jobs 3D batch-eligible, duplicado exacto aislado, contenido no 3D excluido del batch, recuperación tras interrupción, GLB malformado aislado antes de bloquear la cola, asset válido posterior publicado correctamente y drenado terminal completo. Resultado de la validación: 2 `DONE`, 2 `NEEDS_REVIEW`, 1 fallo seguro, 5 ticks, 2.222 ms.

[V1 IMPLEMENTADO — FASE D / OBSERVABILIDAD OPERATIVA] Cada job batch persiste ahora `outcomeStatus`, código de motivo, etapa principal y timings por etapa. El pipeline mide explícitamente importación, análisis geométrico, plan incremental, clasificación, semantic parts, material semantic y publicación de familia; las etapas reutilizadas quedan marcadas como `REUSED` en vez de simular trabajo.

[V1 IMPLEMENTADO — FASE D / OBSERVABILIDAD OPERATIVA] El Control Center muestra métricas de operación: terminales, DONE/REVIEW/FAIL/CANCELLED, tasa de éxito terminal, media, P95, trabajo más lento, motivo recurrente y agregados por etapa con avg/P95/max/fallos. El detalle de cada job muestra código diagnóstico, etapa principal y desglose temporal completo. La pestaña Revisión incorpora también excepciones originadas por JOB con motivo y etapa concretos.

[V1 IMPLEMENTADO — FASE D / OBSERVABILIDAD OPERATIVA] El esquema de queue pasa a V3. Los registros históricos siguen siendo legibles; cuando un outcome antiguo o excepcional carece de traza, SAVIC asigna un diagnóstico seguro de fallback en vez de dejar el job sin explicación.

[V1 VALIDADO — FASE D / OBSERVABILIDAD OPERATIVA] `Run Mass Ingestion Real Probe` PASS con reason codes, etapa principal, timings por etapa y analytics de cola validados sobre lote real. Resultado: P95 de job 1.094 ms, motivo recurrente `UNSUPPORTED_PUBLICATION_FAMILY` (2), 2 `DONE`, 2 `NEEDS_REVIEW`, 1 fallo seguro, 5 ticks y 2.466 ms de drenado total.


### Bloque 8 — Decoración y equipamiento
[V1 IMPLEMENTACIÓN ACTIVA]

[V1 IMPLEMENTADO — FASE E / PLACEABLE GENÉRICO SEGURO] El clasificador V4 reconoce por evidencia nominal explícita `Decoration`, `KitchenEquipment` y `ServiceEquipment` sin convertir automáticamente cualquier objeto desconocido en mobiliario. Sillas/mesas y tokens conflictivos mantienen prioridad para evitar falsos positivos.

[V1 IMPLEMENTADO — FASE E] Existe una familia genérica de placeables pasivos de suelo. Para decoración con evidencia clara de suelo y para equipamiento inequívocamente pasivo genera de forma transaccional: prefab, `RestaurantPlaceableObject`, definición editable, footprint, BoxCollider simple, ancla de suelo, entrada canónica de catálogo, dos previews, metadatos de inspector y readiness de persistencia/navegación. La identidad usa un `CanonicalContentId` estable derivado del `SavicId`.

[V1 IMPLEMENTADO — FASE E] SAVIC no publica silenciosamente objetos que requieren semántica todavía no soportada. Pared, techo y superficie se envían a revisión con reason code específico. Equipamiento funcional (horno, cooler, frigorífico, extractor, fregadero, barra/counter, POS, etc.) se clasifica correctamente pero queda en `NEEDS_REVIEW` con `FUNCTIONAL_ADAPTER_REQUIRED` hasta que exista su adapter gameplay; nunca se degrada a mera decoración.

[V1 IMPLEMENTADO — FASE E] El placeable genérico participa en la invalidación incremental: cambios solo visuales sustituyen `Visual/SourceModel` y previews sin reconstruir collider, footprint ni identidad runtime.

[V1 VALIDADO — FASE E / PLACEABLE GENÉRICO SEGURO] `Run Mass Ingestion Real Probe` PASS con 9 entradas y 7 jobs 3D batch-eligible. El espejo de suelo real se publicó automáticamente como `Decoration`; el wine cooler real se clasificó como `KitchenEquipment` y se detuvo correctamente en `FUNCTIONAL_ADAPTER_REQUIRED`. Resultado: 3 `DONE`, 3 `NEEDS_REVIEW`, 1 fallo seguro, 7 ticks y 19.045 ms de drenado total. P95 observado: 14.266 ms, dominado por el wine cooler; queda abierto como trabajo de rendimiento de asset individual, no como fallo funcional del bloque.

[V1 IMPLEMENTADO — FASE F / PRE-IMPORT ROUTING] Los assets cuyo propio nombre demuestra con alta confianza que requieren un adapter todavía inexistente se resuelven antes de `AssetDatabase.ImportAsset`. Equipamiento funcional explícito y decoración inequívoca de pared/techo/superficie pueden terminar en `NEEDS_REVIEW` sin importar ni analizar geometría pesada. La decisión queda trazada como `PREIMPORT_ROUTE` con reason code concreto y manifest persistido.

[V1 IMPLEMENTADO — FASE F] El wine cooler de prueba (GLB de ~183 MB) ya no debe crear SourceMirror ni entrar en GLTFast durante el batch mientras falte su adapter funcional. La prueba exige además que su duración quede por debajo del umbral de slow-job de 2.000 ms; si no, la optimización no se considera validada.

[V1 VALIDADO — FASE F / PRE-IMPORT ROUTING] `Run Mass Ingestion Real Probe` PASS. El wine cooler de ~183 MB se resolvió por `PREIMPORT_ROUTE` en 11 ms, sin warning de slow operation y sin entrar en importación/análisis 3D pesado. P95 del lote: 2.161 ms; el cuello de botella pasa ahora al espejo de suelo, que tarda 2.161 ms. Drenado total: 4.656 ms para 7 jobs 3D.

[V1 IMPLEMENTADO — FASE G / GENERIC-STATIC FAST PATH] Los placeables genéricos estáticos de alta confianza ya no ejecutan perfiles geométricos específicos de mesa y silla. `SavicModelAnalyzer` incorpora modo `GenericStatic`: conserva bounds, conteos, materiales y datos necesarios para publicación/incremental, pero omite `SavicGeometryProfileAnalyzer` y `SavicChairGeometryAnalyzer` cuando la identidad del asset demuestra que no son pertinentes.

[V1 IMPLEMENTADO — FASE G] La optimización es selectiva: mesas, sillas, equipamiento funcional, wall/ceiling/surface decoration y casos ambiguos mantienen su ruta completa o su review gate. No se relajan los validadores de mesas/sillas.

[V1 IMPLEMENTADO — FASE G / GENERIC-STATIC FAST PATH] El floor mirror usa ya el modo ligero de análisis; la validación real mostró 3.198 ms totales: importación 1.999 ms, análisis 163 ms y publicación 1.033 ms. El análisis dejó de ser el cuello de botella, pero agrupar importación + publicación en un único tick seguía provocando un bloqueo >2 s.

[V1 IMPLEMENTADO — FASE H / STAGED GENERIC PROCESSING] Los placeables genéricos estáticos de alta confianza separan ahora la preparación/importación del SourceMirror de su análisis/publicación. El job persiste `sourcePrepared`, duración total acumulada y máximo tiempo atómico; tras preparar la fuente vuelve a `INGESTED/SOURCE_PREPARED` y continúa en un tick posterior. Una recarga de dominio puede reanudar desde ese checkpoint sin dejar una publicación a medias.

[V1 IMPLEMENTADO — FASE H] El import adapter evita trabajo redundante: ya no recalcula por separado el SHA-256 del archivo archivado antes de materializar el mirror, copia+hashea en una sola pasada, usa move para el primer mirror y, si el mirror ya está validado e importado, reutiliza el `GameObject` sin `ForceUpdate`/reimportación.

[V1 IMPLEMENTADO — FASE H] Queue schema V4 incorpora estado de preparación de fuente y `maximumAtomicDurationMilliseconds`. El tiempo total del asset sigue midiéndose para throughput, pero el criterio de congelación del Editor se evalúa por etapa atómica real.

[V1 VALIDADO — FASE H / STAGED GENERIC PROCESSING] `Run Mass Ingestion Real Probe` PASS. El floor mirror mantuvo `Generic-static lightweight analysis` y procesó en etapas persistentes: total 2.957 ms, máximo atómico 1.576 ms, `prepare-import` 1.355 ms, `reuse-import` 207 ms, análisis 83 ms y publicación 1.285 ms. No se produjo warning de slow operation para el espejo. Wine cooler por pre-import routing: 26 ms. El P95 total del job queda en 2.957 ms, pero el presupuesto de congelación se cumple porque ninguna etapa atómica supera 2.000 ms.

[V1 IMPLEMENTADO — FASE I / IDENTIDAD ≠ READINESS] La clasificación de contenido queda separada explícitamente de la preparación para publicación. En sillas, la identidad `Chair` ya no depende de que el modelo llegue a escala canónica: nombre explícito + proporciones de forma independientes de escala pueden clasificar la familia, mientras que escala física, altura de asiento, semántica y seguridad siguen siendo responsabilidad del planner/quality gate. Esto evita que una silla real con unidades no normalizadas caiga erróneamente en `UNSUPPORTED_PUBLICATION_FAMILY`.

[V1 IMPLEMENTADO — FASE I] Los fallos de la familia silla tienen reason codes propios: `CHAIR_SEMANTIC_REVIEW`, `CHAIR_AUTHORING_REVIEW` y `CHAIR_PUBLICATION_FAILED`. La prueba masiva exige ahora que `chair_master_002` termine dentro de la familia `Chair` (DONE o revisión específica), nunca como familia no soportada.

[V1 VALIDADO — FASE I / IDENTIDAD ≠ READINESS] `Run Mass Ingestion Real Probe` PASS con `Explicit chair family routing: PASS`. La silla explícita ya entra en la familia `Chair` y, cuando no supera readiness automático, queda en revisión específica de silla en vez de caer en `UNSUPPORTED_PUBLICATION_FAMILY`. En esta ejecución el motivo operativo principal fue `CHAIR_SEMANTIC_REVIEW` (1).

[V1 IMPLEMENTADO — FASE J / SOURCE PIPELINE POR ETAPAS] La preparación de cualquier modelo 3D deja de depender de una ruta monolítica. Todos los jobs 3D pasan por checkpoints persistentes: `MIRROR_MATERIALIZED` → `SOURCE_PREPARED` → análisis/publicación. El pre-import routing se evalúa antes del primer checkpoint, por lo que contenido que ya sabemos que necesita review no incurre en I/O/importación innecesaria.

[V1 IMPLEMENTADO — FASE J] El adapter Unity separa materialización hash-addressed del SourceMirror e importación AssetDatabase. La materialización valida SHA-256 y sustituye el mirror de forma atómica; la etapa de importación reutiliza un GameObject ya importado y solo ejecuta `ImportAsset` cuando hace falta. Queue schema V5 persiste `preparationStage`; `sourcePrepared` queda únicamente como campo de migración de snapshots V4.

[V1 IMPLEMENTADO — FASE J] Este diseño se aplica también a mesas y sillas, no solo a decoración genérica. Así una importación, análisis o publicación costosa no se acumulan en el mismo tick. La telemetría conserva duración total y máximo atómico por job.

[V1 VALIDADO — FASE J / SOURCE PIPELINE POR ETAPAS] `Run Mass Ingestion Real Probe` PASS. El floor mirror procesó con checkpoints materialize/import y máximo atómico de 1.137 ms; desglose: materialize 156 ms, prepare-import 1.118 ms, reuse-import 196 ms, análisis 162 ms y publicación 778 ms. Total del job 2.439 ms, sin superar el presupuesto atómico de 2.000 ms. Wine cooler por pre-import routing: 9 ms. Resultado global: 3 `DONE`, 3 `NEEDS_REVIEW`, 1 fallo seguro, 17 ticks y 5.383 ms de drenado.

[V1 REDISEÑADO — FASE K / CANONICAL METRIC SPACE] Se descartan los workarounds experimentales de silla introducidos durante el diagnóstico (fallback ergonómico, normalización de unidades por heurística, readiness alternativa y colliders de envelope). La documentación V1 ya declara seis sillas canónicas validadas por geometría, asiento ~0,453–0,455 m, orientación +Z y semántica automation-ready; por tanto, una reingesta byte-idéntica que aparezca como 0,005 × 0,006 × 0,009 m demuestra un fallo anterior a la lógica de familia.

[V1 IMPLEMENTADO — FASE K] La causa raíz estaba en el espacio de coordenadas del análisis: los analizadores usaban `root.worldToLocalMatrix * owner.localToWorldMatrix`, cancelando la rotación/escala aplicada por el importador en el root del modelo. SAVIC dispone ahora de `SavicMetricSpace`, que elimina únicamente transformaciones externas y traslación de autoría, pero conserva la rotación/escala de importación necesaria para expresar bounds, áreas y alturas en metros físicos.

[V1 IMPLEMENTADO — FASE K] El mismo espacio métrico canónico se aplica de forma transversal a bounds generales, perfil geométrico, perfil geométrico de silla y semántica de mesas/sillas. No es una corrección específica para `BB_Chair_Master_002`: corrige la unidad de medida de todo el pipeline 3D. Las versiones de los analizadores cambian para invalidar resultados/cache previos calculados en el espacio incorrecto.

[V1 IMPLEMENTADO — FASE K] Se restaura el pipeline de silla previamente validado: clasificación basada en evidencias, semántica automation-ready, normalización por altura real de asiento, colliders semánticos compuestos y los mismos contratos BBSIS/Navigation/SaveLoad. No se amplían envelopes ni se inventan medidas para hacer pasar assets.

[V1 VALIDADO — FASE K / CANONICAL METRIC SPACE] `Run Mass Ingestion Real Probe` PASS. La reingesta byte-idéntica de `BB_Chair_Master_002` conserva las dimensiones físicas canónicas 0,497 × 0,86 × 0,55 m y completa publicación en `DONE`. El probe confirma `Canonical metric-space re-ingestion: PASS`, explicit chair family routing, aislamiento de fuente malformada, recuperación, razón operacional, timings por etapa y drenado terminal. Resultado de esta ejecución: 4 `DONE`, 2 `NEEDS_REVIEW`, 1 fallo seguro; P95 3.201 ms; floor mirror máximo atómico 1.686 ms con materialize 178 ms, prepare-import 1.307 ms, reuse-import 205 ms, analyze 163 ms y publish 1.316 ms; wine cooler pre-import routing 15 ms. El principal motivo pendiente pasa a `FUNCTIONAL_ADAPTER_REQUIRED` (1), ya fuera del problema de espacio métrico.

### Fase L — Equipamiento y contratos de gameplay

[V1 IMPLEMENTADO — DISEÑO CANÓNICO] SAVIC distingue entre **equipamiento pasivo colocable** y **equipamiento que representa una autoridad jugable existente**. La decisión no depende de un asset concreto: `SavicEquipmentIntegrationPolicy` traduce evidencias de tipo a contratos ya publicados por Bistro Builder y nunca crea comportamiento de electrodoméstico.

[V1 IMPLEMENTADO] Refrigeración/almacenamiento (`cooler`, `fridge`, `refrigerator`, `freezer`, armarios, estantes y racks) se publica como `KitchenEquipment` pasivo y exige la capacidad de área canónica `food_production`. Esta integración reutiliza `RestaurantAreaMember` y `RestaurantPlacementValidationService`; no añade `KitchenSystem`, estaciones de preparación ni contratos BBSIS de trabajo que el producto no tenga.

[V1 IMPLEMENTADO] La decisión D-003 sigue siendo vinculante: fregaderos, grifos, lavavajillas, campanas y extractores pueden existir como equipamiento visual/colocable, pero no introducen simulación de agua, extracción o ventilación.

[V1 IMPLEMENTADO] Equipamiento que sí coincide con conceptos interactivos existentes permanece bloqueado hasta disponer de un bridge aprobado: hornos/fuegos/plancha/parrilla/freidora y elementos de servicio como barra/pass/TPV. Estos casos conservan `FUNCTIONAL_ADAPTER_REQUIRED`; SAVIC no sustituye a Kitchen, Service, BBSIS ni Interaction.

[V1 IMPLEMENTADO] El contrato de integración queda persistido en el manifest (`integrationMode`, `requiredAreaCapabilityId`) y forma parte del fingerprint de publicación. El Quality Gate comprueba que el prefab publicado contiene exactamente la capacidad requerida y que el contenido pasivo no ha recibido componentes de gameplay inventados.

[VALIDACIÓN PENDIENTE EN UNITY — FASE L] Ejecutar `Run Mass Ingestion Real Probe`. El wine cooler real debe terminar en `DONE`, publicarse como `KitchenEquipment`, exigir exactamente `food_production` y no contener `KitchenSystem` ni `BistroBuilderKitchenSpatialAdapter`. El probe conserva además casos sintéticos que verifican que un horno y un pass siguen requiriendo adapter funcional.

### Bloque 9 — Puertas, paredes y ventanas
[R]

### Bloque 10 — Materiales, imágenes y UI
[R]

### Bloque 11 — Platos, ingredientes, recetas y proveedores
[R]

### Bloque 12 — CI/headless validation
[R]

### Bloque 13 — Intelligence Layer
[F]

## 48. Criterios de salida de SAVIC 3D V1

No se declarará V1 cerrado hasta demostrar:

- DropHere funciona sin trabajo manual adicional;
- el original queda archivado;
- no hay duplicados al reprocesar;
- una mesa válida llega automáticamente al juego;
- una silla válida llega automáticamente al juego;
- preview detalle y catálogo se generan solas;
- aparecen en menú por catálogo, sin código por asset;
- placement funciona;
- BBSIS funciona cuando aplica;
- navegación queda coherente;
- Save/Load roundtrip pasa;
- fixes convergen;
- no existen loops infinitos;

- rollback conserva última versión válida;
- un fallo no bloquea el lote;
- 500 assets no convierten el Editor en inutilizable;
- revalidación afecta solo a dependencias reales;
- toda publicación tiene procedencia.

## 49. Primera prueba real propuesta

El primer hito no será una ventana bonita.

Será tomar un modelo real de mesa desde ContentInbox y conseguir:

1. recogida automática;
2. archivado original;
3. importación;
4. clasificación Mesa;
5. dimensiones/orientación/pivot;
6. materiales;
7. semántica madera/metal;
8. collider;
9. prefab funcional con PlaceableFactory;
10. RestaurantPlacementFootprint;
11. EditableObjectDefinition;
12. preview detalle;
13. thumbnail catálogo;
14. RestaurantPlaceableItemDefinition;

15. alta única en catálogo;
16. aparición automática en menú;
17. BBSIS seating.table;
18. navegación;
19. Save/Load;
20. Quality Gate;
21. PASS o AUTO_CORRECTED + PASS.

Cuando esa vertical funcione de forma determinista e idempotente, se replica el patrón al resto de familias.

## 50. Decisión final de arquitectura

SAVIC no será una colección de scripts que "arreglan assets".

Será una plataforma de producción de contenido con:

- entrada controlada;
- conocimiento por familias;
- planes reproducibles;
- autocorrección con convergencia;
- publicación transaccional;
- integración por adapters;
- validación real;
- revisión excepcional;
- historial;
- caché;
- evolución por versiones.

La regla de diseño más importante es:

"Definir qué significa que una familia de contenido sea válida en Bistro Builder, no configurar manualmente cada asset."

Con esa regla, añadir 10 recursos y añadir 2.000 recursos sigue siendo el mismo problema de producción, no 2.000 tareas manuales.
