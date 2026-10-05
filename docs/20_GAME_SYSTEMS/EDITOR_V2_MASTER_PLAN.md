# Bistro Builder — Editor V2 Master Plan

**Estado:** PLAN CANÓNICO APROBADO · B0 BASELINE PASS · GATE ASSETS4ALL → SAVIC VALIDANDO
**Fecha:** 2026-10-05  
**Sistema:** Bloque 18 — Modo Edición / Construcción  
**Naturaleza:** evolución controlada del editor existente; no reescritura total  
**Interacción vinculante:** [Edit Interaction Design](../ModoEdicion/EditInteractionDesign.md)  
**Criterios de aceptación:** [Acceptance and Validation](../40_TESTING/ACCEPTANCE_AND_VALIDATION.md)

## 0. Resumen en cristiano

Editor V2 no significa tirar el modo edición actual y empezar otra vez.

Bistro Builder ya dispone de piezas valiosas y funcionales: colocación de mobiliario, validación, snapping, Construction Authoring, Draft/Baseline, Undo/Redo, Save/Load, BBSIS, Navigation, Finance y Universal Preview. Editor V2 debe conseguir que todas esas piezas se comporten para el jugador como un único editor profesional.

La idea es sencilla:

**conservar los motores que ya hacen bien su trabajo, crear una coordinación común por encima y sustituir únicamente las partes cuya limitación haya sido demostrada.**

El desarrollo se hará bloque a bloque. Ningún bloque autoriza avanzar al siguiente si no supera su gate. Una implementación nueva nunca sustituirá la antigua hasta demostrar que mantiene o mejora el comportamiento existente sin romper persistencia, economía, espacialidad, navegación ni rendimiento.

## 1. Objetivo de producto

Editor V2 debe permitir que el jugador construya y reforme el restaurante con libertad, precisión y seguridad sin tener que entender los subsistemas internos.

Al terminar V2, el jugador debe poder:

- entrar en Modo Edición únicamente fuera de servicio;
- seleccionar de forma coherente mobiliario y arquitectura;
- coger, mover, rotar, colocar, duplicar, sustituir y retirar elementos;
- construir habitaciones, paredes, puertas, ventanas y superficies;
- recibir snapping y ayudas contextuales sin perder libertad;
- seleccionar y manipular varios elementos cuando corresponda;
- probar una reforma sin comprometer inmediatamente el restaurante confirmado;
- deshacer y rehacer acciones en el orden real en que se realizaron;
- aplicar o descartar una reforma completa;
- entender por qué una colocación es válida, problemática o imposible;
- diagnosticar problemas funcionales del layout;
- guardar, cargar y continuar editando sin divergencias;
- trabajar con catálogos grandes alimentados por SAVIC sin tirones injustificados.

El objetivo no es convertir Bistro Builder en un CAD. El objetivo es que construir un restaurante funcional resulte natural.

## 2. Regla principal de arquitectura

**Este documento no autoriza una reescritura completa del modo edición.**

Editor V2 debe evolucionar las autoridades y servicios existentes. Solo podrá sustituirse un componente cuando exista evidencia de que:

1. impide el comportamiento aprobado;
2. genera duplicidad de autoridad;
3. crea fragilidad o deuda que no puede resolverse limpiamente;
4. o incumple rendimiento, persistencia o seguridad de estado.

No se reimplementará una capacidad simplemente porque resulte más cómodo escribirla de nuevo.

## 3. Autoridades que se conservan

| Responsabilidad | Autoridad que se conserva | Regla Editor V2 |
|---|---|---|
| Viabilidad espacial | BBSIS | Editor V2 consulta y representa; no decide por su cuenta |
| Rutas y circulación | Navigation & Crowd Flow | Editor V2 invalida/solicita; Navigation calcula |
| Derechos lógicos de uso | Interaction & Reservation | No duplicar claims ni reservas |
| Caja, costes y ledger | Finance | Toda reforma económica pasa por Finance |
| Estructura confirmada | Edit/Structure domain | UI nunca se convierte en fuente de verdad |
| Mobiliario colocado | Placement/Lifecycle existentes | Evolucionar mediante adaptadores/coordinación |
| Persistencia | Save/Load canónico | No crear segundo formato de partida |
| Contenido publicado | SAVIC + definiciones canónicas | Editor consume datos; no analiza meshes para suplir metadatos |
| Preview provisional | BB Universal Preview | Un único lenguaje visual provisional |
| Cámara | 369A/369B/369C | Ampliar contextos; no crear controlador paralelo |

## 4. Qué existe y debe aprovecharse

La auditoría estática realizada antes de este plan confirma que el proyecto ya contiene, entre otras piezas:

- RestaurantPlacementTransactionService para comenzar, previsualizar, confirmar y cancelar movimientos;
- RestaurantPlacementValidationService para reglas y conflictos de colocación;
- RestaurantPlacementHistoryService para historial de placeables;
- Construction Authoring con Draft/Baseline y comandos propios;
- BistroBuilderEditRuntimeCoordinator y BistroBuilderEditSession para sesiones estructurales;
- BistroBuilderEditCommitCoordinator para commit económico/estructural;
- integración incremental de BBSIS durante edición;
- integración diferida de Navigation;
- proveedores de Save/Load de estructura y arquitectura;
- BB Universal Preview System;
- grupos vinculados de colocación;
- catálogo data-driven de placeables.

Por tanto, el punto de partida es una arquitectura existente que necesita coordinación y hardening, no un editor vacío.

## 5. Hallazgos que deben verificarse antes de ampliar

La auditoría estática detectó riesgos concretos. No se declaran bugs reproducidos hasta verificarlos dinámicamente.

### 5.1 Historial dividido

Mobiliario y arquitectura disponen de mecanismos de historial diferentes y la UI actual dirige Undo/Redo a uno u otro según la herramienta activa.

**Riesgo:** que el jugador no pueda deshacer una secuencia mixta siguiendo el orden real de acciones.

### 5.2 Reconstrucciones durante Load

La sustitución del documento arquitectónico publica un evento de cambio y el proveedor de persistencia también puede solicitar materialización directa.

**Riesgo:** trabajo duplicado durante carga.

### 5.3 Secuencia de publicación y economía

El commit estructural y la finalización económica tienen fases diferenciadas.

**Riesgo:** un fallo excepcional después de publicar estructura pero antes de finalizar Finance debe tener recuperación demostrable y nunca dejar estados divergentes.

### 5.4 Escalabilidad del catálogo

El catálogo actual reconstruye vistas mediante creación y destrucción de tarjetas.

**Riesgo:** comportamiento insuficiente cuando SAVIC alimente cientos o miles de recursos.

### 5.5 Preview de mobiliario y física

La colocación provisional actual puede mover el Transform runtime del candidato y sincronizar física cuando cambia la pose.

**Riesgo:** coste innecesario o acoplamiento de preview y estado runtime en escenas densas. Debe perfilarse antes de sustituirlo.

## 6. Arquitectura objetivo

Flujo conceptual:

Jugador / UI  
→ Editor V2 Coordinator  
→ Selección común / Historial común / Sesión de reforma / Feedback  
→ Adaptador de Mobiliario | Adaptador de Construcción | Adaptador de Superficies  
→ autoridades existentes: Placement, Edit/Structure, BBSIS, Navigation, Finance, Interaction, Save/Load, SAVIC.

### 6.1 Editor V2 Coordinator

El coordinador será una capa de orquestación, no una nueva autoridad de dominio.

Debe saber:

- si el Modo Edición está activo;
- qué herramienta está activa;
- qué entidad o conjunto está seleccionado;
- qué operación provisional está en curso;
- qué comandos forman el historial global;
- si existe una reforma pendiente;
- qué acciones soporta la selección;
- qué sistema especializado debe ejecutar una orden;
- qué feedback debe presentar la UI.

No debe:

- decidir si una silla cabe sin BBSIS/Placement Validation;
- calcular rutas;
- modificar el ledger;
- inventar contratos de assets;
- serializar un estado alternativo;
- reconstruir gameplay por su cuenta.

## 7. Modelo de estado objetivo

Editor V2 distinguirá como mínimo:

1. **Restaurante confirmado:** estado canónico jugable.
2. **Sesión de reforma:** copia/baseline y cambios pendientes.
3. **Operación provisional:** movimiento, creación o modificación todavía no confirmada dentro de la sesión.
4. **Draft válido o con diagnósticos:** propuesta acumulada.
5. **Review:** validación final antes de aplicar.
6. **Commit:** publicación atómica y coordinada.
7. **Discard:** restauración exacta de la baseline.

La cancelación de una operación provisional no equivale a descartar toda la reforma.

## 8. Orden global de trabajo

La secuencia vinculante será:

### Fase A — Cinturón de seguridad
Ejecutar únicamente **B0 — Baseline y protección**.

### Fase B — Dependencia de contenido
Cerrar la conexión **Assets4All → SAVIC** hasta el gate descrito en la sección 10.

### Fase C — Editor V2
Ejecutar B1 a B16, uno a uno y sin saltos de gates.

No se inicia B1 mientras la Fase B no esté aceptada, salvo una decisión canónica posterior explícita.

## 9. Guion de implementación bloque a bloque

### B0 — Baseline y protección

**En cristiano:** antes de tocar el coche, hacemos fotos, medimos todo y guardamos una llave de vuelta.

Trabajo:

- fijar la integración canónica exacta de partida;
- registrar commit, versión Unity y escena de prueba;
- ejecutar pruebas actuales del Bloque 18, Placement, BBSIS, Navigation y Save/Load;
- capturar métricas de operaciones frecuentes;
- registrar fallos preexistentes;
- crear rama de integración de Editor V2 sin modificar comportamiento.

PASS:

- baseline reproducible;
- resultados archivados;
- ningún cambio funcional introducido;
- punto de retorno inequívoco.

**GATE:** si no podemos distinguir un fallo previo de una regresión nueva, no se continúa.

**Cierre B0 — 2026-10-05: PASS.** Evidencia: [Editor V2 Baseline 2026-10-05](../40_TESTING/EDITOR_V2_BASELINE_20261005.md). La baseline registra dos incidencias preexistentes de rendimiento que no impiden cerrar la fotografía de partida: Load de 5,676 s frente al presupuesto histórico de 5 s y diagnóstico completo de circulación de ~37 s. Ambas quedan como entradas explícitas de hardening; no son regresiones de Editor V2.

### B1 — Cerrar riesgos de la auditoría

**En cristiano:** arreglar primero las grietas del suelo antes de añadir habitaciones.

Trabajo:

- reproducir o descartar cada riesgo de la sección 5 con pruebas instrumentadas;
- reforzar commit/rollback si la secuencia estructura-economía puede divergir;
- eliminar reconstrucciones duplicadas de Load si se demuestran;
- medir Physics.SyncTransforms y preview de mobiliario;
- documentar qué hallazgos eran reales y cuáles no.

PASS:

- cada riesgo tiene veredicto reproducible;
- solo se corrigen problemas demostrados;
- regresión completa del editor actual PASS.

**GATE:** no crear el coordinador sobre una base cuyo commit o Load pueda quedar inconsistente.

### B2 — Editor V2 Coordinator

**En cristiano:** ponemos un jefe común sin despedir a los especialistas.

Trabajo:

- introducir el coordinador;
- registrar adaptadores de mobiliario, construcción y superficies;
- centralizar estado de herramienta y operación;
- mantener las autoridades actuales intactas;
- añadir telemetría/diagnóstico de coordinación.

PASS:

- cambiar entre familias de edición no altera el resultado actual;
- no existe segunda fuente de verdad;
- entrar/salir/cancelar funciona igual o mejor que la baseline.

### B3 — Selección común

**En cristiano:** una silla, una pared o una puerta se seleccionan desde el mismo idioma de interfaz.

Trabajo:

- introducir un modelo EditorSelection;
- resolver capacidades por entidad;
- adaptar mobiliario y arquitectura;
- separar selección de modificación;
- unificar inspector y acciones disponibles.

PASS:

- cada tipo editable expone únicamente acciones válidas;
- seleccionar no modifica estado;
- selección se limpia/restaura correctamente al cambiar de herramienta o salir.

### B4 — Undo/Redo global

**En cristiano:** Ctrl+Z deshace lo último que hiciste, no lo último que hizo el subsistema que casualmente está abierto.

Trabajo:

- coordinar los historiales existentes;
- registrar orden global de comandos;
- soportar comandos compuestos;
- preservar rollback económico;
- definir límites y liberación de recursos del historial.

Prueba mínima:

mover silla → crear pared → cambiar superficie → eliminar mesa → duplicar objeto → Undo completo → Redo completo.

PASS:

- orden exacto;
- mismo fingerprint funcional al volver al origen;
- ningún ID duplicado;
- ledger correcto;
- ninguna referencia huérfana.

**GATE CRÍTICO:** si Undo/Redo mixto no es determinista, no se continúa.

### B5 — Reforma transaccional

**En cristiano:** puedes poner el restaurante patas arriba y pulsar Descartar para volver exactamente al punto de partida.

Trabajo:

- formalizar sesión de reforma común;
- distinguir baseline, draft y operación provisional;
- resumen de operaciones y coste;
- Aplicar cambios;
- Descartar cambios;
- protección al intentar salir con cambios pendientes.

PASS:

- reforma grande + Discard devuelve estructura, mobiliario, dinero y relaciones al estado inicial;
- Apply publica una sola versión coherente;
- un fallo de commit revierte o recupera sin corrupción.

**GATE CRÍTICO:** Discard debe ser exacto.

### B6 — Universal Preview consolidado

**En cristiano:** mover una silla y construir una pared deben parecer partes del mismo juego.

Trabajo:

- todos los adaptadores publican en BB Universal Preview;
- ghost de origen cuando aporte información;
- elevación/asentamiento sutil de mobiliario;
- huella y conflicto localizado;
- snapping visual breve;
- eliminar previews paralelos cuando su sustituto esté probado.

PASS:

- mobiliario y arquitectura comparten lenguaje visual;
- preview nunca hace commit;
- cancelar no deja residuos;
- rendimiento representativo PASS.

### B7 — Snapping contextual

**En cristiano:** el editor entiende dónde tendría sentido colocar algo, pero nunca decide por ti ni se salta las reglas.

Trabajo:

- ampliar el snapping existente mediante providers/perfiles;
- silla ↔ mesa;
- pared/suelo/superficie/techo según contrato;
- puertas/ventanas ↔ host;
- relaciones de conjuntos;
- sugerencia más cercana cuando sea segura y útil.

Regla:

Snapping propone. Placement/BBSIS valida.

PASS:

- ninguna sugerencia invalida autoridad espacial;
- se puede ignorar la sugerencia cuando el diseño lo permita;
- no hay hardcode por asset individual.

### B8 — Multiselección y grupos

**En cristiano:** mesa + cuatro sillas + lámpara pueden moverse como conjunto sin dejar de ser objetos editables.

Trabajo:

- Shift+selección;
- selección por conjunto cuando se apruebe la UX;
- mover, rotar, duplicar y eliminar grupo;
- reutilizar Linked Groups donde corresponda;
- comando compuesto único para historial.

PASS:

- operación grupal atómica;
- Undo/Redo de una sola acción;
- relaciones preservadas;
- elementos siguen pudiendo editarse individualmente.

### B9 — Catálogo escalable

**En cristiano:** tener 1.000 assets no puede convertir el menú en una trituradora de FPS.

Trabajo:

- pooling/reutilización de tarjetas;
- virtualización real del scroll;
- carga lazy de thumbnails;
- búsqueda y filtros;
- favoritos y recientes;
- variantes agrupadas;
- caché e invalidación controlada.

PASS:

- dataset de estrés representativo;
- cambio de categoría/búsqueda sin reconstrucción masiva innecesaria;
- memoria y GC medidos;
- navegación fluida en build Windows objetivo.

### B10 — Sustitución inteligente

**En cristiano:** cambiar esta silla por otra no obliga a recolocar todo desde cero.

Trabajo:

- sustituir selección;
- sustituir selección múltiple;
- opción de sustituir mismo modelo cuando se apruebe;
- preservar pose y relaciones compatibles;
- recalcular si cambian dimensiones/contratos;
- mostrar coste antes de aplicar.

PASS:

- sustitución pasa por las mismas validaciones;
- no conserva relaciones incompatibles a la fuerza;
- Save/Load mantiene el resultado.

### B11 — Diagnóstico del restaurante

**En cristiano:** antes de abrir, el juego puede decirte qué parte de tu distribución va a dar problemas.

Capas opcionales:

- circulación;
- accesibilidad;
- capacidad;
- interacción;
- incidencias de layout.

Trabajo:

- consultar BBSIS/Navigation y demás autoridades;
- representar resultados sin recalcular todo permanentemente;
- localizar problemas;
- ofrecer explicación accionable.

PASS:

- un layout problemático conocido produce diagnóstico correcto;
- diagnóstico no muta el restaurante;
- coste bajo cuando está desactivado.

### B12 — Plantillas y composiciones

**En cristiano:** guardar “mesa con cuatro sillas” como conjunto reutilizable sin duplicar los modelos.

Trabajo:

- almacenar IDs canónicos y transforms relativos;
- guardar relaciones funcionales necesarias;
- colocar plantilla a través del pipeline normal;
- validar coste, espacio y disponibilidad.

PASS:

- crear plantilla;
- reiniciar/cargar;
- volver a colocar;
- resultado válido y persistente;
- ninguna copia física innecesaria de assets.

### B13 — Cámara y visibilidad de arquitectura

**En cristiano:** cuando una pared estorba para editar, la cámara y la presentación ayudan; no luchas contra ellas.

Trabajo:

- extender 369A/B/C con contexto de edición;
- vista superior de precisión;
- memoria de cámara;
- fade/ocultación contextual de elementos obstructivos cuando proceda;
- restauración limpia al volver al juego.

PASS:

- ningún controlador paralelo;
- transiciones suaves;
- no se pierde posición/contexto;
- edición 1920×1080 y 1280×720 usable.

### B14 — Hardening de rendimiento

**En cristiano:** no damos por bueno que “funciona”; tiene que responder bien en un restaurante real.

Medir:

- mover/rotar continuamente;
- multiselección;
- catálogo grande;
- reformas largas;
- Undo/Redo repetido;
- Save/Load;
- BBSIS;
- Navigation;
- GC;
- física;
- render de preview.

Reglas:

- no reconstruir todo el restaurante por cada movimiento del ratón;
- invalidar regiones afectadas cuando sea posible;
- no ampliar sincronizaciones globales de física sin medición;
- previews ligeros y reutilizables;
- diagnósticos costosos bajo demanda;
- medir Editor y Windows build;
- establecer hardware objetivo y presupuestos reproducibles.

PASS:

- presupuestos definidos con baseline;
- ninguna pausa frecuente perceptible injustificada;
- pruebas automáticas de regresión de rendimiento.

### B15 — Queen Test destructiva

**En cristiano:** intentamos romperlo nosotros antes de que lo rompa el jugador.

Escenarios mínimos:

- cancelar durante creación/movimiento;
- salir con cambios pendientes;
- quedarse sin dinero durante una reforma;
- secuencias largas de Undo/Redo;
- grupos parcialmente incompatibles;
- sustitución por asset de diferente tamaño;
- bloquear accesos;
- puertas y paredes en límites;
- restaurante vacío;
- restaurante densamente amueblado;
- Save/Load repetido;
- cargar estados incómodos;
- interrupción simulada en fases de commit.

PASS:

- cero corrupción;
- rollback seguro;
- Console sin Error/Exception/Assert inesperados;
- regresiones dependientes PASS.

### B16 — Integración y retirada de legado

**En cristiano:** solo quitamos la carretera vieja cuando la nueva ya soporta todo el tráfico.

Trabajo:

- integrar bloques validados;
- retirar código antiguo únicamente si ya no tiene consumidores;
- comprobar referencias huérfanas;
- actualizar documentación canónica;
- build Windows final;
- playtest visual final.

PASS:

- todos los gates anteriores PASS;
- no existen dos autoridades para la misma decisión;
- Save/Load final PASS;
- rendimiento final PASS;
- UI final validada;
- merge limpio y trazable.

## 10. Gate obligatorio Assets4All → SAVIC

Antes de B1, la conexión inicial Assets4All → SAVIC debe demostrar un contrato estable suficiente para Editor V2.

Cadena objetivo:

**Asset fuente → Assets4All → contrato analítico → SAVIC → contenido canónico BB → Catálogo → Editor V2**

Editor V2 no analizará geometría para compensar metadatos ausentes.

El contrato debe poder aportar o permitir que SAVIC resuelva de forma fiable:

- identidad estable;
- familia/tipo/categoría;
- dimensiones reales;
- orientación y frente;
- piezas/zonas semánticas relevantes;
- anclaje: suelo, pared, techo o superficie;
- footprint;
- separación y restricciones;
- puntos/puertos funcionales cuando correspondan;
- impacto de navegación;
- perfil BBSIS;
- materiales/acabados;
- previews;
- confianza de inferencias;
- procedencia y trazabilidad.

### Gate mínimo de conexión

Probar recorrido completo con al menos:

1. una mesa;
2. una silla;
3. una lámpara;
4. una decoración;
5. un equipamiento pasivo.

Cada caso debe terminar en Bistro Builder con identidad, prefab/definición resoluble, collider adecuado, previews, categoría, contratos de colocación y datos espaciales suficientes para colocación real.

Ambigüedad crítica debe terminar en revisión, no en invención silenciosa.

## 11. Gates de seguridad que no se pueden saltar

### Gate A — Baseline
Sabemos exactamente qué funcionaba antes de Editor V2.

### Gate B — Undo/Redo
Secuencias mixtas se revierten y rehacen de forma determinista.

### Gate C — Reforma
Discard devuelve exactamente a la baseline.

### Gate D — Save/Load
Una reforma aplicada sobrevive a guardar/cargar sin duplicar IDs, dinero, relaciones o geometría.

### Gate final — Queen + rendimiento
El sistema resiste escenarios destructivos y mantiene respuesta aceptable en hardware objetivo.

Un gate fallido bloquea el bloque siguiente.

## 12. Política Git e integración

- No desarrollar Editor V2 directamente sobre la rama maestra.
- B0 fija la base exacta y crea la rama de integración de V2.
- Cada bloque posterior se desarrolla en rama propia y acotada.
- Una rama de bloque entra en la integración V2 únicamente con sus tests PASS.
- Antes de iniciar un bloque nuevo se sincroniza deliberadamente con la integración canónica vigente y se repiten regresiones relevantes.
- No hacer mega-merge de ramas antiguas para rescatar una función.
- No mezclar SAVIC completo, UI completa y Editor V2 en una única integración.
- No borrar la implementación sustituida hasta que la nueva haya pasado pruebas equivalentes o superiores.
- Los cambios de arquitectura deben actualizar documentación y knowledge bundle en el mismo cambio.

## 13. Reglas de persistencia y rollback

Invariantes obligatorios:

- IDs estables antes y después de Save/Load;
- un commit económico se refleja una sola vez;
- ningún Undo/Redo duplica cobros o devoluciones;
- ningún Load duplica placeables ni estructura;
- operaciones provisionales no se serializan como confirmadas;
- una sesión descartada no deja materializaciones, contratos ni registros activos;
- fallos intermedios conservan un estado recuperable;
- la persistencia no se convierte en autoridad alternativa.

## 14. Reglas de rendimiento

- Validación frecuente debe limitarse al candidato y contexto relevante.
- BBSIS no ejecutará análisis globales por cada frame de arrastre.
- Navigation no reconstruirá toda la topología por cada movimiento provisional.
- Catálogo utilizará virtualización/reutilización cuando la escala lo requiera.
- Previews evitarán instanciación/destrucción masiva.
- Diagnósticos exhaustivos se ejecutarán bajo demanda o en eventos justificados.
- Las optimizaciones se harán a partir de perfiles, no de intuición.
- La aceptación de rendimiento se hará también en build Windows, no únicamente dentro del Editor de Unity.

## 15. Reglas UX

Editor V2 mantiene la dirección visual vigente de Bistro Builder:

- interfaz marfil/crema y latón/acentos contenidos;
- Recoleta para identidad/títulos cuando corresponda;
- Inter para controles y datos;
- selección clara;
- rojo reservado a acciones destructivas;
- viewport como protagonista;
- catálogo lateral;
- inspector contextual;
- herramientas comunes visibles sin duplicar catálogos;
- feedback inmediato y reversible;
- soporte obligatorio 1920×1080 y 1280×720.

La UI puede cambiar radicalmente respecto a una interacción heredada si mejora el comportamiento aprobado, pero no podrá saltarse las autoridades de dominio.

## 16. Fuera de alcance de Editor V2

Salvo decisión posterior explícita:

- soporte móvil;
- agua, gas, extracción o ventilación como simulaciones jugables;
- múltiples plantas;
- tejados complejos;
- paredes curvas;
- herramientas CAD avanzadas;
- física pesada individual para objetos decorativos;
- obreros de construcción simulados;
- edición estructural durante servicio;
- análisis geométrico de assets dentro del runtime del editor como sustituto de SAVIC;
- reescritura de BBSIS, Navigation, Finance o Save/Load.

## 17. Estado de bloques

| Bloque | Estado actual |
|---|---|
| B0 Baseline y protección | PASS |
| Gate Assets4All → SAVIC | VALIDANDO — conexión funcional; falta cobertura real de 5 familias y versionado estable |
| B1 Riesgos de auditoría | NO INICIADO |
| B2 Editor V2 Coordinator | NO INICIADO |
| B3 Selección común | NO INICIADO |
| B4 Undo/Redo global | NO INICIADO |
| B5 Reforma transaccional | NO INICIADO |
| B6 Universal Preview | NO INICIADO |
| B7 Snapping contextual | NO INICIADO |
| B8 Multiselección/grupos | NO INICIADO |
| B9 Catálogo escalable | NO INICIADO |
| B10 Sustitución inteligente | NO INICIADO |
| B11 Diagnóstico | NO INICIADO |
| B12 Plantillas | NO INICIADO |
| B13 Cámara/visibilidad | NO INICIADO |
| B14 Rendimiento | NO INICIADO |
| B15 Queen destructiva | NO INICIADO |
| B16 Integración final | NO INICIADO |

Estados permitidos: NO INICIADO, EN DESARROLLO, VALIDANDO, PASS, BLOQUEADO.

El estado solo cambia a PASS con evidencia conforme a Acceptance and Validation.

## 18. Definición final de DONE

Editor V2 se considerará cerrado únicamente cuando:

- el jugador perciba un único editor coherente;
- mobiliario y arquitectura compartan selección, feedback e historial coordinado;
- las reformas puedan aplicarse o descartarse con seguridad;
- todos los cambios persistentes sobrevivan Save/Load;
- BBSIS, Navigation, Finance, Interaction y gameplay sigan reconociendo correctamente el restaurante;
- el catálogo escale con contenido SAVIC;
- Undo/Redo mixto sea determinista;
- la Queen Test sea PASS;
- la build Windows cumpla los presupuestos de rendimiento establecidos;
- la UI final haya sido validada visualmente;
- no quede una segunda autoridad funcional creada por Editor V2;
- la documentación canónica y los tests reflejen el estado real.

## 19. Regla para agentes y futuras sesiones

Antes de modificar Editor V2, leer como mínimo:

1. este documento;
2. Edit Interaction Design;
3. Authority Matrix;
4. Edit Mode;
5. Construction Authoring;
6. Acceptance and Validation;
7. SAVIC cuando el cambio dependa de assets.

Si una implementación propuesta contradice una autoridad cerrada, se detiene y se justifica el cambio antes de programar.

Si un bloque falla, se corrige ese bloque. No se compensa añadiendo parches en el siguiente.

**Editor V2 se construye como una evolución verificable del editor existente, no como una apuesta de todo o nada.**
