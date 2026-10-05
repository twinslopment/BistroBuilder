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

[V1 EN VALIDACIÓN — CONTINUIDAD CANÓNICA 30/09/2026] La ingesta histórica con queue schema 1 no se activa por recarga. `SavicCanonicalReconciliationService` audita fuente, manifest, identidad y SHA-256; una acción explícita reconstruye solo manifests con fuente verificada y habilita sus jobs en el scheduler actual. Otra acción adjunta un original aportado por el operador únicamente si coincide por hash. El self-test pasó en Unity y la reconciliación real habilitó tres jobs.

#### Diagnóstico de la cola histórica

En `feature/savic-v1`, la cola local conserva 23 jobs y 22 `SourceHash` únicos. Uno corresponde a una mesa publicada y tiene además un job de duplicado exacto. Los otros 21 permanecen `Ingested` sin manifest. Tres fuentes siguen en `ContentSource/SHA256` y sus bytes coinciden con el SHA-256 de la cola; las otras 18 no están en su ruta canónica de este worktree. No se ha demostrado qué operación hizo desaparecer esas 18 fuentes ni que puedan recuperarse de Git.

La cola se guardó con esquema 1, sin `batchEligible` ni checkpoints. El scheduler actual, esquema 5, procesa únicamente jobs habilitados. La exclusión de jobs históricos era deliberada. `JOB_ONLY` es una proyección de un job sin manifest, no una garantía de publicación futura.

#### Contrato de reconciliación

La auditoría exige `SavicId` válido, ruta de archivo canónica exacta, GLB autocontenido, ausencia de colisión de identidades y SHA-256 del original. Distingue fuente lista, ausente, inválida, conflicto y contenido ya gestionado sin modificar la cola. El informe se guarda en `Library/BistroBuilder/SAVIC/Logs/canonical-continuity.json` mediante `Tools > Bistro Builder > SAVIC > Continuity > Audit Canonical Sources`.

`Reconcile Verified Sources` crea solo los manifests de fuentes verificadas que no los tengan y habilita explícitamente los jobs correspondientes. Conserva `SavicId` y `SourceHash`; registra como desconocida la fecha original que la cola antigua no guardó. Se guarda primero el manifest y después la cola, de modo que repetir tras una interrupción termina el trabajo sin duplicar identidad. Ninguna fuente ausente o inválida se habilita.

`Attach Verified Original` acepta un archivo elegido por el operador solo si su SHA-256 coincide con un job cuya fuente falta. Copia mediante temporal al archivo canónico sin mover ni sobrescribir el original elegido. Luego emplea la misma reconciliación; no examina Git, stashes ni otros worktrees. La publicación, clasificación y revisión siguen perteneciendo al pipeline normal.

La prueba aislada cubre cola antigua, fuente correcta, ausente, inválida, ruta inválida, no activación implícita, identidad, repetición idempotente, adjunción por hash, recarga y orden de cola. En Unity 6000.3.19f1 pasó y la reconciliación real dejó `ready=0`, `managed=4`, `missing=18`, `invalid=0`, `conflict=0`. Las 18 fuentes ausentes requieren sus bytes originales.

La primera ejecución automática de los tres jobs habilitados falló en `MATERIALIZE_SOURCE_MIRROR`: el temporal añadía nombre GLB completo y GUID a la ruta del espejo y alcanzó 264–267 caracteres en Windows. Los tres jobs conservaron `SOURCE_MATERIALIZATION_FAILED` y sus manifests conservaron el fallo; ningún asset se publicó en ese primer intento. El informe anterior los contaba incorrectamente como `managed`; la auditoría ahora los muestra por separado como `processingFailed`. El adaptador 3D usa ahora un temporal corto en el mismo directorio para mantener la operación atómica. `Retry Verified Mirror Failures` solo reencola de forma explícita jobs con ese motivo y etapa, después de verificar de nuevo identidad, ruta canónica, manifest y SHA-256. El reintento no cambia otros fallos ni inventa originales.

La validación posterior en Unity 6000.3.19f1 pasó el self-test ampliado y reencoló los tres jobs. El batch real terminó con espejo de suelo `Done/PUBLISHED`, extractor de cocina `NeedsReview/EQUIPMENT_FUNCTION_AMBIGUOUS` y wine cooler `Done/PUBLISHED`. El wine cooler sigue la **Fase L** posterior: `KitchenEquipment` pasivo con `integrationMode=PASSIVE_AREA_PLACEABLE`, capacidad de área `food_production`, `requiresFunctionalAdapter=false` y Quality Gate de publicación válido. Las afirmaciones anteriores de Fases E–K sobre mantener este cooler concreto en review son evidencia histórica supersedida por Fase L. La lectura directa del catálogo serializado confirma una referencia al item de cada una de las dos nuevas publicaciones; el Quality Gate de cada manifest marcó `catalogResolvable=true`. El GLB pesado tuvo `MATERIALIZE_SOURCE_MIRROR=2.020 ms`, `PREPARE_IMPORT_SOURCE=10.938 ms` (importación síncrona de Unity), `IMPORT_SOURCE=1.812 ms` y `FAMILY_PUBLICATION=2.496 ms`, con máximo atómico de 11.008 ms. El cooldown entre ticks no evita esa congelación dentro de `AssetDatabase.ImportAsset`; el rendimiento sigue abierto y exige una estrategia específica para fuentes pesadas, sin alterar bytes/identidad de los originales ni simular que el presupuesto de 2.000 ms se cumple. La auditoría canónica global de 01/10/2026 confirma 22 únicos: 3 `PUBLISHED/CATALOG`, 1 `NEEDS_REVIEW`, 18 `INGESTED/JOB_ONLY`, 0 fallidos. Para los 18, `JOB_ONLY` y la comprobación de `ContentSource` prevalecen sobre el mensaje histórico de ingesta que afirmaba que se había archivado fuente y manifest. El inventario ahora informa explícitamente de esa ausencia y evita contar dos veces una fuente archivada que también tiene job huérfano; la prueba aislada y la comprobación de solo lectura del servicio actualizado conservan 22/3/1/18.

Una búsqueda de solo lectura de 723 GLB en `ProyectoBB` y `Downloads`, con SHA-256 de todos los candidatos, encontró una identidad exacta entre los 18 ausentes: `Meshy_AI_Restaurant_kitchen_sw_0911183620_generate.glb` en `Assets/Assetsparajuego/PuertasCocina` del propio worktree (hash `6a4b064f0b77a4c4759fa5ccdb90fb7bcab78246b638b73d40f86e1303e16d3f`). Otras 33 copias con el mismo hash están en worktrees vecinos. Los 17 hashes restantes no coinciden con ningún GLB accesible en esas carpetas; 399 ZIP examinados contenían una sola entrada GLB, sin nombre coincidente. Esto no prueba ausencia en ubicaciones externas o almacenes no accesibles.

El operador adjuntó ese GLB mediante `Attach Verified Original` el 01/10/2026. SAVIC verificó el hash, archivó el original y creó el manifest conservando el `SavicId` histórico. La cola terminó en `NeedsReview/UNSUPPORTED_PUBLICATION_FAMILY`: análisis geométrico válido (0,819 × 1,900 × 0,171 m), pero clasificación `Unknown` porque el nombre truncado no contiene un token explícito de puerta/ventana/pared. La carpeta de origen `PuertasCocina` es contexto útil, no prueba canónica suficiente para publicar automáticamente como `Door`. El estado comprobado por lectura del inventario es 22 únicos, 3 en catálogo, 2 en revisión, 17 huérfanos, 0 fallidos. El reporte de inventario escrito antes de esta adjunción debe refrescarse en Unity para reflejar esos valores.

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

[V1 VALIDADO — FASE L / EQUIPAMIENTO Y CONTRATOS DE GAMEPLAY] `Run Mass Ingestion Real Probe` PASS sobre la base estable de SAVIC. El wine cooler real termina en `DONE`, se publica como `KitchenEquipment` pasivo, exige exactamente la capacidad `food_production` y el Quality Gate confirma que no contiene `KitchenSystem` ni `BistroBuilderKitchenSpatialAdapter`. Los contratos sintéticos de horno y pass permanecen bloqueados con `FUNCTIONAL_ADAPTER_REQUIRED`. Los avisos de duración observados al importar el GLB pesado se consideran trabajo de rendimiento separado y no invalidan el contrato funcional.

### Bloque 9 — Puertas, paredes y ventanas
[V1 IMPLEMENTACIÓN ACTIVA]

[V1 IMPLEMENTADO — FASE 9A / REGISTRO CONSTRUCTIVO MULTI-ASSET] El `BistroBuilderConstructionAssetKit` conserva las referencias legacy de puerta, ventana y módulos de pared, pero incorpora registros canónicos multi-asset por `definitionId`. Puertas/ventanas almacenan además `openingType`, prefab y tamaño nominal; los módulos visuales de pared almacenan prefab y tamaño nominal. La resolución exacta por ID tiene prioridad y los defaults legacy solo actúan como compatibilidad para `wall.default`, `door` y `window`. Un ID explícito desconocido nunca degrada silenciosamente al asset por defecto.

[V1 IMPLEMENTADO — FASE 9A] `BistroBuilderOpeningVisuals` resuelve ahora el relleno visual mediante `fillDefinitionId` y escala con las dimensiones nominales registradas. `BistroBuilderArchitectureRuntimeMaterializer` resuelve el módulo visual de cada pared mediante `wallDefinitionId`. La geometría de muro, los huecos, Navigation, BBSIS, Finance y Save/Load continúan bajo autoridad del sistema constructivo existente; SAVIC no duplica esos sistemas.

[V1 IMPLEMENTADO — FASE 9B / FAMILIA ARCHITECTURE] El clasificador reconoce explícitamente `Wall`, `Door` y `Window` como familia `Architecture` / categoría `Construction`, con conflictos nominales para evitar falsos positivos como `cabinet_door` o `wall_mirror`. El planner normaliza ancho/grosor entre los ejes horizontales, registra yaw de 90° cuando corresponde, aplica rangos dimensionales seguros por tipo y genera un `CanonicalContentId`/ruta de publicación estable.

[V1 IMPLEMENTADO — FASE 9B] `SavicConstructionPublisher` publica de forma transaccional un prefab visual normalizado, elimina `Collider` y `Rigidbody` de la fuente para no crear una segunda autoridad física, registra el prefab por `definitionId` en el kit constructivo y valida que el mismo ID resuelva exactamente al prefab y dimensiones nominales publicadas. Puertas y ventanas deben quedar sin colliders de paso; las paredes SAVIC son exclusivamente módulos visuales sobre la geometría canónica del materializador.

[V1 IMPLEMENTADO — FASE 9C / PROBE VERTICAL] Existe `Run Construction Vertical Probe`, aislado y reversible. Valida clasificación positiva y negativa, planning, publicación de Wall/Door/Window, registro exacto en el kit, materialización real de una pared SAVIC mediante `BistroBuilderArchitectureRuntimeMaterializer`, rellenos de puerta/ventana mediante `BistroBuilderOpeningVisuals` y ausencia de collider authority en los huecos. Usa el módulo de pared real `BB_Wall_Module_Master_001.glb` y restaura el kit/contenido de prueba al terminar.

[V1 VALIDADO — BLOQUE 9 / PUERTAS, PAREDES Y VENTANAS] `Run Construction Vertical Probe` PASS. La prueba vertical confirma clasificación, planning, publicación, registro por `definitionId`, materialización real de pared SAVIC, rellenos de puerta y ventana, aislamiento de definiciones y ausencia de collider authority en los huecos. Bloque 9 CERRADO.

### Bloque 10 — Materiales, imágenes y UI
[V1 VALIDADO — BLOQUE 10 / IMÁGENES, TEXTURAS Y UI] SAVIC dispone de un pipeline de imagen separado del 3D pero integrado en la misma cola, identidad, archivado, SourceMirror, trazabilidad y rollback. Publica tres roles deterministas: `CONTENT_IMAGE`, `MATERIAL_TEXTURE` y `UI_ICON`.

[V1 IMPLEMENTADO] Las imágenes de contenido se publican como Sprite gestionado; los mapas de material usan perfiles TextureImporter explícitos por evidencia nominal (normal, albedo/basecolor, metallic, roughness, smoothness, occlusion, emission y mask) sin inventar agrupaciones de materiales. Los normal maps se importan como `TextureImporterType.NormalMap`, datos lineales y mipmaps; imágenes de color mantienen sRGB cuando corresponde.

[V1 IMPLEMENTADO] Un nombre explícito `ui_<BBIconId>.<ext>` o `ui_<BBIconId>__<sufijo>.<ext>` sustituye únicamente el sprite del `BBIconId` canónico, conserva su rol semántico y queda etiquetado como contenido SAVIC. El instalador/rebuild de Iconography 21B preserva overrides gestionados por SAVIC en lugar de restaurar silenciosamente el SVG base. Un ID UI desconocido se envía a revisión.

[V1 IMPLEMENTADO] PNG/JPG/JPEG/TGA/PSD/TIF/TIFF disponen de adapter Unity V1. WebP puede archivarse por intake pero queda en `NEEDS_REVIEW / IMAGE_FORMAT_UNSUPPORTED` antes de importación, evitando depender de soporte no canónico del importador.

[V1 VALIDADO] `Run Image Vertical Probe` PASS. La prueba real confirma publicación de imagen de contenido, perfil de normal map, Sprite + binding a `BBIconCatalog`, persistencia del override tras reconstruir el catálogo, rechazo controlado de WebP e ID UI desconocido. Bloque 10 CERRADO.

### Bloque 11 — Platos, ingredientes, recetas y proveedores
[V1 CERRADO — PASS]

[V1 IMPLEMENTADO] SAVIC publica bundles estructurados de contenido reutilizando las autoridades canónicas del juego: ingredientes, formatos comerciales, platos, recetas y proveedores/ofertas. La publicación conserva referencias cruzadas estables entre ingrediente, formato, plato, receta, proveedor y oferta, y actualiza los catálogos de forma atómica.

[V1 VALIDADO] `Run Content Bundle Vertical Probe` PASS. Validado en Unity: publicación de ingrediente, formato comercial, plato, receta y proveedor/oferta; contrato de coste por ración; referencias cruzadas; catálogos atómicos; rechazo controlado de CSV/TSV no soportado. Bloque 11 CERRADO.

### Bloque 12 — CI/headless validation
[V1 IMPLEMENTADO — VALIDACIÓN FINAL EN CURSO]

[V1 IMPLEMENTADO] `SavicV1ClosureGate` ejecuta en una sola pasada los nueve contratos críticos de SAVIC V1: foundation, batch recovery, legacy adoption, incremental invalidation, publication rollback, mass ingestion, construction, images/UI y content bundles. Es invocable desde menú o mediante `BistroBuilder.Editor.Savic.SavicV1ClosureGate.RunFromCommandLine`, genera `Temp/SAVIC/SavicV1ClosureGateReport.json` y devuelve código 0/1 en batch mode.

[V1 VALIDADO EN UNITY EDITOR] `SAVIC V1 CLOSURE GATE - PASS`: 9 PASS / 0 FAIL. Duración observada: 32,4 s. Desglose: foundation 153 ms; batch recovery 360 ms; legacy adoption 266 ms; incremental invalidation 51 ms; publication rollback 1.200 ms; mass ingestion 27.118 ms; construction 1.299 ms; images/UI 1.230 ms; content bundle 728 ms.

[V1 IMPLEMENTADO] `Tools/BistroBuilder/RunSavicV1ClosureGate.ps1` reutiliza `RunUnityBatchSafe.ps1` y ejecuta el Closure Gate con `-batchmode -nographics -accept-apiupdate`, respetando el lock real del proyecto y evitando abrir dos Unity simultáneos. `RunUnityBatchSafe.ps1` admite ahora argumentos Unity adicionales sin alterar su comportamiento por defecto.

[PENDIENTE DE ÚNICO PASS FINAL] Falta ejecutar el nuevo runner batch sobre el proyecto con su `Library`/paquetes ya resueltos. Los intentos sobre un worktree limpio no llegaron a ejecutar SAVIC: el primero falló antes de compilar por IPC de Unity Package Manager y los intentos con `-noUpm` no resolvieron uGUI/TextMeshPro/Input System. Esto es un bloqueo del entorno limpio, no un fallo del Closure Gate ni de SAVIC.

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

## 51. Lote Meshy seleccionado, 01/10/2026

La operación `Continuity/Import Selected GLB Folder` acepta una carpeta seleccionada con GLB. Calcula SHA-256 por archivo y adjunta el original a la identidad histórica si coincide con una fuente ausente. Los hashes nuevos pasan a la ingesta canónica mediante una copia temporal fuera de `DropHere`; el archivo externo permanece intacto. La operación es idempotente y el autotest de continuidad cubre restauración, fuente nueva, repetición y conservación del original.

En el lote real de 14 GLB, Unity informó `restored=8, new=5, duplicates=1, errors=0`. El duplicado ya estaba gestionado. La auditoría de continuidad pasó a 18 gestionados y 9 fuentes históricas ausentes. El inventario calculado registra 27 contenidos únicos, 5 publicados/en catálogo, 12 en revisión y 9 huérfanos. Estos números proceden de la cola, manifiestos y catálogo; no constituyen por sí mismos una validación de colocación en juego.

Se observó una clasificación falsa en el enrutamiento previo a importar: `bar` aislado se interpretaba como estación de servicio funcional, incluso en nombres de taburetes o exportaciones truncadas. La política deja `bar` como contexto; `counter`, `pass`, `register`, `pos` y `cash` conservan el bloqueo por adaptador funcional. Existe un reintento selectivo de revisiones antiguas `FUNCTIONAL_ADAPTER_REQUIRED/PREIMPORT_ROUTE`, condicionado a manifiesto y original verificados y a que la política vigente ya no requiera adaptador. El autotest local pasó. En Unity se reencolaron 3 revisiones verificadas; las tres finalizaron en `NEEDS_REVIEW/UNSUPPORTED_PUBLICATION_FAMILY` después del análisis 3D. El inventario quedó en 27 únicos, 5 en catálogo, 12 en revisión y 9 huérfanos. La corrección eliminó una causa falsa de revisión, pero todavía no proporciona una familia publicable para estos modelos.

Otras revisiones del lote reflejan límites distintos: altura de asiento fuera del rango seguro, dimensiones de mesa inseguras, familia no clasificable o función de equipo ambigua. No se cambian estos umbrales ni se publican automáticamente sin evidencia adicional.

## 52. Calibración acotada de sillas con altura de exportación normalizada

Cuatro GLB reales clasificados como `Chair` muestran el mismo patrón medido: altura de fuente 1,898–1,903 m, asiento a 0,975–1,044 m, perfil de asiento/respaldo/apoyos `automationReady`, confianza geométrica 0,941–1,000 y frente +Z. La escala uniforme derivada de llevar el asiento a los 0,46 m del perfil de comedor es 0,440–0,472; las dimensiones finales calculadas quedan dentro de los límites físicos existentes del planner. Este patrón no demuestra que los metros de la fuente fueran correctos; indica una exportación normalizada que necesita calibración de autoría.

El planner conserva intacta la corrección ordinaria (asiento de fuente 0,24–0,75 m, escala 0,65–1,45). Abre una ruta separada y estrecha solo cuando coinciden clasificación `Chair` respaldada por geometría, semántica preparada, confianza alta, altura de fuente 1,85–1,95 m, ratio de asiento 0,45–0,58 y escala calculada 0,40–0,52. Nombres con señales de barra/taburete quedan excluidos. El mismo validador final de dimensiones, orientación, colisiones, BBSIS, catálogo y publicación sigue siendo obligatorio.

`Retry Best Verified Chair` reencola una sola revisión `CHAIR_AUTHORING_REVIEW/FAMILY_PUBLICATION` por vez, priorizando la mayor confianza de clasificación. Antes de mutar la cola verifica identidad de manifiesto, SHA-256 del original y que el planner vigente acepta el caso. Un autotest aislado cubre aceptación, exclusiones, fuente corrupta e idempotencia; un ensayo de solo lectura con los cuatro manifiestos reales confirma que el plan los acepta y rechaza variantes de taburete, altura distinta o geometría débil.

El 01/10/2026, el autotest de calibración pasó en Unity 6000.3.19f1 y el reintento explícito de `Meshy_AI_Modern_Black_Dining_C_0918083504_generate.glb` terminó en `Done/PUBLISHED`. El manifiesto `e6bcf31ac7a446f185751377a3b7ea9d` registra escala 0,44045, asiento final 0,46 m, 6 colliders semánticos y validaciones de espacio, navegación y persistencia. SAVIC creó prefab, definición de catálogo y previews; la definición aparece por GUID en `RestaurantPlaceableCatalog_Main.asset`. El inventario calculado pasó a 27 únicos, 6 en catálogo, 11 en revisión y 9 históricos sin original. Dos avisos de material reflejan ausencia de apariencia y semántica fiables en la fuente; no invalidaron la publicación. La inspección visual y una prueba de colocación/guardado/carga en juego siguen pendientes; los datos de publicación no sustituyen esa comprobación.

La cifra de 6 en catálogo es **global**, no representa 6 publicaciones del lote de 14 GLB aportado el 01/10/2026. El cruce por SHA-256 de los 14 archivos de Downloads con cola, manifiestos y `ContentSource/SHA256` confirma 14 originales archivados e íntegros: 3 del lote `PUBLISHED`, 11 `NEEDS_REVIEW`. El resultado de importación `restored=8, new=5, duplicates=1` significa que uno ya estaba registrado, no que faltase su archivo. Las 11 revisiones se reparten en 3 `CHAIR_AUTHORING_REVIEW`, 6 `UNSUPPORTED_PUBLICATION_FAMILY`, 1 `EQUIPMENT_FUNCTION_AMBIGUOUS` y 1 `FAMILY_PUBLICATION_FAILED` por dimensiones de mesa inseguras. Los otros 3 artículos del catálogo global proceden de fuentes anteriores.

## 53. Aislamiento de miniaturas publicadas

La inspección de los PNG publicados detectó contaminación visual: las miniaturas de silla, mesa y decoración muestran objetos de la escena abierta. La causa está en `SavicPreviewRenderer`: aunque crea una escena de preview, cambiaba su máscara a la de escenas ordinarias y la cámara manual no tenía asignada la escena privada. El PNG podía pasar la comprobación de varianza aun representando objetos ajenos.

El renderizador V1.1 asigna `Camera.scene` a la escena creada por `EditorSceneManager.NewPreviewScene`, como prescribe la [API de Unity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-scene.html) para limitar la cámara a esa escena, y cambia el fingerprint de preview. La acción `Repair Published Previews` recorre solo manifiestos `PUBLISHED` del catálogo canónico, verifica identidad de item/prefab/rutas y regenera los previews con versión antigua, actualizando fingerprint y validación en el manifiesto. El 01/10/2026 la acción terminó en Unity con `repaired=6, already current=0, skipped=0`. Los seis manifiestos registran ambos previews en V1.1 y `Presentation.Previews=PASS`. La inspección visual de los seis PNG de catálogo y del preview grande de la silla confirmó fondo aislado y ausencia de objetos de la escena abierta. La colocación/guardado/carga en juego de la silla sigue pendiente.

## 54. Revalidación acotada tras cambio de planner

Cuando el planner de sillas cambia, el arranque del Editor reevalúa como máximo ocho jobs ya habilitados que quedaron en `CHAIR_AUTHORING_REVIEW/FAMILY_PUBLICATION`. Antes de volver a encolarlos comprueba identidad de manifiesto, SHA-256 del original archivado y aceptación del planner actual. La cola registra `lastAutomaticChairPlannerRetryVersion` para impedir un bucle tras recarga de dominio si la publicación vuelve a requerir revisión. Los jobs históricos no habilitados siguen exigiendo reconciliación explícita. El autotest de continuidad y calibración cubre un ciclo de rechazo y recarga; la ejecución real y el resultado de las tres sillas figuran en la sección 55.

`Run Published Chair SaveLoad State Probe` extiende el probe existente de mesa al `ItemId` de una silla SAVIC realmente publicada. Comprueba la configuración del proveedor `restaurant.structure`, valida un registro de silla sin `functionalTableId`, serializa y deserializa el JSON, y vuelve a validar el estado. El código completo de `Assembly-CSharp-Editor` compiló con el response file de Unity.

## 55. Resultado real de publicación y cierre, 01/10/2026

La ejecución Unity con dispositivo gráfico de `Run Verified Chair Completion Gate` terminó **PASS**: las tres sillas verificadas pendientes quedaron `Done/PUBLISHED` y el probe de estado Save/Load de silla pasó. La primera ejecución en modo `-nographics` había reintentado esas fuentes sin dispositivo gráfico y produjo previews vacíos; no era un fallo de los GLB. El arranque y el gate ahora exigen render gráfico para esa revalidación. Una revisión `CHAIR_PUBLICATION_FAILED` causada exactamente por preview vacío puede reintentarse una sola vez por versión de renderer, siempre tras verificar identidad, SHA-256 y plan; la cola conserva ese intento para evitar bucles.

El `Mass Ingestion Real Probe` reveló otra regresión independiente: un FBX de silla de prueba se analizaba en metros correctos, pero el prefab quedaba unas cien veces más pequeño. La autoría visual sustituía la escala/rotación raíz aplicada por el importador, mientras `SavicMetricSpace` sí las conservaba en el análisis. `SavicChairPublisher` preserva ahora esos transform del importador y aplica después la calibración del plan; además compara la altura visible del prefab con la altura métrica prevista. El probe de ingesta real volvió a pasar con el FBX canónico.

La inspección de las cuatro miniaturas de sillas publicadas detectó dos siluetas magenta. Sus prefabs tenían un slot de material nulo cada uno; la validación de fuente ya lo había advertido, pero la publicación lo dejaba visible como error. El publicador V1.1 genera un material neutro URP gestionado solo para slots ausentes o con shader no utilizable, mantiene los materiales sanos, exige cero slots inválidos en el prefab publicado y registra `Presentation.ChairMaterials=WARNING` cuando usa ese sustituto. El gate canónico `Validate/Repair Published Chair Appearance` comprueba antes archivo y mirror por SHA-256 y republica únicamente prefabs afectados. En Unity informó `checked=4, repaired=2, invalid material slots=0`; una segunda ejecución informó `repaired=0`. Las dos miniaturas reparadas se inspeccionaron visualmente: muestran las sillas grises y aisladas, sin magenta. El gris es una representación neutra porque esos GLB no aportaron un material utilizable, no una reconstrucción de su textura original.

El **Closure Gate V1** pasó **9/9** en Unity 6000.3.19f1 después de estas correcciones; el informe persistente está en `Library/BistroBuilder/SAVIC/Logs/SavicV1ClosureGateReport.json`. La auditoría canónica posterior registró 27 contenidos únicos, 9 publicados/en catálogo, 8 en revisión, 0 fallidos y 9 fuentes históricas huérfanas. Del lote de 14 GLB seleccionado por el usuario, seis están publicados y ocho continúan en revisión por ausencia de familia publicable, ambigüedad funcional o dimensiones inseguras. SAVIC no los fuerza a catálogo sin evidencia.

El umbral de 2 segundos por operación atómica es **aviso de rendimiento**, no garantía del importador síncrono de Unity. En una ejecución del gate el mirror de suelo alcanzó 3.749 ms de máximo atómico bajo carga; sus etapas de materialización, importación, análisis y publicación siguieron separadas y serializadas. El probe registra ese exceso como advertencia con desglose y mantiene como condiciones de PASS las invariantes funcionales y los checkpoints. La colocación y el ciclo guardado/carga dentro de una partida jugable siguen sin prueba directa; el probe Save/Load valida el contrato de estado, no simula esa sesión.

## 56. Clasificación autónoma de revisiones verificadas

La identidad semántica de un contenido y su aptitud para publicarse son decisiones distintas. El clasificador V4.4 reconoce `Stool` y `BarStool` cuando el nombre contiene una señal explícita de taburete y la geometría tiene límites utilizables. Acepta el fragmento truncado `stoo` solo junto a `bar`; `bar` aislado, conflictos con mesa/silla y geometría sin límites siguen sin autorizar esa clasificación. La confianza geométrica no convierte un taburete en silla de comedor. Actualmente no existe contrato de uso, perfil ni publicador funcional de taburete, así que estos assets permanecen en revisión aunque se conozca su tipo.

`SavicCanonicalReconciliationService.RefreshReviewedClassifications` reevalúa en el Editor las revisiones `UNSUPPORTED_PUBLICATION_FAMILY/CLASSIFICATION` previamente habilitadas. Verifica identidad del manifiesto, ruta canónica y SHA-256 del original antes de actualizar la clasificación almacenada. Solo reencola cuando el tipo resuelto tiene una familia registrada, y la cola limita el reintento a una vez por versión del clasificador. Una familia añadida posteriormente también puede reencolar un tipo ya clasificado, sin pedir al usuario que etiquete los archivos. Las identidades desconocidas o las familias sin publicador conservan `NEEDS_REVIEW` y su evidencia; no se fabrican prefabs ni categorías para hacer bajar el contador.

La ejecución real `Refresh Reviewed Classifications` en Unity informó `refreshed=7`, `queued=0`, `skipped=0`. El informe `Library/BistroBuilder/SAVIC/Logs/autonomous-classification.json` registra nueve jobs habilitados en revisión: cuatro con tipo reconocido y cinco `Unknown`. Dos de los cuatro reconocidos son taburetes que antes figuraban `Unknown`; los otros dos son una mesa con dimensiones de autoría inseguras y equipo de cocina de función ambigua. Ninguno de los nueve cambió a catálogo. El lote seleccionado por el usuario conserva seis publicados y ocho en revisión. El Closure Gate ampliado pasó 10/10 en Unity e incluye el autotest de identidad de taburete, conflictos, hash, revalidación e idempotencia. La inferencia visual de nombres Meshy truncados, la familia funcional de taburetes y la prueba directa de colocación/SaveLoad en partida siguen pendientes.

## 57. Perfil canónico de mesa compacta y resultado real, 02/10/2026

La revisión `FAMILY_PUBLICATION_FAILED` de `Meshy_AI_Ornate_Wooden_Pedesta_0918082945_generate.glb` correspondía a una mesa de pedestal con original SHA-256 archivado, clasificación `Table` respaldada por geometría y fuente de 1,90 m de alto. La normalización uniforme a 0,75 m producía un tablero de 0,592 × 0,592 m. El perfil rectangular previo exigía al menos 0,75 m de lado para dos clientes por su margen de 0,10 m en cada extremo; por eso el planner la rechazaba. La vista previa publicada durante la verificación mostró un tablero cuadrado, por lo que el perfil final usa dos asientos en lados opuestos, no plazas radiales.

El planner de mesas V1.2 admite este perfil solo con clasificación geométrica de alta confianza, tablero casi cuadrado, ambos lados entre 0,59 y 0,75 m, altura física segura y señales de superficie/apoyo suficientes. La nueva definición de seating rectangular compacta conserva 0,55 m útiles por cliente con margen de 0,02 m y tiene contrato BBSIS propio. No se redujeron los límites del perfil rectangular general. SAVIC revalida como máximo cuatro revisiones o mesas ya publicadas cuyo perfil cambió, comprobando identidad de manifiesto, ruta canónica y SHA-256 antes de tocar la cola; `lastAutomaticTablePlannerRetryVersion` impide repetición en la misma revisión. El publicador transaccional conserva el `ItemId` y los valores manuales del catálogo al actualizar el perfil.

El probe con el manifiesto real pasó en Unity 6000.3.19f1. Comprueba definición de seating, contrato BBSIS, exclusión de mesas demasiado pequeñas, alargadas o con geometría débil, original corrupto e idempotencia tras recarga. El `Compact Square Table Completion Gate` reencoló y republicó la mesa mediante la cola canónica: `Done/PUBLISHED`, dos plazas BBSIS emitidas, navegación y persistencia validadas, prefab e item en catálogo y probe de estado Save/Load del `ItemId` concreto **PASS**. El preview grande se inspeccionó visualmente: mesa aislada, sin contaminación de escena. El Closure Gate V1 posterior pasó **10/10**. La auditoría canónica posterior registró **27 únicos, 10 publicados/en catálogo, 7 en revisión, 0 fallidos y 9 fuentes históricas huérfanas**. En el lote de 14 GLB seleccionados por el usuario, esta mesa cambia el balance a **7 publicados y 7 en revisión**. Las revisiones restantes no se fuerzan a catálogo: faltan familias funcionales o evidencia semántica suficiente. La prueba Save/Load valida el contrato de estado, no una sesión jugable de colocación y carga.

## 58. Colocación y SaveGame reales de la mesa SAVIC, 02/10/2026

`Run Published Table Runtime Playtest` abre `Prototype_Restaurant` en Play Mode y selecciona la mesa compacta publicada mediante manifiesto y catálogo, tras volver a verificar el SHA-256 del original. Busca una posición libre dentro de las áreas existentes con un límite de intentos; la crea y confirma por `RestaurantPlaceableCreationService`, guarda mediante `BistroBuilderSaveGameService`, carga esa partida y comprueba que una **nueva instancia runtime** conserva el mismo `ItemId` y dos plazas. Usa un slot diagnóstico libre entre 960 y 979 y exige que el servicio lo elimine al terminar. No guarda cambios en la escena.

La primera variante de la prueba sustituyó una mesa con sillas asociadas; SaveGame rechazó correctamente un vínculo de asiento que apuntaba a la mesa retirada. Se corrigió la preparación del test para buscar suelo libre sin retirar mesas ni sillas. La ejecución final en Unity 6000.3.19f1 terminó **PASS**: colocación canónica, guardado, carga, nueva instancia, dos plazas y eliminación del slot. El informe está en `Library/BistroBuilder/SAVIC/Logs/savic-published-table-runtime-playtest.txt`. Esta prueba demuestra el ciclo runtime de **esta mesa**; no valida por sí sola cada silla, taburete o asset que sigue en revisión.

La auditoría de inventario que queda vigente enumera siete identidades en `NEEDS_REVIEW`: dos `BarStool` reconocidos pero sin familia funcional y cinco `Unknown` cuyos nombres Meshy terminan antes del sustantivo necesario. Se inspeccionó el encabezado glTF de los GLB desconocidos: los nodos y mallas usan nombres genéricos y no hay prompt ni etiqueta semántica en `asset.extras`. El URL de descarga que algunos archivos conservan identifica un objeto convertido, no su función de juego. Sin una fuente semántica verificable y contratos de uso/colocación para taburetes y otras piezas, publicarlos ahora como sillas de comedor o decoración pasiva sería una clasificación falsa. El contador no se reduce artificialmente.

## 59. Contexto de origen verificado y corrección del inventario, 02/10/2026

El clasificador V4.5 puede resolver una puerta con nombre truncado cuando el archivo original también está en una carpeta explícita de puertas dentro de `Assets/Assetsparajuego`. Verifica los bytes por SHA-256, exige que todas las copias coincidentes aporten el mismo contexto y comprueba geometría estática de panel vertical, proporciones de puerta y una señal de puerta batiente en el nombre. Una carpeta de ventanas, hash distinto, conflictos de contexto o geometría débil no autorizan la clasificación. No usa IDs ni nombres de asset concretos como reglas. El autotest y el Closure Gate V1 **10/10** pasaron en Unity 6000.3.19f1. La puerta de cocina existente quedó `PUBLISHED`, con prefab y definición registrados en `ConstructionAssetKit`; no pertenece al catálogo de placeables.

La auditoría posterior descubrió una causa adicional de contadores incorrectos: `BuildLatestJobMap` elegía una reingesta `DuplicateExact` más reciente como autoridad del estado. Así ocultaba el `NEEDS_REVIEW` del extractor de cocina. El inventario prioriza ahora el job de procesamiento real frente a la reingesta duplicada, tanto con manifiesto como en jobs huérfanos. Si solo queda el duplicado y existe manifiesto, conserva el estado y la causa del manifiesto. El autotest Block1 pasó en Unity y cubre revisión, fallo, huérfano y manifiesto sin job primario frente a duplicados posteriores.

La auditoría canónica de **2026-10-02 11:53:07 UTC** registra **27 únicos, 11 publicados, 10 en catálogo de placeables, 7 en revisión, 0 fallidos y 9 fuentes históricas ausentes**. El contador transitorio de seis revisiones anterior a esta corrección no es evidencia de cierre. Del lote de 14 GLB seleccionado siguen publicados siete y en revisión siete; la nueva puerta procede de una fuente anterior. Permanecen dos taburetes identificados sin familia funcional, cuatro identidades desconocidas y un equipo de cocina de función ambigua. Los originales de los siete pendientes están archivados; no requieren recuperación Git/stash.

Plan mínimo siguiente: analizar por separado asiento y soporte de taburetes con y sin respaldo, conservando intactos los límites de silla de comedor; validar los perfiles contra originales reales y negativos; después definir el contrato canónico de uso, colocación, BBSIS y persistencia antes de publicar la familia. Para las fuentes de nombre truncado, obtener la descripción original verificable del proveedor. El `.blend` disponible de la pieza `Realistic_freestandin` tiene el mismo tamaño y nombres genéricos sin propiedades semánticas; no resuelve por sí solo su función. En el momento de esta auditoría Meshy no tenía sesión autenticada en el navegador integrado; la continuación siguiente incorpora el acceso autorizado por el usuario. Publicar un taburete como silla o un extractor como mueble de suelo no satisface este plan.

## 60. Evidencia de origen Meshy, 02/10/2026

El usuario confirmó el inicio de sesión en Meshy. La inspección de sus tarjetas y descripciones originales identifica las siete revisiones como tres taburetes de barra, un armario independiente de almacenamiento, una lámpara de pie, una barra curva y una campana extractora de cocina. La asociación no depende del nombre truncado: el `HostUrl` del flujo NTFS `Zone.Identifier` de cada GLB descargado contiene el identificador de tarea Meshy y coincide con la tarea de la tarjeta inspeccionada. Se conservan títulos completos y, para armario y lámpara, la descripción original; se eliminan los parámetros firmados de las URL.

`SavicProviderMetadataService` incorpora esta evidencia al manifiesto canónico solo tras comprobar SHA-256 del GLB descargado, original archivado, identidad de tarea y tarjeta Meshy. El archivo de evidencia se guarda en `ContentSource/ProviderMetadata` con identidad por hash; su lectura comprueba integridad, tarea, fuente y confinamiento de ruta. La clasificación utiliza únicamente el asunto positivo de la descripción: frases de exclusión como `No table` o `no wall` no aportan identidad. Los cambios de evidencia invalidan las semánticas derivadas; repetir una incorporación idéntica es idempotente. Unity Mono rechaza las rutas NTFS de flujos alternativos, por lo que su lectura canónica usa un handle Windows y un stream gestionado. Las rutas de evidencia evitan duplicar dos hashes completos en el árbol para respetar el límite de rutas del entorno.

El clasificador 4.6 distingue `StorageFurniture` y `FloorLamp`. Un armario explícitamente independiente utiliza el contrato existente de placeable estático de categoría `Furniture`, sin atribuirle inventario ni producción de cocina. Los títulos completos también identifican el tercer taburete antes desconocido. La lámpara requiere su contrato de iluminación, los taburetes su familia funcional y la barra su contrato de servicio. La campana conserva una restricción explícita de colocación elevada; el publicador genérico de suelo la rechaza. La identidad confirmada por el proveedor permite resolver ambigüedad semántica, pero no sustituye las comprobaciones físicas ni de integración.

La ejecución canónica incorporó **7 evidencias verificadas, 0 rechazos**, actualizó las clasificaciones y publicó el armario `8a5c37cab8eb4366ab675afa66af65ad` como `bb_storagefurniture_8a5c37cab8eb4366ab675afa66af65ad`. El inventario posterior, **02/10/2026 12:56:32 UTC**, confirma **27 únicos, 12 publicados, 11 en catálogo de placeables, 6 en revisión, 0 fallidos y 9 históricos sin original**. Del lote de 14 fuentes seleccionadas hay ocho publicadas y seis en revisión. La actualización de identidad de la campana conserva el bloqueo funcional del job; una clasificación actual no equivale a una publicación lista.

El armario pasó una prueba real de Play Mode: colocación mediante el servicio canónico, guardado y carga con el mismo `ItemId`, nueva instancia runtime y conservación de categoría, footprint y collider. El slot diagnóstico fue eliminado. La inspección de su preview detectó material magenta: la fuente carecía de un material utilizable. El publicador genérico 2.1 aplica la política existente de material neutro de sillas mediante `SavicSourceMaterialFallback`, compartida por ambas familias; captura el material dentro de la transacción de publicación, conserva materiales válidos, documenta el fallback y rechaza prefabs con slots inválidos. No inventa las texturas ausentes. La segunda inspección visual confirmó armario gris neutro, sin magenta ni contaminación de escena.

La verificación final en Unity 6000.3.19f1 terminó con **Closure Gate 11/11 PASS**, prueba de evidencia Meshy e invalidación incremental aprobadas, cuatro sillas con **0 reparaciones / 0 materiales inválidos** y cuatro placeables genéricos con **1 reparación / 0 materiales inválidos**. Repetir la comprobación genérica dio **0 reparaciones**, confirmando idempotencia. Evidencia persistente: `provider-material-final-verification.log`, `canonical-content-inventory.json` y `savic-published-storagefurniture-runtime-playtest.txt` en `Library/BistroBuilder/SAVIC/Logs`.

El cierre con cero revisiones sigue pendiente: tres taburetes requieren su contrato funcional y geometría de asiento, la lámpara su integración de iluminación, la barra curva su contrato de servicio y la campana colocación elevada. La evidencia original de Meshy está disponible en los siete manifiestos; no hay que volver a pedir al usuario que identifique cada objeto.

## 61. Contrato mínimo de lámparas de pie

La lámpara pendiente tiene identidad original Meshy verificada y bounds medidos de 0,438 × 1,900 × 0,438 m. No tiene publicador funcional registrado ni materiales utilizables. Su rechazo actual no indica un fallo de importación: falta una adaptación que autorice iluminación y valide su posición dentro de la geometría. El catálogo ya contiene la categoría `Lighting`; el proyecto utiliza luces nativas de Unity y no tiene otra autoridad de lámparas que deba duplicarse.

Plan mínimo: perfil de autoría editable por familia; analizar base, estructura central y volumen superior de pantalla en espacio métrico; aceptar únicamente identidad explícita de lámpara de suelo y forma compatible, sin adivinar a partir del nombre cortado. La posición de emisión debe derivar del volumen superior medido. Intensidad, alcance y temperatura son decisiones visibles del perfil de autoría, no datos inferidos del GLB. Una adaptación funcional agrega una sola luz nativa al prefab dentro de la transacción existente y la valida antes de registrar catálogo, previews y persistencia. Colocación, footprint y navegación conservan sus autoridades existentes. Se exigen negativos de forma/identidad, comprobación de emisión mediante render, publicación real y colocación/SaveGame save/load del asset antes de cerrar esta familia.

Implementado y comprobado en Unity 6000.3.19f1 el 02/10/2026: `SavicFloorLampProfile` conserva los límites físicos y ajustes de luz en un perfil editable; el planner mide bandas de base, fuste y pantalla mediante triángulos recortados en espacio métrico. No depende de que los vértices coincidan con la altura de las bandas y conserva rotación/escala del importador. La adaptación `FLOOR_LIGHT` agrega y valida una sola luz puntual nativa, habilitada y realtime; repetir su aplicación conserva la misma emisión. No introduce una simulación eléctrica. Los negativos rechazan caja, forma de pared, perfil inválido, luz duplicada y plan alterado sin actualizar su fingerprint.

El original archivado y su mirror SHA-256 se verificaron antes de la publicación. El emisor real queda a **1,66 m**, dentro de la pantalla medida. Un render de prueba con luz apagada/encendida registró un incremento medio de iluminación de **0,03301**; ambas imágenes y el preview grande fueron inspeccionados, sin magenta. La fuente utiliza el material neutro compartido porque no tiene apariencia utilizable. El Closure Gate pasó **12/12** y la cola canónica publicó `cb63bf50dd1a4e7db5d1d5b02a2d62ab` en categoría `Lighting`.

La prueba posterior de Play Mode terminó **PASS**: colocación canónica, SaveGame save/load, mismo `ItemId`, nueva instancia runtime y conservación de categoría Lighting, footprint, collider y emisor. El slot diagnóstico se eliminó. Evidencia: `floor-lamp-real-completion.log`, `floor-lamp-emission-off.png`, `floor-lamp-emission-on.png`, `floor-lamp-runtime-playtest.log` y `savic-published-floorlamp-runtime-playtest.txt` en `Library/BistroBuilder/SAVIC/Logs`.

La auditoría canónica de **2026-10-02 16:18:21 UTC** registra **27 únicos, 13 publicados, 12 en catálogo de placeables, 5 en revisión, 0 fallidos y 9 históricos sin original**. El lote seleccionado queda en **9 publicados y 5 en revisión**. Permanecen los tres taburetes, la barra curva y la campana. El cierre completo sigue pendiente de sus contratos funcionales y pruebas reales.

## 62. Geometría y contrato pendiente de taburetes

La revisión de los tres taburetes conserva originales e identidades Meshy verificadas; falta un módulo de publicación `BarStool`. El analizador de sillas busca asiento solo entre el 30 y el 70 % de altura y necesita respaldo para su orientación. Esto excluye el asiento superior de taburetes sin respaldo y puede confundirlo con el reposapiés. El perfil de silla de comedor fija 0,46 m y no es un sustituto válido para esta familia.

La integración existente también impide resolverlo con un cambio de etiqueta: `RestaurantSeat.IsAssociated` depende de una mesa y un slot; la regla de colocación de asientos solo consulta mesas. `BistroBuilderBarServiceSpot` ya representa plazas de barra con identidad, puntos de cliente/camarero y reserva canónica, pero no incorpora asientos dinámicos procedentes del catálogo. Se necesita una asociación canónica de taburete/plaza de barra que conserve las autoridades espaciales, lógicas y de animación antes de publicar.

Primer paso seguro: un analizador específico mide superficies superiores anchas y apoyo inferior, incluyendo taburetes sin respaldo y rechazando bandas estrechas de reposapiés. Debe probarse con formas sintéticas negativas y los tres originales reales verificados; conservar sus medidas y evidencia sin cambiar estados ni publicar hasta disponer del contrato de uso.

`SavicBarStoolGeometryAnalyzer` y su diagnóstico canónico ya pasaron en Unity 6000.3.19f1. Analizan todos los triángulos dentro de un presupuesto de dos millones, con rotación/escala del importador y sin la traslación externa de la raíz. Buscan la superficie superior con mayor área proyectada y relleno suficiente; la pequeña cúspide de un cojín no sustituye el asiento ancho. Rechazan reposapiés estrecho, superficie aislada sin estructura alta y bounds inválidos. El autotest incluye taburete con/sin respaldo, cúspide de cojín, escala de centímetros, traslación externa e igualdad del resultado al repetir.

El diagnóstico real de **2026-10-02 16:53:23 UTC** volvió a verificar SHA-256 de cada archivo archivado y mirror antes de medir y conservó todos los estados. Resultado **3/3 PASS geométrico**:

| Identidad verificada | Altura del asiento en la fuente | Cobertura proyectada | Relleno de superficie |
|---|---:|---:|---:|
| Blue Quilted Bar Stool | 1,212 m | 0,608 | 0,759 |
| Black Cushioned Bar Stool | 1,892 m | 0,927 | 0,962 |
| Silver Ring Bar Stool | 1,902 m | 0,635 | 0,783 |

Son medidas del GLB exportado, **no alturas físicas de autoría aprobadas**. El próximo contrato requiere un perfil de altura de barra/asiento, normalización uniforme y orientación respaldada por geometría, así como vinculación de asiento a plaza de barra, colocación, reservas, navegación, representación y SaveGame. No debe registrar `RestaurantSeat` de mesa ni publicar como decoración. Evidencia guardada en `bar-stool-real-geometry.log` y `bar-stool-real-geometry.json`; las cinco revisiones siguen abiertas.

## 63. Vinculación dinámica de plazas de barra

La barra pendiente conserva su original y título Meshy verificados. Además de la autoría geométrica, falta integrar una barra creada desde catálogo con los servicios existentes: el registro de barra descubre plazas de escena en `Awake`; `BistroBuilderSpatialRuntimeBinder` vincula asientos de mesa, mesas y puertas, sin registrar plazas de una barra colocable. Publicar un prefab que contenga solo markers no resolvería servicio ni persistencia de sus identidades funcionales.

Plan mínimo: un componente funcional de la barra utiliza `RestaurantPlaceableRegistry` para distinguir instancia provisional y activada. Deriva las identidades de plaza de la identidad de instancia persistida y de un índice estable, y registra las plazas en `BistroBuilderBarServiceRegistry`; BBSIS sigue usando `BindBarSpot` y el contrato canónico existente. El guard de ciclo de vida rechaza dependencias/configuración incompletas y retirada mientras haya clientes. La desactivación cancela sus leases y registros. El descubrimiento inicial debe dejar a este componente gestionar sus plazas, para no registrar prefabs provisionales ni identidades de plantilla. Las pruebas deben demostrar provisional sin efectos, dos copias con identidades distintas, rechazo de conflicto, reserva/retirada y retirada sin registros residuales antes de publicar una barra.

Implementado `BistroBuilderBarPlaceableBinding` como adaptación modular y guard del ciclo de vida existente, sin un nuevo registro de reservas. Las plazas se identifican como `bar.placeable.<InstanceId>.slot_<índice>`; confirmar el registro del colocable las activa, retirarlo limpia registros, leases y proveedores espaciales. El descubrimiento estático de barra omite plazas gestionadas por este componente y un rebuild vuelve a vincular las instancias confirmadas. El binding verifica contrato `work.bar`, los tres puertos canónicos, puntos propios de la barra, dependencias y conflictos de identidad; no permite retirar plazas con grupo o lease activo. La configuración de una plaza ocupada no puede cambiar su identidad o puntos. Un conflicto espacial en la segunda plaza revierte también la primera plaza nueva. El adaptador espacial recibe explícitamente el servicio BBSIS resuelto por el binding.

El autotest de integración pasó en Unity: provisional sin registros, dos instancias con IDs distintos, alta mediante evento de registro del colocable, idempotencia, asignación de un grupo por el registro canónico, lease BBSIS real, guards de ocupación, identidad estable al reactivar, rollback parcial y limpieza completa. La regresión posterior terminó con **Closure Gate 14/14 PASS** y **servicio de barra existente 59/59 PASS**, incluyendo el validador de `Prototype_Restaurant`. Evidencia: `bar-placeable-binding-self-test.log`, `bar-binding-canonical-regression.log` y `SavicV1ClosureGateReport.json`; gate completado **2026-10-02 17:13:24 UTC**. Estas son pruebas de integración del componente y regresión del servicio existente; no equivalen a una barra real publicada ni a su SaveGame jugable.

El audit visual posterior verificó y renderizó los originales de barra y campana sin cambiar estados ni el archivo fuente. Previews conservados bajo `Assets/Generated/BistroBuilder/SAVIC/Diagnostics/SourceAudit/<SavicId>` y log `pending-service-source-audit.log` (**2 fuentes verificadas, exit 0**). La barra real es un mostrador curvo con una zona interior vacía y acceso trasero. Una caja/collider/huella rectangular que cubra todo su bounding box taparía el acceso del camarero: su autoría requiere descomposición del cuerpo físico y comprobación de ruta hasta el punto de servicio antes de publicar. La campana necesita altura de montaje y viabilidad espacial elevada; las huellas existentes resuelven solapamiento en XZ, por lo que subir solo la malla no demuestra ese contrato. El preview actual de campana muestra principalmente su cara posterior; no sustituye una inspección de su cara funcional.

## 64. Geometría física compuesta para colocación y navegación

Causa comprobada en código: `RestaurantPlacementValidationService` y `BistroBuilderNavigationService.RebuildNavigationTopology` consumen un único rectángulo de `RestaurantPlacementFootprint`. BBSIS ya admite piezas estáticas compuestas, pero estas no llegan a ambas consultas. Para una barra en U esto cierra artificialmente su interior y acceso aunque el GLB esté abierto. No basta con quitar el bloqueo de la huella: Navigation consulta las piezas estáticas a través de su topología y BBSIS conserva la autoridad de leases dinámicos.

Plan mínimo: adaptación opcional que consume las cajas estáticas del proxy BBSIS de la misma raíz, sin otra lista autoritativa. Mantener el rectángulo exterior para límites de área y procedencia de conflictos; comparar las piezas para colisión/separación y proyectarlas a Navigation. Limitar cantidad, comprobar valores finitos, escala positiva, orientación horizontal, propiedad de raíz y contención en la envolvente. Rechazar colocación con adaptación inválida y conservar su rectángulo como bloqueo conservador de navegación. Los objetos sin adaptación mantienen el comportamiento previo. Antes de usarla en la barra real, probar hueco, paredes, separación, transformación de pose, datos inválidos, ruta nativa y regresiones de edición/navegación. Este puente no publica assets ni resuelve por sí solo autoría de mostrador, taburetes o campana elevada.

La prueba nativa negativa demostró un defecto previo adicional: con el interior correctamente bloqueado por la envolvente de respaldo, `TryBuildOperationalDockRoute` todavía devuelve una ruta porque valida la aproximación a un punto del anillo y añade el destino original sin comprobar el último segmento. Evidencia: `compound-physical-native-final-regression.log`, excepción `Invalid compound data left its cavity traversable`. Corrección mínima antes de publicar: cada candidato de docking debe superar la consulta estructural existente para su enlace final al destino, antes de competir como mejor ruta. Mantener tolerancias de interacción y autoridades existentes; verificar el caso negativo y la regresión instalada de Navigation/Edit Mode.

Implementados `BistroBuilderSpatialPhysicalFootprintAdapter` y `BistroBuilderPhysicalPlacementGeometry`, consumidos por el validador existente y la topología de Navigation. No mantienen otro registro geométrico: proyectan cajas estáticas BBSIS propias de la raíz; conservan su separación mínima y la envolvente para límites de área. Rechazan más de 128 piezas, anclajes ajenos/articulados, formas no soportadas, pose inclinada, escala negativa, valores no finitos y piezas que exceden la envolvente. Los datos inválidos bloquean colocación candidata y conservan la caja exterior como obstáculo existente. Se corrigió también el enlace final de docking demostrado por la prueba negativa.

Verificación del código integrado en `compound-physical-verified-regression.log` (**exit 0**): autotest geométrico, colocación nativa libre en el interior y bloqueada en las paredes, límites de área conservados, rechazo de candidato inválido y bloqueo conservador del objeto existente; ruta nativa de camarero `GridFallback` de **5,000 m**, muestreada cada 5 cm contra las piezas físicas con semiancho de agente de 0,28 m. Al invalidar una pieza no existe ruta hacia el interior bloqueado. Regresiones instaladas: **Edit Mode core 84/84 PASS** en escena aislada y **Navigation 17 22/22 PASS** en `Prototype_Restaurant`. El primer ensayo conjunto del core había encontrado un bloqueo ajeno en la puerta de su fixture porque el test reconstruye todos los sujetos BBSIS de la escena; el runner ejecuta ahora ese fixture antes de cargar Prototype. No se cambiaron sus aserciones ni las reglas de puerta.

`SavicCompoundBodyGeometryAnalyzer` deriva el cuerpo conservador desde todos los triángulos estáticos del modelo, usando la conversión métrica canónica. Rasteriza la intersección triángulo/celda en XZ, combina runs en cajas sin cambiar la unión ocupada y verifica si el vacío comunica con el exterior; un agujero cerrado no prueba acceso. Presupuesto: cuatro millones de triángulos de **instancia**, cuadrícula máxima de 48×48 y 128 cajas. El analizador general cuenta recursos de malla únicos: esta proyección mide cada instancia, por lo que compartir una malla no puede omitir piezas físicas. Pruebas aprobadas de U abierta, agujero cerrado, caja sólida, triángulo diagonal sin rellenar su bounding box, equivalencia entre cuadrícula y cajas, centímetros/rotación/traslación externa, repetición y límites.

El diagnóstico real de **2026-10-02 18:28:53 UTC**, después de volver a verificar SHA-256 del archivo archivado y mirror de `Arctic Curve Bar`, recorrió **2.754.580 triángulos**. Resultado: **39 cajas**, cuadrícula **48×31**, **491 celdas ocupadas**, vacío accesible con apertura **+Z** y holgura interior medida en fuente de **0,293 m**. Raster inspeccionado: conserva la concavidad abierta del mostrador. Evidencia: `bar-compound-real-geometry-final.log` (**exit 0**), `bar-compound-real-geometry.json` y `bar-compound-source-geometry-a790cc592bd44915a934db197178564d.png`. Las cajas siguen siendo evidencia de fuente; faltan perfil físico, altura de mostrador, markers fuera del cuerpo, autoría de collider/subject/semántica, publicación y prueba real de SaveGame. No se cambió el estado de la barra ni se afirmó una ruta de su prefab final.

Cierre de esta ampliación: **Closure Gate 16/16 PASS** completado **2026-10-02 18:32:02 UTC**, seguido de auditoría nueva en la misma ejecución con **exit 0** (`compound-body-final-closure-inventory.log`). Inventario: **27 únicos, 13 publicados, 12 catálogo placeables, 5 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original** fuera del lote solicitado. El lote de 14 conserva nueve publicados y cinco revisiones. Los bloqueos funcionales siguen abiertos: tres taburetes con asociación a plazas de barra, barra real con servicio/persistencia y campana elevada. No se descartaron assets ni se alteraron estados para cerrar el contador.

Continuidad: recomputar la geometría desde el source verificado dentro del futuro planner de barra, almacenar plan/fingerprint canónico y aplicar perfil físico explícito; los logs del diagnóstico no son una autoridad de autoría. Integrar el cuerpo raíz con IDs de instancia y conservar provisional sin registro. Revisar el proxy de las plazas (`ConfigureBarProxy` deriva actualmente una caja entre markers) y el proveedor semántico de raíz: la colocación consulta sujetos/proveedores de raíz, mientras que la vinculación dinámica existente gestiona plazas hijas. Comprobar cuerpos/puertos y leases sin cerrar el hueco ni crear registros provisionales antes de publicar. El contrato actual ancla `bar.transfer` en el punto real de servicio del camarero. Después enlazar los taburetes, y resolver el estrato elevado de campana bajo D-003. La descripción de Lighting/Furniture en GenericPublisher todavía requiere autoría canónica con preservación de valores manuales.

## 65. Cuerpo de barra y semántica de instancia provisional

Causa verificada: el assessment de colocación consulta proveedores de la raíz, pero las plazas de barra exponen sus tres puertos como componentes hijos. El cuerpo compuesto necesita un sujeto raíz y esos puertos para el preflight. Además, `SpatialSubject.Configure` registra automáticamente cualquier ID activo y `RebuildSubjects` vuelve a descubrirlo: asignar el ID provisional sin política de ciclo de vida produciría un obstáculo fantasma. La recopilación semántica global tampoco distingue una propuesta provisional de una instancia confirmada. Por último, `ConfigureBarProxy` deriva una caja física entre cliente y camarero, aunque el cuerpo de esta barra ya está medido y contiene un hueco.

Plan mínimo: propietario opcional de ciclo de vida espacial cuya elegibilidad consulta `RestaurantPlaceableRegistry`, aplicado al sujeto raíz y plazas de la barra compuesta. BBSIS conserva su registro único y rechaza descubrimiento/alta de sujetos provisionales; los demás sujetos mantienen su política anterior. Un adaptador de cuerpo raíz prepara su identidad a partir de InstanceId, expone puertos para evaluar la propuesta y limita su semántica global a colocables confirmados. Las plazas hijas siguen decidiendo reservas/servicio con su adaptador existente; su proxy aporta solo operación cuando el cuerpo físico pertenece a la raíz y no duplica semánticas. Vincular/desvincular el cuerpo junto con las plazas existentes, probar identidad, provisional/rebuild, cuerpos/puertos reales, leases, bloqueo de propuestas y limpieza antes de la publicación.

Implementación verificada en `bar-body-spatial-native-verified.log` con **exit 0**: instancia provisional sin alta incluso durante rebuild, preflight con puertos propios, raíz y plazas sin duplicar cuerpos/semánticas, asignación y lease canónicos, rechazo de retirada ocupada, dos identidades, conflicto durante registro con rollback parcial y reintento limpio. Rebuild real, Navigation y calidad espacial excluyen la propuesta provisional. Regresiones de colocación/ruta nativa, core **84/84**, Navigation **22/22** y barra existente **59/59 PASS**. Esta prueba usa un cuerpo sintético; no afirma publicación ni SaveGame de la barra Meshy.

## 66. Perfil físico y plan canónico del mostrador

Causa pendiente: la exportación Meshy no demuestra unidades físicas de una barra utilizable; aplicar directamente su altura total de 0,539 m la convertiría en un mostrador demasiado bajo, y la altura máxima incluye fixtures. Las 39 cajas del diagnóstico tampoco constituyen un plan autoritativo. Plan mínimo: medir la superficie superior ancha dominante desde los triángulos, distinguirla de una cúspide estrecha y recomputar cuerpo/interior dentro del planner. Un perfil de autoría explícito establece altura de mostrador de 1,05 m, límites físicos y separación de puertos; no presenta estas elecciones como medidas del original. Conservar escala uniforme, cuerpo, puntos, hash de fuente/metadata/perfil y fingerprint del plan. Rechazar caja sólida, taburete, superficie insuficiente, hueco estrecho y perfil inválido; verificar origen real y repetición antes de integrar el publicador. Mantener la revisión hasta las pruebas de rutas, servicio y persistencia del prefab real.

La primera medición del plano dominante encontró **0,339 m** en la fuente y habría normalizado el total a 1,668 m. Antes de usarla en un prefab, la inspección del original y el espectro de triángulos superiores demostraron dos niveles: plano inferior ancho (cobertura 0,201) y repisa superior a **0,460 m** (cobertura 0,096). El nivel más ancho no equivale al mostrador superior del cliente. Evidencia del diagnóstico conservada en `bar-counter-source-upper-spectrum.log` (**exit 0**) y `bar-compound-real-geometry.json`. El analizador 1.2 usa el nivel superior suficientemente ancho, con umbral de cobertura 0,08 y área al menos 20% de la dominante; piezas pequeñas de grifería/cúspides no califican. El perfil inicial aún no publicado se ajustó a ese umbral general. Un negativo/positivo de barra de dos niveles protege esta distinción; volver a verificar el plan real antes de considerar autoría final.

Plan real verificado en Unity **2026-10-02 19:31:19 UTC**, `bar-counter-real-authoring-final.log` (**exit 0**): superior de fuente **0,460121 m**, perfil explícito **1,05 m**, escala uniforme **2,282010**, envolvente física **4,340×1,230×2,789 m**, **39 piezas** y holgura de camarero **0,668 m**. Los puntos se derivan del hueco y su apertura +Z; cliente fuera del frente cerrado y camarero dentro del hueco, comprobados contra todas las cajas. El planner recomputa fuente/cuerpo; rechaza metadata geométrica desactualizada y no usa el JSON de diagnóstico como autoridad. Fingerprint incluye fuente, metadata, perfil, versiones, dimensiones, puntos y piezas. Pruebas de dos niveles, cúspide, unidades/rotación/traslación, repetición, invalidez del perfil, caja sólida, taburete y hueco estrecho aprobadas. SHA-256 del original archivado y mirror, y metadata Meshy, verificados nuevamente.

`bar-counter-real-authoring-plan.json` conserva el plan de la prueba, sin modificar estados ni manifiestos. La familia/publicador de barra aún deben recomputarlo y guardarlo en `manifest.barCounter` mediante la cola canónica. Siguiente paso: autoría de visual con escala uniforme, colliders físicos descompuestos y proxy raíz equivalentes, plaza nativa y markers; validar configuración, navegación real, servicio/leases y SaveGame antes de publicar. El publicador genérico exige todavía BoxCollider raíz, considera navigationReady equivalente a footprintReady y anuncia que no hay contrato BBSIS; estas tres reglas necesitan adaptación específica al módulo funcional compuesto, manteniendo validación conservadora para los demás placeables.

`SavicBarCounterFunctionAdapter` ya aplica el plan: escala el contenedor Visual conservando la conversión del importador, elimina la caja raíz y colliders de fuente, crea una pieza física por caja y su proxy BBSIS equivalente, y configura una plaza de servicio nativa con sus puntos orientados. Usa el binding y cuerpo raíz de la sección 65. Valida perfil/fingerprint, contrato canónico, transforms, dimensiones, piezas, puerto/ocupación física y ausencia de duplicados. Aplicación repetida no aumenta piezas/plazas. Negativos de caja raíz que cierra el hueco, collider modificado, contenedor desplazado y punto operativo dentro del cuerpo rechazados. La prueba del source real pasó en `bar-counter-real-function-authoring.log` (**exit 0**); bounds de Renderer del source normalizado coinciden con el plan y minY=0. No se ha registrado todavía la familia de publicación ni cambiado la clasificación canónica.

Verificación nativa del **source real autorado** en `bar-counter-real-native-service-route.log` (**exit 0**): altas de cuerpo/plaza derivadas de identidad de instancia, asignación por el registro de barra y lease BBSIS de cliente, retirada ocupada rechazada y limpieza completa. Navigation consume exactamente **39 piezas** y devuelve ruta `GridFallback` de **3,394 m** hasta el punto de camarero, muestreada cada 5 cm con semiancho de agente de 0,28 m, sin atravesar el cuerpo. La prueba usa autoridades nativas en una escena aislada de Editor; no equivale a creación desde catálogo, servicio con IA en Play Mode o SaveGame de un prefab persistente. Estas pruebas siguen siendo el siguiente requisito antes de publicación real.

Cierre verificado después de integrar autoría/validación: **Closure Gate 18/18 PASS**, completado **2026-10-02 19:46:54 UTC** y auditoría fresca en la misma ejecución, `bar-counter-function-final-closure-inventory.log` (**exit 0**). Inventario conserva **27 únicos, 13 publicados, 12 catálogo placeables, 5 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original** fuera del lote. El lote solicitado conserva nueve publicados y cinco revisiones. No se alteró una clasificación/estado para declarar la barra terminada. Próxima implementación: módulo de familia `BarCounter` con clasificación demostrada, planificación/metadata/rollback en publicador canónico y prueba de prefab de catálogo/SaveGame en Play Mode. Recomputar el plan desde source verificado; los JSON de pruebas no deben entrar como autoridad. Resolver después taburetes y estrato elevado de campana. Mantener valores manuales al corregir la descripción genérica de Lighting/Furniture.

## 67. Aceptación runtime antes de publicar barra

Causa verificada: el publicador genérico une construcción de artifacts, alta en catálogo y estado PUBLISHED en una sola ejecución de Editor; su readiness equipara huella a navegación y solo admite collider raíz. Esto no prueba el ciclo de una barra funcional. Plan mínimo: preparar candidato con la misma transacción/autoría compartida, sin entrada en catálogo principal y manteniendo NEEDS_REVIEW; readiness distingue geometría estructural de aceptación runtime. La familia recomputa geometría y perfil desde source, y exige evidencia vinculada al fingerprint del plan y dependencia del prefab antes de finalizar publicación. La prueba Play Mode usa una copia runtime del catálogo canónico con el candidato, sin escribir el catálogo principal; crea por el servicio existente, comprueba registros/ruta/lease, SaveGame save/load con identidad estable/nueva instancia y elimina el slot. Solo al completar esa prueba se conserva aceptación y se publica mediante la transacción canónica. Cambios en plan, perfil o prefab invalidan esa aceptación. No crear otro registro de catálogo o reservas ni dar por publicado un candidato.

Familia `BarCounter` y clasificación registradas en el pipeline canónico. El publicador 2.3 admite el cuerpo compuesto y conserva la revisión del candidato sin entrada en catálogo principal. La aceptación enlaza fuente, plan, dependencia del prefab, informe SHA-256 y seis comprobaciones de creación/registro/ruta/lease/persistencia/limpieza. La reconciliación solo vuelve a encolar una aceptación actual, verificada contra el original; nunca salta directamente a Done. Negativos de identidad, prueba incompleta, prefab/plan/informe desactualizados e idempotencia aprobados. Dependencias de servicios nativos del binding/cuerpo son caches runtime; el prefab conserva referencias internas y contrato, sin serializar servicios de escena.

La primera prueba en `Prototype_Restaurant` demostró que su layout existente está ocupado: 120 poses rechazadas por geometría/espacio operativo de mesas, sillas y barra instalada, o límites del comedor. Evidencia completa en `bar-counter-runtime-placement-evidence.log`. Se preparó un layout temporal en Play Mode retirando 38 mesas/sillas por el ciclo de vida canónico, conservando obstáculos, área, validadores y servicios originales; no se guarda la escena. El candidato pasó creación, identidad raíz/plaza, asignación/lease, guard de retirada ocupada, ruta GridFallback muestreada y SaveGame con nueva instancia e identidad estable. El slot 960 se eliminó realmente. Evidencia: `bar-counter-runtime-canonical-layout.log`, aceptación 20:28:38 UTC.

Publicación real por la cola y transacción canónicas, seguida de regresiones core **84/84**, Navigation **22/22**, servicio de barra **59/59** y Closure Gate **18/18 PASS**: `bar-counter-runtime-accepted-publication.log`, **UnityActualExitCode=0**. Auditoría nueva **2026-10-02 20:32:41 UTC**: **27 únicos, 14 publicados, 13 catálogo placeables, 4 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original** excluidos del lote solicitado; selected14 = diez publicados y cuatro revisiones. Una segunda prueba de la barra publicada resolvió el item desde el catálogo principal real y las definiciones reales de SaveGame, sin instalar catálogo candidato; creación, registros, rutas/leases antes/después de load y slot eliminado PASS, `bar-counter-published-main-catalog-playtest.log`, **UnityActualExitCode=0**. No equivale a demostrar una jornada completa de IA atendiendo pedidos nuevos. Siguen pendientes tres taburetes funcionales y campana elevada.

## 68. Orientación y escala física de taburetes

Durante el cierre de Play Mode aparecieron dos errores de TMP: el clon runtime de Recoleta conserva material/atlas del asset persistente, y `TMP_FontAsset.OnDestroy` intenta destruirlos. Las aserciones funcionales y el slot sí pasaron, pero Console limpia exige corregir esta regresión antes de aceptar cierre completo. Causa en `BistroBuilderTypography.Title` (`Object.Instantiate` superficial del font) y destructor TMP instalado. Plan mínimo: el clon runtime debe poseer sus propias texturas/material y enlazar su material a su atlas; conservar el asset original. El verificador de SAVIC debe observar Error/Exception/Assert hasta volver a Editor y guardar aceptación únicamente tras esa limpieza, sin filtrar estos errores.

Plan mínimo antes de publicar: extender la medida de asiento con evidencia de geometría por encima de la superficie. Un respaldo unilateral permite orientar el frente en sentido opuesto a su centro de área; estructura superior simétrica/ambigua conserva revisión. Un taburete sin estructura alta puede adoptar frente +Z como convención explícita de autoría, no como orientación original descubierta. Normalizar uniformemente la altura de asiento a un perfil canónico de barra, conservar hashes/perfil/fingerprint y comprobar medidas físicas de los tres originales antes de integrar asociación a plazas. El contrato nativo de plaza continúa como autoridad de servicio/reservas; no registrar RestaurantSeat de mesa, aumentar capacidad artificialmente ni publicar como decoración.

Implementados analizador de asiento **1.1**, `SavicBarStoolAuthoringPlanner` y perfil `Assets/Data/Restaurant/SAVIC/BarStoolProfile_Standard.asset`. El perfil establece asiento **0,75 m** para mostrador **1,05 m**; estas son dimensiones explícitas de autoría, no unidades deducidas del GLB. El respaldo de Blue da un desplazamiento superior unilateral de **−0,40 m en Z** respecto al asiento: frente medido +Z, corrección yaw −0,036°. Black y Silver carecen de estructura alta significativa; +Z es una convención explícita para su autoría sin respaldo. Una estructura simétrica superior no permite inferir frente y se rechaza para el plan.

Verificación real **2026-10-02 20:42:49 UTC**, SHA-256 de originales y mirrors, tres planes repetibles y tres visuales normalizados: `bar-stool-real-normalized-visual.log`, **UnityActualExitCode=0**. Escalas uniformes Blue **0,618870**, Black **0,396499**, Silver **0,394310**; envolventes aproximadas **0,577×1,177×0,564 m**, **0,360×0,755×0,357 m**, **0,396×0,750×0,396 m**. Renderer dentro de la envolvente, minY=0, punto de asiento a 0,75 m y frente +Z comprobados sobre cada source real. Fingerprint cubre fuente, metadata, versiones, perfil, orientación, dimensiones y punto de asiento. Negativos de perfil NaN, geometría desactualizada, estructura superior ambigua y fingerprint modificado PASS. La prueba conserva planes en `bar-stool-real-geometry.json`; no modifica manifiestos/estados ni publica taburetes. La familia futura debe recomputar el plan desde source y guardarlo canónicamente; ese JSON no es autoridad.

Pendiente concreto: componente de asiento de barra con asociación a plaza canónica, regla de colocación y guards de ocupación/lease, semántica de asiento y representación por Animation existente, identidad/rehidratación tras SaveGame. La plaza conserva su capacidad y servicio nativos; un taburete no crea capacidad nueva por el mero hecho de publicarse. `RestaurantSeatingPlacementConstraintRule` solo consulta mesas y debe mantenerse separado del nuevo contrato de barra.

## 69. Console limpia y propiedad de fuentes runtime

El clon runtime de Recoleta conserva ahora copias propias del atlas y material, con el material enlazado a su atlas. Así el destructor de TMP libera solo recursos runtime. El verificador sigue Error/Exception/Assert durante Play Mode y su salida hasta volver al Editor; una aceptación 1.1 exige `consoleClean=true` y solo se guarda después de esa limpieza. No se filtran los errores encontrados. Reprueba completa del catálogo principal real **2026-10-02 20:49:41 UTC**: creación, registros/plaza, asignación/lease, rutas antes/después de load, identidad estable/nueva instancia y eliminación de slot PASS; **Console runtime y limpieza de Editor sin errores** y **UnityActualExitCode=0** en `bar-counter-runtime-strict-acceptance.log`.

Cierre posterior **2026-10-02 20:51:55 UTC**, `bar-counter-and-stool-profile-final-verification.log`, **UnityActualExitCode=0**: **Closure Gate 19/19 PASS**, core **84/84**, Navigation **22/22** y barra existente **59/59 PASS**. El nuevo test de propiedad usa el font real persistente: atlas/material independientes, pixels GPU y tabla de glifos conservados, destrucción de recursos propios y hash del source sin cambios. La reutilización del publicador genérico captura también el item antes de reasignar previews y editar catálogo, preservando rollback. Auditoría fresca: **27 únicos, 14 publicados, 13 catálogo placeables, 4 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original** fuera del lote; selected14 = **10 publicados + 4 revisiones**. Los cuatro pendientes son Blue/Black/Silver BarStool y Commercial Kitchen Exhaust Hood. No quedan pruebas Unity de este corte en ejecución.

Continuidad: integrar asociación de asiento/plaza con registro de barra, BBSIS, Navigation y Animation existentes, guards de ocupación y persistencia, sin otra capacidad/reserva autoritativa. Recomponer y almacenar `manifest.barStool` mediante la futura familia, no desde el informe. Después campana en estrato elevado con cara funcional verificada, sin D-003. Las descripciones nuevas de Furniture/Lighting ya se generan correctamente y preservan valores manuales; revisar actualización de items previamente publicados que conserven texto generado antiguo. No declarar SAVIC terminado mientras sigan cuatro revisiones.

## 70. Contrato nativo de taburete y separación entre aproximación y asiento

Auditoría previa a esta ampliación: `BistroBuilderBarServiceSpot` solo tenía puntos de cliente/camarero y ocupación nativa; `RestaurantSeat` pertenece a la topología de mesas. `CustomerMovementView` dirigía al grupo al `CustomerPoint` de suelo. Introducir un SeatFrame elevado como destino de Navigation o crear otra ocupación de taburetes duplicaría autoridades y no probaría que un cliente se sienta.

Implementado `BistroBuilderBarSeatBinding`: asociación opcional de un taburete confirmado con **una plaza nativa existente de capacidad 1**, sin crear plazas ni capacidad. `Occupant` se deriva de `AssignedCustomerGroup`. Exige identidad del cuerpo `spatial.bar.seat.<InstanceId>.body`, alta real en PlaceableRegistry/BBSIS y plaza/cuerpo de barra presentes en sus registros. Perfil, cuerpos y frames deben ser finitos, propios y horizontales; asiento alineado con el CustomerPoint, frente compatible, distancia vertical al mostrador autorado y aproximación de suelo detrás del asiento fuera de su cuerpo físico. El `CounterSurfacePoint` es un dato de autoría opcional de la plaza; una barra sin ese dato **no acepta** una asociación de taburete por suposición.

La plaza expone `CustomerApproachPoint` separado del CustomerPoint original y SeatFrame elevado. Navigation usa la aproximación de suelo. El lease nativo conserva su puerto/ocupación originales y únicamente puede excluir el cuerpo BBSIS del taburete asociado y validado mediante `relatedSubjectId`; cuerpos o leases ajenos siguen bloqueando. Se impide sustituir identidad/superficie de una plaza asociada, retirar taburete ocupado/reservado y retirar barra con taburetes asociados. La autoría provisional no registra cuerpo ni cliente. **La asociación todavía es una API explícita: no hay descubrimiento automático por colocación, familia de publicación ni persistencia de enlaces de taburete integrados.**

Prueba nativa en Unity: provisional aislado, asociación única/idempotente, capacidad conservada, ocupación de registro real, guard de grupo/lease, rechazo de segunda copia, cuerpo o reserva ajenos, orientación/altura/approach incompatibles, datos no finitos, cuerpo ausente y limpieza. `bar-seat-native-association-second.log`, **UnityActualExitCode=0**. La prueba inicial encontró un error en su fixture (intentaba añadir de nuevo el footprint ya requerido por PlaceableObject); corregido usando el componente existente, sin modificar datos reales.

## 71. Regresión demostrada del coordinador BBSIS de barras dinámicas

La integración de taburetes reveló una limitación concreta del coordinador operacional: su lista de adaptadores se construía al arranque mediante escaneo de escena y no atendía altas posteriores de BarServiceRegistry. Además, una plaza ya liberada saltaba la reconciliación sin soltar su lease de cliente. La prueba añadió una plaza **después** de configurar el coordinador, la asignó mediante el registro nativo y pidió reconciliación sin rebuild manual. Falló como se esperaba con `Operational coordinator did not grant the newly registered bar's customer lease without a manual rebuild`, **UnityActualExitCode=1**, en `bar-dynamic-coordinator-before-fix.log`. Es evidencia de regresión; el PASS anterior de asignación/lease directo de barra no demostraba este flujo automático.

Corrección dentro de `BistroBuilderOperationalSpatialCoordinator`: vinculación por plazas del registro nativo, suscripción a altas/bajas, reconstrucción inicial tras Start, liberación de lease al desaparecer el grupo y limpieza al retirar la plaza. No se añade un registro de reservas ni se modifican decisiones de gameplay. El autotest prueba alta tardía, lease automático, liberación lógica y baja con limpieza; incorporado al gate. La aceptación de barra publicada usa ahora **reconciliación del coordinador** para conceder y liberar su lease, antes y después de SaveGame, sin llamar directamente a la adquisición para demostrar ese camino.

Reprueba desde catálogo principal real **2026-10-02 22:24:21 UTC**: creación canónica, IDs cuerpo/plaza persistidos, asignación/lease y liberación automáticos, busy guard, rutas GridFallback muestreadas cada 5 cm, SaveGame con nueva instancia Unity, slot eliminado y Console limpia hasta Editor. `bar-counter-dynamic-coordinator-runtime-acceptance.log`, **UnityActualExitCode=0**; aceptación 1.1 actualizada y hashes comprobados nuevamente al cierre. No afirma una jornada completa de IA.

## 72. Autoría física real de taburetes y pendientes exactos

Planner de taburetes **1.1** añade aproximación calculada desde envolvente física y perfil: radio de cliente **0,32 m**, margen **0,10 m**, tolerancia de asociación **0,08 m** y máximo yaw **10°**. Son parámetros comunes explícitos. Implementado y registrado `SavicBarStoolFunctionAdapter` (`NATIVE_BAR_SEAT`): escala/yaw uniformes del source normalizado, un collider/cuerpo BBSIS conservador, footprint compartido, SeatFrame/approach propios, anchors y contrato `seating.bar` en `BB_SpatialContract_Seat_Bar_Stool.asset`. No crea RestaurantSeat/Table/plazas de barra; el sujeto depende del ciclo de vida del taburete y el prefab provisional permanece inelegible. Este contrato todavía debe integrarse en el catálogo canónico de familias espaciales al habilitar publicación.

**Tres originales y mirrors SHA-256 verificados**, planes recomputados/repetibles y autoría real aplicada dos veces: bounds/altura/frente normalizados, un cuerpo/collider, frames propios, provisional sin alta/ocupación y negativos de approach/collider manipulado PASS. `bar-stool-real-native-function-authoring.log`, **UnityActualExitCode=0**, informe `bar-stool-real-geometry.json` de **2026-10-02 22:32:20 UTC**. El informe sigue sin ser autoridad de publicación; no se han estampado planes ni cambiado estados reales de taburetes mediante esta prueba. La futura familia debe recomputarlos en su módulo.

Regresión final **2026-10-02 22:39:35 UTC**, `bar-seat-function-final-verification.log`, **UnityActualExitCode=0**: **Closure Gate 21/21**, core **84/84**, Navigation **22/22**, barra existente **59/59** y BBSIS operativo fase 2B **18/18 PASS**. Prueba vigente de la barra publicada (fuente/plan/perfil/prefab/informe) comprobada. Auditoría fresca: **27 únicos, 14 publicados, 13 catálogo placeables, 4 NEEDS_REVIEW, 0 FAILED, 0 inbox, 9 históricos sin original excluidos**; selected14 conserva **10 publicados + 4 revisiones**. Los cuatro pendientes siguen siendo Blue/Black/Silver BarStool y Commercial Kitchen Exhaust Hood.

Continuidad mínima y comprobable:

1. Completar asociación automática y regla de colocación por pose propuesta, con semántica BBSIS relacionada **solo** con la plaza/cuerpo de barra compatibles. Derivar identidad real del cuerpo al confirmar/rehidratar. El componente actual no lo hace automáticamente. Una activación funcional que falle debe revertir mediante el lifecycle canónico; no confiar en que un callback de registro convierta ese fallo en éxito.
2. Autoría canónica del CounterSurfacePoint a partir de `manifest.barCounter.counterHeightMeters`; el prefab de barra publicado aún no lo contiene. Preservar valores manuales/rollback y volver a probar la barra si cambia su prefab. No deducir ese punto desde el log.
3. Integrar representación por Animation. El prefab real `Assets/Prefabs/Customers/CustomerGroupPrefab.prefab` es un placeholder sin hijos/Animator; `BistroBuilderAdvancedCustomerMemberVisualGroup` crea cápsulas por miembro. El bootstrap V1 requiere Animator y `CharacterAnimationServiceV1.ApplyPresentationTargets` solo consume manos/mirada; un SeatFrame por sí solo no eleva/alinea la pose sentada. Existe el Humanoid certificado ya usado por los probes de Animation (`Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx`) y recetas sit/idle/stand. Usar una adaptación visual canónica, conservar el root lógico de Navigation y demostrar cliente realmente sentado; no presentar una cápsula de pie o un actor diagnóstico sin integración como aceptación.
4. Persistir/reconstruir el enlace mínimo por IDs nativos. `restaurant.structure` versión 1 guarda placeables y enlaces de sillas de mesa; su carga prioriza mesas (0), sillas (2) y otros (1), por lo que una barra/taburete nuevos no tienen orden de dependencia ni enlace persistido. Extender con compatibilidad para archivos anteriores, validar antes de reconstrucción destructiva y probar SaveGame real, ocupación/reservas, repetición y nueva instancia Unity.
5. Habilitar familia, plan común y candidatos transaccionales únicamente con esas integraciones; publicar tras aceptación real actual, conservando revisión mientras falte. Después campana elevada/cara funcional y estrato espacial, sin extracción D-003; revisar textos generados antiguos de items Lighting/Furniture conservando manuales.

No queda Unity de estas pruebas en ejecución; se preserva el estado real y la tarea sigue pendiente de cero revisiones.

## 73. Asociación automática por pose y activación transaccional

Implementada la resolución automática de una plaza de barra compatible desde la pose propuesta del taburete, sin mover la instancia ni reservar durante el preflight. Comprueba altura de superficie/asiento, frente, aproximación de suelo, cuerpo confirmado, plaza libre y coincidencia única; rechaza ambigüedad, ocupación, leases y datos inválidos. La regla se instala en el servicio de restricciones existente. Una barra con taburetes asociados no puede moverse dejando enlaces huérfanos. El catálogo espacial incluye ahora `seating.bar`.

`RestaurantPlaceableRegistry` ofrece participación opcional en la activación: los bindings de barra y taburete confirman sus registros funcionales antes del evento público. Un fallo devuelve error y revierte altas parciales e índices del colocable. La identidad del cuerpo de taburete deriva de `InstanceId`, no de la plantilla; el provisional sigue sin cuerpo ni asociación globales. No se añade otra autoridad de ocupación o capacidad.

La semántica candidata relaciona la aproximación exclusivamente con el puerto de cliente de la plaza compatible mediante sujeto e ID semántico exactos. No exceptúa los puertos de camarero/transferencia ni cuerpos o reservas ajenos. BBSIS concede el lease de cliente omitiendo solo el cuerpo del taburete confirmado y asociado. Pruebas negativas de pose, frente, ambigüedad, excepción semántica excesiva y fallo de activación con rollback; repetición y reactivación sin duplicados. Evidencia: `bar-seat-automatic-placement-first.log` y gate posterior de la sección 76.

## 74. Superficie de barra autorada y persistencia de enlaces mínimos

El adaptador funcional de barra **1.1.0** crea un `CounterSurfacePoint` propio desde el plan canónico: datum a **1,05 m**, proyectado en XZ sobre el punto de cliente. Es un datum de asociación autorado; no afirma descubrir un punto físico nuevo en el GLB. La validación rechaza una superficie alterada. El fingerprint incorpora la versión del adaptador y la aceptación verifica también la autoría del prefab actual.

La actualización de la barra publicada pasó por reconciliación verificada de fuente/plan, reencolado idempotente por fingerprint y preparación/publicación canónicas. Se preservó la entrada existente de catálogo. No se editaron estados para simular aceptación ni se usó un script externo de recuperación. `bar-counter-canonical-surface-upgrade.log` y aceptación candidata posterior terminaron con exit 0. La reprueba final del catálogo principal real, **2026-10-02 23:44:05 UTC**, confirma colocación, registros, lease y liberación automáticos, rutas antes/después de SaveGame, mismo ItemId/InstanceId con nueva instancia Unity, slot eliminado y Console limpia hasta Editor: `bar-counter-native-seat-foundation-runtime-final.log`, exit 0, aceptación 1.1 actual.

`restaurant.structure` pasa a **versión 2**, con enlaces mínimos `seatInstanceId`, `barInstanceId` e índice estable de plaza. Occupant y reservas permanecen en sus autoridades nativas. La migración pura desde v1 conserva el contenido anterior y normaliza la lista ausente a vacía, sin inventar asociaciones. Antes de destruir el estado vivo se validan identidades, roles, duplicados, capacidad, índice y compatibilidad de los frames de los prefabs en sus poses guardadas. La reconstrucción crea la barra antes del taburete y comprueba el enlace real resultante; la retirada prepara taburetes antes de barras. Pruebas de migración y negativos en `bar-seat-persistence-contract`, gate 23/23.

Los **tres GLB reales**, con archive/mirror SHA-256 verificados, pasaron autoría y asociación automática nativa con la barra publicada. Una prueba aislada registra cuerpos/plaza, asigna grupo y lease reales, guarda los IDs como JSON y reconstruye **dos veces ambos objetos** conservando InstanceId/ID de plaza con nuevas instancias Unity; termina sin sujetos ni leases residuales. Evidencia: `bar-stool-real-automatic-native-roundtrip-second.log`, exit 0, `bar-stool-real-geometry.json` de **23:41:47 UTC**. Esto demuestra integración nativa y reconstrucción JSON en Editor: **todavía no demuestra SaveGame de taburetes en Play Mode, representación sentada ni publicación**. Sus manifiestos y revisiones se conservaron.

## 75. Regresión comprobada de lease cacheado después de carga

La prueba extendida del coordinador borra el estado transitorio BBSIS como ocurre al cargar y consulta el lease de la plaza. Antes de corregirlo, `BarSpatialAdapter.HasCustomerLease` respondía por un string cacheado aunque BBSIS ya no conservase ese lease; impedía reconocer la ausencia y reconciliar correctamente. `bar-cached-lease-before-fix.log` termina en exit 1 con esa aserción.

Corrección en las autoridades existentes: BBSIS expone la consulta de lease activo con su limpieza de expiración; el adaptador consulta esa verdad y permite la adquisición normal cuando el ID cacheado ya no existe. No se crea otro registro ni se renueva una reserva al consultar. La reprueba final demuestra reset, ausencia real, nueva concesión automática y liberación/cleanup. El primer runner posterior descubrió una limitación del fixture: su raíz `HideAndDontSave` no era redescubierta por el rebuild de carga. Se corrigió únicamente ese fixture a una raíz descubrible como las instancias reales; no se relajaron las aserciones ni se alteró el rebuild para aceptar sujetos invisibles.

## 76. Cierre verificable de este avance y continuidad

`bar-seat-automatic-persistence-verified-regression.log` termina con **return code 0**: **Closure Gate 23/23**, core **84/84**, Navigation **22/22**, servicio de barra **59/59** y BBSIS operacional fase 2B **18/18 PASS**. Gate completado **2026-10-02 23:49:46 UTC**; aceptación actual de la barra publicada comprobada nuevamente contra fuente, plan, perfil, prefab e informe.

Auditoría canónica fresca **2026-10-02 23:49:51 UTC**: **27 únicos, 14 publicados, 13 catálogo placeables, 4 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original** fuera del lote solicitado. Selected14 sigue en **10 publicados + 4 revisiones**. No se descartan taburetes ni campana para reducir el contador; SAVIC permanece abierto.

Pendientes mínimos, en orden:

1. Representación sentada real mediante Animation y los clientes canónicos: modelo Humanoid certificado/recetas existentes, sit/idle/stand y adaptación del SeatFrame visual, manteniendo el root lógico de Navigation en suelo. El placeholder de cápsulas y la mera asociación no demuestran un cliente sentado.
2. Familia y plan común BarStool recomputados desde source/metadata/perfil; candidatos con aceptación vinculada a fuente/plan/prefab actual. Conservar revisión hasta demostrar creación desde catálogo, asociación, ocupación/reservas, rutas, Animation, **SaveGame real en Play Mode**, carga repetida, nueva instancia, cleanup y Console limpia. La asociación automática debe admitir solo destinos reconstruibles por el contrato de persistencia habilitado.
3. Campana con cara funcional comprobada y estrato espacial elevado; subir la malla no resuelve conflictos XZ. Sin simulación de extracción D-003.
4. Revisar textos generados antiguos de Lighting/Furniture conservando autoría manual.

No queda un proceso Unity de este corte ejecutando la tarea. La continuidad se conserva en documentos y automatización; el cierre positivo requiere una nueva auditoría cero NEEDS_REVIEW/cero FAILED y aceptación pertinente, todavía pendiente.

## 77. Clientes Humanoid reales y representación de asiento de barra

La ampliación funcional autorizada integra el modelo Humanoid certificado `UAL1_Standard.fbx` mediante `BistroBuilderCustomerHumanoidProfile` y el perfil canónico `CustomerHumanoidProfile_Standard.asset`. `CustomerGroupPrefab.prefab` referencia este perfil y el grupo visual crea un miembro real por cliente; conserva sus hit targets. El bootstrap de Animation omite el actor de raíz cuando existen estos presentadores, evitando dos controladores sobre el mismo Animator. El fallback anterior de cápsulas solo permanece para grupos sin perfil, no demuestra aceptación sentada.

`BistroBuilderCustomerBarSeatPresenter` consulta ocupación nativa, asiento asociado, llegada real de `CustomerMovementView` y lease activo BBSIS. Usa las recetas existentes sit/idle/stand del servicio Animation V1. Alinea únicamente la representación y la pelvis al SeatFrame más un offset común autorado de 0,10 m; no mueve el root lógico, concede leases ni cambia ocupación. Cancelación/rehidratación destruyen sus propios recursos y sesiones. La orientación de Blue sigue derivada del respaldo; Black/Silver mantienen la convención explícita de frente. El perfil representa un Humanoid funcional básico, no una mejora artística de personajes.

Pruebas reales sobre los tres sources y luego sobre el prefab canónico: Navigation llega al approach de suelo antes de sentarse, lease/ocupación nativos, rodillas dobladas (aproximadamente 95°/96°), pelvis dentro de 0,025 m del datum, root lógico intacto, salida stand/idle, liberación y materiales sin error. `customer-bar-seat-visual-prototype.log` y `customer-bar-seat-canonical-visual.log` terminan con exit 0. Capturas `customer-seated-<SavicId>.png` muestran el Humanoid sentado; inspeccionadas Blue y Silver. La representación basal usa idle; no se afirma animación walk nueva ni integración de asientos de mesa.

## 78. Familia BarStool, aceptación real y publicación canónica

`SavicBarStoolFamilyModule` recomputa el plan físico desde source/metadata/perfil y genera un plan común Seating. El adaptador `NATIVE_BAR_SEAT` 1.1 exige una barra dinámica persistible como destino de su asociación. No crea plazas ni capacidad adicional. El publicador prepara candidatos con motivo `BAR_STOOL_RUNTIME_ACCEPTANCE_PENDING`; la aceptación funcional común exige fuente/plan/prefab, dependencias del cliente/Animation, SHA del informe y todas las verificaciones de creación/asociación/ruta/lease/Animation/SaveGame repetido/cleanup/Console. Las pruebas negativas rechazan 45 variantes de proof incompleto u obsoleto entre los tres assets.

La aceptación candidata real pasó para los tres taburetes: creación canónica en Prototype, asociación automática, cliente del prefab canónico, ocupación y lease nativos, movimiento y sit/idle/stand, dos cargas SaveGame por asset con mismos ItemId/InstanceId e ID de plaza y nuevas instancias Unity, reubicación de un nuevo cliente después de cada carga, slot diagnóstico eliminado y Console limpia hasta Editor. `bar-stool-candidate-current-description-acceptance.log`, exit 0. Se corrigió el texto generado Seating que aún describía decoración antes de esta última aceptación; se conserva texto manual.

Una prueba inicial rechazó correctamente el approach del taburete fuera del área funcional al colocar la barra cerca del borde (`bar-stool-candidate-real-savegame-first.log`, exit 1). Se cambió la búsqueda de poses del fixture para probar posiciones centrales; no se relajaron áreas, obstáculos ni validadores. El layout temporal retira mobiliario de mesa mediante lifecycle y no guarda la escena. Los flujos de llegada/comedor/salida ajenos se aíslan en la prueba; ocupación se ejerce mediante el registro nativo. Las instantáneas SaveGame están **sin ocupante**: no se afirma recuperación de una sesión de servicio activa ni una jornada completa de IA.

Reconciliación verifica archive SHA y proof actual, encola una vez por fingerprint y vuelve al procesamiento normal; nunca marca Done directamente. Bootstrap integra este reencolado de taburetes. `bar-stool-canonical-final-publication.log`, exit 0: los tres candidatos llegan a PUBLISHED y catálogo mediante la transacción canónica. Auditoría de **03/10/2026 01:03:05 UTC**: **27 únicos, 17 publicados, 16 catálogo placeables, 1 NEEDS_REVIEW, 0 FAILED, 0 inbox, 9 históricos sin original** fuera del lote; selected14 = **13 publicados + 1 revisión**. La única revisión restante es Commercial Kitchen Exhaust Hood.

Reprueba posterior de los tres publicados en Play Mode, sin catálogo candidato: `bar-stool-main-catalog-runtime-acceptance.log`, exit 0, informe **01:08:18 UTC**, dos cargas por asset, nuevos objetos/IDs estables/links nativos, clientes sentados y Console limpia. Se prepara una reprueba estricta adicional que exige igualdad de cada definición resuelta por MainCatalog y SaveDefinitionCatalog y elimina reconfiguración del binding en el fixture. SAVIC no está cerrado mientras quede la campana en revisión.

## 79. Reprueba estricta de los tres publicados

`bar-stool-main-catalog-strict-native-acceptance.log` termina con **UnityActualExitCode=0**, informe **03/10/2026 01:14:31 UTC**. La prueba exige que la definición de catálogo sea el asset Main y que MainCatalog y SaveDefinitionCatalog resuelvan exactamente cada ItemDefinition publicado. Usa el binding tal como lo activa la creación canónica, sin reconfigurarlo en el fixture. Los tres assets pasan llegada/ocupación/lease, cliente Humanoid sentado y stand, **seis cargas SaveGame reales en total**, IDs/links estables con objetos nuevos, slot eliminado, limpieza y Console sin Error/Exception/Assert hasta Editor. Capturas finales de Blue, Black y Silver inspeccionadas, sin material de error. Los proofs se guardan solamente al completar toda la prueba; siguen vinculados a dependencias/informe actuales.

Pendiente único del lote: **Commercial Kitchen Exhaust Hood**, SavicId `f6a9de165fed4865abe5f5348b9265b7`, fuente SHA-256 `1fb44487930489b7c936c54b13682d05e13dbca049ab1f683ad4704fd3ef9177`. Original y mirror siguen disponibles y el título Meshy ya está verificado; no requiere pedir nombres ni buscar assets antiguos. El preview actual enseña principalmente la cubierta posterior/superior. El siguiente paso debe auditar cara inferior/frontal sobre el source real y diseñar un contrato espacial elevado común antes de autorar/publicar. `RestaurantPlacementShape` y `BistroBuilderSpatialVolume` actuales resuelven XZ, y Navigation proyecta los cuerpos estáticos: una malla subida no constituye un estrato elevado. La ampliación debe preservar un fallback conservador para geometría o alturas desconocidas, comprobar conflictos a igual altura/solapes reales, paso por debajo y persistencia/Console, sin simular extracción D-003.

## 80. Regresión final y continuidad guardada

`bar-stool-published-final-verified-regression.log` termina con **UnityActualExitCode=0**: **Closure Gate 24/24, core 84/84, Navigation 22/22, barra 59/59, BBSIS operacional 2B 18/18 y Animation V1 13/13 PASS**; clientes 10G PASS. Gate terminado **03/10/2026 01:17:33 UTC**. Comprueba nuevamente los proofs actuales de barra/taburetes publicados, negativos de aceptación y cola idempotente sin saltar procesamiento. Auditoría fresca **01:17:39 UTC**: **27 únicos, 17 publicados, 16 catálogo placeables, 1 NEEDS_REVIEW, 0 FAILED, 0 inbox, 9 históricos sin original** fuera del lote; selected14 = **13 publicados + 1 revisión**. No queda Unity de este corte ejecutando la tarea.

La ruta de reutilización del publicador ahora actualiza solamente descripciones generadas reconocibles. Reparación transaccional de los textos antiguos de FloorLamp/StorageFurniture: **2 corregidos, segunda aplicación 0**, mismos archivos físicos de prefab, ItemId y categoría; texto manual preservado. No se modifica funcionalidad ni estado de publicación para esta reparación.

Entre 01:17 y el siguiente despertar, el control automático de aprobación no pudo ejecutar una actualización documental por límite de uso de la cuenta; no fue rechazo de seguridad. No se intentó eludirlo. La comprobación posterior confirmó uso ordinario disponible y se reanudó la documentación. Los tests Unity ya lanzados terminaron correctamente durante esa espera. La campana sigue en revisión por el contrato elevado pendiente de la sección 79; el objetivo cero/cero permanece abierto.

## 81. Cara inferior real y causa del bloqueo elevado

Auditoría nativa del original de Commercial Kitchen Exhaust Hood: archive y mirror vuelven a coincidir por SHA-256; cinco vistas en escena de preview aislada, sin cambiar manifiesto ni estado. `hood-native-source-views-verified.log` termina con UnityActualExitCode=0. La vista Bottom muestra una banda de filtros/láminas inclinadas y una cavidad inferior abierta; Front (cámara en −Z) muestra la fascia lisa y Back (cámara en +Z) muestra la banda inclinada de filtros. Las cinco vistas fueron inspeccionadas; esos nombres describen ejes de cámara, no una orientación original inferida del título. Esta geometría es compatible con el título Meshy verificado de campana, pero no demuestra extracción ni ventilación. El primer render inferior con fondo claro no superó el umbral de contraste; se iluminó desde la vista y se usó fondo oscuro exclusivamente en el diagnóstico, conservando el gate visual.

Causa raíz pendiente demostrada en código: SpatialVolume.Overlaps y PlacementCollisionUtility proyectan siempre a XZ; PhysicalFootprintAdapter tampoco transporta espesor Y y Navigation prueba únicamente la huella. No existe un intervalo vertical físico compartido. Elevar Visual no modifica ninguno de esos bloqueos. Fuente, mirror, metadata y geometría siguen disponibles; la revisión funcional es correcta y permanece intacta.

Plan mínimo antes de publicar: intervalo vertical opcional en las primitivas BBSIS y de colocación, conservando geometría legacy/altura desconocida como columna conservadora. Las cajas estáticas autoradas transportan el intervalo desde su proxy, con pose horizontal, escala positiva, valores finitos y candidato sin mutar Transform. Un intervalo inválido nunca libera espacio. Los conflictos usan XZ y altura; límites de área conservan la envolvente XZ. Navigation prueba el mismo cuerpo contra una envolvente humana de altura explícita en su configuración, incluyendo el segmento final y paso inferior; no borra obstáculos elevados de la topología. Las consultas/claims antiguos sin altura siguen conservadores, sin inventar altura de mesas, leases o paredes existentes. Probar solape parcial/igual altura, separación, altura desconocida o inválida, escala/rotación/traslación, límites y ruta humana bajo/sobre cuerpo antes de usarlo en una familia.

Después, perfil de campana explícito (dimensiones y altura de instalación autoradas, no unidades físicas descubiertas del GLB), cuerpo elevado y lifecycle canónicos, clasificación/familia/candidato transaccionales y aceptación real de colocación, paso inferior, bloqueo por cuerpo, SaveGame repetido y Console limpia. No crear simulación de extracción D-003 ni afirmar anclaje a un techo que el proyecto no representa. Publicación y cero revisiones quedan pendientes de esa evidencia.

## 82. Prueba nativa del intervalo vertical común

Implementado el intervalo opcional en SpatialVolume y PlacementShape, y el espesor autorado en SpatialProxyPart. El puente físico transporta centro/altura desde la misma pieza, manteniendo límites XZ por envolvente. Ausencia, NaN o intervalo inválido conservan bloqueo; poses inclinadas o escalas inválidas se rechazan por la adaptación. Colocación usa intersección vertical y separación mínima; Navigation conserva el cuerpo en topología y compara planner/validación estructural/solver local con altura humana explícita de 2 m.

`elevated-native-spatial-second.log` demostró una regresión: al invalidar la altura de una caja pequeña, el respaldo planar completo caía dentro de la tolerancia de endpoint y Navigation aún devolvía ruta hasta su interior. Corrección en la autoridad: clearance completo para cuerpo acotado y envolvente de adaptación inválida, sin alterar endpoints de objetos legacy. La primera compilación del runner también detectó variables out tras short-circuit sin inicializar; corregida su inicialización, sin cambiar aserciones.

Prueba nativa posterior: colocación inferior permitida y solape físico a igual/parte de altura rechazado, límites de área conservados, pose candidata escalada/rotada/trasladada sin mutar root, BBSIS concede/libera claim inferior acotado y rechaza cabeza dentro del cuerpo; ruta GridFallback de **8,000 m**, humana 0–2 m, muestreada cada 5 cm y pasando por debajo del centro; baja altura/endpoint y altura inválida rechazados. El candidato inválido falla de forma segura y el existente sigue bloqueando. Esta es evidencia de autoridades nativas sobre un fixture, no publicación ni SaveGame de la campana real.

La ejecución integrada `elevated-native-spatial-verified.log` pasó esas pruebas, gate **25/25**, core/Navigation/barra/fase 2B, pero terminó **exit 1** al comprobar proofs publicados desactualizados por el cambio de dependencia del proxy. No se declara ese run como cierre. La barra fue reaceptada desde MainCatalog real con SaveGame y Console limpia (`elevated-foundation-published-bar-runtime.log`, exit 0). El gate estricto de taburetes detectó también sus dependencias anteriores (`elevated-foundation-published-stools-runtime.log`, exit 1).

Se conserva `Matches` en la publicación y en la reprueba estricta de catálogo principal. Se añadió una etapa explícita de revalidación candidata de publicados que exige fuente/plan/perfil/informe/cliente/Animation intactos y autoría física actual; ejecuta de nuevo todos los asserts runtime y solo escribe proof tras éxito completo. No cambia jobs, catálogo ni estados. Una propuesta de retirar el match previo del gate estricto fue rechazada por la revisión automática y no se aplicó. La alternativa mantiene el gate y vuelve a demostrar primero aceptación candidata, después catálogo principal.

`elevated-foundation-stool-candidate-revalidation.log` terminó exit 0 e informe **04:53:03 UTC** después de probar las tres fuentes y seis cargas reales, pero un filtro del runner seguía buscando NEEDS_REVIEW al volver al Editor y no escribió ningún proof PUBLISHED. Por tanto, ese exit 0 no demostró la recertificación: la posterior reprueba estricta lo rechazó. El filtro ahora conserva el tipo de sesión y exige exactamente tres proofs completos; también elimina los proofs temporales de sesiones anteriores al entrar en Play Mode. La ejecución corregida `elevated-foundation-stool-candidate-revalidation-complete.log` terminó **exit 0**, con los tres proofs escritos **05:02:01 UTC**, dependencias actuales, seis cargas reales, clientes sentados, IDs/links estables, slot borrado y Console 0 hasta Editor. La reprueba estricta final y auditoría fresca se documentan después de su ejecución.

## 83. Cierre verificado de la base elevada y continuidad de la campana

Auditoría fresca **03/10/2026 05:13:42 UTC**: **27 únicos, 17 publicados, 16 catálogo placeables, 1 NEEDS_REVIEW, 0 FAILED, 0 inbox**, nueve históricos sin original fuera del lote solicitado. Selected14 conserva **13 publicados y una revisión**. La campana `f6a9de165fed4865abe5f5348b9265b7` mantiene su revisión funcional; no se alteró su job, clasificación ni estado para cerrar los contadores.

`elevated-spatial-final-verified-regression.log` termina **UnityActualExitCode=0**: contrato vertical nativo, **gate 25/25**, core **84/84**, Navigation **22/22**, servicio de barra **59/59**, BBSIS operativo 2B **18/18**. Todos los proofs funcionales publicados coinciden de nuevo con sus dependencias actuales. El preflight nativo también prueba cuerpo candidato y semántica candidata trasladados en Y, rotación/traslación XZ sin mutar el Transform, espacio operativo inferior explícito permitido y altura operativa desconocida bloqueada. El fixture no asigna alturas a estaciones existentes.

La barra publicada se reprueba **04:43:58 UTC** con catálogo principal, servicio/leases/rutas y SaveGame reales (`elevated-foundation-published-bar-runtime.log`, exit 0). Los tres taburetes pasan después la reprueba **estricta** MainCatalog y SaveDefinitionCatalog **05:10:21 UTC** (`elevated-foundation-stool-main-catalog-strict-verified.log`, exit 0): llegada Navigation, asociación/ocupación/lease, cliente canónico sentado, dos cargas reales por asset, mismos IDs/plaza/links con nuevas instancias, reocupación y limpieza, slot borrado y Console 0 hasta Editor. Los informes individuales vuelven a decir main catalog; no se usó clon candidato en esa última ejecución. Los checkpoints siguen desocupados; no se afirma recuperación de una sesión de servicio activa ni jornada IA completa.

Pendiente concreto para publicar la campana:

1. Perfil elevado explícito de autoría física y escala uniforme, recomputado desde fuente analizada, SHA-256 y perfil. La altura de instalación será un valor autorado, no una medida original deducida ni un techo descubierto.
2. Binding común de cuerpo estático pasivo sobre PlaceableRegistry/BBSIS, con identidad derivada de InstanceId persistido, provisional excluido, activación transaccional, rollback, rebuild y baja limpia. No reutilizar el servicio de barra para una campana.
3. Mantener el anclaje de colocación en el suelo y la envolvente de límites del área; elevar el cuerpo/malla con espesor común. `RestaurantArea.ContainsPosition` usa colliders 3D de área, no demuestra altura de techo. Resolver suelo mediante la autoridad existente, sin inventar un plano mundial ni serializar servicios de escena.
4. Integrar el caso verificado de campana dentro de KitchenEquipment, capacidad canónica food_production y publicador transaccional, preservando otros equipos y IDs/manuales. D-003 permite aquí integración espacial pasiva; no simular extracción, ventilación ni una estación de cocina.
5. Candidato real antes de catálogo: pruebas nativas de geometría/colocación/leases/desconocidos y Play Mode con ruta por debajo del cuerpo, límites, persistencia repetida/nuevas instancias, cleanup y Console hasta Editor. Publicar por la cola solo con proof actual y después repetir aceptación desde catálogo principal y auditoría.

Las cinco vistas originales ya están inspeccionadas en `Diagnostics/SourceAudit/<SavicId>`: cara inferior abierta con filtros inclinados, fascia −Z, banda de filtros +Z y lateral. Los nombres Front/Back de los archivos identifican ejes de cámara; no certifican el frente original del proveedor. Fuente y mirror siguen disponibles y verificados; no hace falta recuperación Git/stash ni buscar los antiguos assets faltantes. La base vertical está probada; la campana todavía no tiene perfil, familia/adaptador funcional ni aceptación runtime propios.

## 84. Campana elevada: autoría y lifecycle pasivo canónicos

El bloqueo funcional de la campana se resuelve dentro de KitchenEquipment, sin registrar un TypeId duplicado. El módulo especializado recomputa el plan desde original analizado, SHA-256, metadata verificada y `OverheadEquipmentProfile_Standard.asset`. El perfil autorado fija anchura **2,00 m** y cara inferior instalada **2,20 m** sobre el anclaje de suelo; escala uniforme y cuerpo final **2,0000 × 0,5169 × 0,9811 m**. Son decisiones explícitas de autoría, no unidades originales aprobadas ni una altura de techo descubierta. El plan común requiere la capacidad real `food_production` y mantiene el anclaje/envolvente de área en suelo.

`PASSIVE_OVERHEAD_BODY` crea un cuerpo físico elevado y su proxy BBSIS con el mismo espesor vertical, conservando geometría de colocación/Navigation compartida. `BistroBuilderPassiveBodySpatialBinding` participa en la transacción de PlaceableRegistry: identidad derivada del InstanceId persistido, activación confirmada antes de ser elegible, provisional excluido de registro/rebuild/topología, rollback de altas parciales, repetición y limpieza. Consulta leases activos en BBSIS para rechazar activación o retirada que invadan un derecho vigente; no introduce otro registro de reservas. Los servicios de escena son caches runtime y no se serializan en el prefab. La campana es equipamiento espacial **pasivo**: no simula extracción, ventilación ni una estación de producción D-003.

El primer probe real falló porque el GUID del contrato nuevo tenía 33 caracteres y Unity no cargaba el asset; evidencia conservada en `overhead-real-authoring-native-first.log`, exit 1. Se corrigió el GUID a 32 caracteres y se repitió la prueba completa: `overhead-real-authoring-native-second.log`, exit 0. Original y mirror SHA verificados, planes repetibles, bounds Renderer/minY reales, aplicación doble, aislamiento provisional/rebuild, rollback por lease, baja/reactivación y limpieza. Ruta nativa GridFallback **6,000 m**, muestreada cada **5 cm**, agente de radio **0,28 m** y altura **2 m**, bajo el cuerpo; lease inferior conocido permitido, penetración de cabeza y altura desconocida rechazadas. La prueba usa clones en memoria y comprueba que el manifiesto de fuente queda intacto.

## 85. Lote solicitado cerrado con aceptación real y auditoría cero/cero

La cola canónica prepara primero un candidato en revisión `OVERHEAD_RUNTIME_ACCEPTANCE_PENDING`. La aceptación exige fuente/plan/perfil/prefab actuales, SHA del informe y creación, aislamiento provisional, binding, capacidad de área, ruta, leases, dos cargas SaveGame, cleanup y Console limpia hasta volver al Editor. Reconciliación verifica esa evidencia y reencola una sola vez por fingerprint; la publicación usa la transacción existente, sin marcar Done artificialmente. Se comprobaron **18 variantes negativas** de proof incompleto/obsoleto y alteración de altura física; estados de duplicados e históricos se preservan.

`overhead-candidate-runtime-first.log`, exit 0, aceptación **03/10/2026 05:47:30 UTC**: colocación en área de cocina real, cuerpo/claims/ruta nativos, dos cargas SaveGame con ItemId/InstanceId estables y nuevas instancias Unity, limpieza canónica, slot eliminado y Console sin Error/Exception/Assert hasta Editor. `overhead-canonical-publication-verified.log`, exit 0: publicación posterior por la cola. Reprueba **estricta desde MainCatalog y SaveDefinitionCatalog reales**, sin clon candidato: `overhead-main-catalog-strict-runtime-verified.log`, exit 0, aceptación **05:53:31 UTC**, todas las verificaciones anteriores y dos cargas aprobadas. Informe `savic-overhead-candidate-f6a9de165fed4865abe5f5348b9265b7-runtime-playtest.txt` identifica expresamente Actual MainCatalog; su nombre histórico no cambia la autoridad comprobada. ContentId publicado: `bb_kitchenequipment_f6a9de165fed4865abe5f5348b9265b7`.

Regresión final `overhead-final-canonical-verified-regression.log`, **UnityActualExitCode=0**: probe de fuente real repetido con el adaptador final, aceptación/cola idempotentes, **Closure Gate 26/26**, core **84/84**, Navigation **22/22**, barra **59/59**, BBSIS operacional 2B **18/18 PASS** y proofs actuales de todos los publicados funcionales comprobados. No se presentan Animation 13/13 y clientes 10G del corte anterior como pruebas repetidas en esta ejecución; la aceptación estricta de taburetes con Animation/SaveGame real sigue vigente de 05:10:21 UTC.

Auditoría fresca **03/10/2026 05:57:01 UTC**, `canonical-content-inventory.json`: **27 únicos, 18 publicados, 17 catálogo placeables, 0 NEEDS_REVIEW, 0 FAILED, 0 inbox y 9 históricos sin original**. El lote seleccionado de **14 GLB queda publicado completo**, sin revisión ni fallo; las nueve entradas históricas permanecen fuera del lote por instrucción del usuario, conservadas y sin publicación fabricada. La diferencia entre 18 publicados y 17 placeables corresponde a la definición de construcción registrada por su autoridad propia.

Este cierre certifica el lote y sus contratos probados. No afirma reconocimiento universal de cualquier asset futuro, recuperación de servicio ocupado al cargar, jornada completa de IA, techo inferido ni extracción simulada. La automatización de completar este lote deja de ser necesaria al verificar este cierre.

## 86. Integración comprobada con presentación

El usuario solicita combinar SAVIC con `feature/bb-presentation-interaction-quality-v1` desde `b595fd99`. Fuente funcional guardada en `ef1fcbb7`. La copia nueva `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation` pasa las aceptaciones estrictas de barra, tres taburetes y campana con MainCatalog/SaveDefinition, navegación/leases y SaveGame reales. Auditoría **03/10/2026 16:33:56 UTC: 18 únicos, 18 publicados, 17 catálogo placeables, cero revisiones y fallidos**; los nueve jobs históricos sin original permanecen en la carpeta fuente. Regresión final exit 0: gate **26/26**, core **84/84**, Navigation **22/22**, barra **59/59** y BBSIS 2B **18/18**.

El checkout conserva ahora los 18 SourceMirror GLB vía LFS y los bytes de siete evidencias ProviderMetadata; se corrigieron pérdidas de módulos de pared y cambios de hash introducidos al combinar ramas. Los gates estrictos `Matches` se mantienen: dependencias modificadas requieren aceptación candidata real antes del catálogo principal. Evidencia y fallos anteriores conservados en [informe de integración](40_TESTING/SAVIC_PRESENTATION_INTEGRATION_2026-10-03.md). La prueba responsive detecta diferencia de altura entre barras, cuyo código procede de la base de presentación; no se declara cerrado el gate visual ni se modifica la estética aprobada como parte de este merge.

Actualización de destino incorporada: `0172c0fb`, cuatro iconos aprobados de Carta. Prueba nativa Carta **26 PASS / 0 FAIL** y regresión canónica final repetida exit0 con gate26/core84/Navigation22/barra59/BBSIS2B18. Auditoría definitiva **03/10/2026 16:39:39 UTC: 18 publicados, 17 catálogo placeables, 0 NEEDS_REVIEW y 0 FAILED**. La copia comprobada conserva el historial de la base y del nuevo commit de presentación.

## 87. Operación normal desde la ventana de SAVIC — 05/10/2026

Causa demostrada del pendiente de Editor: la ventana presentaba las fichas, pero no ofrecía importar una carpeta, reintentar un asset, verificar un candidato funcional o sustituir explícitamente su original. La identidad de ingesta se deduplicaba por SHA; otro contenido se trataba como otro asset. Tampoco había una transacción persistente para conservar la publicación anterior durante una revisión de fuente. El lote cerrado y sus pruebas de runtime no resolvían este flujo del usuario.

`Tools > Bistro Builder > SAVIC > Open Control Center` incorpora **Importar carpeta GLB** y, en la ficha, **Revalidar asset / Reintentar procesamiento**, **Verificar funcionamiento**, **Actualizar original** y **Adjuntar original**. Las acciones usan Intake, JobStore, SourceProcessing, reconciliación y publicadores existentes. Un reintento conserva su historial y entra en la cola; no concede PUBLISHED ni elimina fallos sin procesar. Las incidencias superadas por un trabajo posterior quedan en Historial y dejan de contarse como fallos activos. Los errores sin identidad siguen visibles.

La verificación selecciona una identidad exacta y reutiliza los verificadores reales de barra, taburete o campana. Conserva SHA/plan/prefab, BBSIS/Navigation/Animation, SaveGame, cleanup y Console. Una sesión de Editor guarda las escenas abiertas, pausa la cola y restaura ambas al terminar; exige guardar las escenas antes de empezar. No crea ni reconfigura el perfil Humanoid durante una verificación interactiva. Un publicado se comprueba estrictamente desde MainCatalog; un candidato solo puede publicarse después de su aceptación actual.

**Actualizar original** archiva bytes nuevos verificados, mantiene SavicId/ContentId y añade la fuente anterior a `sourceRevisions`. La revisión comienza como INGESTED: unos bytes nuevos no heredan la aceptación de la publicación anterior. Una transacción `SAVIC/SourceUpdates` conserva manifiesto, cola, catálogo, publicación y sus informes, con backups SHA en la caché del proyecto. La autoría se ejecuta en una escena temporal aislada; no ensucia la escena guardada del usuario. GLB y FBX autocontenidos son los formatos de esta acción; GLTF con sidecars se rechaza antes de modificar la publicación. Un cambio de función, error de importación o aceptación fallida restaura la versión válida anterior y conserva la propuesta y su diagnóstico. Un cierre con PREPARING/VERIFYING se recupera antes del batch al volver a abrir Unity.

Pruebas reales en Unity 6000.3.19f1, worktree aislado de revisión:

- Botones nativos activados mediante eventos de UI Toolkit: importación, pestaña Cola/reanudación, reintento y actualización. Se comprueban publicación, GUIDs estables, precio manual, entrada única de catálogo, bytes externos conservados, duplicado de revisión antigua y rollback de un GLB malformado. La fuente de ensayo es un GLB real con un marcador JSON que cambia su SHA sin inventar geometría.
- Tres aperturas reales del Editor: un job reclamado se recupera; una segunda revisión real se prepara y se simula la ausencia de su último marcador COMMITTED; el siguiente proceso restaura fuente/publicación/GUID/precio y conserva la propuesta. La interrupción del marcador es inyectada explícitamente en la prueba.
- Verificación individual desde el botón para barra, taburete y campana: runtime nativo, SaveGame y cleanup aprobados; escena guardada y pausa previa restauradas. El taburete prueba un cliente Humanoid sentado y dos cargas; la campana, dos cargas y paso inferior en cocina real.
- Revisión funcional de la barra desde **Actualizar original**: fuente nueva → candidato y aceptación real → publicación por la cola → segunda aceptación estricta del MainCatalog. Se conservan identidad y GUIDs. La prueba detectó que heredar PUBLISHED al cambiar la fuente restauraba el manifiesto anterior en vez de continuar la aceptación; el ciclo INGESTED corrige esa causa.
- **Gate 27/27 PASS**, ahora incluye Editor UX. Auditoría **05/10/2026 11:54:49 UTC: 18 únicos, 18 publicados, 17 catálogo placeables, 0 NEEDS_REVIEW, 0 FAILED, 0 inbox, 0 huérfanos**; cola vacía y proofs funcionales actuales. Todos los procesos finales anteriores terminaron con exit 0.

Evidencia y alcance: [operaciones de Editor](40_TESTING/SAVIC_EDITOR_OPERATIONS_2026-10-05.md). Esta entrega cubre SAVIC dentro de Unity y el lote existente; la conexión con Assets4ALL sigue siendo un trabajo separado. No certifica clasificación universal, GLTF dependiente de archivos externos ni carga de servicio ocupado.