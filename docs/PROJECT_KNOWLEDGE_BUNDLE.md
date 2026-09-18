# Bistro Builder - Project Knowledge Bundle

Generated from every tracked source .md in the repository.
This is a single ingestion point for agents; original files remain authoritative.

## Mandatory precedence
1. ENTRYPOINT and CANONICAL.
2. Integrated code and public contracts when they describe verified behavior.
3. SUPPORTING and TECHNICAL_EVIDENCE.
4. HISTORICAL and THIRD_PARTY only for context and traceability.

Historical material never overrides a later canonical decision.

---

## SOURCE: AGENTS.md

Category: ENTRYPOINT

# Bistro Builder — instrucciones para agentes

Esta raíz contiene la fuente de verdad técnica y de producto del proyecto.

## Ingesta Markdown obligatoria
Antes de modificar código en una sesión nueva:
1. Leer **completo** `docs/PROJECT_KNOWLEDGE_BUNDLE.md`.
2. Consultar `docs/ALL_MARKDOWN_INDEX.md` para localizar la fuente original de cada decisión.
3. Si el bundle/índice no existe o algún `.md` fuente ha cambiado, ejecutar `Tools/BistroBuilder/RefreshMarkdownKnowledge.ps1` y volver a leer el bundle.
4. Si no se puede ejecutar el script, enumerar todos los `.md` versionados con Git y leerlos respetando la precedencia siguiente.

Los dos archivos generados (`ALL_MARKDOWN_INDEX.md` y `PROJECT_KNOWLEDGE_BUNDLE.md`) no son fuentes independientes: derivan del resto del corpus Markdown.

## Precedencia documental
1. `ENTRYPOINT`: este `AGENTS.md` y `docs/README.md`.
2. `CANONICAL`: decisiones vigentes y documentos de `00_PRODUCT`, `10_ARCHITECTURE`, `20_GAME_SYSTEMS`, `30_UI_UX`, `40_TESTING` y `90_DECISIONS`.
3. Contratos públicos y comportamiento comprobable del código integrado.
4. `SUPPORTING` y `TECHNICAL_EVIDENCE`.
5. `HISTORICAL` y `THIRD_PARTY`, solo para contexto/trazabilidad.

Una fuente histórica nunca prevalece sobre una decisión canónica posterior.
## Reglas obligatorias
- No reabrir sistemas cerrados salvo regresión demostrable o nueva decisión explícita.
- No duplicar autoridades entre sistemas. Respetar `docs/10_ARCHITECTURE/AUTHORITY_MATRIX.md`.
- Mantener arquitectura modular, data-driven y sin hardcode específico por asset.
- BBSIS decide viabilidad espacial; Interaction & Reservation derechos lógicos; Navigation rutas/tráfico; Animation representación; Gameplay/IA intención y resultado.
- Modo Edición funciona fuera de servicio y no simula obreros construyendo.
- No introducir agua, extracción, gas o ventilación como simulaciones jugables salvo decisión posterior explícita.
- Los cambios deben ser no destructivos, idempotentes cuando corresponda y acompañados de validadores/autotests.
- Ningún PASS se declara solo por compilar: aplicar `docs/40_TESTING/ACCEPTANCE_AND_VALIDATION.md`.
- Al encontrar contradicciones, conservar evidencia y actualizar la fuente canónica; no borrar historia.
- Si una decisión de producto/arquitectura cambia, actualizar los `.md` afectados y regenerar índice + bundle en el mismo commit.

## Estado vivo
`docs/00_PRODUCT/ROADMAP.md` es el índice operativo de bloques y sistemas transversales. Si cambia el estado real de una rama o integración, actualizarlo en el mismo cambio.

---

## SOURCE: docs/README.md

Category: ENTRYPOINT

# Bistro Builder — documentación canónica

Consolidación iniciada el **12/09/2026** a partir de documentación del repositorio, DOCX históricos y decisiones de chats desde abril de 2026. Los originales no se eliminan.

## Leer en este orden
1. [`00_PRODUCT/PRD.md`](00_PRODUCT/PRD.md) — producto y alcance.
2. [`00_PRODUCT/ROADMAP.md`](00_PRODUCT/ROADMAP.md) — estado vivo.
3. [`10_ARCHITECTURE/SYSTEM_MAP.md`](10_ARCHITECTURE/SYSTEM_MAP.md) — mapa de sistemas.
4. [`10_ARCHITECTURE/AUTHORITY_MATRIX.md`](10_ARCHITECTURE/AUTHORITY_MATRIX.md) — fronteras de autoridad.
5. [`90_DECISIONS/DECISION_REGISTER.md`](90_DECISIONS/DECISION_REGISTER.md) — decisiones vinculantes/superadas.
6. [`30_UI_UX/UI_UX_DEFINITIVE.md`](30_UI_UX/UI_UX_DEFINITIVE.md) y [`30_UI_UX/CAMERA_369.md`](30_UI_UX/CAMERA_369.md).
7. [`20_GAME_SYSTEMS/CONSTRUCTION_AUTHORING.md`](20_GAME_SYSTEMS/CONSTRUCTION_AUTHORING.md) para Modo Edición player-ready.
8. [`40_TESTING/ACCEPTANCE_AND_VALIDATION.md`](40_TESTING/ACCEPTANCE_AND_VALIDATION.md) — definición de PASS.

## Fuente de verdad
Una decisión posterior y explícita prevalece sobre una propuesta antigua. Los documentos legacy permanecen como trazabilidad, no como autoridad cuando contradicen el Decision Register o un documento canónico más reciente.

## Chats sin artefacto
Las decisiones que solo vivían en conversaciones están registradas en [`99_HISTORY/CHAT_SOURCE_REGISTER.md`](99_HISTORY/CHAT_SOURCE_REGISTER.md). No se transcriben chats completos: se conserva la decisión, su estado y su impacto técnico.

## Ideas no canónicas
[`90_DECISIONS/OPEN_QUESTIONS_AND_LEGACY_IDEAS.md`](90_DECISIONS/OPEN_QUESTIONS_AND_LEGACY_IDEAS.md) separa brainstorming histórico de requisitos vigentes.

## Historia y auditoría
Ver [`99_HISTORY/LEGACY_SOURCE_REGISTER.md`](99_HISTORY/LEGACY_SOURCE_REGISTER.md) y [`40_TESTING/DOCUMENTATION_MIGRATION_AUDIT_20260912.md`](40_TESTING/DOCUMENTATION_MIGRATION_AUDIT_20260912.md).
## Ingesta global para agentes
`PROJECT_KNOWLEDGE_BUNDLE.md` concatena todos los `.md` fuente versionados del proyecto en orden de precedencia y es el punto único de ingestión para Astra/Codex/agentes compatibles.

`ALL_MARKDOWN_INDEX.md` mantiene el inventario y trazabilidad hacia cada archivo original, incluidos Markdown situados en la raíz o dentro de `Assets/`.

Regenerar ambos con:

`Tools/BistroBuilder/RefreshMarkdownKnowledge.ps1`

Los archivos originales siguen siendo la fuente editable; el bundle es un artefacto derivado para contexto.

---

## SOURCE: docs/00_PRODUCT/PRD.md

Category: CANONICAL

# Bistro Builder — PRD canónico

**Producto:** videojuego/gestor de restaurantes 3D para PC instalable.
**Experiencia base:** vista superior oblicua/isométrica, comedor y cocina en la misma escena, gestión durante servicio y construcción/edición fuera de servicio.

## Objetivo
Permitir al jugador crear, operar y hacer crecer restaurantes creíbles mediante decisiones de layout, carta, personal, compras, servicio, economía y reputación, con sistemas conectados y legibles en lugar de micromanagement físico innecesario.

## Pilares
- **Construir y adaptar:** habitaciones, paredes, puertas, mobiliario/equipamiento y decoración mediante Modo Edición profesional.
- **Operar:** clientes, comandas, cocina, camareros, entrada/sala/barra y resolución de incidencias durante el servicio.
- **Gestionar:** carta, inventario FEFO, proveedores, personal, horarios, reservas, marketing, finanzas y progresión.
- **Decidir bajo presión:** capacidad de cocina, esperas, prioridades, satisfacción, caja y reputación deben producir trade-offs claros.
- **Escalar sin fragilidad:** arquitectura modular, data-driven, persistible, validable y extensible sin hardcode por asset.

## Requisitos de producto vinculantes
- La construcción estructural debe ser comprensible y jugable; no basta con poder mover mesas y sillas.
- El Modo Edición solo está disponible fuera de servicio; construcción instantánea, sin obreros simulados.
- Espera general mediante lista virtual; espera física solo en barra cuando corresponda.
- Cocina comunica Fluida/Cargada/Saturada/Bloqueada y permite reducir entrada, pausar nuevas comandas por plato y priorizar hasta 3 comandas.
- Gestión de camareros por zonas con asignación automática por cercanía, carga y prioridad; jefe de sala opcional.
- `Caja` significa dinero del servicio y `Satisfacción` la satisfacción del servicio.
- No se simulan como gameplay agua, extracción, gas ni ventilación; tampoco zonas de personal jugables.
- La presentación visual base es común a todos los locales salvo excepciones explícitas.

## Requisitos de calidad
- Guardado/carga coherente entre sistemas y servicios activos.
- Operaciones frecuentes de edición deben responder sin pausas perceptibles injustificadas.
- Sistemas cerrados deben disponer de validadores/autotests y pruebas funcionales, no solo compilación.
- UI clara, sobria y operativa; información accionable antes que decoración.

## Fuera de alcance por defecto
Ideas históricas no ratificadas posteriormente —por ejemplo avatar detallado, simulaciones técnicas de instalaciones o complejidad física ornamental— no son requisitos hasta decisión explícita.

---

## SOURCE: docs/00_PRODUCT/ROADMAP.md

Category: CANONICAL

# Bistro Builder — roadmap vivo

**Corte documental:** 12/09/2026. `CERRADO` significa cierre dentro del alcance declarado; una regresión posterior puede abrir hardening sin invalidar todo el diseño.

| Área | Estado canónico | Nota |
|---|---|---|
| 365B/C/D Seating, snapping e indicadores | CERRADO | PASS |
| 366/366B Save/Load estructura + `game.general` | CERRADO | PASS |
| 367A–F Carta, comandas, preparación, consumo, compartidos/pases | CERRADO | PASS en su alcance |
| 2.2 Inventario/Almacén V1 | CERRADO | FEFO, lotes, recepciones, mínimos/previsión, UI |
| 2.3 Proveedores V1 | CERRADO | mercado, formatos/ofertas, pedidos, storage, compra Inteligente |
| 3 Economía y Finanzas | CERRADO | cierre formal 28/08/2026 |
| 4 Personal | CERRADO | arquitectura laboral separada de agentes operativos |
| 5 Horarios y Turnos | CERRADO | cierre posterior prevalece sobre auditorías antiguas pendientes |
| 6 Reservas | CERRADO | capacidad, disponibilidad, servicio, persistencia, UI, Queen |
| 7 Marketing / 8 Reputación / 9 Progresión | CERRADO V1 | rama consolidada 7–9 |
| 10 Clientes / 11 Comandas / 12 Cocina / 13 Camareros avanzados | CERRADO V1 | ramas específicas integradas/hardened |
| 14 Entrada, Sala y Barra | CERRADO V1 | espera virtual general; barra con espera física |
| 15 Fin de servicio/día | CERRADO V1 | rama específica |
| 16 Nueva partida/apertura | CERRADO V1 + hardening | existe V2 de apertura |
| 17 Navegación V1 | CERRADO V1 + hardening | Crowd Flow transversal continúa integración/regresiones |
| 18 Modo Edición/Construcción | CORE V1 CERRADO; UX/HARDENING ACTIVO | 84/84 reportados en núcleo; Construction Authoring/18N sigue hasta player-ready |
| 21A UI/UX definitiva | EN DESARROLLO | diseño vinculante y rama propia; cierre aún no ratificado |

## Sistemas transversales
- **BBSIS v1:** COMPLETO, VALIDADO Y CERRADO; hardening solo ante regresión real.
- **Interaction & Reservation v1:** IMPLEMENTADO, VALIDADO Y CERRADO; auditoría destructiva futura antes de vertical slice/beta.
- **Character & Interaction Animation v1:** INTEGRADO, VALIDADO Y SUBIDO; futuras ampliaciones son V2/hardening.
- **Navigation & Crowd Flow:** contratos de autoridad fijados; implementación/hardening activo sin duplicar BBSIS ni Animation.
- **BBPLFS:** desarrollo activo en `feature/bbplfs-v1`.
- **Climate & Weather:** implementación V1 existente; cierre final pendiente de validación/ratificación.

## Integración actual
`playtest/all-current-20260912` contiene trabajo posterior a `integration/chat-final-20260911` y, al crear esta documentación, tenía conflictos de merge sin resolver. Esta rama documental se aisló deliberadamente para no tocar esa integración.

---

## SOURCE: docs/10_ARCHITECTURE/AUTHORITY_MATRIX.md

Category: CANONICAL

# Bistro Builder — matriz de autoridad

| Decisión / estado | Autoridad | No debe decidirlo |
|---|---|---|
| Intención y prioridad de tarea | Gameplay / IA | Animation, Navigation, BBSIS |
| Derecho lógico a usar/asignar/poseer | Interaction & Reservation | BBSIS, Navigation, Animation |
| Viabilidad y reserva espacial | BBSIS | Interaction, Navigation, Animation |
| Ruta, velocidad, heading y llegada | Navigation & Crowd Flow | BBSIS, Animation |
| Pose visual, blending, IK, gaze, recovery | Character Animation | Gameplay, BBSIS, Navigation |
| Estado de comanda y líneas | Order domain | Cocina, UI, Animation |
| Estado/preparación de cocina | Kitchen domain | UI, Animation |
| Empleado, salario, experiencia | Staff | Waiter runtime, Finance |
| Tarea operativa de camarero | WaiterTaskCoordinator | Staff, UI |
| Turno planificado | Staff Schedule | Staff roster, Waiter runtime |
| Caja y ledger | Finance | Staff, Suppliers, UI |
| Stock/lotes/FEFO | Inventory | Suppliers, Kitchen UI |
| Mercado/ofertas/pedidos proveedor | Suppliers | Inventory, Finance |
| Reserva de cliente/capacidad | Reservations | Seating visual, UI |
| Estructura colocada del restaurante | Edit/Structure domain | UI, BBPLFS |
| Condición meteorológica global | Climate | mesa individual, zona exterior individual |

## Reglas de frontera
- `EmployeeId` y `WaiterId` son identidades distintas; el binding de sesión las conecta.
- Wait Ticket se reserva a colas semánticas reales; no sustituye reservas espaciales.
- Claims/Spatial Leases pertenecen solo a BBSIS.
- UI emite comandos y presenta snapshots; no muta estado canónico directamente.
- BBPLFS propone/genera layout, pero la estructura final debe pasar por las mismas reglas de edición y validación.
- La persistencia conserva estados de cada autoridad; no crea un dominio alternativo.

---

## SOURCE: docs/10_ARCHITECTURE/BBPLFS.md

Category: CANONICAL

# BB Procedural Layout & Furnishing System (BBPLFS)

**Estado canónico:** sistema transversal en desarrollo activo, rama `feature/bbplfs-v1`.

## Objetivo
Generar y distribuir layouts/furnishing de restaurante de forma procedural sin convertirse en un editor paralelo ni saltarse las autoridades canónicas del proyecto.

## Reglas vinculantes
- El resultado debe terminar integrado en el proyecto y subido a su rama correspondiente.
- BBPLFS propone/genera; la estructura final se valida mediante los mismos contratos de Modo Edición/BBSIS que una edición manual.
- No duplica pathfinding, reservas espaciales, economía, persistencia ni Presentation.
- Debe producir resultados editables posteriormente por el jugador.
- El pipeline debe ser modular, determinista cuando se requiera reproducibilidad y compatible con assets data-driven.

## Integraciones
- BBSIS: viabilidad espacial y contratos.
- Navigation/Crowd: circulación y rutas resultantes.
- Edit Mode: materialización/edición del layout final.
- Economía: costes cuando el flujo de juego los aplique.
- Persistence: estructura y furnishing mediante formatos canónicos.

## Cierre
No declarar cerrado hasta demostrar generación, validación, edición manual posterior, persistencia y ausencia de autoridad duplicada en un escenario de restaurante representativo.

---

## SOURCE: docs/10_ARCHITECTURE/BBSIS.md

Category: CANONICAL

# BB Spatial Interaction System (BBSIS)

**Estado canónico:** V1 COMPLETO, VALIDADO Y CERRADO. No reabrir diseño salvo regresión demostrable.

## Autoridad
BBSIS es la única autoridad de **viabilidad y reserva espacial** para interacciones. No decide intención de gameplay, derechos lógicos, navegación ni animación.

## Conceptos vinculantes
- separación estricta `Render ≠ Spatial`;
- `Adaptive Spatial Proxy` para representación espacial robusta;
- `Spatial Contracts` como requisitos de interacción;
- traits y matriz de compatibilidad;
- `Claims / Spatial Leases` para exclusividad espacial temporal;
- `Spatial Episodes` como ciclo de una interacción espacial;
- `Work Edges`, `Ports` y `Seat Bays` como interfaces de uso;
- `Dynamic Sweep` para volumen requerido durante movimiento/interacción;
- `Mobility Envelope` y `Carry Envelope`;
- `Spatial Gates`, `Critical Routes` y `Flow Quality`;
- LOD lógico y perfiles de tuning data-driven.

## Reglas
- Interaction & Reservation no replica Claims/Leases.
- Navigation puede consultar viabilidad pero conserva autoridad de trayectoria.
- Animation representa la resolución espacial sin modificarla.
- Modo Edición y BBPLFS deben validar contra contratos espaciales comunes.
- Puertas y sillas pueden cambiar su ocupación espacial durante episodios relevantes; no se simulan mediante física por bisagra como autoridad de gameplay.

## Evolución
Cambios futuros deben tratarse como hardening/V2 y demostrar que respetan contratos públicos y compatibilidad con sistemas ya integrados.

---

## SOURCE: docs/10_ARCHITECTURE/CHARACTER_ANIMATION.md

Category: CANONICAL

# BB Character & Interaction Animation System

**Estado canónico:** V1 INTEGRADO, VALIDADO Y SUBIDO. Cualquier ampliación futura se trata como V2/hardening.

## Arquitectura vinculante
`Gameplay decide → BBSIS valida → Navigation llega → Animation representa`.

## Autoridad
Animation controla únicamente representación visual del personaje: selección de Motion Recipe equivalente, blending, layers/masks, IK/constraints visuales, gaze, progreso visual, interruptibilidad, recovery y LOD.

## Modelo de datos
- `Motion Recipes` como recetas de representación;
- `Interaction Family Profile` para familias de interacción;
- `Asset Interaction Descriptor` para capacidades visuales del asset;
- `Runtime Resolved Plan` como plan visual resuelto en ejecución.

## Reglas
- No decide navegación, reservas espaciales, derechos lógicos ni ownership de props.
- No cambia el resultado de gameplay.
- No almacena una segunda verdad sobre seating, workstation o custody.
- Debe tolerar interrupciones, cancelaciones y rehidratación de estado sin romper las autoridades externas.
- El pipeline de authoring/validación debe ser no destructivo y disponer de validadores.

## Integraciones
Navigation proporciona movimiento lógico y llegada; BBSIS anchors/puertos válidos; Interaction puede exponer el derecho lógico; sistemas de objetos exponen su estado. Animation traduce esos hechos a una representación creíble sin apropiarse de ellos.

---

## SOURCE: docs/10_ARCHITECTURE/INTERACTION_RESERVATION.md

Category: CANONICAL

# BB Interaction & Reservation System

**Estado canónico:** V1.0 IMPLEMENTADO, VALIDADO Y CERRADO. Existe una auditoría destructiva futura obligatoria antes de vertical slice/beta.

## Autoridad
Coordina derechos **lógicos**, no espaciales. Gameplay/IA solicita; Interaction concede/revoca; BBSIS conserva toda autoridad espacial.

## Primitivas públicas V1
- `Logical Assignment`: relación lógica actor/recurso/función.
- `Task Claim`: derecho lógico temporal sobre una tarea.
- `Use Permit`: permiso de uso de un recurso.
- `Custody`: posesión/transferencia lógica de objetos o resultados.
- `Wait Ticket`: solo para colas semánticas reales.

Todas utilizan el `Logical Grant Kernel`, con handles generacionales, holder, resource/scope, generation/epoch, reason codes, dependencias, invalidación y liberación idempotente.

## Reglas vinculantes
- No almacenar posiciones, volúmenes, rutas, Claims ni Spatial Leases de BBSIS.
- No decidir pathfinding ni circulación.
- No decidir resultado de gameplay ni representación visual.
- Seating lógico, workstations y recursos compartidos usan grants; su accesibilidad física se valida por BBSIS.
- Cancelación, destrucción, fin de turno o pérdida de acceso deben liberar/inutilizar grants sin derechos fantasma.
- Exclusividad real implica un único owner lógico cuando el recurso lo exige.

## Auditoría futura
Antes de vertical slice/beta ejecutar carga prolongada, Save/Load en transferencias, cancelaciones encadenadas, edición con recursos activos, replay determinista, búsqueda de locks legacy y perfil CPU/GC. El objetivo es 0 grants huérfanos y 0 duplicidades exclusivas.

---

## SOURCE: docs/10_ARCHITECTURE/NAVIGATION_CROWD.md

Category: CANONICAL

# BB Navigation & Crowd Flow System

**Estado canónico:** diseño especializado cerrado en sus fronteras; implementación/hardening transversal activo. El Bloque 17 Navegación V1 figura cerrado, pero Crowd Flow continúa integración y regresiones.

## Autoridad
Responsable de rutas, circulación y tráfico humano. Prioridad de diseño: **robustez → gameplay → rendimiento → naturalidad → simulación**.

## Familias contempladas
Clientes, camareros, cocineros, resto de personal, grupos, repartidores y carritos.

## Responsabilidades
- calcular y actualizar trayectoria;
- velocidad lógica y heading;
- llegada y replanificación;
- separación/circulación y resolución de bloqueos;
- coste de rutas y gestión de tráfico;
- tratamiento coherente de grupos y agentes con distintos envelopes.

## Fronteras vinculantes
- BBSIS decide si el espacio/episodio es físicamente válido y sus Claims/Leases.
- Interaction & Reservation decide derechos lógicos sobre destino/recurso.
- Animation representa locomoción; no decide trayectoria.
- Gameplay/IA decide qué destino/acción persigue el actor.

## Casos espaciales obligatorios
Las puertas ocupan espacio durante apertura y las sillas cambian ocupación al sentarse/levantarse. Navigation debe consumir esa realidad espacial sin convertirse en autoridad de bisagras, asientos o contratos BBSIS.

## Cierre/hardening
No declarar cierre transversal definitivo mientras existan fixes activos de deadlocks/separación o integración. Las regresiones deben resolverse en esta autoridad, sin parches de movimiento dentro de Animation, Staff o sistemas de servicio.

---

## SOURCE: docs/10_ARCHITECTURE/PERSISTENCE.md

Category: CANONICAL

# Bistro Builder — persistencia y Save/Load

## Estado
La base 366/366B y los hitos funcionales 367 asociados están validados dentro de su alcance. La persistencia continúa siendo una preocupación transversal: cada nuevo sistema debe integrarse en el SaveGame universal, no crear guardados paralelos.

## Principios vinculantes
- Cada dominio conserva su snapshot versionado y una identidad estable.
- Los proveedores definen fases coherentes de Prepare / Apply / Finalize cuando existen dependencias.
- La prevalidación debe poder rechazar datos inválidos antes de mutar estado vivo.
- Guardar durante servicio activo debe permitir reconstruir bindings y estado operativo sin duplicados.
- Las referencias de escena no sustituyen IDs persistentes.
- Un Load debe ser determinista respecto al snapshot cargado y no depender de búsquedas tardías o temporizadores arbitrarios.

## Secciones conocidas
- `game.general`: base general 366B.
- estructura del restaurante y seating.
- `service.runtime`: servicio activo y agentes/órdenes operativos.
- `staff.state`: empleados persistentes.
- `staff.schedule`: planificación de turnos.
- `finance.runtime`: autoridad financiera.
- estados de inventario, proveedores, reservas, progresión y demás dominios integrados.
- `climate.runtime`: estado climático V1 cuando su rama se integre/cierre.

## Regla de integración
Si un sistema necesita restaurar una relación entre dos autoridades, debe persistir identificadores/estado mínimo y reconstruir el binding en el orden correcto. No serializar GameObjects como sustituto del dominio.

## Gate
Todo sistema nuevo o ampliado debe demostrar round-trip, carga cruzada cuando aplique, Save/Load en estados no triviales y ausencia de duplicación o corrupción tras repetición.

---

## SOURCE: docs/10_ARCHITECTURE/SYSTEM_MAP.md

Category: CANONICAL

# Bistro Builder — mapa canónico de sistemas

## Principio central
Cada sistema tiene una autoridad explícita. Integrar significa consumir contratos públicos, no copiar estado ni crear una segunda fuente de verdad.

## Cadena runtime
`Gameplay/IA → Interaction & Reservation → BBSIS → Navigation → Animation → sistema de dominio`.

## Capas principales
| Capa | Responsabilidad |
|---|---|
| Gameplay / IA | intención, prioridades y resultado de gameplay |
| Interaction & Reservation | asignaciones, permisos lógicos, custody y colas semánticas reales |
| BBSIS | viabilidad espacial, contratos, claims/leases, puertos, sweeps y gates |
| Navigation & Crowd Flow | rutas, velocidad lógica, heading, llegada, circulación y tráfico humano |
| Character Animation | representación visual, motion recipes, blending, IK, gaze y recovery |
| Sistemas de dominio | clientes, comandas, cocina, personal, inventario, economía, reservas, etc. |
| Presentation/UI | lectura y comandos; nunca autoridad del estado de dominio |
| Persistence | snapshots versionados y orden de rehidratación entre autoridades |

## Sistemas transversales adicionales
- **BBPLFS:** generación y distribución procedural de layouts y furnishing; consume contratos canónicos.
- **Modo Edición/Construcción:** edición estructural del restaurante fuera de servicio.
- **Climate & Weather:** condiciones globales simplificadas; interior confortable.
- **UI Design System:** lenguaje visual común sin redefinir reglas de negocio.

## Regla de integración
Un sistema puede proyectar datos derivados de otro, pero no poseer la fuente original. Finanzas recibe nómina calculada por Personal; Horarios filtra elegibilidad pero no crea empleados; Animation representa custody pero no la decide.

## Persistencia
Los proveedores de Save/Load deben conservar identidad estable, ser versionados y respetar dependencias de Apply/Finalize. No se permiten serializadores paralelos para el mismo estado canónico.

---

## SOURCE: docs/20_GAME_SYSTEMS/CLIMATE.md

Category: CANONICAL

# BB Climate & Weather System

**Estado:** implementación V1 existente en rama propia; cierre final pendiente de validación/ratificación.

## Decisiones vinculantes
- Lluvia, nieve y viento son fenómenos simples: no existen subtipos jugables.
- No se simula dirección de lluvia, nieve ni viento.
- Todas las mesas exteriores reciben la misma condición meteorológica base, sin diferencia por norte/sur/este/oeste.
- La temperatura exterior es uniforme para todas las zonas exteriores.
- Pérgola y parasol usan una lógica de cobertura equivalente dentro del alcance actual.
- El interior se considera confortable; no se simulan extremos térmicos interiores.
- Clima estándar pseudoaleatorio; evitar meteorología extrema o microclimas innecesarios.

## Arquitectura V1 existente
Núcleo determinista, estados globales, forecast de 5 días, integración con GameClock/calendario, persistencia `climate.runtime`, controlador visual, BB Climate Studio, instalador, validador y autotests.

## Regla de integración
Climate publica una condición global y sus efectos de gameplay. Terraza/FOH/cliente consumen esa condición; ninguna mesa genera su propio clima ni calcula dirección local.

## Gate de cierre
Compilación limpia, instalación idempotente, validación/autotests, prueba visual y funcional de cambio climático, round-trip de persistencia y ausencia de regresiones en terraza/servicio.

---

## SOURCE: docs/20_GAME_SYSTEMS/CONSTRUCTION_AUTHORING.md

Category: CANONICAL

# Bistro Builder — Construction Authoring / hardening de Bloque 18

## Estado reconciliado
El núcleo lógico de Bloque 18 llegó a cerrar una batería reportada de 84/84 autotests y se declaró V1 cerrado en un punto del desarrollo. Playtests posteriores demostraron que **la experiencia de construcción todavía no estaba lista para jugador**, por lo que Construction Authoring/18N continúa como hardening y UX activa.

## Núcleo ya cubierto
- paredes con IDs estables y uniones T/X;
- habitaciones automáticas, split/merge y `RoomId`;
- openings/puertas sobre segmentos;
- geometría y casos cóncavos;
- Undo/Redo y modelo Draft/Baseline;
- persistencia y restauración;
- integración con Economía, BBSIS y Navigation.

## Problemas reales detectados en playtest
- mover o eliminar mesas/sillas podía dejar la aplicación pensando;
- `Confirmar`, `Validar` y `Guardar` no daban feedback perceptible suficiente;
- la pantalla inicial de diseño podía permanecer visible/bloqueando;
- no era evidente cómo crear una pared o una habitación desde la UI;
- tener infraestructura correcta no bastaba para explicar al jugador qué podía hacer.

## Decisiones de hardening
- preservar núcleo autoritativo existente; no reconstruir BBACS/BBSIS/Nav/SaveGame desde cero;
- previews son no autoritativos y nunca hacen commit por sí solos;
- selección directa, snapping visible, recálculo/recentrado topológico y Undo por gesto;
- puertas/ventanas usan `Openings` + segmentos, no CSG como autoridad;
- V1 no incluye múltiples pisos, tejados, paredes curvas ni CAD avanzado;
- auditar cambios locales y ramas antes de integrar; nunca descartar trabajo ajeno automáticamente.

## Gate player-ready
Un usuario debe poder abrir Modo Edición y, sin conocer el código, crear una habitación/pared, añadir una puerta, colocar/mover/eliminar mobiliario, entender validez/coste, confirmar, guardar, cargar y continuar editando con respuesta fluida y feedback inequívoco.

---

## SOURCE: docs/20_GAME_SYSTEMS/EDIT_MODE.md

Category: CANONICAL

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

---

## SOURCE: docs/20_GAME_SYSTEMS/MANAGEMENT_SYSTEMS.md

Category: CANONICAL

# Bistro Builder — sistemas de gestión

## Inventario / Almacén (2.2)
Un único almacén jugable. Lotes internos no se gestionan manualmente; FEFO, caducidad/deterioro lento y realista, recepciones, mínimos, alertas y previsión. Desperdicio/mermas profundas quedan descartados del alcance actual.

## Proveedores (2.3)
Datos maestros separados de mercado dinámico y estado de partida. Pedidos con ciclo `Draft → Confirmed → PendingDelivery → InDelivery → Delivered / Cancelled`. Formatos comerciales y ofertas desde V1. Editores de Proveedores e Ingredientes mantienen logos/imágenes, Undo/Redo, Dirty y validación. La recomendación automática se denomina **Inteligente**.

## Economía (3)
`BistroBuilderFinanceService` / `finance.runtime` es la única autoridad monetaria. Caja, ledger, ventas, costes, gastos, nóminas, resultados, históricos y financiación convergen en esa autoridad; otros dominios proyectan hechos económicos sin wallets paralelos.

## Personal (4)
`EmployeeId` persistente no es `WaiterId`. Personal posee identidad laboral, salario, experiencia, habilidades y estado del empleado; el binding de sesión conecta empleados con agentes operativos. `WaiterTaskCoordinator` conserva autoridad de tareas.

## Horarios (5)
`staff.schedule` planifica por día/servicio, cobertura y coste proyectado. Filtra quién es elegible para el binding de sesión; no crea empleados ni Waiters. Las ediciones de planificación se realizan con restaurante cerrado.

## Reservas (6)
Gestiona capacidad y disponibilidad temporal/lógica de reservas de clientes, integrada con seating y servicio sin apropiarse de las reservas espaciales BBSIS.

## Marketing, Reputación y Progresión (7–9)
Marketing modifica demanda mediante acciones/costes definidos; Reputación refleja experiencia acumulada; Progresión gobierna avance/desbloqueos. Deben integrarse con Finanzas y servicio mediante contratos, no mediante estado monetario o satisfacción duplicado.

## Regla transversal
Presentation ofrece consulta y comandos. Ninguna pantalla escribe directamente en snapshots de estos dominios.

---

## SOURCE: docs/20_GAME_SYSTEMS/SERVICE_SYSTEMS.md

Category: CANONICAL

# Bistro Builder — servicio, clientes, comandas, cocina y sala

## Clientes y mesas
El servicio debe mantener grupos/clientes, seating, consumo individual y compartido, cuenta y limpieza. La ficha contextual del cliente/mesa expone información básica y acciones operativas como `Disculpa`, `Explicar demora` o `Agilizar cuenta` cuando proceda.

## Comandas
La comanda canónica soporta líneas, consumidores múltiples y pases. En compartidos, una línea puede permanecer `Served` hasta que todos los consumidores hayan reclamado/consumido; los pases se liberan según política. La autoridad de estados de línea no pertenece a Kitchen ni a UI.

## Cocina
Estados operativos visibles: **Fluida / Cargada / Saturada / Bloqueada**. Acciones de gestión aprobadas: **Reducir entrada**, **Pausar nuevas comandas** por plato y **Priorizar comanda** con máximo 3 prioridades simultáneas.

## Camareros
Gestión por zonas: comedor interior, terraza, barra y apoyo. Asignación automática considera cercanía, carga y prioridad; jefe de sala opcional. No existen descansos manuales como minijuego. Personal laboral y agentes runtime siguen siendo dominios separados.

## Entrada, sala y barra
La espera general se representa mediante lista virtual, evitando masas físicas innecesarias. Solo la barra mantiene espera física cuando corresponde. El HUD puede mostrar `Espera: X clientes`.

## Asignación de mesa
Algoritmos de política previstos/aprobados: **Equilibrado**, **Priorizar satisfacción**, **Rotación**, **Proteger reservas** y **Equilibrar zonas**. La política selecciona; BBSIS/Navigation resuelven viabilidad y desplazamiento.

## Fin de servicio/día
El cierre debe agotar o resolver trabajo operativo, consolidar resultados económicos/experiencia y dejar un estado persistible coherente para el siguiente ciclo. El Bloque 15 V1 está cerrado; extensiones deben integrarse sin crear un segundo cierre de día.

## Regla de experiencia
`Caja` y `Satisfacción` en HUD se refieren al servicio actual. Las métricas históricas pertenecen a sus sistemas de gestión/reputación correspondientes.

---

## SOURCE: docs/20_GAME_SYSTEMS/SYSTEM_CATALOG.md

Category: CANONICAL

# Bistro Builder — catálogo canónico de sistemas de juego

| Bloque | Sistema | Estado V1 | Fuente/nota |
|---|---|---|---|
| 2.2 | Inventario / Almacén | CERRADO | lotes, caducidad diaria, FEFO, recepciones, mínimos, previsión, UI |
| 2.3 | Proveedores | CERRADO | catálogo/mercado/pedidos, ofertas, storage, editor, compra `Inteligente` |
| 3 | Economía y Finanzas | CERRADO | `finance.runtime` / ledger como autoridad monetaria |
| 4 | Personal | CERRADO | Employee persistente separado de agentes runtime |
| 5 | Horarios y Turnos | CERRADO | planificación, cobertura, binding, persistencia, UI |
| 6 | Reservas | CERRADO | capacidad, disponibilidad, servicio, persistencia, UI |
| 7 | Marketing | CERRADO V1 | integrado con demanda/economía |
| 8 | Reputación | CERRADO V1 | consecuencias de experiencia y servicio |
| 9 | Progresión | CERRADO V1 | progreso/desbloqueos en alcance actual |
| 10 | Clientes avanzados | CERRADO V1 | comportamiento/tipos avanzados |
| 11 | Comandas avanzadas | CERRADO V1 | extensión sobre flujo canónico 367 |
| 12 | Cocina avanzada | CERRADO V1 | estaciones, colas, preparación e integración |
| 13 | Camareros avanzados | CERRADO V1 | coordinación operativa, zonas/carga |
| 14 | Entrada / Sala / Barra | CERRADO V1 | entrada, asignación, espera y barra |
| 15 | Fin de servicio/día | CERRADO V1 | cierre operativo del ciclo |
| 16 | Nueva partida / apertura | CERRADO V1 | existe hardening/V2 posterior |
| 17 | Navegación V1 | CERRADO V1 | transversal Crowd Flow sigue hardening |
| 18 | Modo Edición / Construcción | ACTIVO | 18M/18N + Construction Authoring |
| 21A | UI/UX definitiva | ACTIVO | rama específica; cierre aún no ratificado |

## Regla
El estado V1 no autoriza a reescribir un bloque desde otro sistema. Correcciones posteriores deben preservar su autoridad pública y documentarse como hardening, integración o V2.

---

## SOURCE: docs/30_UI_UX/CAMERA_369.md

Category: CANONICAL

# Bistro Builder — cámara profesional 369

**Estado:** 369A validada; 369B implementada históricamente; 369C3 contextual/no destructiva aceptada. La UX final actual **no expone vistas predefinidas**.

## Controles reservados
- `WASD` / flechas: mover.
- `Q / E`: rotación horizontal.
- `R / F`: subir / bajar.
- `Shift`: desplazamiento rápido.
- Rueda: zoom.
- Botón central: pan con ratón.
- Botón derecho: rotación/pitch.
- Bordes de pantalla: desplazamiento cuando no estén protegidos por UI.

Estos controles quedan reservados a cámara: la UI no debe asignar atajos que entren en conflicto.

## Comportamiento contextual
Seleccionar elementos puede recentrar suavemente la vista sin aplicar zoom automático. Actividad y elementos operativos pueden centrar/seleccionar su objetivo. La transición de cámara debe ser suave y funcional, no cinematográfica en detrimento del control.

## 369B — decisión sustituida
General/Isométrica llegaron a validarse y Cenital fue retirada. Posteriormente se decidió **no ofrecer vistas predefinidas por ahora**. El código 369B puede mantenerse como infraestructura histórica, pero no deben reaparecer botones/atajos de General, Isométrica, Cenital u otras vistas sin una nueva decisión explícita.

## 369C — regla no destructiva
La cámara contextual/inspección nunca redistribuye mobiliario ni modifica el layout. Una versión temprana produjo una regresión destructiva sobre `Prototype_Restaurant.unity`; 369C3 fijó que la cámara solo observa/transiciona y que el layout pertenece al Modo Edición canónico.

## Protección de UI
El input de cámara solo debe actuar cuando procede del Game View y respetar UI bajo puntero/bordes. Scroll o movimiento sobre herramientas/paneles no debe disparar zoom o edge-pan accidental.

---

## SOURCE: docs/30_UI_UX/UI_UX_DEFINITIVE.md

Category: CANONICAL

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

## Principios
UI contextual y progresiva, PC como referencia. Presentation lee snapshots y emite comandos: no duplica lógica ni se convierte en autoridad de dominio. La cámara ayuda a comprender el restaurante y nunca se convierte en protagonista de la experiencia.

---

## SOURCE: docs/40_TESTING/ACCEPTANCE_AND_VALIDATION.md

Category: CANONICAL

# Bistro Builder — aceptación y validación

## Definición de PASS
Un sistema no queda cerrado solo porque compile. El cierre exige evidencia proporcional al riesgo y al alcance real.

## Gates mínimos
- compilación Unity limpia;
- instalación idempotente o no destructiva;
- validador estructural sin errores;
- autotest/self-test sin fallos;
- prueba funcional sobre la escena o flujo real;
- Save/Load cuando el sistema persista estado;
- Console final sin Error, Exception ni Assert inesperados;
- auditoría de autoridad: ninguna segunda fuente de verdad;
- regresiones relevantes de sistemas dependientes.

## Sistemas visuales/espaciales
Además de métricas, requieren evidencia visual o prueba jugable. No se acepta PASS basado únicamente en contadores automáticos cuando el defecto puede ser perceptivo o de interacción.

## Rendimiento
Las operaciones frecuentes del jugador deben evitar bloqueos perceptibles. Edición, selección, mover/eliminar y confirmaciones deben probarse en condiciones representativas, no solo en escenas mínimas.

## Persistencia
Round-trip, carga cruzada cuando corresponda, repetición y estados incómodos. No duplicar IDs, agentes, grants, estructura ni dinero tras Load.

## Queen / pruebas destructivas
Los bloques críticos deben incluir escenarios integrados que intenten romper invariantes, con rollback seguro cuando la prueba muta datos/escena.

## Cierre documental
Al declarar un bloque COMPLETO/VALIDADO/CERRADO, actualizar `00_PRODUCT/ROADMAP.md`, la documentación del sistema y la evidencia de validación en el mismo cambio.

---

## SOURCE: docs/40_TESTING/DOCUMENTATION_MIGRATION_AUDIT_20260912.md

Category: CANONICAL

# Auditoría — migración documental canónica 12/09/2026

## Resultado
**PASS documental** para la consolidación realizada en `docs/canonical-knowledge-20260912`.

## Fuentes incorporadas
- documentación Markdown/TXT técnica ya existente en el repositorio;
- 7 DOCX fundacionales convertidos a Markdown histórico sin modificar los originales;
- decisiones recuperadas de chats de producto/sistemas que no tenían artefacto documental propio;
- estado de ramas y código actual para distinguir infraestructura existente de UX vigente.

## Contradicciones reconciliadas
- **Horarios:** documentos intermedios `pendiente` quedan históricos frente al cierre posterior.
- **Cámara 369B:** vistas predefinidas implementadas históricamente, pero retiradas de la UX final actual.
- **Bloque 18:** core/arquitectura V1 cerrados; playtests posteriores abren hardening de Construction Authoring/UX sin rehacer el núcleo.
- **Clima:** implementación V1 existente, pero cierre global no se declara hasta validación final.
- **BBSIS/Interaction/Animation:** diseño/autoridad cerrados; fixes posteriores se clasifican como hardening, no rediseño.

## Comprobaciones
- `git diff --check`: PASS.
- Markdown auditado: **43 archivos** bajo `docs/`.
- Enlaces relativos rotos: **0**.
- Código/escenas modificados por esta migración: **0**.
- Fuentes DOCX originales eliminadas/modificadas: **0**.
- Integración conflictiva `playtest/all-current-20260912` modificada: **0**.

## Criterio de uso
A partir de esta migración, agentes y desarrolladores deben arrancar por `AGENTS.md` + `docs/README.md`. Los documentos históricos sirven para trazabilidad, no para contradecir el Decision Register o los documentos canónicos.

---

## SOURCE: docs/90_DECISIONS/DECISION_REGISTER.md

Category: CANONICAL

# Bistro Builder — registro de decisiones vinculantes

**Regla:** una decisión explícita posterior prevalece sobre propuestas anteriores. `VIGENTE` = obligatoria; `SUPERADA` = conservar solo como historia.

| ID | Estado | Decisión |
|---|---|---|
| D-001 | VIGENTE | Arquitectura modular, data-driven, validable; evitar hardcode por asset. |
| D-002 | VIGENTE | Modo Edición solo fuera de servicio y sin obreros simulados. |
| D-003 | VIGENTE | No simular agua, extracción, gas ni ventilación como gameplay. |
| D-004 | VIGENTE | Un único almacén jugable; FEFO; lotes no gestionados manualmente. |
| D-005 | VIGENTE | Espera general virtual; espera física solo en barra cuando corresponda. |
| D-006 | VIGENTE | Cocina usa estados Fluida/Cargada/Saturada/Bloqueada y máximo 3 comandas priorizadas. |
| D-007 | VIGENTE | Camareros por zonas con asignación automática por cercanía/carga/prioridad; jefe de sala opcional. |
| D-008 | VIGENTE | BBSIS es única autoridad espacial; Render no equivale a Spatial. |
| D-009 | VIGENTE | Interaction & Reservation solo gobierna derechos lógicos; Wait Ticket únicamente para colas semánticas reales. |
| D-010 | VIGENTE | Navigation gobierna rutas/circulación; Animation solo representa. |
| D-011 | VIGENTE | Character Animation V1 está integrado/cerrado; futuras ampliaciones son V2/hardening. |
| D-012 | VIGENTE | BBSIS V1 está cerrado; no reabrir salvo regresión real. |
| D-013 | VIGENTE | UI de Servicio: navegación horizontal superior; Actividad izquierda; contexto derecha; acciones abajo. |
| D-014 | VIGENTE | `Caja` = dinero del servicio; `Satisfacción` = satisfacción del servicio. |
| D-015 | VIGENTE | No mostrar/reservar vistas predefinidas de cámara en UI final. |
| D-016 | SUPERADA | 369B exponía presets General/Isométrica; ya no forman parte de la experiencia final. |
| D-017 | VIGENTE | WASD queda reservado al control de cámara. |
| D-018 | VIGENTE | Clima sin dirección ni subtipos de lluvia/nieve/viento; condición exterior uniforme. |
| D-019 | VIGENTE | Interior confortable; sin simulación térmica interior extrema. |
| D-020 | VIGENTE | Todos los cambios de sistemas deben integrarse/subirse a su rama correspondiente. |
| D-021 | VIGENTE | Cada commit solicitado al usuario debe llevar Summary/Description exactos proporcionados por el asistente. |
| D-022 | VIGENTE | BBPLFS es sistema transversal activo; no debe operar como herramienta aislada del proyecto. |
| D-023 | VIGENTE | Feedback universal de edición: preview/snapping/materialización/validez/Undo-Redo, abstracto y no destructivo. |
| D-024 | VIGENTE | La presentación visual base es común a los locales salvo excepción explícita. |
| D-025 | VIGENTE | Los PASS visuales/espaciales requieren evidencia visual/funcional, no solo métricas. |
| D-026 | VIGENTE | La persistencia es universal/versionada; no crear guardados paralelos para un mismo dominio. |

---

## SOURCE: docs/90_DECISIONS/OPEN_QUESTIONS_AND_LEGACY_IDEAS.md

Category: CANONICAL

# Bistro Builder — ideas históricas no canónicas / preguntas abiertas

Estas ideas aparecen en documentación fundacional o chats, pero **no deben tratarse como requisito vigente** salvo promoción explícita al PRD/Decision Register.

## Identidad y nueva partida
- Crear avatar del jugador con nombre, país, físico y vestimenta.
- Decidir compra vs. alquiler del local y cuándo elegir especialidad gastronómica.
- Mostrar tamaño/precio/mensualidad de locales candidatos con mayor profundidad que el flujo V1 actual.

## Carta y restaurante
- Diseños de portada de carta seleccionables.
- Popularidad explícita de cada plato como dato jugable separado.
- Especialización culinaria formal del restaurante y bonus específicos por especialidad del cocinero.
- Baños detallados por género/WC/lavabos como sistema de construcción/operación.

## Servicio ampliado
- Propinas con lógica propia.
- Pedidos online/delivery: aceptar/rechazar/pausar, prioridad sala-online, empaquetado y repartidores.
- Menú del día: primero, segundo, postre, bebida, precio fijo y cupo diario.
- Reputación online separada para delivery.

## Personal/marketing históricos
- Negociación o franja salarial esperada por candidato más profunda.
- Que horarios excesivos provoquen petición automática de aumento salarial.
- Campañas específicas TV/prensa/radio/RRSS con duración y atracción porcentual explícita.

## Regla
La presencia de código experimental o de un DOCX antiguo no promueve automáticamente estas ideas a alcance V1. Para activarlas, registrar una nueva decisión y definir autoridad, UX, persistencia, economía y criterios de aceptación.

---

## SOURCE: Assets/BistroBuilder/UI/Iconography/README.md

Category: SUPPORTING

# Bistro Builder — Iconografía 21B

Sistema canónico y data-driven para implementar la propuesta de iconografía de Bistro Builder en Unity.

## Estado visual

Cada icono usa una única fuente gráfica y cuatro estados de presentación:

- **Normal**: neutro claro.
- **Hover**: mayor luminosidad, borde reforzado, escala `1.055` y elevación visual de `2 px`.
- **Seleccionado**: dorado Bistro Builder, borde dorado y subrayado inferior.
- **Desactivado**: contraste reducido y sin interacción.

Tamaños canónicos: **16 / 24 / 32 / 48 px**.

El color semántico se reserva para estado o énfasis: positivo, atención, crítico, información y acento.

## Instalación en Unity

1. Cambiar a la rama `feature/21b-iconography-system`.
2. Abrir el proyecto y esperar a que Unity compile.
3. Ejecutar `Bistro Builder > UI > Iconografía > Instalar o actualizar`.
4. El instalador descarga las fuentes SVG fijadas a **Lucide 1.45.0**, las guarda en `Assets/BistroBuilder/UI/Iconography/Icons/` y reconstruye `Assets/Resources/BistroBuilder/UI/BBIconCatalog.asset`.
5. Ejecutar `Bistro Builder > UI > Iconografía > Validar catálogo`.
6. El resultado esperado es `VALIDATION PASS` y cero entradas sin sprite.

El instalador es idempotente: puede ejecutarse de nuevo sin duplicar assets ni entradas.

## Uso runtime

Añadir `BBIconButton` al control uGUI que deba representar un icono. Asignar:

- `Icon Image`: `Image` que muestra el sprite.
- `Background Image`: superficie del control, si existe.
- `Border Image`: borde independiente, si existe.
- `Selection Underline`: línea inferior de selección, si existe.
- `Canvas Group`: opcional.
- `Icon Id`: concepto canónico que representa el control.

`BBIconButton` resuelve el sprite desde `BBIconCatalog`; ningún panel debe hardcodear rutas de fichero.

Para iconos de estado, activar `Use Semantic Color`. Para navegación y objetos normales debe permanecer desactivado.

Código de ejemplo:

```csharp
iconButton.Configure(BBIconId.NavActivity);
iconButton.SetSelected(true);
iconButton.SetInteractable(true);
```

## Familias incluidas

- Navegación principal
- Áreas y objetos
- Acciones
- Estados
- Clientes y reservas
- Comida y bebida
- Economía
- Direccionales y generales
- Indicadores en escena

La misma forma se reutiliza cuando el significado es el mismo. Por ejemplo, `users`, `calendar-days`, `hourglass` o `sparkles` pueden tener varios usos semánticos sin duplicar el SVG.

## Fuente gráfica

Las fuentes iniciales se sincronizan desde **Lucide 1.45.0**. Lucide se utiliza como base coherente para la primera implementación porque mantiene una gramática lineal consistente y permite sustituir cualquier icono posteriormente sin cambiar los contratos públicos `BBIconId`.

El contrato del juego es `BBIconId`, no Lucide. Una futura familia dibujada específicamente para Bistro Builder puede reemplazar sprites dentro del catálogo sin modificar gameplay ni las vistas consumidoras.

Consulta `THIRD_PARTY_NOTICES.md` para atribución y licencia.

---

## SOURCE: docs/BarraSuperiorIconos.md

Category: SUPPORTING

# Barra superior del juego

La barra usa el catálogo SVG y los efectos de `feature/21b-iconography-system` (`34694c7`). Incluye Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing y Reputación, conectados a sus pantallas existentes. La selección usa dorado, subrayado y fondo; al pasar el ratón los iconos aumentan y se elevan suavemente.

El nombre del restaurante, el estado del servicio, el calendario y la hora provienen de la partida. El menú de opciones y el desplegable del restaurante permiten entrar en edición, abrir Progreso, Comandas o Cocina, alternar pantalla completa y cerrar paneles. El menú bloquea la interacción con la construcción mientras está abierto.

La prueba `BistroBuilderTopNavigationPlayTest.RunBatch` verifica catálogo, sprites, animación al pasar el ratón, selección, navegación real entre Personal e Inventario, regreso a Actividad, menú de opciones y persistencia de los textos tras reconstruir la barra. Resultado: PASS. Captura de comprobación: `Logs/TopNavigation1920.png`.

Ejecutable: `Builds/Windows/BistroBuilder_Edicion/BistroBuilder.exe`. El lanzador `Jugar_Pantalla_Completa.cmd` abre la versión de Windows a pantalla completa sin bordes.

---

## SOURCE: docs/BBPLFS_V1_DESIGN.md

Category: SUPPORTING

# BB Procedural Layout & Furnishing System (BBPLFS)

Status: V1.0 DESIGN CLOSED / READY FOR UNITY IMPLEMENTATION
Branch: `feature/bbplfs-v1`

## Permanent principles
- BBPLFS assists the existing classic Edit/Build Mode; it never replaces it.
- Global Understanding + Selective Generation: the full premises may be understood, but only the explicit Design Scope may be generated or modified.
- Manual and procedural work can be mixed in any order.
- Once a generated result is accepted, it becomes normal editable content; it is not locked into a procedural mode.
- Tool-First: AI interprets intent and context; deterministic tools generate and validate layouts whenever possible.
- BBPLFS does not duplicate BBSIS, Navigation, Interaction & Reservation, Economy, or Edit Mode authority.

## Block 1 — System contract
BBPLFS is responsible for:
- understanding the purchased/rented premises;
- interpreting the player's requested scope and design intent;
- selecting compatible furnishing/equipment candidates;
- generating, comparing and optimizing layout alternatives;
- requesting spatial validation from BBSIS;
- requesting circulation evaluation from Navigation;
- delivering previews/results to Edit Mode for accept/modify/cancel.

BBPLFS is not responsible for:
- final spatial validity;
- pathfinding or crowd simulation;
- logical reservations/interactions;
- authoritative prices or financial transactions;
- materializing construction outside Edit Mode;
- modifying areas outside the active Design Scope.
## Block 2 — Premises Model
`PremisesModel` is a lightweight working representation of the acquired premises.
It references existing authoritative data instead of duplicating it.

It contains:
- premises identity and revision;
- detected spaces/rooms and their usable contours;
- walls, openings, doors, relevant windows, columns and fixed obstacles;
- connections between spaces and external/internal access points;
- existing objects and their editability/lock state;
- derived potentially usable areas for cheap filtering;
- semantic observations and confidence where interpretation is not structural fact.

`PotentiallyUsable` is never equivalent to BBSIS validity.

`DesignScope` is separate from `PremisesModel` and defines exactly what one BBPLFS operation may touch:
- selection;
- zone;
- room;
- multiple rooms;
- whole premises.

The scope also records objects to preserve, objects that may be moved, and objects that are untouchable.
Relevant Edit Mode structural changes invalidate only the affected premises data where practical.

## Block 3 — AI + LayoutBrief
AI is part of BBPLFS V1 from day one.
Its role is semantic interpretation, not arbitrary spatial placement.
AI may:
- classify the requested function and style;
- interpret natural-language priorities, capacity wishes and restrictions;
- use the Premises Model to explain suitable or unsuitable choices;
- translate the request into a validated `LayoutBrief`.

AI may not:
- emit authoritative coordinates/rotations;
- declare BBSIS or Navigation validity;
- expand the Design Scope without explicit player action;
- assume demolition, construction or relocation of locked/fixed elements;
- silently relax a hard player constraint.

### LayoutBrief — minimum contract
- `ScopeRef` — exact Design Scope to operate on.
- `SpaceFunction` — dining, kitchen, bar, waiting, support, etc.
- `GoalProfile` — Balanced, MaxCapacity, MaxComfort, ServiceEfficient or Premium.
- `CapacityTarget` — preferred value/range when relevant.
- `StyleTags` — compact semantic style request.
- `BudgetLimitRef` — optional authoritative budget/cost context reference.
- `PreserveExisting` — what existing work must remain.
- `RequiredElements` — mandatory functional elements requested by the player.
- `ForbiddenElements` — explicitly excluded elements.
- `HardConstraints` — requirements that may not be relaxed.
- `SoftPreferences` — preferences the scorer may trade off.

The brief contains intent, not placement instructions.
### Interpretation rules
1. Explicit player input overrides AI inference.
2. Structural facts from the Premises Model override AI assumptions.
3. Missing non-critical preferences use deterministic project defaults.
4. Missing critical information is requested only when it changes the meaning or feasibility of the operation.
5. AI uncertainty is never converted into a hard constraint.
6. If the requested result is infeasible, BBPLFS reports the conflict and may offer feasible alternatives; it does not silently violate requirements.
7. The same `LayoutBrief` can also be produced by normal UI controls, so layout generation does not depend on free-text AI being available.

### Examples
`"Comedor contemporáneo para 40 personas con buena circulación"`
→ scope selected by player; function Dining; capacity 40; style Contemporary; circulation/service preference high; remaining fields defaulted.

`"Completa esta sala pero no muevas las mesas que ya puse"`
→ current scope; preserve existing tables; generator may only fill available remainder.

`"Hazme todo el restaurante"`
→ valid only when the player explicitly selects/authorizes whole-premises scope; otherwise the AI must not expand scope.

## Closed decisions so far
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Next design block: Asset Layout Profiles + Furnishing Sets.

## Block 4 — Asset Layout Profiles
`AssetLayoutProfile` is the small BBPLFS-facing description of an asset. It does not duplicate full asset, BBSIS or Economy data.

Minimum fields:
- `AssetRef` — canonical catalog asset.
- `LayoutRole` — table, seat, counter, appliance, storage, decor, etc.
- `LayoutFamily` — interchangeable family used to reduce search space.
- `FunctionTags` — functions this asset can fulfil.
- `StyleTags` — compact visual/style classification.
- `QualityTier` — coarse quality/premium level where relevant.
- `PlacementType` — floor, wall, support surface or other supported placement.
- `AllowedOrientations` — only when the asset is not freely rotatable for layout purposes.
- `CompatibilityTags` — semantic compatibility with set slots/other assets.
- `FastFootprintRef` — revisioned derived footprint/envelope used only for cheap candidate generation.
- `BBSISDescriptorRef` — authoritative spatial-validation reference.
- `CostRef` — authoritative economy/catalog price reference.

Rules:
- Asset dimensions, ports, Work Edges, Seat Bays, sweeps and true spatial validity remain authoritative outside BBPLFS.
- `FastFootprintRef` is disposable cache data; if stale, it is rebuilt from canonical data.
- Visual variants that behave identically may share one layout family/profile and differ only at concrete asset selection.
- Assets4All/catalog authoring should populate as much of this profile automatically as reliable metadata permits; uncertain semantic tags remain reviewable rather than silently trusted.
## Block 5 — Furnishing Sets
A `FurnishingSet` is a functional recipe, not a prefab and not a permanent ownership container.

It defines:
- `SetRole` — functional purpose, e.g. DiningSet4, BarRun, PrepStation.
- required slots and quantities;
- optional slots;
- compatibility requirements for each slot;
- simple layout relationships needed to generate the set: around, aligned, attached, repeated or adjacent;
- permitted arrangement families when the function requires them.

Example:
`DiningSet4` = 1 dining table + 4 compatible dining seats arranged around it.

Rules:
- Sets describe what must exist together; BBSIS decides whether the concrete arrangement is spatially usable.
- Sets may be partially satisfied by existing manual objects inside the Design Scope.
- Locked/manual objects can therefore become fixed members of a generated set without being recreated.
- A set may substitute compatible concrete assets without changing its functional meaning.
- The generator first works with a small compatible candidate pool/layout families, then resolves concrete assets before final BBSIS/Navigation validation.
- BBPLFS must never enumerate the entire catalog combinatorially when equivalent families can be collapsed.
- Once the player accepts the result, set membership is not required to keep the objects editable; normal Edit Mode remains authoritative for subsequent manual editing.

## Asset matching rule
Concrete asset selection uses deterministic filtering first:
1. required function/slot compatibility;
2. availability and scope restrictions;
3. hard player requirements;
4. footprint/layout compatibility;
5. style, quality, cost and soft preferences for ranking.

AI may translate player language into tags/preferences, but it may not bypass these filters or invent catalog assets.
## Current closure state
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Block 4 Asset Layout Profiles: CLOSED.
- Block 5 Furnishing Sets: CLOSED.
- Next design block: Layout Generator — candidate positions, room patterns, irregular geometry and candidate generation strategy.

## Block 6 — Layout Generator

The V1 generator uses a staged deterministic pipeline, not free placement by AI.

### 6.1 Input
The generator receives:
- `PremisesModel`;
- active `DesignScope`;
- validated `LayoutBrief`;
- compatible `FurnishingSets` and asset families;
- preserved/locked existing content.

### 6.2 Placement domain
The generator derives only plausible placement domains inside the selected scope.
Sources include:
- usable floor regions;
- wall bands;
- corners;
- room axes;
- adaptive grids;
- existing alignment lines;
- anchors created by fixed/manual content;
- functional adjacency zones.

The domain is sparse and semantic: BBPLFS must not brute-force every coordinate of the room.
### 6.3 Candidate generation
For each furnishing module, BBPLFS creates a bounded set of candidate poses using:
- wall-aligned placement;
- row/column patterns;
- staggered patterns when useful;
- center/axis placement;
- corner placement;
- adjacency to compatible modules;
- continuation of manually established patterns;
- a coarse adaptive grid for free placement.

Only meaningful orientations from the asset/family profile are generated.
Candidates failing cheap containment, boundary or obvious-overlap tests are discarded immediately.

### 6.4 Irregular geometry
Irregular/concave rooms are supported in V1.
BBPLFS uses polygon containment plus configuration-space style exclusion for fast geometric pruning.
No-Fit Polygon / Minkowski-style techniques may be used internally where they reduce repeated overlap tests, but they are geometry tools, not the global layout solver.

### 6.5 Layout construction
Layouts are assembled from candidates incrementally.
The default search strategy is a bounded heuristic/beam search with deterministic ordering and seeded tie-breaking.
It prioritizes the most constrained or functionally important modules first, then expands the best partial layouts.
Branch-and-bound pruning removes partial layouts that cannot beat current feasible candidates or cannot satisfy hard targets.
CP-SAT is retained as a targeted solver option for dense discrete subproblems, not as the mandatory engine for every room.
It is especially suitable when candidate positions are already discrete and many optional rectangular placements must be selected without overlap.

### 6.6 Validation ladder
Generation uses three levels:
1. cheap BBPLFS geometric precheck;
2. BBSIS validation for shortlisted layouts;
3. Navigation evaluation for layouts that pass BBSIS.

A BBPLFS precheck never means spatial validity.
BBSIS remains the authority for Seat Bays, Work Edges, Ports, Sweeps, doors, envelopes and spatial gates.
Navigation remains the authority for reachability, routes, circulation and traffic quality.

### 6.7 Candidate budget
Search is explicitly bounded by time/work budgets.
BBPLFS returns the best valid layouts found rather than searching for a mathematically proven global optimum.
The generator must degrade gracefully on large/complex rooms by reducing candidate density and search breadth, never by relaxing hard constraints.

### 6.8 Determinism
Same premises revision + same brief + same catalog revision + same seed must reproduce the same candidate ordering and result set.
Randomized exploration is allowed only through an explicit stored seed.
This makes previews, saves, testing and debugging reproducible.
### 6.9 Output
The generator returns a small shortlist of valid candidate layouts, normally 3–5 when enough distinct solutions exist.
Near-duplicates are removed using simple structural differences such as module count, dominant orientation, aisle structure and zone occupancy.
It is valid to return fewer candidates when the room or hard constraints do not support meaningful alternatives.

Generated content never extends beyond `DesignScope`.
Existing manual content marked preserve/locked is treated as part of the environment, not regenerated.

### V1 generator decisions
- No AI coordinate placement.
- No exhaustive continuous search.
- No single packing algorithm as the whole solution.
- Semantic sparse candidate generation first.
- Cheap geometry for pruning only.
- Bounded heuristic/beam search is the default layout builder.
- Branch-and-bound pruning is used where useful.
- CP-SAT is optional for appropriate discrete subproblems.
- NFP/Minkowski-style geometry is optional for irregular-shape pruning.
- BBSIS and Navigation validate shortlisted layouts through their own authority.
- Search is reproducible and time/work bounded.

## Closed decisions so far
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Block 4 Asset Layout Profiles: CLOSED.
- Block 5 Furnishing Sets: CLOSED.
- Block 6 Layout Generator: CLOSED.
## Block 7 — Constraint Model, Validation, Scoring and Optimization

BBPLFS separates non-negotiable validity from preferences.

### 7.1 Hard constraints
A candidate is rejected if any hard constraint fails.
Sources are:
- explicit player requirements in `LayoutBrief`;
- active `DesignScope` and preserve/lock rules;
- required furnishing-set composition;
- catalog/asset compatibility and availability;
- BBSIS authoritative spatial validation;
- Navigation authoritative reachability/critical circulation requirements;
- authoritative budget ceiling when the player defines one as mandatory.

Hard constraints are never converted into penalties and are never relaxed to obtain a higher score.
Each rejection keeps machine-readable reason codes so the UI/AI can explain why a request is infeasible.

### 7.2 Soft objectives
Valid candidates are compared using a small normalized objective vector:
- `CapacityScore`;
- `ComfortScore`;
- `ServiceEfficiencyScore`;
- `CirculationQualityScore`;
- `AestheticCoherenceScore`;
- `CostFitnessScore`.

No additional objective is added unless it changes player-visible decisions.

### 7.3 Metric authority
BBPLFS owns only the comparison formula.
- Capacity comes from the realized functional layout.
- Comfort uses BBPLFS layout-level spacing/composition metrics plus BBSIS results where relevant.
- Service efficiency consumes Navigation route/service-cost metrics and layout structure.
- Circulation quality consumes Navigation results; BBPLFS does not recreate pathfinding.
- Aesthetic coherence uses deterministic layout/style rules: alignment, repetition, spacing consistency, family/style compatibility and composition balance.
- Cost fitness uses authoritative catalog/Economy values; BBPLFS does not own prices or transactions.

### 7.4 Validation ladder
For each promising candidate:
1. BBPLFS cheap geometry/semantic checks;
2. BBSIS validates true spatial usability;
3. Navigation validates required access and evaluates circulation;
4. hard budget/brief constraints are checked against authoritative values;
5. only fully valid candidates enter scoring.

External validators return structured metrics and reason codes, not placement decisions.
BBPLFS may use those results to choose another candidate or request a new generation pass.

### 7.5 Scoring
Each soft objective is normalized to a stable 0–1 range using project tuning data.
The active `GoalProfile` supplies weights and optional minimum quality floors.
Final ranking is a weighted score only after all hard constraints and profile floors pass.

This keeps the five player modes as profiles of one optimizer rather than separate algorithms.

### 7.6 Goal profiles
V1 profiles:
- `Balanced` — no single objective dominates.
- `MaxCapacity` — favors capacity while preserving mandatory comfort/circulation floors.
- `MaxComfort` — favors spacing and circulation over seat count.
- `ServiceEfficient` — favors service routes and circulation quality.
- `Premium` — favors comfort, aesthetic coherence and higher-quality compatible assets; it never means spending money for its own sake.

Exact weights are tuning data, not hardcoded architecture.
They can be adjusted without changing the solver.

### 7.7 Optimization loop
The optimizer:
1. ranks valid layouts under the selected profile;
2. keeps a small elite set;
3. applies bounded local improvements such as small translations, orientation swaps, equivalent-module substitutions or row-spacing adjustments;
4. revalidates any change that can affect BBSIS or Navigation;
5. stops when the work budget is exhausted or improvement becomes negligible.

Local refinement never changes the requested Design Scope or hard requirements.

### 7.8 Alternatives and diversity
The final shortlist is selected from high-scoring valid layouts with a minimum structural difference.
Difference may use module distribution, dominant orientation, aisle structure, zone occupancy and capacity.
A slightly lower-scoring candidate may be retained when it provides a meaningfully different player choice.

BBPLFS does not maintain a complex Pareto subsystem in V1.

### 7.9 Infeasible requests
If no valid layout exists, BBPLFS returns:
- the failed hard constraints;
- the best near-feasible diagnostic candidates when useful;
- deterministic relaxation suggestions ordered by smallest impact.

Relaxations are proposals only. The player must explicitly change the brief/scope before regeneration.
AI may phrase the explanation but cannot silently apply the relaxation.

### V1 decisions
- Hard validity and soft scoring are strictly separated.
- BBSIS and Navigation remain authoritative validators.
- One normalized objective vector powers all player modes.
- Goal-profile weights are data-driven tuning values.
- Weighted ranking is used only after validity and minimum floors pass.
- Optimization uses bounded local refinement, not an unbounded metaheuristic.
- Diverse high-quality alternatives are retained without a heavy Pareto architecture.
- Failure is explainable through structured reason codes and explicit relaxation proposals.

## Closed decisions so far
- Block 1 System Contract: CLOSED.
- Block 2 Premises Model: CLOSED.
- Block 3 AI + LayoutBrief: CLOSED.
- Block 4 Asset Layout Profiles: CLOSED.
- Block 5 Furnishing Sets: CLOSED.
- Block 6 Layout Generator: CLOSED.
- Block 7 Constraint Model + Validation/Scoring/Optimization: CLOSED.

## Macro-block A — Incremental re-layout + mixed manual/procedural editing

BBPLFS never owns a room after generation. Accepted results immediately become normal Edit Mode content.

### Re-layout states
Objects in the active scope may be:
- `Free` — BBPLFS may move/replace them.
- `Preserve` — keep the current object and position unless the player changes this rule.
- `Locked` — untouchable by BBPLFS.

### Incremental repair
When geometry or content changes, BBPLFS first repairs only the affected neighborhood.
It identifies impacted generated/manual relationships, reopens only relevant placements and tries local reinsertion/re-spacing before any full-room regeneration.
A full regeneration is allowed only when local repair cannot satisfy the active hard constraints or when the player explicitly requests it.

### Mixed workflow
Supported V1 flows include:
- manual room + BBPLFS completion;
- BBPLFS room + later manual edits;
- manual kitchen + BBPLFS dining room;
- BBPLFS kitchen + manual dining room;
- partial-zone optimization inside an otherwise manual room;
- multi-room generation only when explicitly selected.
### Edit Mode transaction
BBPLFS output is always a proposal/preview owned by the existing Edit Mode transaction flow.
The player can inspect alternatives, accept one, modify it manually before confirmation, or cancel without changing the authoritative scene.
Construction/destruction needed by a proposal is materialized only through Edit Mode.

### UX contract
Minimum player flow:
1. select scope;
2. choose BBPLFS action or describe intent to AI;
3. receive validated alternatives;
4. compare capacity/cost/key trade-offs;
5. preview one alternative in the normal scene;
6. accept, modify manually, regenerate, or cancel.

The system must not force the player through AI text entry; the same operations are reachable through normal controls.

### Preservation rule
Manual work is treated as first-class input, not as noise to overwrite.
BBPLFS should maximize retained valid work during incremental changes, but preservation is secondary to explicit hard constraints.
If preservation makes the request infeasible, the system reports exactly what is blocking it and asks the player to change the scope/rule rather than moving it silently.

Macro-block A: CLOSED.
## Macro-block B — Auto Furnish, reusable layouts, scale and persistence

### Auto Furnish
`Auto Furnish` is the standard orchestration of the already-defined pipeline:
Premises/Scope → LayoutBrief → compatible sets/assets → candidate generation → BBSIS → Navigation → scoring → alternatives.
It is not a separate generator and does not bypass validation.

### Reusable parametric layouts
V1 supports `LayoutTemplate` data for repeatable design intent without storing room-specific coordinates.
A template may define:
- supported space function;
- required/optional furnishing sets;
- preferred arrangement families;
- relative anchors/adjacencies;
- capacity or density ranges;
- style/quality defaults;
- default goal profile and soft preferences.

Templates adapt to the current room and catalog through the normal generator.
A template never guarantees that a layout is valid in another premises.

### Multi-room / whole-premises generation
The same pipeline can run over multiple explicitly authorized spaces.
Each space keeps its own geometry and constraints while the generator may optimize shared goals such as total capacity, cost or service efficiency.
Unselected rooms remain read-only context.
### Change tracking and invalidation
BBPLFS keys derived data by premises revision, scope revision, catalog/profile revision and relevant tuning revision.
Structural edits invalidate only affected spatial data where practical.
Catalog/profile changes invalidate affected candidate pools/templates, not unrelated premises understanding.
Accepted scene content remains authoritative even if BBPLFS caches are discarded.

### Performance and graceful degradation
Generation has configurable work budgets by operation size/quality target.
To remain responsive, BBPLFS may reduce candidate density, beam width, refinement passes or number of alternatives.
It may never reduce BBSIS/Navigation validity requirements or silently relax hard constraints.
Heavy recomputation should reuse cached footprints, room decomposition, compatibility pools and unchanged validation results when their revisions still match.

### Failure/fallback behavior
If a good complete layout cannot be produced, BBPLFS may return:
- fewer valid alternatives;
- a valid partial completion when the brief permits it;
- an infeasibility report with explicit player-approved relaxation options.
It must not fill the room with knowingly invalid content merely to return a result.

### Persistence
Persist only durable intent/data that is useful after reload:
- accepted scene objects through the normal project save system;
- player-created `LayoutTemplate`/preset data when saved;
- explicit preserve/lock metadata where owned by Edit Mode/shared authoring data;
- stored seed/brief only when needed to reproduce a saved procedural operation.
Transient candidate pools, scores and previews are rebuildable caches and need not be authoritative save data.

Macro-block B: CLOSED.
## Macro-block C — V1 architecture audit and closure

### Authority audit
No authority duplication is permitted:
- Edit Mode owns authoritative construction/edit transactions and final materialization.
- BBSIS owns spatial usability and spatial contracts.
- Navigation owns routes, reachability, circulation and traffic evaluation.
- Interaction & Reservation owns logical grants/reservations; BBPLFS only consumes requirements when relevant.
- Catalog/Assets4All owns canonical asset identity/metadata sources; BBPLFS consumes layout profiles.
- Economy owns authoritative prices/budgets/transactions; BBPLFS only evaluates cost references.
- BBPLFS owns only interpretation-to-layout orchestration, candidate generation, layout comparison and optimization within Design Scope.

### Public V1 contracts
Implementation must expose clear equivalents of:
- `PremisesModel` / revisioned premises snapshot;
- `DesignScope`;
- `LayoutBrief`;
- `AssetLayoutProfile`;
- `FurnishingSet`;
- `LayoutTemplate`;
- candidate layout/result model;
- BBSIS validation request/result adapter;
- Navigation evaluation request/result adapter;
- scoring/profile configuration;
- Edit Mode preview/commit/cancel handoff.

Names may change during implementation, but these responsibilities may not collapse across authority boundaries.
### V1 invariants
- No operation writes outside explicit `DesignScope`.
- AI never owns authoritative placement coordinates or validity.
- Accepted results become ordinary editable scene content.
- Manual and procedural editing remain interchangeable.
- Hard constraints are never traded for score.
- BBSIS/Navigation failures cannot be overridden by BBPLFS scoring.
- Same authoritative inputs + seed reproduce the same generator ordering/results within the same algorithm/tuning revision.
- Derived caches are disposable and never replace canonical project data.

### V1 acceptance criteria
V1 is ready to ship only when it can demonstrably:
- analyze purchased/rented premises and build a valid premises snapshot;
- generate only within selected room/zone/multi-room scopes;
- accept UI and natural-language briefs;
- preserve locked/manual work;
- generate multiple useful alternatives for representative dining/kitchen/bar/support cases;
- handle non-rectangular rooms and fixed obstacles;
- reject layouts failing BBSIS or mandatory Navigation checks;
- rank valid layouts under all five goal profiles;
- complete/repair an already partially furnished room without unnecessary full regeneration;
- preview/accept/cancel through normal Edit Mode;
- reload accepted results as ordinary project content;
- fail explainably when the requested layout is infeasible.

### Explicitly outside V1
- AI-generated arbitrary coordinates/scene edits;
- learning player taste through opaque online training;
- automatic structural renovation without explicit Edit Mode authorization;
- replacing BBSIS/Navigation with BBPLFS geometry estimates;
- mathematically proving a global optimum;
- complex Pareto/evolutionary optimizer infrastructure unless later evidence justifies it;
- procedural ownership that prevents manual editing after acceptance.
### Main implementation risks
1. Candidate explosion in dense rooms — control with semantic candidate pools, family collapsing and strict budgets.
2. Expensive repeated BBSIS/Navigation validation — shortlist aggressively and cache revision-safe results.
3. Weak asset metadata — reject/flag incomplete profiles instead of making silent semantic guesses.
4. Procedural edits fighting manual intent — preserve/lock rules and local repair are mandatory.
5. Style scoring becoming subjective/opaque — keep V1 aesthetics based on explicit deterministic composition/style metrics and data-driven tuning.
6. AI hallucinating capabilities or scope — structured `LayoutBrief` validation is mandatory before generation.

### Final simplification audit
Removed from V1 as unnecessary architecture unless implementation evidence later proves a need:
- dedicated Pareto archive;
- evolutionary optimizer as a core subsystem;
- simulated annealing as a named core dependency;
- separate candidate-conflict subsystem exposed as architecture;
- duplicated scene graph authority;
- procedural room ownership;
- solver-backend abstraction layers with no immediate use.

The V1 architecture remains six practical responsibilities:
1. Premises understanding + scope.
2. AI/UI intent → `LayoutBrief`.
3. Asset profiles + furnishing sets/templates.
4. Layout generation and incremental repair.
5. External validation + scoring/optimization.
6. Edit Mode preview/accept/manual continuation.

Macro-block C: CLOSED.

# BBPLFS V1.0 — DESIGN CLOSED / READY FOR UNITY IMPLEMENTATION
All design blocks and the three closure macro-blocks are binding for V1 unless implementation uncovers evidence requiring an explicit design amendment.

---

## SOURCE: docs/CursoresYFocus.md

Category: SUPPORTING

# Cursores, selección y foco

Cinco PNG transparentes de 64 × 64 en `Assets/Resources/BistroBuilder/UI/Cursors`: Normal, Hover, Blocked, Drag y Rotate. Flecha oscura, filo marfil y hoja de olivo; hover verde con halo y bloqueo rojo con símbolo de prohibición. Arrastre usa cuatro direcciones y rotación una flecha curva. Fuente editable: `Tools/BistroBuilder/GenerateCursorAssets.ps1`.

`BistroBuilderPointerFeedback` es el único propietario del cursor. Prioriza los controles de UI; en edición consulta los resultados existentes de colocación, muestra arrastre durante el movimiento, rotación al girar y prohibición al colocar en una posición inválida. No calcula navegación ni vuelve a validar geometría. Los cambios de cursor se envían al sistema solamente al cambiar de estado y se restablecen al perder foco.

`BistroBuilderInteractionSurface` añade borde dorado al pasar el ratón, selección verde y foco azul independiente de la selección. Tab y Mayús+Tab recorren los controles activos e interactuables, excluyendo los lanzadores ocultos. Los iconos de la barra conservan su selección dorada y reciben el mismo foco accesible. Las categorías y herramientas de construcción y las filas de marketing publican su selección real. Las tarjetas del catálogo bloqueadas usan un shader en escala de grises y una etiqueta «No disponible».

En el restaurante, el mobiliario muestra una huella dorada al pasar el ratón y verde al seleccionarlo. La selección de construcción aplica los mismos colores a paredes, aberturas y habitaciones. Los controles de mover y girar presentan sus cursores correspondientes.

Prueba reproducible: Unity batch `-executeMethod BistroBuilderPointerPlayTest.RunBatch`. Verifica importación, transparencia, prioridad de bloqueo, hover, selección independiente del foco, teclado, tarjeta bloqueada y restauración de estados. Resultado en `Logs/PointerFeedbackTest.txt`; captura en `docs/Images/CursoresYFocus.png`.

---

## SOURCE: docs/ModoEdicionConstruccion.md

Category: SUPPORTING

# Modo edición · construcción y assets

Implementación en Unity 6000.3.19f1 basada en las decisiones de «Diseñar UI UX definitiva» y la petición de construir habitaciones con el ratón.

## Diseño aplicado

- Se conserva la cámara isométrica del juego, sin vistas predefinidas ni nuevos atajos sobre WASD, Q/E, R/F, Shift o rueda.
- **Catálogo de artículos vertical y compacto a la izquierda**, inspector a la derecha, restaurante en el centro y acciones de construcción/edición abajo. Al entrar en Modo Edición, el catálogo sustituye temporalmente a Actividad; al salir, Actividad vuelve a ocupar ese lateral. La barra horizontal inferior no se usa como catálogo principal.
- El catálogo tiene estados abierto, compacto y oculto temporalmente; cabecera con búsqueda en tiempo real, filtros y ordenación. Categorías base: Todos, Mesas, Asientos, Barra, Cocina, Almacenamiento, Iluminación, Decoración y Exterior. Puertas, ventanas, paredes y habitaciones permanecen en Construcción.
- Las tarjetas priorizan miniatura, nombre legible, precio y estado. En ancho normal se usan dos columnas; las variantes de color/material se agrupan bajo un mismo artículo. Los bloqueados aparecen desaturados con «No disponible» y motivo; la falta de dinero se comunica como estado económico separado.
- Un clic en una tarjeta activa el ghost/preview para colocar en el restaurante. La colocación puede repetirse hasta cancelar con Esc. Favoritos y Recientes aceleran la reutilización; el scroll es vertical y debe virtualizar tarjetas cuando el volumen de assets lo requiera.
- Mientras el puntero esté sobre el catálogo, la UI bloquea edge-pan, zoom accidental, colocación y selección del mundo. El ghost informa validez, motivo de rechazo y coste; el Draft no descuenta dinero ni hace commit hasta Aplicar.
- El HUD existente muestra presupuesto, coste del borrador calculado por Finance y aforo del registro de asientos.
- Sin tutorial inicial. La barra de estado explica la herramienta y los motivos de rechazo.
- Los cambios pendientes se aplican, descartan o conservan al intentar salir.
- El tema usa los tokens visuales existentes; la fuente de títulos sigue siendo inyectable mediante el tema del proyecto.

## Uso

1. Fuera del servicio, pulsa **Edición** en la barra superior.
2. **Habitación por arrastre**: elige Salón, Cocina, Baño, Barra o Terraza; mantén pulsado, arrastra y suelta. También acepta dos clics en esquinas opuestas.
3. **Pared continua**: arrastra un tramo o marca sus extremos con dos clics. Con dos clics puedes continuar desde el extremo anterior. Escape cancela el gesto.
4. **Módulo de pared**: selecciona 0,5 / 1 / 2 / 4 m, gira con el botón y coloca módulos con clics sucesivos.
5. **Puerta / Ventana**: acerca el cursor a una pared y pulsa. Se crea un hueco geométrico alojado en esa pared y su asset visual.
6. **Seleccionar**: arrastra el centro de la pared para desplazarla o sus extremos para editar uniones. El inspector permite copiar, eliminar y desplazar huecos a lo largo de la pared.
7. **Deshacer / Rehacer**: cada habitación completa es una sola operación. Ctrl+Z / Ctrl+Y actúan sobre arquitectura cuando su herramienta está activa.
8. **Aplicar cambios** confirma a través del coordinador y la autoridad económica. **Descartar** recupera el documento confirmado. **Abrir mobiliario** devuelve el control al sistema existente.

Los recintos contiguos reutilizan paredes coincidentes, también cuando comparten sólo parte de una pared larga. Las aberturas deben caber en su pared, no solaparse y respetar sus límites verticales.

## Assets incluidos

Carpeta: `Assets/Resources/BistroBuilder/Construction/`.

| Tipo | Contenido |
|---|---|
| Kit | `ConstructionAssetKit.asset`, referencias usadas en runtime |
| Paredes | Prefabs de 0,5 / 1 / 2 / 4 m, altura 2,8 m, grosor 0,12 m; meshes propios |
| Puerta | Marco de roble, hoja abierta y tirador; paso libre de colliders |
| Ventana | Marco grafito, montante, alféizar y cristal transparente |
| Materiales URP | Enlucido cálido, caliza, roble, grafito y vidrio azulado |
| Iconos | Selección, pared, módulo, habitación, puerta, ventana y mobiliario; sprites transparentes |

Se pueden regenerar desde **Tools → Bistro Builder → Edit Mode → Create Construction Assets**. El generador conserva los GUID de los assets existentes. Las paredes arbitrarias usan el generador geométrico; los prefabs son las piezas reutilizables del kit.

## Integración

- Se recupera la base Construction Authoring de `feature/18n-construction-authoring-v1` y el HUD V2 de `feature/21a-ui-ux-definitive` en esta carpeta, ampliándolos con arrastre, módulos, assets e interfaz jugable.
- `BistroBuilderArchitecturePlayerTool` queda como adaptador para escenas y llamadas antiguas; no procesa un segundo juego de entradas.
- El bootstrap instala la herramienta y el panel sobre la escena existente, sin serializar un HUD duplicado en `Prototype_Restaurant`.
- La previsualización sólo genera representación visual. Finance, BBSIS, Navigation y el documento persistente cambian a través del commit canónico.
- El flujo inicial utiliza la misma herramienta, reconoce los IDs canónicos `zone.*` y conserva compatibilidad con los IDs de zona antiguos.
- Guardar y cargar usan el documento arquitectónico existente; no se introduce otro formato de partida.
- Un local vacío puede guardarse y cargarse durante el diseño inicial. La demanda utiliza los registros activos de mesas y barra, y no impide persistir un diseño sin aforo. La apertura sigue exigiendo un restaurante operativo.

## Validación reproducible

- `BistroBuilderConstructionAssetInstaller.InstallAndTestBatch`: generación de assets, suite de 32 escenarios y regresión de habitaciones compartidas, historial, aberturas y kit.
- `BistroBuilderConstructionMousePlayTest.RunBatch`: eventos reales de Mouse en Play Mode; arrastre, previsualización, deshacer/rehacer, huecos, módulo, salida protegida y vuelta al mobiliario.
- `BistroBuilderEditBlock18QueenTest.RunFromCommandLine`: regresión del commit financiero, materialización, BBSIS, navegación y guardado/carga.

Los resultados de ejecución se registran en `Logs/Construction*.log` y `Logs/ConstructionMousePlayTest.txt`. El test gráfico guarda `Logs/ConstructionWorkspace.png` cuando hay dispositivo gráfico disponible.

## Resultado verificado · 12 septiembre 2026

- Construction Authoring: **32/32 escenarios, 341 assertions, 0 fallos**.
- Regresión adicional: **PASS** (pared compartida parcial, identidad e historial, huecos solapados, normales del suelo, prefabs, materiales URP e iconos).
- Ratón en Play Mode: **PASS** (arrastre real, una operación de deshacer por habitación, puertas pulsando en la cara del muro, ventanas, módulos, salida protegida, vuelta a mobiliario y vaciado real del local).
- Prueba final ampliada: **PASS**, nueva partida vacía con guardado inicial, carga completa, conservación del local vacío y bloqueo de apertura. Usa un slot libre de diagnóstico y lo elimina al terminar. Registro: `Logs/ConstructionMousePlayFinal.log`.
- Queen Test Block 18: **PASS** (commit financiero, BBSIS, Navigation, Save/Load, bloqueo durante servicio y rollback exacto).
- Queen Test repetido después de la corrección de demanda: **PASS**, `Logs/ConstructionQueenFinal.log`.
- Revisión visual de la interfaz y el kit completada; se corrigieron el sentido de las caras del suelo, la importación de sprites y la altura de los botones.
- Windows Player: **Build PASS**, Unity 6000.3.19f1, 110.876.361 bytes, generado el 12/09/2026 a las 08:25:26 UTC. Las ocho advertencias son campos/eventos sin uso de sistemas existentes.
- Ejecutable: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`. Prueba de arranque del Player: **PASS**, sin excepciones detectadas; detalle en `Logs/ConstructionPlayerSmokeResult.txt`.
- Build final corregida: **PASS**, `Builds/Windows/BistroBuilder_Edicion/BistroBuilder.exe`, 110.876.873 bytes, 12/09/2026 a las 13:34:25 UTC. Ocho advertencias preexistentes de campos/eventos sin uso; cero errores. Registro: `Logs/ConstructionWindowsFinalBuild.log`.
- Lanzador de pantalla completa sin bordes verificado en una pantalla física de **1920 × 1080**.
- Arranque de la build final sin ventana: **PASS**, sin excepciones detectadas; `Logs/ConstructionFinalPlayerSmokeResult.txt`. La comprobación se cierra sin interrumpir la partida visible.

## Ejecutar a pantalla completa

La actualización del 14/09/2026 corrige los bloqueos al eliminar muebles y cargar escenarios. La [medición de rendimiento](RendimientoModoEdicion.md) documenta la corrección y sus pruebas.

Haz doble clic en `Jugar_Pantalla_Completa.cmd`, en la raíz del proyecto. Abre la build Windows en pantalla completa sin bordes y anula el modo ventana que pudiera recordar una prueba anterior. No necesita tener Unity abierto. Las siguientes builds también incluyen este lanzador junto al ejecutable.

La versión final está en `Builds/Windows/BistroBuilder_Edicion`. Para distribuir el juego, copia esa carpeta completa; conserva `BistroBuilder_Data` y las DLL junto al ejecutable. Puedes salir con Alt+F4. Se conserva la build de playtest anterior para no interrumpir una sesión abierta mientras se genera la versión final.

![Modo edición en Unity, con los assets generados](ModoEdicionConstruccion.png)

---

## SOURCE: docs/OpcionesYCarta.md

Category: SUPPORTING

# Opciones y Carta

El selector del restaurante abre las herramientas del local. El engranaje abre una pantalla independiente con Partida, Audio, Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal.

- Guardar y cargar utilizan `BistroBuilderSaveGameService`, con listado asíncrono de partidas. Sobrescribir, cargar y salir requieren confirmación en la interfaz.
- Autosave es opcional, cada cinco minutos reales, y utiliza exclusivamente el espacio 999. Respeta los bloqueos del servicio de persistencia; no sustituye guardados manuales.
- Volumen, pantalla, sincronización vertical, límite de FPS, pausa en Opciones y movimiento reducido se conservan mediante PlayerPrefs. El acceso directo a pantalla completa tiene prioridad sobre el modo de ventana guardado.
- Movimiento reducido elimina el desplazamiento y escalado de los iconos y las pulsaciones animadas. El idioma disponible es español; la pantalla de Controles muestra las acciones actuales.
- Los nuevos SVG de Opciones son assets locales originales registrados en el catálogo 21B. El instalador no intenta descargarlos de Lucide.
- Carta reserva 76 unidades para cada barra y permite desplazamiento vertical cuando falta altura. Los paneles de Actividad y Contexto se ocultan durante la gestión. Horarios permanece accesible desde el menú del restaurante.

Prueba: `BistroBuilderOptionsPlayTest.RunBatch`. Verifica separación de menús, nueve categorías, catálogo de iconos, bloqueo de construcción, ausencia de paneles superpuestos y Carta en 1920×1080 / 1280×720. Genera capturas en `docs/Images` y el resultado en `Logs/OptionsTest.txt`.

---

## SOURCE: docs/RendimientoModoEdicion.md

Category: SUPPORTING

# Corrección de bloqueos al editar y cargar · 14/09/2026

Se reproducía una pausa de unos 30 segundos después de eliminar una silla. La integración de navegación recalculaba todas las conexiones entre entrada, cocina y mesas en el mismo fotograma. Durante una carga repetía ese diagnóstico sobre estados parciales del restaurante.

## Cambio

- Las altas, bajas y movimientos siguen reconstruyendo la geometría de navegación e invalidando rutas. El diagnóstico exhaustivo `EvaluateCirculationHealth` queda disponible como consulta explícita y no se ejecuta automáticamente por cada mueble. Un cambio de topología invalida su informe anterior.
- Las actualizaciones automáticas de BBSIS y navegación se agrupan durante una carga y se publican al terminar, también después de un rollback. Las actualizaciones explícitas de los proveedores de persistencia siguen disponibles.
- La eliminación y creación de muebles al cargar ceden el control al alcanzar 6 ms de trabajo o el límite de objetos por fotograma. Se conservan validación, referencias, cancelación y asociaciones de asientos.

## Medición

Prueba automatizada en Play Mode de Unity sobre `Prototype_Restaurant`: guardado temporal en un slot libre, eliminación mediante el servicio real, observación de los fotogramas posteriores, carga completa y limpieza del slot. No sobrescribe partidas existentes.

| Medida | Antes | Corregido |
|---|---:|---:|
| Mayor fotograma tras borrar una silla | 30.736 ms | 164 ms |
| Duración total de carga | 66.362 ms | 1.085 ms |
| Mayor fotograma durante carga y estabilización | 34.713 ms | 55 ms |
| Diagnósticos completos por eliminación | 1 | 0 |
| Diagnósticos completos por carga | 2 | 0 |

Son mediciones de este equipo en el editor; no garantizan un framerate concreto en otros equipos. El tiempo de compilación e inicio del editor no forma parte de la medición. La prueba verifica además que la silla deja de bloquear el paso, que se recupera su obstáculo al cargar y que vuelven los 38 muebles. Impone límites de 250 ms por fotograma y cinco segundos de carga para detectar regresiones de este bloqueo.

Resultados: `Logs/EditPerformance-baseline.json`, `Logs/EditPerformance-final.json` y `Logs/EditPerformanceBudgeted.log`. El valor `fullHealthMs: -1` indica que la repetición final omite la medición aislada del diagnóstico explícito, que sigue fuera de las operaciones medidas.

Regresión completa del Bloque 18 repetida con estos cambios: **PASS** (Finance, BBSIS, Navigation, Save/Load, restricciones de servicio y rollback). Registro: `Logs/EditPerformanceQueen.log`.

Build Windows: **PASS**, generada el 14/09/2026 a las 06:57:59 UTC; 110.878.409 bytes, cero errores y ocho advertencias preexistentes. Ejecutable actualizado en `Builds/Windows/BistroBuilder_Edicion/BistroBuilder.exe`. Registro: `Logs/EditPerformanceWindowsBuild.log`.

Ejecutar: `BistroBuilderEditPerformanceProbe.RunBatch` con `-bistroPerfFinal` para aplicar los límites de regresión; sin ese argumento realiza la medición inicial del diagnóstico completo.

---

## SOURCE: docs/TipografiaYProfundidad.md

Category: SUPPORTING

# Tipografía y profundidad visual

Implementación de las referencias de tipografía, estados y profundidad del usuario.

## Fuentes

- Recoleta en H1 y H2, encabezados y marca. Archivo suministrado por el usuario: `recoleta.zip`, que contiene `Recoleta-RegularDEMO.otf`; copia de trabajo `Assets/Resources/BistroBuilder/UI/Typography/Recoleta.otf`.
- Esta DEMO sustituye letras acentuadas por el sello del fabricante. Para evitarlo, el atlas de títulos usa sus caracteres ASCII y deriva los restantes a Georgia del sistema (Inter como segundo respaldo). La pantalla inicial IMGUI usa Georgia en encabezados porque no admite respaldo por carácter. La fuente original se conserva intacta; al incorporar la versión completa sin marca DEMO se utiliza el juego de caracteres completo.
- Inter Regular para cuerpo, campos y texto secundario; Inter SemiBold para H3, etiquetas y KPI. Versión 4.1 del [repositorio oficial](https://github.com/rsms/inter/releases/tag/v4.1). Licencia incluida en `Inter-LICENSE.txt`.
- Escala base a 1920 × 1080: H1 34, H2 24, H3 18, cuerpo 15, etiqueta 14, caption 12 y KPI 27. Ajuste acotado para los controles de tamaño fijo. Recoleta no se aplica a tablas o controles funcionales.
- Atlas TMP generados por `BistroBuilderTypographyInstaller.Prepare`, también ejecutado antes de compilar. Los archivos de fuente se conservan junto a los atlas para caracteres dinámicos y la pantalla inicial IMGUI.

## Capas

| Nivel | Color | Uso | Sombra |
|---|---|---|---|
| Fondo | `#1D1B17` | Base de pantalla | Sin sombra |
| Superficie 1 | `#302A22` | Paneles y navegación | Y 2, blur 8, opacidad 8% |
| Superficie 2 | `#53483A` | Tarjetas y contenido interno | Y 4, blur 16, opacidad 10% |
| Superficie 3 | `#DAC8B1` | Menús y elementos flotantes | Y 8, blur 24, opacidad 12% |

Bordes de 1 px: normal 8%, hover claro 16%, selección dorada, atención naranja, crítico rojo y desactivado discontinuo 6%. El foco de teclado permanece azul. Las tarjetas seleccionadas mantienen el relleno verde y adoptan el borde dorado de la nueva referencia.

Sombras realizadas con una malla de caída gradual. Los fondos redondeados y los materiales de vidrio se comparten; no se generan texturas ni se ejecuta una captura de pantalla por frame. El HUD usa la textura opaca de URP y un filtro de nueve muestras. Presets disponibles: ligero 8 px / 70%, medio 16 px / 50%, fuerte 24 px / 35%. El HUD principal usa ligero. Los menús elevados y las pantallas de gestión usan fondos sólidos.

## Estados semánticos

`BistroBuilderStatusBadge` presenta Correcto en verde, Atención en ámbar, Crítico en rojo, Información en azul y Desactivado en gris. Incluye texto y marcador, además del color. La paleta de textos de estado está centralizada en `BistroBuilderUiTokens`.

## Comprobación

`BistroBuilderVisualLanguagePlayTest.RunBatch` verifica las familias reales, shader de HUD, niveles de superficie, navegación a Personal y menú de opciones. Genera `Logs/VisualLanguageTest.txt` y capturas en `docs/Images`: `TipografiaYProfundidad.png`, `UIProfundidadJuego.png`, `UITipografiaPersonal.png`.

---

## SOURCE: AUDITORIA_BLOQUE_5_HORARIOS.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — Bloque 5: Horarios y Turnos

Estado documental: **implementación estática completada / pendiente validación real en Unity**.

## Alcance

El Bloque 5 añade planificación de horarios y turnos sobre el sistema de Personal del Bloque 4. No sustituye StaffService, RestaurantServiceStateService, WaiterTaskCoordinator ni SaveGame.

### 5A — Fundación
- `staff.schedule` V1 como snapshot persistente independiente.
- Turnos por `EmployeeId`, día y servicio gastronómico.
- Perfil canónico de ventanas y horizonte de planificación.
- Motor puro y `BistroBuilderStaffScheduleService` como única autoridad de planificación.

### 5B — Planificación y cobertura
- Asignación y retirada de turnos.
- Reemplazo atómico de plantilla por servicio.
- Copia de planes y autocompletado mínimo.
- Cobertura y coste salarial proyectado.
- La cobertura efectiva solo cuenta empleados activos y actualmente disponibles, igual que el runtime 5C.

### 5C — Integración de servicio
- `BistroBuilderStaffScheduleSessionBridge` filtra la sesión operativa de Personal según el turno.
- Reutiliza los `Waiter` existentes y delega la sesión a 4D.
- No crea agentes ni tareas y no asume autoridad sobre `WaiterTaskCoordinator`.

### 5D — Persistencia
- Sección universal `staff.schedule`, opcional y versionada.
- Prepare 8875, Apply 450 y Finalize 10700.
- `staff.state` se aplica antes (400); `service.runtime` después (500); binding 4D después (550).
- La prevalidación universal de Load comprueba estructura autosuficiente; la integridad cruzada `EmployeeId` se comprueba en Apply contra `staff.state` objetivo.
- Gates: JSON round-trip y Save cruzado A/B.
- El gate de carga cruzada canónico dispone de `.meta` estable y no existe una segunda implementación duplicada.

### 5E — UI
- Fachada y pantalla Presentation no autoritativas.
- Navegación por día y Comida/Cena.
- Asignación de camareros, cobertura, coste, autocompletado y copia de plan.
- Instalador idempotente y gate de frontera arquitectónica.

### 5F — Queen Test
- Preflight read-only.
- Queen Test reversible con slots temporales.
- Plan real → 5C → 4D → `WaiterId` real.
- Servicio Open y checkpoint universal.
- El Load Open solo se ejecuta después de detectar una mutación operativa real.
- Cierre únicamente con tareas agotadas y camareros ligados Idle.
- En Closed se prueba `staff.schedule A → B → Load → A`.
- Rollback integral y borrado de slots tanto en PASS como en fallo recuperable.
- El runner evita dependencias de definite-assignment por short-circuit en la ruta de rollback.
- Gate estático específico para impedir fabricación de `Waiter`, `WaiterTask`, manipulación directa de elegibilidad o serialización paralela al SaveGame universal.

## Gates acumulativos

`BistroBuilderStaffBlock5ReadinessSelfTest` agrupa:
1. 5A fundación.
2. 5B planificación.
3. 5C binding con 4D.
4. 5D JSON.
5. 5D Save cruzado.
6. 5E frontera Presentation.
7. 5F arquitectura Queen.

## Genealogía

La cadena canónica se ha reconciliado contra el HEAD actual de 4G y cada tramo queda con **0 commits por detrás** de su base inmediata:

`feature/4g-staff-queen-test` → `feature/5a-staff-scheduling-foundation` → `feature/5b-staff-schedule-planning` → `feature/5c-staff-schedule-service-binding` → `feature/5d-staff-schedule-persistence` → `feature/5e-staff-schedule-ui` → `feature/5f-staff-schedule-queen-test`.

La rama 5F contiene todos los cambios de 4G actual y los hitos 5A–5E, además de sus propios preflight, Queen Test, gate acumulativo y documentación.

## Condición de cierre

Todo el trabajo de código, endurecimiento estático, persistencia, Presentation, herramientas y genealogía previsto para 5A–5F queda implementado. Este documento **no declara validación runtime**. Para cerrar formalmente el Bloque 5 deben completarse en Unity:
- compilación limpia;
- instalación acumulativa sobre la escena principal;
- validadores y autotests sin fallos;
- preflight 5F;
- Queen Test 5F completo;
- inspección visual de la UI;
- Console final sin Error, Exception ni Assert inesperados.

---

## SOURCE: docs/FINANCE_3A_3I_HARDENING_AUDIT.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — Endurecimiento financiero 3A–3I

## Objetivo

Este documento fija el resultado de la auditoría transversal realizada antes de construir 3J — UI jugable de Finanzas y Caja.

La regla arquitectónica sigue siendo vinculante: `finance.runtime` es la única autoridad de caja y ledger. Ningún bloque analítico, deuda, inventario, marketing, colocables o históricos mantiene una segunda caja.

## Riesgos corregidos

1. **Carrera durante Load en 3I.** Los cambios de calendario emitidos durante una carga ya no procesan vencimientos mientras `SaveGameService.IsBusy`. 3I reconcilia explícitamente después de un Load correcto.
2. **Liquidez optimista.** Una proyección incompleta deja de considerarse sana. 3I distingue información no resuelta e incorpora compromisos de proveedor y gastos operativos recurrentes conocidos del horizonte.
3. **Pago de deuda parcial.** Las patas nuevas de las cuotas pagables de un corte se publican mediante un único batch atómico de 3A. Una cuota parcialmente existente en ledger es una inconsistencia dura.
4. **Consistencia deuda ↔ ledger.** La validación es bidireccional: deuda pagada exige movimientos exactos y ningún movimiento `sourceSystem=financing` puede quedar huérfano.
5. **Default reversible accidentalmente.** El préstamo conserva memoria de haber entrado en default hasta quedar totalmente liquidado.
6. **Días financieros confundidos con días operativos.** 3H separa actividad de servicio, actividad que afecta al resultado y actividad puramente financiera. Un desembolso de préstamo no convierte el día en jornada operativa.
7. **Rachas de pérdidas.** 3I analiza días completados con actividad de resultado; un nuevo día vacío o de pura tesorería no borra una racha de pérdidas.
8. **Escalabilidad 3G/3H.** Los intervalos se proyectan capturando snapshots una sola vez y recorriendo ledger/costes en una pasada por rango, evitando el patrón de clonar y recorrer todo por cada día.
9. **Financiación escondida en “otros”.** 3G/3H conservan por separado desembolsos de préstamos, devolución de principal e intereses financieros.
10. **Caducidad sin impacto económico.** Product Cost 3D conserva una baja económica no monetaria para `Expiration/Waste`. Reduce el resultado mediante 3G pero nunca vuelve a sacar caja: la compra ya fue pagada al proveedor.
11. **Compatibilidad Product Cost v1.** Los campos aditivos de bajas económicas se normalizan al cargar snapshots v1 anteriores que no los contenían.
12. **Sección 3I opcional malinterpretada.** Una partida antigua puede no contener `finance.financing.runtime` únicamente si `finance.runtime` tampoco demuestra movimientos de financiación. Si el ledger contiene deuda y la sección falta, el Load falla de forma segura.
13. **Nómina desconectada tras crear Personal/Horarios.** 3E ya no se limita al contrato abstracto de nómina: `BistroBuilderStaffPayrollFinanceBridge` consume la sesión real de Personal, publica un débito idempotente por día/servicio y añade los turnos explícitamente planificados a las obligaciones proyectadas que 3I usa para liquidez. Staff conserva salarios/empleados y 3A continúa siendo la única caja.

## Semántica contable de bajas de inventario

Una caducidad o merma no genera un segundo débito de caja. La salida de caja ocurrió al pagar la compra. La baja se registra en `finance.product_cost.runtime` como coste analítico persistente y se incluye en el resultado del periodo.

La transacción de inventario agregada actual no conserva la asignación exacta de lotes eliminados, por lo que V1 congela una valoración de referencia y la marca expresamente como `Estimated`. No se presenta como coste SupplierActual.

## Gate estructural y autotest global

El menú de endurecimiento ejecuta:

`Tools → Bistro Builder → Finanzas → 3 - Endurecer + validar + autotest`

Debe quedar con 0 errores estructurales y 0 fallos de autotest. El total de checks se obtiene dinámicamente porque incluye todos los autotests históricos 3A–3I más invariantes nuevas.

## Queen Test financiera global endurecida

Menú canónico:

`Tools → Bistro Builder → Finanzas → 3 - QUEEN TEST FINANCIERA GLOBAL ENDURECIDA`

La prueba:

- exige restaurante `Closed`;
- reserva dos slots temporales libres entre 980–989;
- guarda un rollback real de la partida;
- fabrica venta, marketing, inversión, baja económica no monetaria y financiación;
- comprueba que la baja reduce resultado sin mover caja;
- comprueba liquidez completa y racha de pérdidas sobre días completados;
- guarda un checkpoint real con Finance, Product Cost y Financing;
- avanza al primer vencimiento y paga la cuota mediante batch atómico;
- muta el ledger después del checkpoint;
- carga el checkpoint real;
- verifica que no aparece una cuota fantasma causada por `CalendarChanged` durante Load;
- exige igualdad exacta de snapshots Finance/ProductCost/Financing del checkpoint;
- reconstruye y compara resultados 3G e históricos 3H;
- carga el rollback real inicial;
- exige igualdad exacta con el estado financiero inicial;
- elimina ambos slots temporales;
- exige `Error / Exception / Assert = 0`.

## Criterio de avance a 3J — CUMPLIDO

Este gate quedó cumplido y revalidado el 28/08/2026. Los cuatro requisitos siguientes se conservan como evidencia histórica:

1. Unity compile con 0 errores en la rama de endurecimiento.
2. El instalador/gate global termine con 0 errores y 0 fallos.
3. La Queen Test financiera global endurecida muestre `SUPERADA`.
4. La escena endurecida y cualquier cambio resultante queden versionados.

**Estado final:** los cuatro puntos están cumplidos. 3A–3I queda endurecido y su Queen Test global está SUPERADA; el Bloque 3 completo se cierra conjuntamente con 3J según `FINANCE_BLOCK_3_CLOSURE_20260828.md`.

---

## SOURCE: docs/FINANCE_3J_UI.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — 3J UI jugable de Finanzas y Caja

## Estado

**COMPLETO, VALIDADO Y CERRADO — 28/08/2026.**

La implementación original de `feature/3j-finance-cash-ui` quedó integrada en la escena vigente y revalidada junto al endurecimiento 3A–3I, la Queen financiera global y la integración posterior con Personal/Horarios.

## Principio arquitectónico

3J es Presentation + una fachada de lectura. No introduce una nueva autoridad económica.

Cadena de dependencias:

- 3A `BistroBuilderFinanceService` → caja y ledger canónicos.
- 3G `BistroBuilderFinancialResultsService` → resultado por servicio/día.
- 3H `BistroBuilderFinancialHistoryService` → históricos, KPIs y comparativas.
- 3I `BistroBuilderFinancingService` → liquidez, riesgo y contratos de deuda.
- 3J `BistroBuilderFinanceDashboardService` → read-model efímero para la UI.
- 3J `BistroBuilderFinanceRuntimeView` → presentación e interacción del jugador.

`BistroBuilderFinanceDashboardService` no implementa `IBistroBuilderSaveSectionProvider`, no conserva saldo, no posee ledger, no duplica históricos y no persiste deuda.

La única acción monetaria iniciada por 3J es aceptar una oferta de financiación. Se canaliza así:

`UI 3J → FinanceDashboardService → FinancingService 3I → FinanceService 3A`

La vista nunca publica directamente en el ledger.

## Pantallas jugables

### Resumen

Presenta de forma compacta:

- caja actual;
- disponible tras compromisos con proveedores;
- ventas del día;
- resultado operativo del día;
- estado de liquidez;
- riesgo financiero;
- obligaciones próximas;
- deuda vencida;
- liquidez proyectada;
- margen y COGS del día;
- contribución de Desayuno / Comida / Cena.

### Resultados

Separa expresamente:

- ventas;
- COGS reconocido y teórico;
- margen bruto;
- gastos operativos;
- nóminas;
- marketing;
- portes;
- deterioro/caducidad de inventario;
- bajas de activos;
- intereses de financiación;
- resultado operativo.

Los gastos generales no se reparten artificialmente entre servicios.

### Caja

Presenta tesorería sin confundirla con beneficio:

- caja actual;
- entradas y salidas del día;
- variación neta;
- compras a proveedores;
- inversiones;
- principal de deuda;
- préstamos recibidos;
- reventa de activos;
- liquidez proyectada;
- movimientos recientes del ledger 3A en orden inverso.

### Históricos

Ventanas disponibles:

- 7 días;
- 30 días;
- 90 días;
- todo el histórico permitido por 3H.

Métricas gráficas:

- ingresos;
- resultado operativo;
- variación de caja.

El gráfico es uGUI nativo, sin texturas ni dependencias externas, y agrega históricos largos a un máximo de 180 buckets visuales.

También muestra KPIs y comparación con el periodo anterior de igual duración cuando existe.

### Financiación

Muestra exclusivamente las ofertas y contratos publicados por 3I:

- principal;
- plazo;
- interés total;
- total a devolver;
- elegibilidad e impedimento real;
- deuda pendiente;
- próxima cuota;
- estado del préstamo.

Aceptar financiación exige un modal de confirmación. La UI genera un token estable para esa confirmación y 3I conserva la idempotencia de la operación.

## Integración de input

Al abrir Finanzas:

- se deshabilita temporalmente la cámara profesional;
- se deshabilita temporalmente la interacción de edición;
- los otros accesos globales se ocultan mediante `BistroBuilderFinanceUiModalCoordinator`;
- al cerrar se restauran los estados anteriores.

El coordinador existe como puente aditivo porque la capa transversal UI 2.3JKL-B2 fue creada antes de Finanzas y no ofrece actualmente registro público de módulos nuevos. No toca ninguna autoridad de dominio.

## Seguridad visual

- ScrollViews usan `RectMask2D`, nunca `Mask` clásico transparente.
- Botones persistentes usan `Transition.None` y `Navigation.None` para evitar flashes/estado Selected de uGUI.
- La selección visual es determinista.
- Los importes usan céntimos autoritativos y formato `es-ES`; la UI no recalcula dinero con `float`.
- `Unknown` de liquidez se muestra como información incompleta, nunca como estado sano.

## Herramientas de validación

### Instalación

`Tools → Bistro Builder → Finanzas → 3J - Instalar + validar + autotest`

Instalador idempotente y transaccional. Hace backup byte a byte de la escena y revierte si falla cualquier gate.

### Validador

Comprueba:

- base 3A–3I estructuralmente limpia;
- una única fachada 3J;
- una única vista 3J;
- un único coordinador modal;
- referencias exactas a las autoridades canónicas;
- root único `BB_3J_FinanceUI` bajo el HUD canónico;
- ausencia de una sección Save 3J o ledger paralelo;
- persistencias 3A/3D/3I siguen siendo únicas.

### Autotest

Cubre contratos puros de:

- rangos 7/30/90/Todo;
- formato monetario/porcentajes/estados;
- deep clone del read-model;
- gráfico y límite de buckets;
- token de confirmación;
- ausencia de persistencia propia.

### Prueba runtime real

`Tools → Bistro Builder → Finanzas → 3J - Prueba runtime real`

Automática. Entra en Play Mode, recorre las cinco pantallas, prueba periodos y gráficos, valida el aislamiento modal, abre una confirmación de financiación sin mover caja, acepta después una financiación real por 3I, verifica ledger/deuda, restaura exactamente los snapshots iniciales 3A/3I y sale de Play Mode.

Gate final esperado:

`PRUEBA RUNTIME 3J SUPERADA`

con `Error/Exception/Assert: 0`.

## Gate de cierre

3J queda declarado cerrado porque se han cumplido y revalidado todos estos puntos:

1. compilación Unity: 0 errores;
2. endurecimiento 3A–3I: validación/autotest limpios;
3. Queen Test financiera global endurecida: SUPERADA;
4. instalador/validador/autotest 3J: limpios;
5. prueba runtime real 3J: SUPERADA;
6. revisión visual funcional de las cinco pantallas: PASS, sin solapamiento de accesos globales posteriores;
7. escena instalada y versionada en la rama de cierre final; commit/push se realiza como último acto del cierre.

---

## SOURCE: docs/FINANCE_BLOCK_3_CLOSURE_20260828.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — Cierre formal Bloque 3

Fecha de cierre: **28/08/2026**

Estado: **COMPLETO, VALIDADO Y CERRADO**

## Alcance cerrado

El Bloque 3 comprende la cadena financiera 3A–3J:

- caja y ledger canónicos;
- ventas, compromisos y costes de producto;
- gastos operativos y nóminas;
- marketing e inversión;
- resultados, históricos y financiación;
- UI jugable de Finanzas y Caja.

`BistroBuilderFinanceService` / `finance.runtime` sigue siendo la única
autoridad monetaria. Ninguna ampliación de cierre introduce caja paralela.
## Correcciones finales de cierre

La auditoría final detectó dos huecos reales sobre la escena vigente:

1. La escena había perdido wiring persistente de 3I/3J al avanzar otros bloques.
   El instalador final 3A–3J lo recupera de forma transaccional e idempotente.
2. Tras crear Personal y Horarios, 3E seguía teniendo solo el contrato abstracto
   de nómina. Se añadió `BistroBuilderStaffPayrollFinanceBridge` para que:
   - la sesión real de Personal produzca la nómina del servicio;
   - el pago llegue a 3E y al ledger 3A como un único débito idempotente;
   - los turnos explícitos futuros entren en la proyección de obligaciones de 3I;
   - Staff siga siendo la única autoridad de empleados y salarios.

También se endureció `BistroBuilderFinanceUiModalCoordinator` para reconocer
accesos globales `Open*` creados por módulos posteriores y evitar que Reservas,
Horarios o Personal queden visualmente superpuestos sobre Finanzas.
## Evidencia final

Última validación ejecutada en Unity 6000.3.19f1:

- 3A–3I validación: **36 OK / 0 errores**;
- 3A–3I autotest: **360 OK / 0 fallos**;
- 3J validación: **26 OK / 0 errores**;
- 3J autotest: **42 OK / 0 fallos**;
- preflight final acumulativo: **6 OK / 0 fallos**;
- Queen financiera global endurecida: **SUPERADA**;
- runtime real 3J: **SUPERADA**;
- nómina Staff real: **PASS**, 4 empleados y 37.200 céntimos;
- proyección Staff → 3E → 3I y rollback de horario: **PASS**;
- regresión Bloque 4: **25 OK / 0 fallos**;
- regresión Bloque 5: **7 OK / 0 fallos**;
- preflight 6F: **9 OK / 0 fallos**;
- Queen 6F Reservas: **PASS**.

La revisión visual de Resumen, Resultados, Caja, Históricos y Financiación
confirma composición funcional y ausencia de solapamiento de accesos globales.

---

## SOURCE: docs/INTERACTION_V1_FUTURE_HARDENING_AUDIT.md

Category: TECHNICAL_EVIDENCE

# BB Interaction & Reservation v1.0 — Auditoría futura de robustez

**Estado:** PENDIENTE / GATE FUTURO OBLIGATORIO
**Sistema actual:** v1.0 IMPLEMENTADO / VALIDADO / CERRADO
**No reabre el diseño v1.0 salvo que la auditoría demuestre un defecto real.**

## Cuándo debe activarse
Ejecutar esta auditoría antes de considerar Bistro Builder listo para un vertical slice estable, beta o fase equivalente de producción, y cuando exista suficiente integración real de restaurante como para someter el sistema a carga prolongada.

Se debe detectar especialmente cuando ya estén estabilizados e integrados con gameplay real: BBSIS, Navigation/Crowd Flow, Animation, cocina, camareros, seating, platos/custody y Save/Load.

## Objetivo
Intentar romper deliberadamente Interaction & Reservation v1.0 bajo condiciones realistas y extremas. No añadir funcionalidades por defecto: buscar fugas, derechos fantasma, duplicidades, estados imposibles, problemas de reconciliación y degradación de rendimiento.

## Áreas obligatorias
- Reservas/grants fantasma tras cancelar, destruir, despedir, terminar turno o perder acceso.
- Exclusividad: nunca dos owners para un recurso exclusivo.
- Save/Load en puntos incómodos de transferencias, cocina, pickup, seating y WaitingAtBar → mesa.
- Edición en vivo: mover/eliminar mesas, sillas y estaciones con tareas activas.
- Fallos encadenados: destino inaccesible + cancelación + reasignación + desaparición del holder.
- Simulación prolongada con alta concurrencia y saturación.
- Replay determinista de escenarios equivalentes.
- Auditoría de fronteras: Interaction no invade autoridad de BBSIS, Navigation, Gameplay/IA, Inventory ni Animation.
- Búsqueda exhaustiva de locks/reservas legacy paralelas.
- Perfil de CPU, asignaciones de memoria/GC y escalado con cientos/miles de solicitudes.

## Criterio de cierre de la auditoría
No dar PASS sólo porque compile. Debe existir evidencia de pruebas largas y destructivas, invariantes finales limpias, 0 derechos huérfanos, 0 duplicidades exclusivas y Save/Load repetido sin corrupción lógica.

Si aparecen defectos, corregirlos dentro de la autoridad del sistema responsable. Si el fallo pertenece a otro sistema, identificarlo y derivarlo; no crear una solución paralela desde Interaction.

## Recordatorio para futuras sesiones
**DETECTAR ESTE GATE Y AVISAR AL USUARIO CUANDO SE ALCANCE LA FASE DE VERTICAL SLICE/BETA O CUANDO LOS SISTEMAS CENTRALES YA ESTÉN INTEGRADOS Y SEA POSIBLE UNA SIMULACIÓN DE RESTAURANTE PROLONGADA.**

---

## SOURCE: docs/STAFF_4B_DESIGN.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — 4B Plantilla, contratación y despido

Estado: implementación preparada para validación Unity. No cerrado.

## Principios

- `BistroBuilderStaffService` continúa siendo la única autoridad de la plantilla.
- El mercado de candidatos no posee empleados: solo propuestas de contratación.
- Contratar convierte un candidato en un `Employee` nuevo con `EmployeeId` nuevo; el `CandidateId` nunca se reutiliza como `EmployeeId`.
- Despedir no destruye el registro histórico. El empleado pasa a `Dismissed` y `Unavailable`.
- Un empleado con binding de servicio activo no puede despedirse. 4B define el contrato `IBistroBuilderStaffSessionAssignmentQuery`; 4D lo implementará mediante el binding EmployeeId ↔ WaiterId.
- Personal no mueve dinero. El salario esperado y contractual se expresa en céntimos, pero 3A/3E siguen siendo autoridades monetarias.
- Los candidatos se generan de forma determinista desde día + generación + salt del perfil, con variación acotada de experiencia, habilidades y salario.
- La plantilla de nombres es dato de authoring, no una lista de empleados hardcodeados.

## Mercado V1

- 5 candidatos por defecto.
- Refresco como máximo una vez por día de juego.
- El candidato contratado desaparece inmediatamente del mercado.
- No hay entrevistas, negociación ni coste de contratación en 4B.
- La economía de nómina queda desacoplada hasta la integración correspondiente.

## Despido V1

Política canónica:

1. Solo puede despedirse un empleado `Active`.
2. Si 4D informa de un binding activo, el despido se rechaza.
3. Al despedir: `employmentStatus = Dismissed`, `availability = Unavailable`.
4. El EmployeeId y su historial permanecen en `staff.state` para trazabilidad/migración.
5. 4B no calcula indemnizaciones.

## Persistencia

4B mantiene el mercado en memoria y expone snapshot/restore para 4E. `staff.state` sigue siendo el agregado de empleados; 4E registrará las secciones Save y coordinará el orden con `service.runtime`.

---

## SOURCE: docs/STAFF_4C_DESIGN.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — 4C Experiencia, habilidades, rendimiento y formación

Estado: implementación preparada para validación Unity. No cerrado.

## Principios

- La experiencia se concede exclusivamente al aplicar un resultado final y trazable de servicio.
- No existe XP por frame, por tiempo arbitrario ni por abrir/cerrar UI.
- El nivel profesional se deriva de XP mediante `StaffDevelopmentProfile`; no se persiste como una segunda fuente de verdad.
- El rendimiento V1 conserva hechos reales: servicios, tareas completadas/fallidas, mesas atendidas y duración total de tareas.
- No se inventa una puntuación de eficiencia para rellenar UI. La capa de consulta puede derivar tasa de finalización y promedios de esos contadores.
- Las cuatro habilidades V1 siguen siendo Velocidad, Atención, Organización y Trato al cliente.
- 4C no altera todavía el comportamiento del agente operativo. La futura integración de modificadores será centralizada y testeable.

## Progresión V1

Perfil inicial:

- Nivel máximo: 10.
- XP necesaria para el siguiente nivel crece progresivamente.
- XP base por servicio completado: 18.
- XP por tarea completada: 2.
- XP máxima procedente de tareas por servicio: 30.

Un cierre de servicio se identifica mediante un `operationId` estable. El mismo resultado aplicado de nuevo se trata como replay y no vuelve a sumar XP, contadores ni eventos.

## Rendimiento

`BistroBuilderEmployeeServicePerformanceReport` es el contrato que 4D rellenará desde el runtime real. Contiene únicamente:

- tareas completadas;
- tareas fallidas;
- mesas atendidas;
- duración total de tareas;
- identidad estable de la operación de cierre.

El resumen consultivo deriva:

- tasa de finalización;
- tiempo medio por tarea;
- tareas medias por servicio.

## Formación V1

Existen cuatro mejoras pequeñas e independientes, cada una +2 a una habilidad y con máximo de repeticiones configurado por datos:

- Ritmo de servicio → Velocidad.
- Atención al detalle → Atención.
- Organización de sala → Organización.
- Trato al cliente → Trato con clientes.

No hay árboles de talentos ni cursos complejos.

La infraestructura admite un `financialCostCents`, pero las definiciones V1 tienen coste 0. Si un diseñador configura un coste mayor que cero antes de existir un gateway financiero atómico validado, `StaffDevelopmentService` rechaza la operación. Personal nunca crea un monedero alternativo ni debita caja directamente.

## Autoridad

Toda mutación sigue terminando en `BistroBuilderStaffService`. El motor 4C produce un snapshot candidato y `TryCommitDomainMutation` comprueba que deriva exactamente de la revisión actual antes de publicarlo. Esto impide que una operación normal se disfrace de restauración Save/Load y evita commits obsoletos.

## Persistencia futura

El desarrollo vive dentro de cada `Employee` en `staff.state`. No se crea una sección `staff.development.state` separada. 4E persistirá el agregado completo una vez que 4D haya definido los bindings de sesión necesarios para guardado durante servicio activo.

---

## SOURCE: docs/STAFF_4D_HARDENING_20260820.md

Category: TECHNICAL_EVIDENCE

# 4D — Hardening previo a 4E

Estado: implementado, pendiente de compilación y validación real en Unity.

## Hallazgo estático

`BistroBuilderStaffSessionService.TrySetAllWaitersEligible` aplica `Waiter.TrySetStaffServiceEligibility` secuencialmente. El setter actual puede rechazar la transición a `false` cuando un agente está ocupado. Por tanto, si varios `Waiter` se procesan y uno intermedio rechaza el cambio, los anteriores pueden quedar ya modificados y los posteriores no procesados.

Esto no corrompe `staff.state` ni la cola de tareas, pero puede dejar un estado runtime de elegibilidad parcialmente aplicado. Ese estado no debe propagarse a 4E Save/Load.

## Decisión vinculante de hardening

Antes de iniciar 4E debe cumplirse una de estas dos soluciones equivalentes:

1. hacer que la elegibilidad sea un gate exclusivo de **nuevas asignaciones**, permitiendo desactivar un `Waiter` ocupado sin cancelar ni alterar su tarea actual; o
2. mantener el rechazo actual y convertir la operación por lote en transaccional, con preflight/rollback completo.

La opción preferida es la primera porque `staffServiceEligible` solo participa en `Waiter.IsAvailable`: no es autoridad de la tarea ni del movimiento. Desactivar nuevas asignaciones mientras una tarea ya aceptada continúa evita estados parciales y no sustituye `WaiterTaskCoordinator`.

## Invariantes que no pueden romperse

- `WaiterTaskCoordinator` sigue siendo la única autoridad de tareas.
- `Waiter` sigue siendo el agente operativo existente; no se crea un segundo sistema de camareros.
- Cambiar elegibilidad nunca cancela, completa, reasigna ni muta una tarea activa.
- `EmployeeId ↔ WaiterId` sigue siendo responsabilidad exclusiva de 4D.
- 4E no debe serializar GameObjects, Transform ni referencias `Waiter`.
- Ningún gate se considera validado hasta compilar y ejecutar tests reales en Unity.

## Gate para permitir 4E

4E solo puede comenzar cuando:

- la transición de elegibilidad por lote no pueda dejar estado parcial;
- `TryRestoreSessionSnapshot` pueda rehidratar todos los bindings o fallar sin dejar agentes parcialmente habilitados;
- el cierre de sesión no destruya un binding mientras el agente siga ejecutando trabajo real;
- el autotest 4D cubra explícitamente la semántica elegida para un `Waiter` ocupado.

---

## SOURCE: docs/STAFF_4E_REBASE_AUDIT.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — 4E Personal / Rebase y auditoría estática

Estado: implementado estáticamente / pendiente de compilación y validación Unity.

## Base autoritativa

`feature/4e-staff-persistence-v2` es una extensión lineal de la rama canónica `feature/4d-staff-service-binding`. La rama anterior `feature/4e-staff-persistence` quedó divergida y no debe usarse como base de nuevas modificaciones.

Los gates 4D de elegibilidad transaccional, preflight de restore/cierre y preparación segura de Load viven en la rama canónica 4D; 4E v2 los hereda sin duplicar su autoridad.

## Secciones de Save incluidas

- `staff.state`: autoridad persistente Employee; no guarda Waiter ni tareas.
- `staff.recruitment`: mercado de candidatos y metadatos de refresco; no guarda empleados ni dinero.
- `staff.session.runtime`: binding EmployeeId ↔ WaiterId y métricas de la sesión; no guarda GameObjects ni sustituye `service.runtime`.

## Orden de carga vinculante

### Prepare — orden DESCENDENTE

`BistroBuilderSaveGameService` ordena `PrepareForLoad` de **mayor a menor**. Por ello el desmontaje seguro queda:

1. `service.runtime`: Prepare 9000 — activa el scope global de restauración y limpia primero tareas, flujos y asignaciones operativas.
2. `staff.session.runtime`: Prepare 8950 — desmonta bindings y elegibilidad después del runtime operativo.
3. `staff.recruitment`: Prepare 8900 — limpia mercado temporal.
4. `staff.state`: Prepare 8850 — vacía la plantilla al final, cuando ya no quedan bindings runtime vivos.

### Apply — orden ASCENDENTE

1. `staff.state`: Apply 400.
2. `staff.recruitment`: Apply 425.
3. `service.runtime`: Apply 500 (autoridad existente).
4. `staff.session.runtime`: Apply 550.

El binding de Personal se restaura después de que `service.runtime` haya reconstruido el estado operativo de los Waiter. Esto evita que Personal active/desactive agentes antes de que la autoridad de servicio termine de aplicar su snapshot.

### Finalize — orden ASCENDENTE y scope de restauración

1. `staff.state`: 10500.
2. `staff.recruitment`: 10600.
3. `staff.session.runtime`: **10950**.
4. `service.runtime`: **11000**.

Esta relación es deliberada y vinculante. `service.runtime` mantiene `BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = true` desde Prepare y, en su Finalize 11000, quita ese scope y reanuda `WaiterTaskCoordinator`, camareros, clientes y llegadas. Por tanto, Personal debe validar y rehidratar el binding **antes** de 11000, mientras el mundo operativo sigue congelado.

`BistroBuilderSaveGameService` detiene la secuencia de Finalize en el primer `context.Fail`. Si `staff.session.runtime` no puede rehidratarse en 10950, `service.runtime` no llega a reanudar el mundo objetivo y el Save universal entra en su rollback global. El validador y el instalador 4E v2 comprueban explícitamente esta ventana segura.

## Compatibilidad con partidas antiguas

Las tres secciones son opcionales. `staff.state` se vacía durante Prepare para impedir contaminación entre partidas. `staff.recruitment` restaura un snapshot vacío y, si la sección no existía, genera un mercado nuevo determinista para el `DayIndex` cargado. `staff.session.runtime` descarta bindings anteriores y, si el save legacy declara servicio activo, solicita a 4D reconstruir una sesión contra los Waiter ya restaurados por `service.runtime`, todavía dentro del scope global de restauración.

## Endurecimiento 4D exigido por 4E

Los helpers de seguridad están integrados en `BistroBuilderStaffSessionService` y existen en la rama canónica 4D:

- `BistroBuilderStaffEligibilityBatch`: aplica planes uniformes o mixtos de elegibilidad como una transacción y restaura exactamente los estados previos si cualquier Waiter rechaza la operación.
- `BistroBuilderStaffSessionRestorePreflight`: rechaza WaiterId inexistentes/duplicados antes de mutar elegibilidad durante restore/rehidratación.
- `BistroBuilderStaffSessionClosePreflight`: `TryFinalizeClosedSession(...)` lo ejecuta antes de consolidar ciclos o aplicar XP/rendimiento, exigiendo que los camareros ligados sigan existiendo y estén realmente libres.
- `BistroBuilderStaff4DPrepareForLoadSelfTest`: exige que `PrepareForRuntimeLoad` no comprometa suspensión, tracking ni bindings antes de validar índice y batch transaccional.

## Gate de serialización real

`BistroBuilderStaff4EJsonRoundTripSelfTest` usa directamente `BistroBuilderJsonSaveSerializer` (`unity-json-v1`) para serializar y deserializar `staff.state`, `staff.recruitment` y `staff.session.runtime`, y vuelve a ejecutar sus validaciones canónicas.

Este gate no sustituye una prueba real de Save/Load: detecta incompatibilidades de modelo/JsonUtility de forma temprana y determinista.

## Gates aún pendientes antes de cerrar 4D/4E

No queda un gate estático conocido de wiring 4D bloqueando 4E. Continúan pendientes los gates que requieren el proyecto real:

1. Compilación limpia en Unity.
2. Instalación acumulativa sobre la escena canónica.
3. Validadores y autotests 4D/4E en Unity.
4. Round-trip Save/Load real durante servicio activo, verificando el mismo `EmployeeId ↔ WaiterId`, ausencia de duplicados, tareas coherentes y métricas sin doble aplicación.

Hasta superar esos gates, 4D y 4E no deben marcarse como cerrados ni validados.

---

## SOURCE: docs/STAFF_4G_QUEEN_TEST_PLAN.md

Category: TECHNICAL_EVIDENCE

# 4G — Queen Test real de Personal

Estado: **runner y alcance funcional implementados estáticamente / no validados en Unity**.

## Objetivo

Cerrar el Bloque 4 únicamente después de demostrar, en una partida real y con rollback completo, que Personal funciona de extremo a extremo sin sustituir ninguna autoridad existente.

## Herramientas 4G

- `Tools > Bistro Builder > Personal > 4G - Queen Test preflight`: comprueba autoridades, wiring, Save, UI completa y agentes antes de mutar la partida.
- `Tools > Bistro Builder > Personal > 4G - Autotest estático`: exige que el runner use las autoridades canónicas y prohíbe fabricar `Waiter`, `WaiterTask`, otra cola o aplicar XP directamente.
- `Tools > Bistro Builder > Personal > 4G - Autotest mutación observable`: exige que el Save/Load activo demuestre primero una divergencia real entre el checkpoint y el runtime posterior.
- `Tools > Bistro Builder > Personal > Bloque 4 - Gate acumulativo 4D-4G`: ejecuta en una sola pasada los gates puros/estáticos de 4D, aislamiento/round-trip 4E, Presentation y formación 4F, y preparación 4G.
- `Tools > Bistro Builder > Personal > 4G - QUEEN TEST reversible`: ejecuta el flujo final con rollback integral y dos slots temporales libres del rango 980–989.

## Precondición obligatoria

Ejecutar primero el preflight en Play Mode. Debe devolver 0 errores y comprobar:

- autoridad única de SaveGame, Staff, Recruitment, Development y Session;
- una única Facade, Screen y `BistroBuilderStaffPlayerTrainingPanel` de Presentation;
- registro real de `staff.state`, `staff.recruitment` y `staff.session.runtime` en SaveGame;
- mercado de candidatos disponible;
- snapshots válidos de plantilla y binding;
- UI 4F correctamente cableada y con opciones de formación derivadas del perfil canónico 4C;
- agentes `Waiter` reales disponibles para el binding;
- servicio `Closed` y sin sesión 4D activa para iniciar el Queen flow.

## Contrato Save/Load endurecido

`service.runtime` conserva la autoridad operativa y mantiene el scope global de restauración hasta su Finalize 11000. `staff.session.runtime` finaliza en 10950: después de `game.general`, pero antes de que el servicio quite el scope y reanude `WaiterTaskCoordinator`, camareros, clientes y llegadas. Si Personal falla al validar/rehidratar, SaveGame detiene Finalize antes de reanudar el mundo objetivo y activa su rollback global.

## Queen flow reversible implementado

1. Localiza dos slots diagnósticos libres entre 980 y 989 y guarda rollback integral con el SaveGame universal.
2. Abre/cierra la pantalla 4F real y comprueba que Presentation puede mostrarse sin convertirse en autoridad.
3. Captura `staff.state`, mercado, `staff.session.runtime`, estado de servicio y número real de `Waiter`.
4. Contrata un candidato mediante `BistroBuilderStaffPlayerFacade` y exige desaparición del `CandidateId`, `EmployeeId` nuevo y válido, incremento exacto de plantilla y número de `Waiter` inalterado.
5. Cambia la disponibilidad del empleado a `Unavailable` y de vuelta a `Available` por la autoridad canónica.
6. Ejecuta una formación V1 gratuita real mediante 4C/fachada; la UI 4F también expone estas formaciones sin crear ninguna integración financiera alternativa.
7. Deja temporalmente no disponibles los demás empleados con rol operativo de camarero. El rollback inicial cubre esta mutación y garantiza que el empleado recién contratado sea el único elegible para la sesión diagnóstica.
8. Pasa `Closed → Preparing`, inicia 4D y exige binding real `EmployeeId ↔ WaiterId`; después abre `Open` mediante `RestaurantServiceStateService`.
9. Espera hasta 180 s una tarea **real** completada observada por 4D. El runner no crea clientes, tareas, colas, XP ni métricas.
10. Guarda checkpoint con servicio `Open` y espera hasta 60 s una mutación **observable** posterior. Solo cuando demuestra `A != B` permite cargar el checkpoint; si no hay mutación, falla y ejecuta rollback.
11. Tras Load exige restauración exacta de `staff.state`, mercado y `staff.session.runtime`, mismo estado `Open`, mismo número de `Waiter`, mismo `WaiterId` ligado y mismas métricas guardadas.
12. Inicia `Closing` y espera hasta que `WaiterTaskCoordinator.ActiveTaskCount == 0` y todos los agentes ligados estén `Idle`; solo entonces completa `Closed`.
13. Exige que 4D haya aplicado XP y rendimiento exactamente una vez al empleado objetivo y vuelve a invocar la finalización para comprobar idempotencia sin mutación adicional.
14. Guarda/carga un segundo checkpoint `Closed` y exige persistencia exacta de plantilla, mercado, XP/skills/rendimiento y sesión inactiva.
15. Carga el rollback inicial, comprueba igualdad exacta del estado previo y elimina ambos slots diagnósticos.

## Gates de cierre

El Bloque 4 no puede declararse cerrado hasta obtener simultáneamente:

- Unity compila con 0 errores;
- instaladores 4A–4F, incluida la formación 4F, sin error;
- validadores estructurales sin error;
- gate acumulativo 4D–4G sin fallos;
- Queen Test 4G real completo con rollback confirmado;
- prueba visual 4F, incluida la modal de formación, sin referencias vacías, duplicados de filas ni bloqueo de input;
- Save/Load durante servicio activo sin duplicar `Employee`, `Waiter`, tareas ni secciones;
- logs Unity sin errores.

## Principio de seguridad

El Queen Test nunca fabrica una segunda fuente de verdad. Toda mutación pasa por las autoridades ya implementadas y el rollback utiliza el SaveGame universal. Si el servicio real no produce trabajo o una mutación persistible observable dentro de sus timeouts, la prueba falla y restaura el rollback; nunca sustituye esa ausencia por métricas sintéticas.

---

## SOURCE: docs/STAFF_BLOCK_4_ARCHITECTURE.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — Bloque 4 Personal

## Estado

El Bloque 4 se desarrolla sobre `feature/4a-staff-foundation`, derivada del estado más reciente de `feature/3j-finance-cash-ui`.

El Bloque 3 no se reinterpreta ni se duplica. Su endurecimiento y 3J continúan pendientes de los gates finales en Unity. Personal solo consumirá contratos públicos cuando corresponda.

## Auditoría previa obligatoria del runtime existente

Antes de crear Personal se revisó el código real de camareros, servicio y guardado activo.

### `Waiter` ya es el agente operativo

`Waiter` es un `MonoBehaviour` de simulación con:

- `WaiterId` entero de runtime;
- estado operativo `WaiterState`;
- destino/mesa/barra/comanda actuales;
- capacidad de reparto;
- disponibilidad operativa;
- eventos de cambio de estado.

No contiene contrato laboral, salario, experiencia, habilidades ni identidad persistente de empleado.

**Decisión vinculante:** `WaiterId` NO se convierte en `EmployeeId`.

Son identidades de ciclos de vida distintos:

`EmployeeId persistente -> binding de sesión -> WaiterId / Waiter operativo`

El `EmployeeId` sobrevive servicios, escenas y ausencia del empleado. El `WaiterId` sigue identificando al agente/slot operativo usado por el servicio actual.

### `WaiterTaskCoordinator` sigue siendo autoridad de tareas

El coordinador existente:

- registra/desregistra `Waiter` dinámicamente;
- mantiene la cola de tareas;
- recupera tareas al retirar un agente;
- coordina entregas y rondas;
- funciona por eventos.

Personal no crea una segunda cola, no decide navegación y no sustituye este coordinador.

### `service.runtime` ya persiste el servicio activo

El proveedor existente conserva un checkpoint operativo y persiste, entre otros elementos, los camareros mediante `BistroBuilderWaiterRuntimeSaveRecord` con:

- `waiterId`;
- posición;
- rotación.

Al cargar, `service.runtime` busca los `Waiter` existentes en la escena por `WaiterId`, rechaza duplicados, limpia asignaciones y restaura transformaciones. Las comandas también conservan la referencia al `waiterId` operativo.

Actualmente `service.runtime` no instancia una plantilla laboral ni crea agentes a partir de empleados. Esa frontera se preserva.

### Economía 3E ya tiene el contrato correcto

`BistroBuilderOperatingExpenseService` declara que no posee empleados y expone `TryPostPayrollBatch(...)` para recibir una nómina ya calculada externamente.

Por tanto:

- Personal será autoridad del salario contractual;
- Personal no tendrá caja ni ledger;
- el futuro cálculo/pago de nómina se proyectará hacia 3E;
- 3A continuará como única autoridad monetaria.

## Arquitectura objetivo

```text
staff.state (Personal persistente)
        |
        | EmployeeId + rol + disponibilidad
        v
Staff Service / Scheduling contracts
        |
        | binding de sesión (4D)
        v
Waiter / futuro agente operativo existente
        |
        v
WaiterTaskCoordinator + flujos de servicio
        |
        | hechos reales terminados
        v
Rendimiento / XP del Employee persistente
```

No existe dependencia inversa desde `Waiter` hacia el dominio persistente para almacenar salario, contrato o progreso.

## 4A — Fundación canónica

### Identidad

`EmployeeId` usa el formato:

`emp_<32 hex minúsculas>`

Se genera desde GUID y nunca desde:

- nombre;
- índice;
- posición;
- GameObject;
- orden de creación visible.

### Estado canónico

`BistroBuilderStaffSnapshot` reserva el esquema:

- `schemaId = staff.state`
- `schemaVersion = 1`
- `revision`
- colección de empleados.

4A define el modelo pero todavía no registra el proveedor de Save. La integración real con Save/Load corresponde a 4E, después de que 4D haya definido el binding que también debe sobrevivir a un guardado activo.

### Employee V1

El registro persistente contiene:

- EmployeeId;
- nombre y apellido;
- RoleId;
- estado laboral;
- disponibilidad persistente;
- salario contractual por servicio en céntimos;
- día de contratación;
- experiencia;
- cuatro habilidades V1: velocidad, atención, organización y trato;
- configuración de responsabilidad/zona sin lógica operativa;
- contadores históricos mínimos de rendimiento;
- revisión individual.

No contiene:

- referencia a Waiter;
- GameObject;
- Transform;
- tarea activa;
- saldo/caja;
- pathfinding;
- referencias a Presentation.

### Estados separados

La disponibilidad persistente no intenta representar todo el runtime.

4A distingue:

- estado laboral: Active / Inactive / Dismissed;
- disponibilidad persistente: Available / Unavailable.

`Assigned` y `Working` serán estados derivados del binding de sesión de 4D. No se guardan como booleanos redundantes dentro del Employee.

### Roles dirigidos por datos

`BistroBuilderStaffRoleCatalog` evita un enum rígido de profesiones.

V1 instala únicamente:

- `waiter` — Camarero/a — adaptador `waiter.agent`.

Futuros roles (cocina, jefe de sala, barra, ayudantes) podrán añadirse como datos y nuevos adaptadores sin reescribir Employee.

### Authority

`BistroBuilderStaffService` es la autoridad de aplicación de Personal. Mantiene el snapshot, valida mutaciones, devuelve copias profundas y publica eventos.

No contiene referencias a:

- `Waiter`;
- `WaiterTaskCoordinator`;
- `BistroBuilderFinanceService`;
- `BistroBuilderOperatingExpenseService`.

## Orden de subhitos

Se mantiene la propuesta original porque la auditoría confirma que es técnicamente coherente:

### 4A — Fundación canónica de Personal

Identidad, modelos, roles, autoridad, eventos, invariantes y tests base.

### 4B — Plantilla, contratación y despido

Mercado/candidatos, contratación idempotente, despido seguro, roster activo e inactivo.

El despido de un empleado actualmente ligado a un agente se bloqueará o diferirá mediante el contrato de binding; no destruirá directamente un `Waiter` desde el dominio.

### 4C — Experiencia, habilidades, rendimiento y formación

XP determinista por hechos terminados, progresión lenta, métricas reales y formación sencilla. La formación tendrá contrato económico sin wallet alternativo.

### 4D — Binding Personal ↔ Servicio

Registro de sesión que impondrá:

- un EmployeeId como máximo por agente;
- un agente como máximo por EmployeeId;
- rol compatible con adaptador;
- lifecycle ligado al servicio/checkpoint;
- alta/baja segura en `WaiterTaskCoordinator`;
- lectura de carga real desde tareas;
- captura de hechos terminados para 4C.

El binding será la única capa que conoce simultáneamente EmployeeId y `Waiter`.

### 4E — Persistencia y Save/Load

Proveedor `staff.state` versionado y coordinación con `service.runtime`.

Durante un servicio activo se conservará el vínculo EmployeeId ↔ identidad operativa necesaria para reconstruir la sesión sin duplicar agentes. El orden de Prepare/Apply/Finalize se definirá contra el proveedor real de `service.runtime`, no mediante temporizadores ni búsquedas tardías.

### 4F — UI jugable definitiva

Presentation consultiva y comandos. Sin mutación directa de snapshots ni polling por frame.

### 4G — Integración y Queen Test Real

Servicio representativo con varios empleados/agentes, actividad real, Save/Load activo, contratación/despido, progresión y regresiones de comandas/cocina/sala/barra.

## Invariantes vinculantes para todo el Bloque 4

1. `EmployeeId != WaiterId` y nunca se derivan entre sí.
2. Un Employee persistente puede existir sin agente operativo.
3. Destruir/desactivar un agente nunca destruye el Employee.
4. `WaiterTaskCoordinator` continúa siendo autoridad de tareas de camarero.
5. Personal nunca crea pathfinding alternativo.
6. Personal nunca posee saldo, ledger ni dinero mutable.
7. Salarios se almacenan en céntimos y la integración de pago utilizará 3E/3A.
8. Presentation nunca escribe directamente en `staff.state`.
9. XP y rendimiento solo proceden de hechos discretos y trazables, nunca por frame.
10. Ningún Employee persistente contiene referencias Unity a objetos de escena.
11. Save/Load activo no puede crear dos bindings para un mismo EmployeeId ni dos EmployeeId para un agente.
12. El Bloque 4 no se cerrará hasta superar instalación, validación, autotest, runtime real y regresiones.

## Gate 4A

Herramienta única:

`Tools → Bistro Builder → Personal → 4A - Instalar + validar + autotest`

El instalador:

- exige escena guardada y fuera de Play;
- hace backup byte a byte;
- crea/reutiliza el catálogo de roles;
- crea/reutiliza un único `BistroBuilderStaffService` en `GameSystems`;
- no modifica Waiter, tareas, Finanzas ni Save;
- guarda la escena;
- ejecuta validador y autotest;
- restaura la escena y elimina únicamente assets creados por la instalación si cualquier gate falla.

4A no se considera cerrado hasta obtener compilación Unity limpia y resultados automáticos sin errores/fallos.

---

## SOURCE: docs/STAFF_BLOCK_5_SCHEDULING.md

Category: TECHNICAL_EVIDENCE

# Bistro Builder — Bloque 5 / Horarios y Turnos

Estado: implementación en curso / no validado en Unity.

## Objetivo

Añadir planificación real de plantilla sobre el Bloque 4 sin convertir el horario en otra fuente de verdad de empleados ni de agentes operativos.

Separación vinculante:

`EmployeeId persistente (4A) -> turno planificado (5) -> filtro de elegibilidad de sesión -> binding 4D -> Waiter real -> WaiterTaskCoordinator`.

Horarios no crea empleados, no crea Waiter, no abre el restaurante, no paga salarios y no persiste mediante un Save paralelo.

## Alcance V1

- planificación por DayIndex y servicio gastronómico;
- horizonte configurable, inicialmente 7 días;
- turnos con ventana horaria configurada por perfil;
- asignar/desasignar EmployeeId activos;
- cobertura prevista de camareros y coste salarial proyectado;
- edición únicamente con restaurante Closed;
- integración con 4D como filtro, no como sustituto del binding;
- persistencia `staff.schedule` dentro del SaveGame universal;
- UI jugable de planificación;
- Queen Test reversible.

## Roadmap técnico

- **5A — Fundación**: dominio, perfil, motor puro y `StaffScheduleService`.
- **5B — Planificación y cobertura**: operaciones masivas seguras, copia entre servicios/días, suficiencia y previsión salarial.
- **5C — Integración con servicio**: política de elegibilidad consumida por 4D; solo los EmployeeId programados para día/servicio pueden ligarse.
- **5D — Persistencia**: sección `staff.schedule` del SaveGame universal, round-trip y compatibilidad legacy.
- **5E — UI jugable**: calendario de servicios, plantilla, cobertura, coste previsto y comandos mediante fachada Presentation.
- **5F — Queen Test**: planificar -> abrir -> binding filtrado -> Save/Load activo -> cerrar -> Load -> rollback.

## Gates de cierre

El Bloque 5 solo podrá cerrarse después de compilación Unity 0 errores, instaladores/validadores/autotests limpios, prueba visual de UI, Save/Load activo y Queen Test real sin duplicados ni errores de Console.

---

## SOURCE: REVISION_TECNICA_367C.md

Category: TECHNICAL_EVIDENCE

# BistroBuilder 367C — Revisión técnica

## Decisión arquitectónica

El ciclo existente no se sustituye de golpe. `RestaurantOrder` pasa a ser una
fachada operativa temporal y cada instancia queda enlazada a un agregado
`BistroBuilderCanonicalOrder`.

La integración es estricta: una transición legacy no se confirma hasta que la
autoridad canónica la ha aplicado correctamente. Esto evita una sincronización
tardía basada únicamente en eventos, que podría dejar dos estados diferentes si
un listener falla.

## Atomicidad

`TryAdvanceAllLinesToState` clona el agregado, recorre la ruta normal de cada
línea, valida la copia completa y solo entonces sustituye la comanda original y
sus índices. Una línea inválida no produce una actualización parcial.

## Autoridades

- `BistroBuilderCanonicalOrderService`: identidad, líneas, platos, precios y
  estado canónico.
- `RestaurantOrder`: fachada coarse requerida temporalmente por cocina,
  camareros, cuenta y mesa.
- `BistroBuilderCanonicalOrderIntegrationService`: traducción y puerta
  transaccional entre ambos modelos.

La fachada no puede avanzar de forma independiente cuando está enlazada.

## Preparación para service.runtime

Cada comanda conserva:

- Canonical OrderId.
- OrderLineId por plato físico.
- CustomerId lógico por miembro del grupo.
- DishId.
- precio congelado.
- mesa, grupo y referencia legacy.
- pase y servicio.

La futura carga con el restaurante abierto podrá reconstruir primero las
comandas canónicas y después recrear las fachadas operativas mediante el mismo
contrato de registro.

## Limitación consciente

Todos los platos de una comanda siguen avanzando juntos porque la cocina y la
entrega actuales trabajan a nivel de `RestaurantOrder`. No se finge que ya
existe procesamiento individual.

El siguiente bloque deberá migrar `KitchenSystem`,
`WaiterTaskCoordinator` y `FoodDeliveryServiceFlow` para operar con
`OrderLineId`. La estructura creada aquí no tendrá que cambiar.

---

## SOURCE: REVISION_TECNICA_367D.md

Category: TECHNICAL_EVIDENCE

# BistroBuilder 367D — Revisión técnica

## Arquitectura

367D conserva `BistroBuilderCanonicalOrderService` como única autoridad de
las líneas. Cocina, reparto y la fachada legacy solicitan operaciones mediante
`BistroBuilderOrderLineExecutionService`; no mutan directamente el agregado.

El flujo operativo queda dividido así:

1. `OrderSystem` y 367C crean y enlazan la comanda canónica.
2. `KitchenSystem` indexa las líneas `Queued` y procesa una unidad física.
3. `BistroBuilderOrderLineExecutionService` confirma las transiciones.
4. `WaiterTaskCoordinator` crea una tarea por `OrderLineId`.
5. `FoodDeliveryServiceFlow` coordina recogida, tránsito y entrega.
6. `RestaurantOrder` permanece como fachada coarse para los sistemas legacy.

## Decisiones de integridad

- Los precios continúan congelados en la línea canónica creada por 367B.
- Las tareas de reparto se identifican por comanda y `OrderLineId`.
- La cola de cocina evita duplicados mediante un índice de LineId.
- La asignación de una línea a un camarero es transaccional: si el camarero
  rechaza la asignación, la línea vuelve a `ReadyForPickup`.
- Una preparación interrumpida puede volver de `Preparing` a `Queued`.
- Una entrega interrumpida puede volver de `AssignedForDelivery` o `InTransit`
  a `ReadyForPickup`.
- `Served` no tiene rollback para impedir duplicar un plato ya entregado.
- El pago consume todas las líneas servidas sobre una copia validada y sustituye
  el agregado únicamente cuando la operación completa es válida.

## Persistencia futura

`BistroBuilderKitchenRuntimeSnapshot` no guarda referencias de escena. Usa:

- `KitchenId`;
- `CanonicalOrderId`;
- `OrderLineId`;
- `DishId`;
- OrderId legacy;
- secuencia FIFO;
- duración total;
- tiempo restante;
- indicador de línea activa.

La restauración exige que las comandas legacy y canónicas ya estén disponibles
y valida todas las referencias antes de sustituir la cola en memoria.

## Compatibilidad

Se mantienen las APIs legacy necesarias para no romper pruebas o sistemas aún
no migrados. El instalador desactiva la antigua autoridad automática de reparto
para que no compita con `WaiterTaskCoordinator`.

## Comprobaciones estáticas realizadas

- Delimitadores C# balanceados ignorando comentarios y literales.
- Sin tipos de nivel superior duplicados en el proyecto combinado.
- Un `.meta` por script y ningún GUID duplicado.
- GUID de todos los scripts sustituidos conservado.
- Sin `Quaternion.sqrMagnitude`.
- Sin comparaciones relacionales directas entre enums.
- Parámetros `out` inicializados o delegados por rutas explícitas.
- Sin `Find` por frame; el descubrimiento runtime es puntual al iniciar.
- ZIP y hashes SHA-256 verificados tras su creación.

La compilación definitiva corresponde a Unity, porque el entorno de generación
no incluye el compilador ni los assemblies de la versión exacta del proyecto.

---

## SOURCE: REVISION_TECNICA_367D1.md

Category: TECHNICAL_EVIDENCE

# BistroBuilder 367D1 — revisión técnica del hotfix

## Causa raíz

`StartCoroutine(ProcessLinesRoutine())` ejecuta el enumerador de forma síncrona
hasta su primer `yield`. En 367D, `processingRoutine` se asignaba únicamente cuando
`StartCoroutine` devolvía. Antes de esa devolución, `TryBeginPreparation` cambiaba
la fachada legacy a `Preparing`, lo que disparaba `HandleOrderStateChanged`,
`EnqueueQueuedLines` y una nueva llamada a `EnsureProcessingRoutine`.

Como `processingRoutine` todavía era `null`, se iniciaba una segunda corrutina. Las
dos compartían `activeWork`, por lo que la segunda podía sobrescribir el trabajo de
la primera y dejar una línea canónica huérfana en `Preparing`.

## Corrección

- Reclamación atómica lógica del consumidor antes de llamar a `StartCoroutine`.
- Segundo intento de reclamación rechazado mientras el bucle está activo o
  arrancando.
- Liberación en `finally`.
- No se conserva el manejador si la corrutina termina síncronamente.
- Se suprime el reinicio automático durante una parada deliberada.

## Invariantes resultantes

1. Una instancia de `KitchenSystem` tiene como máximo un consumidor de cola.
2. Una única capacidad provisional mantiene como máximo un `activeWork`.
3. Las líneas pendientes conservan FIFO y permanecen `Queued` hasta ser activas.
4. Cada `OrderLineId` entra una sola vez en preparación por intento válido.
5. Una interrupción deliberada libera la reclamación antes de reconstruir la cola.

## Compatibilidad

- No cambia GUID de ninguno de los cuatro scripts sustituidos.
- No cambia el modelo canónico, los IDs ni el formato de snapshots.
- No requiere modificar la escena manualmente.
- Es acumulativo sobre 367D.

---

## SOURCE: REVISION_TECNICA_367E.md

Category: TECHNICAL_EVIDENCE

# BistroBuilder 367E — Revisión técnica

## Objetivo

Sustituir el temporizador único de `CustomerDiningFlow` por una autoridad runtime que controle el consumo de cada `CustomerId`, cada pase y cada `OrderLineId`.

## Autoridad y responsabilidades

`BistroBuilderCustomerDiningService` es la única autoridad de consumo. Mantiene una sesión por comanda canónica activa y un runtime persistible por cliente.

El estado de `CustomerGroup` y `RestaurantTable` se conserva como fachada operativa compatible. Ya no decide cuándo ha terminado de comer cada cliente.

## Flujo individual

Cada cliente sigue esta secuencia:

`WaitingForDish → Eating → Completed`

También existen estados terminales `Cancelled` y `Failed`.

Un cliente comienza un pase cuando todas las líneas que consume en ese `CourseIndex` están `Served`, `Consumed` o `Cancelled`. Otros miembros del mismo grupo pueden continuar en `WaitingForDish`.

Al terminar el tiempo individual, el cliente registra reclamaciones de consumo sobre sus líneas. Una línea pasa de `Served` a `Consumed` únicamente cuando todos sus consumidores la han reclamado.

## Atomicidad

`BistroBuilderCanonicalOrderService.TryConsumeServedLines` opera sobre una copia profunda del agregado. Valida todos los `LineId` antes de sustituir la comanda original.

Un `LineId` inválido, duplicado o no servido rechaza el lote completo. No quedan líneas parcialmente consumidas.

## Protección contra reentrada

Las mutaciones canónicas publican eventos síncronos. 367E no ejecuta reconciliaciones dentro de esos eventos: únicamente encola el `OrderId`.

El drenaje se realiza fuera de la pila de mutación mediante una guardia explícita y un límite de seguridad. Esta decisión evita repetir el defecto de corrutinas reentrantes corregido en 367D1.

## Recuperación transaccional

Si una interrupción ocurre después de persistir todas las reclamaciones de una línea compartida pero antes de aplicar `Served → Consumed`, la reconciliación detecta la línea completamente reclamada y finaliza la transición de forma idempotente.

## Cuenta y pago

`BillServiceFlow` consulta la guardia de consumo:

- antes de entregar la cuenta;
- después de la entrega;
- antes de completar el pago.

La cuenta solo queda autorizada cuando:

- todos los clientes están `Completed` o `Cancelled`;
- todas las líneas están `Consumed` o `Cancelled`;
- el agregado canónico está `Completed`;
- la fachada legacy está `Served`.

## Persistencia futura

`BistroBuilderCustomerDiningRuntimeSnapshot`, esquema 1, conserva:

- `OrderId` y `LegacyOrderId`;
- referencias estables de grupo y mesa;
- `CustomerId`;
- estado individual;
- pase actual;
- tiempo restante exacto;
- reclamaciones de líneas consumidas;
- revisiones y estado de cuenta.

El contrato está preparado para integrarse en `service.runtime`. Este paquete no registra todavía una sección de guardado de servicio abierto.

## Rendimiento

- Sin búsquedas de escena por frame.
- Índices por `OrderId` con comparador ordinal.
- Buffers reutilizables para finalizaciones y reconciliaciones.
- Sin LINQ en el runtime operativo.
- Sin creación de corrutinas por cliente.
- El avance temporal es lineal respecto a los clientes de comandas activas y no genera basura administrada por frame.

## Compatibilidad

Se conservan los GUID de:

- `BistroBuilderCanonicalOrderService.cs`
- `CustomerDiningFlow.cs`
- `FoodDeliveryServiceFlow.cs`
- `BillServiceFlow.cs`

`CustomerDiningFlow` permanece como adaptador pasivo para no romper prefabs ni referencias serializadas.

## Alcance diferido

367E no incorpora todavía:

- representación visual individual de cada comensal;
- reglas jugables completas de platos compartidos;
- secuenciación operativa de varios pases;
- rasgos y velocidades personales de consumo;
- persistencia completa del restaurante abierto.

Esas capacidades se añadirán sobre los contratos de `CustomerId`, consumidores, `CourseIndex` y snapshot creados aquí.

---

## SOURCE: REVISION_TECNICA_367F.md

Category: TECHNICAL_EVIDENCE

# BistroBuilder 367F — Revisión técnica

## Objetivo

Cerrar la ejecución funcional de platos compartidos y varios pases sobre las bases validadas de 367D1 y 367E.

## Autoridades

- `BistroBuilderCanonicalOrderService`: única autoridad transaccional de estados de línea.
- `BistroBuilderOrderCompositionService`: compositor puro de peticiones a partir de un perfil de datos.
- `BistroBuilderCourseAndSharingService`: autoridad operativa de liberación de pases y proyección persistible.
- `BistroBuilderCustomerDiningService`: autoridad de consumo por cliente y reclamaciones parciales de líneas compartidas.
- `KitchenSystem`: consumidor de líneas `Queued`, incluidas liberaciones posteriores.

## Flujo canónico

1. La comanda se crea con todas sus líneas en `Draft`.
2. Al enviarla a cocina, 367F somete todas las líneas a `Submitted` y libera únicamente el pase inicial a `Queued`.
3. La cocina procesa solo líneas liberadas.
4. Un plato compartido pasa a `Served` una sola vez y cada consumidor registra su propia reclamación.
5. La línea permanece `Served` mientras falte algún consumidor.
6. Tras completar todos los consumidores, la línea pasa atómicamente a `Consumed`.
7. Según la política, 367F libera el siguiente pase de `Submitted` a `Queued`.
8. La cuenta permanece bloqueada hasta que todos los clientes y todas las líneas estén resueltos.

## Políticas publicadas

- `PerTable`: libera un pase cuando todas las líneas de pases inferiores están `Consumed` o `Cancelled`.
- `PerCustomer`: libera las líneas del siguiente pase cuyos consumidores hayan resuelto sus pases inferiores.
- `Hybrid`: líneas individuales por consumidor; líneas compartidas coordinadas por mesa.
- `Manual`: no libera automáticamente; queda preparado para una acción operativa posterior.

La instalación piloto utiliza `PerTable`.

## Protección frente a reentrada

Los eventos canónicos y de consumo son síncronos, pero los manejadores de 367F solo encolan `OrderId`. La evaluación se drena bajo una guardia explícita después de terminar la mutación que originó el evento. La cocina aplica el mismo patrón para descubrir líneas liberadas posteriormente. Esto evita repetir la reentrada de corrutinas encontrada en 367D.

## Operaciones atómicas nuevas

- `TrySubmitOrderAndReleaseCourse(...)`
- `TryReleaseSubmittedLines(...)`

Ambas trabajan sobre una copia profunda, validan el agregado completo y solo sustituyen la comanda cuando toda la operación es válida.

## Compatibilidad legacy

La fachada `RestaurantOrder` continúa existiendo para el flujo actual, pero ya no exige que el número de líneas sea igual al número de clientes. La validez del enlace se comprueba por cobertura exacta de `CustomerId`, permitiendo relaciones muchos-a-muchos entre clientes y líneas.

## Persistencia futura

El snapshot de esquema 1 conserva:

- `OrderId` y `LegacyOrderId`;
- política de coordinación;
- pase inicial;
- pases liberados;
- líneas liberadas;
- revisión.

Las reclamaciones parciales de consumidores permanecen en el snapshot 367E/367F de consumo individual. La integración definitiva con guardado de servicio abierto se realizará en `service.runtime`.

## Alcance diferido

- selección jugable detallada de quién comparte cada plato;
- UI final de pase para cocina y sala;
- disparo manual desde camarero/jefe de sala;
- preparación anticipada y conservación térmica;
- bandejas y capacidad de transporte: 367G;
- modalidades de barra: 367H;
- recetas e inventario: 368.

---

## SOURCE: docs/99_HISTORY/CHAT_SOURCE_REGISTER.md

Category: HISTORICAL

# Bistro Builder — registro de decisiones procedentes de chats

La consolidación del 12/09/2026 incorpora decisiones que no tenían PDF/DOCX/MD propio. Este registro evita que desaparezcan por vivir solo en conversaciones.

| Conversación / frente | Decisiones incorporadas |
|---|---|
| Hoja de ruta Bistro Builder | estados vivos de bloques y sistemas transversales |
| Diseñar UI UX definitiva | HUD superior horizontal, Actividad, contexto derecho, franja inferior, estados visuales, tipografía, interacción contextual |
| Cámara profesional 369A/B/C | controles reservados, recentrado, 369C no destructiva, retirada posterior de vistas predefinidas |
| Implementar / Continuar BBSIS Unity | cierre BBSIS v1, contratos espaciales y hardening posterior |
| Investigar sistema de reservas / Interaction & Reservation | primitivas lógicas, Logical Grant Kernel y fronteras con BBSIS/Nav/Animation |
| Diseñar navegación y tráfico humano | autoridad de rutas/circulación, familias de agentes, puertas/sillas y prioridades de robustez |
| Animaciones runtime | autoridad visual, Motion Recipes y cierre/integración V1 |
| Implementa modo edición / Bistro listo para probar | Bloque 18, playtests, feedback, problemas UX y Construction Authoring |
| Procedural Layout & Furnishing System | BBPLFS, rama propia y obligación de integración con sistemas canónicos |
| Climatología Bistro Builder | clima uniforme, sin dirección/subtipos/microclimas; interior confortable |
| Inventario / Proveedores / Economía / Personal / Horarios / Reservas | decisiones de autoridad y estados de cierre que sustituyen documentos intermedios antiguos |

## Regla
Cuando una conversación posterior cambie una decisión, actualizar `90_DECISIONS/DECISION_REGISTER.md` y el documento canónico afectado. No copiar conversaciones completas al repositorio: conservar la decisión, su estado y su impacto técnico.

## Limitación de la migración
El objetivo es preservar decisiones de producto/arquitectura, no transcribir cada intercambio. Las conversaciones son evidencia histórica; los Markdown canónicos son la fuente operativa para agentes y desarrollo.

---

## SOURCE: docs/99_HISTORY/LEGACY_SOURCE_REGISTER.md

Category: HISTORICAL

# Bistro Builder — registro de fuentes históricas

Este registro preserva la procedencia sin convertir todo material antiguo en requisito vigente.

## Documentos del repositorio
- Auditorías y revisiones `365*`, `366*`, `367*` de seating, persistencia, carta, comandas, consumo y pases.
- `docs/FINANCE_*` — diseño, hardening y cierre del Bloque 3.
- `docs/STAFF_*` y `AUDITORIA_BLOQUE_5_HORARIOS.md` — Personal/Horarios; algunos estados intermedios quedaron superados por cierres posteriores.
- `docs/INTERACTION_V1_FUTURE_HARDENING_AUDIT.md` — auditoría futura obligatoria, no reapertura automática de V1.

## Documentos externos localizados
- `Desktop/ProyectoRestaurants!.docx` — concepto inicial amplio del juego.
- `Desktop/TiposClientes.docx` — ideas iniciales de arquetipos de cliente.
- `Desktop/PedidosOnline.docx` — propuesta de pedidos online/delivery.
- `Desktop/MenudelDia.docx` — propuesta de menú del día.
- `Desktop/ZonasRestaurante.docx` y `Desktop/Propinas.docx` — actualmente sin contenido material.

## Conversaciones
Desde abril de 2026 se tomaron decisiones que nunca generaron DOCX/PDF. Se consideran fuente histórica válida cuando contienen una decisión explícita. Las decisiones vigentes recuperadas se trasladan a `90_DECISIONS/DECISION_REGISTER.md` y al documento canónico del sistema correspondiente.

## Política
- No borrar los originales.
- No asumir que una idea de un DOCX inicial sigue aprobada.
- No promover a requisito una pregunta, duda o brainstorming antiguo.
- Ante contradicción, prevalece la decisión explícita posterior y se conserva la anterior como `SUPERADA`.
- El código integrado sirve como evidencia técnica, pero no debe usarse para reintroducir una decisión de producto posteriormente revocada.

---

## SOURCE: docs/99_HISTORY/legacy-docx/DISCULPAS.md

Category: HISTORICAL

# Legacy source - DISCULPAS

> Preservation conversion. Historical source; canonical docs take precedence.

## DISCULPAS_

Cuando un cliente tiene una mala experiencia, el jugador puede elegir cómo compensarlo antes de que se vaya o deje reseña. *Pensar si eliminar o añadir problemas que pongo a continuación*

ProblemaEjemplo

Espera largaEl cliente lleva 15 min sin plato

Plato equivocadoRecibe algo que no pidió

Plato fríoEl plato tardó demasiado en servirse

Comida mal hechaQuemada, cruda o mala presentación

Mesa suciaEl cliente se sienta en una mesa mal limpiada

Pedido incompletoFalta bebida, postre o plato

Mal servicioCamarero tarda, ignora o se equivoca

Baño sucioCliente se queja de higiene

Ruido/ambiente maloDemasiado ruido, mala temperatura

Opciones:

Ignorar

Disculparse

Compensar

Ignorar: No cuesta dinero ni tiempo.

Efecto:

el cliente sigue molesto

mayor probabilidad de mala reseña

baja reputación si se repite

¿Cuándo usar? Solo cuando el plato no llegue a tiempo porque haya saturación de gente y pedidos.

---

## SOURCE: docs/99_HISTORY/legacy-docx/MenudelDia.md

Category: HISTORICAL

# Legacy source - MenudelDia

> Preservation conversion. Historical source; canonical docs take precedence.

Menú del día

Cada día, antes de abrir el restaurante, el jugador puede crear un menú cerrado con:

1 primer plato

1 segundo plato

1 postre

1 bebida incluida o no incluida

precio fijo

número máximo de menús disponibles

Ventajas:

El menú del día debe ser más barato que pedir platos sueltos, pero más fácil de vender en volumen.

Variedad diaria

Atraer clientes al mediodía

Pensar en cómo hacer menú del día rentable.

Pensar si ponemos un máximo de menús del día

Pensar en cómo el cliente puede elegirlo al igual que los platos de carta

Pensar en pantalla de opiniones de los clientes sobre el menú del día (apartado especial en opiniones)

---

## SOURCE: docs/99_HISTORY/legacy-docx/PedidosOnline.md

Category: HISTORICAL

# Legacy source - PedidosOnline

> Preservation conversion. Historical source; canonical docs take precedence.

Pedidos online

Durante el servicio, además de clientes en sala, entran pedidos online desde una tablet, TPV o pantalla de pedidos.

El jugador decide:

Aceptar pedido → entra en cola de cocina. ¿Poder asignar un cocinero sólo para pedidos online?

Rechazar pedido → no genera trabajo, pero puede bajar reputación online si se hace mucho.

Pausar pedidos online → útil en horas punta.

La idea es que el jugador piense: “¿Me compensa aceptar este pedido ahora o voy a saturar la cocina?”

Paso 1 — Entra pedido online

Aparece una notificación: Pedido online #042

2 hamburguesas

1 ensalada

1 tiramisú

Tiempo límite: 18 minutos

Pago estimado: 34 €

Penalización por retraso: baja valoración online

Opciones:

Aceptar

Rechazar

Posponer 1 minuto o tiempo X

Paso 2 — El pedido entra en cocina

Una vez aceptado, se imprime o aparece en pantalla como ticket:

Ticket online

Mesa: no aplica

Tipo: entrega

Prioridad: media

Hora límite: 18:45

En cocina se mezcla con los pedidos de sala.

Aquí está la gracia: si se aceptan demasiados pedidos online, los clientes sentados también esperan más.

Paso 3 — Cocina prepara el pedido

Cada plato usa la misma lógica que los platos de sala:

necesita ingredientes

ocupa muebles y electrodomésticos de cocina

ocupa cocinero

tiene tiempo de preparación

Ejemplo:

PlatoTiempoEstación

Hamburguesa5 minPlancha

Ensalada3 minPreparación fría

Tiramisú1 minPostres

Paso 4 — Empaquetado

Cuando todos los platos están listos, el pedido pasa a una zona nueva: Zona de empaquetado (NO ESTABA PREVISTA AL PRINCIPIO, HABRÁ QUE VER CÓMO LO AÑADIMOS ETC…)

¿Quién lo empaqueta?

Aquí se necesita:

bolsas

cajas

cubiertos desechables

etiquetas

bebida si aplica

Estados del pedido:

En cocina

Listo para empaquetar

Empaquetando

Esperando repartidor

Entregado

Paso 5 — Repartidor recoge

Aparece un repartidor en la entrada o mostrador.

Si el pedido está listo:

lo recoge

ganas dinero

sube reputación si fue rápido

Si el repartidor espera demasiado:

penalización leve

baja valoración online

¿Qué necesitaríamos añadir al juego para esta sección?

Objetos visuales

Tablet de pedidos online

Pantalla de cocina / impresora de tickets

Estantería de pedidos preparados

Bolsas de reparto por ejemplo con las iniciales del Restaurante

Cajas de comida

Zona de recogida

Repartidor con mochila térmica con las iniciales del restaurante

El jugador podrá elegir:

Priorizar sala

Priorizar online

Equilibrado

Esto afecta la reputación.

Si priorizas online:

ganas más volumen

clientes del comedor se enfadan

Si priorizas sala:

mejor experiencia presencial

pierdes oportunidades online

Activar o pausar pedidos:

Botón simple: Pedidos online: ON/OFF

Ejemplo:

hora tranquila → activas online

hora punta → pausas

necesitas dinero rápido → aceptas más riesgo

-Reputación y sistema de valoraciones: Donde se refleje el servicio en mesa y a domicilio (para desarrollar y pensar)

---

## SOURCE: docs/99_HISTORY/legacy-docx/Propinas.md

Category: HISTORICAL

# Legacy source - Propinas

> Preservation conversion. Historical source; canonical docs take precedence.

*(Source document contains no text.)*

---

## SOURCE: docs/99_HISTORY/legacy-docx/ProyectoRestaurants_.md

Category: HISTORICAL

# Legacy source - ProyectoRestaurants!

> Preservation conversion. Historical source; canonical docs take precedence.

## IDEA PRINCIPAL

El usuario es el encargado de gestionar el / los restaurantes.

-Particiona el local: Cocina, salón, baños…

-Amuebla cada estancia: mesas, sillas, luces, decoración, electrodomésticos, cocina, baños…

-Elige los platos que se incluyen en la carta del restaurante / Editar platos (Añadir y/o eliminar ingredientes), edición de recetas.

-Elige y compra a los proveedores tanto comida como muebles y electrodomésticos

-Contrata y entrena a los empleados del restaurante: Camareros, maître, cocineros, ayudantes de cocina…

-Solicitar crédito al banco – Devolver crédito

-Configurar campaña de Marketing: TV, Radio, RRSS…

-Horario de apertura y cierre

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

ENTRANDO EN MATERIA, PRIMEROS PASOS

El usuario crea un personaje / avatar:

Nombre y apellidos

País de procedencia

Edición de físico y vestimenta

No sé cómo, pero tendríamos que darle un presupuesto inicial para lo siguiente:

El usuario tendrá que elegir comprar / ¿alquilar? un local entre varios que estén a la disponibles en venta / ¿alquiler? Cada local disponible en venta / ¿alquiler? debería mostrar:

Tamaño

Precio definitivo de compra o mensualidad de alquiler

NO SE EN QUÉ MOMENTO EL USUARIO DEBE ELEGIR LA ESPECIALIDAD DEL RESTAURANTE, SI AL ADQUIRIR EL LOCAL O CUÁNDO

Ya con el local disponible, se abrirá la vista desde dentro y como estará diáfano hay que editarlo desde el modo Edición:

Elegir cada estancia:

-Salón comedor

-Cocina

-Baños

Pintar, elegir tipos de suelo y Amueblar cada estancia:

-Salón comedor: Disposición de las mesas teniendo en cuenta la distancia para el paso entre una y otra

Añadir luces, lámparas, velas, candeleros, etc.…

Añadir decoración extra (cuadros, carteles, lo que decidamos que haya de extra)

-Cocina:   Añadir luces. Disposición de los fogones, encimeras, friegaplatos industrial, electrodomésticos extra (batidora, microondas…) *PARA COCINAR SEGÚN QUÉ PLATOS, LA COCINA TENDRÁ QUE DISPONER DE LOS ELECTRODOMÉSTICOS NECESARIOS*

-Baños:   Añadir luces. Disposición de baños entre Hombres y Mujeres, y dentro de cada uno elegir la cantidad de WC’s y lavaderos de manos.

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

Una vez amueblado el restaurante:

## CONTRATAR PERSONAL

Habrá un icono de Contratación o similar que abrirá un desplegable en el que podremos elegir entre varios posibles empleados.

Por cada aspirante, se muestra: -Avatar programado aleatoriamente

-Nombre y apellidos

-Años

-Experiencia: En la experiencia dependerá varias cosas:

Experiencia Maitre: Experiencia Normal

Experiencia camareros: Experiencia Normal

Experiencia cocineros: Experiencia Normal y Experiencia Especialidad (Italiana, Española, Francesa…) AQUÍ YA TENDRIAMOS QUE SABER ENTONCES LA ESPECIALIDAD DEL RESTAURANTE

Experiencia Ayudantes de cocina: Experiencia Normal

Si la experiencia es Especialidad, el cocinero cocinará más rápido el plato y con mas calidad. Si el cocinero tiene experiencia Normal, tardará más tiempo y no saldrá tan rico con lo que los clientes se podrán quejar de que el plato no está rico.

-Franja de salario esperado: El salario que espera de primeras cada uno dependerá de su puesto y su experiencia.

Salón Comedor: -Maitre

-Camareros

Cocina: -Cocinero o cocineros

- ¿Ayudante de cocina?

No se si que cada empleado haga el horario de todo el día o tener dos horarios, comidas y cenas.

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

DISEÑO DEL MENÚ

Suponiendo que ya tenemos elegida la especialidad del restaurante, deberíamos diseñar la carta de nuestro restaurante:

-Elegir un diseño de portada entre varias opciones (modernas, casuals, clásicas, yo que se…) y mostrar en el momento cómo quedaría el diseño (No me metería al menos en las primeras versiones en poder editar y personalizar las portadas)

-Mostrar una lista de recetas según se pinche en las opciones de: -Entrantes

-Primeros platos

-Platos principales

- ¿Postres?

Cada plato mostrará una previsualización según vayas haciendo click en el y una pequeña descripción del mismo:

Ingredientes

Mas apto o menos apto para la especialidad de nuestro restaurante

Dificultad de preparación. Si es difícil de preparar, pero nuestro cocinero tiene experiencia alta, el tiempo de preparación será menos y saldrá mas rico.

No sé si añadir si es un plato conocido o no entre los clientes (POPULARIDAD)

Y la opción de añadirlo o quitarlo de la carta.

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## PROVEEDORES

Esta sección la tengo muy entre pinzas.

Se muestran los proveedores que van poniéndose en contacto con nosotros y que podemos elegir entre trabajar con ellos o no.

Ellos aleatoriamente, nos mandarán un email o como sea dándose a conocer y mostrándonos su género y precios, haciéndonos alguna oferta o no.

Tipos de proveedores: -Mobiliario

-Fruta

-Carnes y pescados

-Condimentos, pasta…

Podemos pensar si se ponen en contacto con nosotros según seamos mas “famosos” o también nos escriban para hacerse ellos hueco.

También habrá proveedores mejores y peores. No todos los proveedores nos ofrecerán mejores productos.

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## MARKETING

Sección con pinzas también.

No estará disponible nada más empezar, dejaremos un plazo (no se cuánto) para poder disponer de ellas.

¿pueden contratarse simultáneamente?

Varias formas de publicitarse:

Anuncios de TV: -Descripción (% de atracción de clientes). Entiendo que según contrates mas tiempo de campaña, mas clientes atrae, no se cómo quedaría esto.

Duración de la campaña

Precio

-Prensa y radio: Lo mismo

-RRSS: lo mismo

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## HORARIOS

El usuario debe elegir el horario de apertura y cierre del restaurante.

Si se elige un horario muy exhaustivo, en el caso que los trabajadores trabajen una jornada entera se podrán quejar, pidiendo un aumento de sueldo.

Si el horario es más justo, más ajustado, los trabajadores estarán más contentos. Habría que matizar esta idea.

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## TIEMPO DE SERVICIO

El tiempo para servir un plato deberíamos programarlo, teniendo en cuenta la habilidad de los cocineros y la rapidez de los camareros.

Aquí entra también que habrá que programar la inteligencia para elegir las mejores rutas para llegar a las mesas.

*VER sección de DISCULPAS*

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## COSAS PARA DIFERENCIARNOS

-Disculpas:

Cuando un cliente tiene una mala experiencia, el jugador puede elegir cómo compensarlo antes de que se vaya o deje reseña. *Pensar si eliminar o añadir problemas que pongo a continuación*

ProblemaEjemplo

Espera largaEl cliente lleva 15 min sin plato

Plato equivocadoRecibe algo que no pidió

Plato fríoEl plato tardó demasiado en servirse

Comida mal hechaQuemada, cruda o mala presentación

Mesa suciaEl cliente se sienta en una mesa mal limpiada

Pedido incompletoFalta bebida, postre o plato

Mal servicioCamarero tarda, ignora o se equivoca

Baño sucioCliente se queja de higiene

Ruido/ambiente maloDemasiado ruido, mala temperatura

Opciones:

Ignorar

Disculparse

Compensar

Ignorar: No cuesta dinero ni tiempo.

Efecto:

el cliente sigue molesto

mayor probabilidad de mala reseña

baja reputación si se repite

-Lógica de PROPINAS (a desarrollar y pensar)

-Tipos de clientes diferentes:

ejecutivo → quiere rapidez

familia → tolera la espera, pide más platos

pareja → valora ambiente

foodie → exige calidad

Zonas de restaurante

Interior

terraza

barra

Y cada zona:

atrae clientes distintos

funciona diferente

Pedidos online

Durante el servicio, además de clientes en sala, entran pedidos online desde una tablet, TPV o pantalla de pedidos.

El jugador decide:

Aceptar pedido → entra en cola de cocina. ¿Poder asignar un cocinero sólo para pedidos online?

Rechazar pedido → no genera trabajo, pero puede bajar reputación online si se hace mucho.

Pausar pedidos online → útil en horas punta.

La idea es que el jugador piense: “¿Me compensa aceptar este pedido ahora o voy a saturar la cocina?”

Paso 1 — Entra pedido online

Aparece una notificación: Pedido online #042

2 hamburguesas

1 ensalada

1 tiramisú

Tiempo límite: 18 minutos

Pago estimado: 34 €

Penalización por retraso: baja valoración online

Opciones:

Aceptar

Rechazar

Posponer 1 minuto o tiempo X

Paso 2 — El pedido entra en cocina

Una vez aceptado, se imprime o aparece en pantalla como ticket:

Ticket online

Mesa: no aplica

Tipo: entrega

Prioridad: media

Hora límite: 18:45

En cocina se mezcla con los pedidos de sala.

Aquí está la gracia: si se aceptan demasiados pedidos online, los clientes sentados también esperan más.

Paso 3 — Cocina prepara el pedido

Cada plato usa la misma lógica que los platos de sala:

necesita ingredientes

ocupa muebles y electrodomésticos de cocina

ocupa cocinero

tiene tiempo de preparación

Ejemplo:

PlatoTiempoEstación

Hamburguesa5 minPlancha

Ensalada3 minPreparación fría

Tiramisú1 minPostres

Paso 4 — Empaquetado

Cuando todos los platos están listos, el pedido pasa a una zona nueva: Zona de empaquetado (NO ESTABA PREVISTA AL PRINCIPIO, HABRÁ QUE VER CÓMO LO AÑADIMOS ETC…)

¿Quién lo empaqueta?

Aquí se necesita:

bolsas

cajas

cubiertos desechables

etiquetas

bebida si aplica

Estados del pedido:

En cocina

Listo para empaquetar

Empaquetando

Esperando repartidor

Entregado

Paso 5 — Repartidor recoge

Aparece un repartidor en la entrada o mostrador.

Si el pedido está listo:

lo recoge

ganas dinero

sube reputación si fue rápido

Si el repartidor espera demasiado:

penalización leve

baja valoración online

¿Qué necesitaríamos añadir al juego para esta sección?

Objetos visuales

Tablet de pedidos online

Pantalla de cocina / impresora de tickets

Estantería de pedidos preparados

Bolsas de reparto por ejemplo con las iniciales del Restaurante

Cajas de comida

Zona de recogida

Repartidor con mochila térmica con las iniciales del restaurante

El jugador podrá elegir:

Priorizar sala

Priorizar online

Equilibrado

Esto afecta la reputación.

Si priorizas online:

ganas más volumen

clientes del comedor se enfadan

Si priorizas sala:

mejor experiencia presencial

pierdes oportunidades online

Activar o pausar pedidos:

Botón simple: Pedidos online: ON/OFF

Ejemplo:

hora tranquila → activas online

hora punta → pausas

necesitas dinero rápido → aceptas más riesgo

-Reputación y sistema de valoraciones: Donde se refleje el servicio en mesa y a domicilio (para desarrollar y pensar)

Menú del día

Cada día, antes de abrir el restaurante, el jugador puede crear un menú cerrado con:

1 primer plato

1 segundo plato

1 postre

1 bebida incluida o no incluida

precio fijo

número máximo de menús disponibles

Ventajas:

El menú del día debe ser más barato que pedir platos sueltos, pero más fácil de vender en volumen.

Variedad diaria

Atraer clientes al mediodía

Pensar en como hacer menú del día rentable.

Pensar si ponemos un máximo de menús del día

Pensar en cómo el cliente puede elegirlo al igual que los platos de carta

Pensar en pantalla de opiniones de los clientes sobre el menú del día (apartado especial en opiniones)

---

## SOURCE: docs/99_HISTORY/legacy-docx/TiposClientes.md

Category: HISTORICAL

# Legacy source - TiposClientes

> Preservation conversion. Historical source; canonical docs take precedence.

-Tipos de clientes diferentes:

ejecutivo → quiere rapidez

familia → tolera la espera, pide más platos

pareja → valora ambiente y tranquilidad

foodie / critico hostelero → exige calidad

---

## SOURCE: docs/99_HISTORY/legacy-docx/ZonasRestaurante.md

Category: HISTORICAL

# Legacy source - ZonasRestaurante

> Preservation conversion. Historical source; canonical docs take precedence.

*(Source document contains no text.)*

---

## SOURCE: Assets/BistroBuilder/UI/Iconography/THIRD_PARTY_NOTICES.md

Category: THIRD_PARTY

# Third-party notices — Iconografía 21B

## Lucide

Bistro Builder Iconography 21B can synchronize SVG icon sources from Lucide version 1.45.0.

Copyright (c) Lucide Contributors

Source: https://github.com/lucide-icons/lucide

License: ISC License.

Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
