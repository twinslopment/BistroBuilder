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

## SOURCE: docs/00_PRODUCT/BRANCH_AUDIT_20260918.md

Category: CANONICAL

# Bistro Builder — Auditoría de ramas e integración
**Fecha:** 2026-09-18
**Base auditada:** `integration/production-all-current-20260918`
**Rama maestra creada:** `integration/master-current-20260918`
**Objetivo:** mantener una única base acumulativa y decidir qué trabajo puede integrarse sin reintroducir código antiguo.

## Criterios usados
- Ancestro Git: si la rama ya está contenida en producción, no se vuelve a mezclar.
- Equivalencia de parche: se usa `git cherry` para detectar trabajo ya integrado con otro hash.
- Merge-tree: simulación de merge sin tocar el working tree para detectar conflictos.
- Estado real del worktree: se revisan cambios sin commit, además de los commits.
- Evidencia de validación: build, autotest o validadores existentes.
- Regla conservadora: una rama antigua no se integra entera solo porque contenga una mejora útil.

## Rama maestra
Se creó `integration/master-current-20260918` desde la producción vigente.
Punto inicial: `bc1543b feat(furniture): harden authoring workflow`.
La rama fue publicada en origin y se usa desde ahora como base acumulativa.

## Integrado durante esta auditoría
### Catalog UI V1
Rama: `feature/catalog-ui-v1`.
Estado previo: limpia, 7 commits exclusivos, 12 archivos de contribución y merge simulado limpio.
Validación: build Windows correcta, `Build Finished, Result: Success`.
Resultado: integrada en la maestra con `e4796e22 merge(catalog): integrate validated catalog UI v1`.
## Ya contenido en producción — no volver a mezclar
- `feature/dish-images-catalog`: su historial comprometido ya es ancestro de producción.
- `feature/18n-construction-authoring-v1`: contenido comprometido ya absorbido.
- `feature/bbplfs-v1`: ya absorbido.
- `integration/ui-combined-final-20260917-v2`: ya absorbido.
- `integration/astra-chat-live-20260914`: ya absorbido.
- Economía V1, horarios/personal, reservas, marketing/reputación/progresión, clientes avanzados y comandas avanzadas: ramas canónicas ya absorbidas.

## Sistemas cuyo nombre de rama engaña
### Clima
El worktree `BistroBuilder_ClimateV1` mostraba 24 archivos staged como nuevos porque su base es antigua.
Comparación de blobs: los 24 archivos son idénticos byte a byte a producción.
Producción además contiene el commit integrado `5f9d64ce playtest: integrate climate weather v1`.
Decisión: no merge; Clima ya está dentro.

### Animación
La rama original conserva commits no equivalentes, pero producción contiene una reconstrucción posterior:
- `0191ab50 animation: rebuild character interaction animation v1`
- `7278c19f animation: harden runtime bootstrap and final V1 validation`
- `04f3de98 animation: finalize integrated V1 scene and legacy gate compatibility`
Decisión: no reintroducir los commits antiguos `1887d66` / `6441c31`.

### Interaction & Reservation
Los seis commits exclusivos de la rama tienen parches equivalentes en producción.
Producción contiene además `f23e6378 interaction: restore logical reservation system v1`.
Decisión: no merge.

### Navigation & Crowd Flow
Todos los parches específicos de la rama auditada tienen equivalentes en producción.
Producción incluye `49df2207`, `90b13e62`, `76199b61` y el hardening posterior.
Decisión: no merge.
## UI / iconografía
### UI visual polish
Los commits de HUD, selección de mesa y corrección del parpadeo están ya integrados en producción con otros hashes:
`195d74d3`, `4b27c92c`, `ce0072c6`, `cf962809`, `4067376f`, `4d6095d6`.
Solo aparece una diferencia de patch-id en un test, no en la funcionalidad.
Decisión: no merge de la rama completa.

### 21A UI/UX definitiva
La rama es una base vieja y produce conflictos add/add contra los mismos sistemas ya existentes.
Los commits finales de cierre/navegación tienen equivalentes en producción.
Decisión: conservar como referencia histórica; no merge.

### 21B Iconography
Producción ya contiene 169 assets bajo Iconography, catálogo runtime y `BBIconographyRuntime`.
El merge de la rama antigua genera numerosos conflictos add/add en metas y recursos.
Decisión: no merge de rama completa; cualquier icono nuevo se compara individualmente.

## Bloques 12–17 y BBSIS
- Cocina avanzada: el core está en producción; la rama antigua contiene principalmente hardening de harness/batch y entra con conflicto de escena.
- Camareros avanzados: producción contiene las restauraciones y rutas profesionales; la rama antigua entra con múltiples add/add.
- Front of House: core restaurado en producción; el único commit de metadata no justifica mezclar toda la rama y el archivo ya existe.
- Fin de servicio: todos sus commits auditados tienen equivalentes en producción.
- Nueva partida: producción ya contiene V2 (`a4304711`) y el arreglo de startup/local vacío (`6b99e907`); no traer V1/V2 antiguas otra vez.
- BBSIS: producción contiene rebuild e incremental hardening (`765167dd`, `c9e52556`, `423b3bfc`).
Decisión: no merge masivo de ninguna de estas ramas antiguas.
## Playtest/all-current y fixes agregados
`playtest/all-current-20260912` y `fix/empty-premises-boundary-scroll-access` son agregados sobre bases antiguas.
Contienen UI 21A/21B, scroll, BBSIS y new-game que producción ya posee en implementaciones posteriores.
El merge simulado produce muchos conflictos add/add.
Producción ya contiene:
- `50022910 21C: implement scoped internal scrolling`
- `ffa3e23f 21C: make menu scrolling universal and clean UI artifacts`
- `62201886 18N: fix empty premises boundaries and scroll access`
- `e8b1f21b fix(edit-mode): restore premises floor and remove select mode`
Decisión: no merge de estos agregados.

## Worktrees con cambios locales sin commit
### BistroBuilder raíz
Rama: `feature/dish-images-catalog`.
Estado observado: 52 archivos tracked modificados + 115 untracked.
Mezcla UI/UX, construcción, documentación, settings y herramientas.
Decisión: NO integrar en bloque. Debe separarse por sistema y validar cada bloque.

### CatalogUIV1
Después de la build validada aparecieron cambios locales nuevos en:
- `RestaurantPlaceableCatalogPanel.cs`
- `RestaurantEditInteractionController.cs`
- `BistroBuilderUiShell.cs`
- nuevo `BistroBuilderUiShell.EditModeChrome.cs`
Ese WIP no forma parte del merge estable `e4796e22`.
Decisión: mantener fuera hasta compilar, revisar encoding/UI y validar visualmente.

### ConstructionAuthoringV1
Los cambios locales actuales son principalmente ProjectSettings, URP/TMP, metadatos ThirdParty y solución IDE.
No hay nuevo bloque funcional de código pendiente en ese worktree.
Decisión: no subir automáticamente.

### OpeningV2 / UIUXDefinitive
Los cambios locales restantes son probes, settings, scripts auxiliares y reportes.
No representan una versión canónica superior al contenido integrado.
Decisión: no subir automáticamente.
## Política de integración desde hoy
1. Todo sistema nuevo parte de `integration/master-current-20260918`.
2. Se desarrolla en una rama propia.
3. Se exige worktree limpio o cambios claramente acotados.
4. Se valida compilación y, cuando exista, autotest/build funcional.
5. Se simula merge contra la maestra.
6. Si está estable, se integra y se hace push automáticamente.
7. Una rama vieja no se mergea completa para rescatar una sola corrección.
8. Los cambios locales mezclados se separan primero por sistema.
9. La build global se genera siempre desde la rama maestra.
10. Producción histórica queda como referencia; la maestra pasa a ser el punto de acumulación actual.

## Estado tras la auditoría
- Maestra creada y publicada.
- BBFFVAS ya incluido por la base de producción.
- Catalog UI V1 validado e integrado.
- Clima confirmado presente.
- Animation V1 confirmado integrado mediante reconstrucción posterior.
- Interaction/Reservation confirmado presente.
- Navigation/BBSIS y bloques canónicos principales confirmados presentes.
- Ramas agregadas/antiguas marcadas para no merge masivo.
- WIP local identificado y aislado de la maestra.

## Cierre de consolidación — 2026-09-19
Durante la separación del WIP se detectaron dos mejoras válidas que habían quedado fuera de la rama acumulativa por integraciones posteriores.

### Recuperación de Construcción
- Recuperada la reutilización de paredes compartidas al crear habitaciones mediante `ConstructionWallCoverage.AppendUncovered`.
- Recuperado el alta runtime de `BistroBuilderConstructionPlayerPanel`.
- Añadida/restaurada cobertura de regresión para habitaciones que reutilizan límites existentes.
- Commit de recuperación: `e14fc4d7`.
- Merge en maestra: `7ca5ed7c`.
- Validación Unity 6000.3.19f1: **32/32 escenarios, 341 assertions, 0 fallos**.

### Recuperación de Navegación
- Los rebuilds de topología disparados por edición quedan diferidos mientras un `Load` está restaurando el restaurante.
- El rebuild pendiente se ejecuta al terminar la carga, evitando topologías intermedias.
- Se conserva el hardening moderno de navegación; no se recuperó la versión antigua completa.
- Commit de recuperación: `a295d578`.
- Merge en maestra: `6999e779`.
- Save/Load real de Navigation 17: **PASS, exit code 0**.

### Build final de la maestra
- Rama validada: `integration/master-current-20260918`.
- Unity: `6000.3.19f1`.
- Resultado: **Build Successful / Build Finished, Result: Success**.
- Player total: **163038680 bytes**.
- Warnings: **13**.
- Pantalla: `FullScreenWindow`, resolución nativa activa.
- Salida: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

La rama `integration/master-current-20260918` queda como base acumulativa canónica para el siguiente trabajo estable.

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
- **BB Furniture Finishes & Variants Authoring System (BBFFVAS):** IMPLEMENTACIÓN TÉCNICA V1 ACTIVA; núcleo, autoría, Acabado Automático, miniaturas, publicación, runtime binding e importación Smart Assets/Asset Studio BB validados en Unity 6000.3.19f1. Integración en producción pendiente.

## Integración actual
`playtest/all-current-20260912` contiene trabajo posterior a `integration/chat-final-20260911` y, al crear esta documentación, tenía conflictos de merge sin resolver. Esta rama documental se aisló deliberadamente para no tocar esa integración.

## Actualización 24/09/2026 — Nueva partida
Diseño marfil clásico de tres opciones aprobado y maqueta interactiva incorporada en integration/master-current-20260918. Pantalla nativa integrada con tres preparaciones, creación/carga y menú de retorno; no cambia el cierre funcional del servicio de apertura. Fuente: [Nueva partida](../30_UI_UX/NEW_GAME_APPROVED.md).

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
- **BBFFVAS — Acabados y Variantes de Mobiliario:** herramienta interna de autoría para definir, completar, validar y publicar acabados/variantes visuales sin duplicar geometría; no es UI de jugador.

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

En el HUD de modo normal/servicio, **Climatología se muestra en la barra horizontal inferior**, junto a **Velocidad** y `Caja`. Esta ubicación es parte de la composición UI vinculante y no debe duplicarse en la barra superior.

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

## Dirección gráfica aprobada — Propuesta C revisada
La referencia visual vinculante para el Catálogo de artículos y su inspector es la **Propuesta C revisada**. Sustituye las propuestas A, B y D como dirección principal.

- Mantener una estética clara, cálida y premium coherente con Bistro Builder: superficies marfil/crema, texto carbón, acentos verde oliva y sombras suaves. Evitar apariencia de herramienta CAD fría, interfaz arcade o paneles excesivamente oscuros.
- El catálogo izquierdo se presenta como panel claro de esquinas redondeadas, jerarquía limpia y respiración visual. Cabecera con título, cierre, búsqueda; debajo categorías con icono + texto, filtros secundarios y cuadrícula de dos columnas.
- Las tarjetas usan miniatura grande y homogénea, nombre y precio. Selección activa: contorno verde oliva y confirmación/check discreto. Favorito: estrella/acento cálido. Bloqueado: desaturado, candado y motivo legible.
- El viewport central sigue siendo el protagonista. La cuadrícula debe leerse integrada con el suelo. El ghost usa transparencia y contorno/huella verde de validez sin ocultar el asset ni el pavimento.
- El inspector derecho es un panel claro y vertical. Orden visual: nombre del artículo → preview grande → descripción breve → precio/ámbito → variantes de color → dimensiones → **Reglas de colocación** → estado final de colocación.
- **Reglas de colocación** se muestran como checklist con iconos verdes y, para una silla estándar de suelo, incluye como mínimo: `Se puede colocar en suelos`, `Requiere espacio libre` y `Apto para interior y exterior`. Las reglas reales dependen de los metadatos del artículo; la UI no inventa capacidades.
- El estado final del inspector usa una caja verde suave, por ejemplo `Listo para colocar`, acompañada por una explicación breve (`No hay obstrucciones en este espacio`). Un estado inválido debe conservar la misma jerarquía y explicar el motivo.
- La barra inferior mantiene fondo claro y separa **contextos/herramientas de edición** de **acciones sobre el objeto**. Puede exponer Construir/Superficies/Paredes/Decoración/Iluminación/Servicios/Otro según contexto y acciones como Eliminar/Rotar/Duplicar; no debe convertirse en un segundo catálogo de artículos.
- La barra superior conserva la identidad global de Bistro Builder, el estado `Modo Edición`, controles de cámara/edición compatibles, fecha/hora, caja y acceso a continuar/salir sin competir con el viewport.
- Recoleta se reserva para identidad/títulos cuando corresponda e Inter/sans equivalente para controles, datos y microcopy.
- Espaciado, iconografía y estados deben reutilizar el Design System del juego; no crear un lenguaje visual paralelo exclusivo del editor.

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

## SOURCE: docs/20_GAME_SYSTEMS/FURNITURE_FINISHES_VARIANTS_AUTHORING.md

Category: CANONICAL

# Bistro Builder — Sistema de Acabados y Variantes de Mobiliario

**Nombre técnico:** BB Furniture Finishes & Variants Authoring System (BBFFVAS)

**Estado:** IMPLEMENTACIÓN TÉCNICA V1 — núcleo validado en Unity 6000.3.19f1; integración en producción pendiente

**Ámbito:** herramienta interna de autoría para el creador; no es una mecánica ni una interfaz destinada al jugador.

## 1. Objetivo

BBFFVAS permite crear, completar, previsualizar, validar, organizar y publicar acabados y variantes visuales de mobiliario ya integrado en Bistro Builder sin duplicar prefabs manualmente, sin tocar código y sin editar materiales uno a uno en el Inspector estándar de Unity.

El caso base es un único mueble maestro —por ejemplo `BB_Chair_Master_001`— del que pueden publicarse múltiples acabados comerciales manteniendo geometría, pivote, footprint, colliders, contratos BBSIS y comportamiento.

## 2. Principios vinculantes

1. **Editor-only.** La herramienta existe para producción de contenido. El jugador no accede a ella.
2. **Geometría única.** Un cambio exclusivamente visual no crea otro modelo, collider ni contrato espacial.
3. **Variantes data-driven.** Una variante es una combinación estable de acabados asignados a zonas semánticas.
4. **Biblioteca reutilizable.** Maderas, telas, metales, piedra, vidrio, pintura, cuero y otros acabados se registran una vez y pueden reutilizarse en muchos muebles.
5. **No sobrescritura automática.** Ninguna automatización modifica trabajo visual válido salvo orden explícita del creador.
6. **Preview antes de aplicar.** Toda propuesta automática se previsualiza y requiere aprobación.
7. **IDs estables.** Renombrar un acabado o una variante no rompe referencias ni contenido publicado.
8. **Undo/Redo real.** Las operaciones de autoría deben ser reversibles mediante el sistema de edición del Editor.

## 3. Qué es y qué no es una variante

Una **variante de acabado** cambia apariencia: madera, color, tapizado, metal, piedra, vidrio, mapas PBR o parámetros compatibles.

No es una variante de acabado si cambia geometría, dimensiones, patas, brazos, respaldo, número de plazas, footprint, collider, puertos de interacción, animaciones o comportamiento. Ese caso pertenece a otro asset o a una variante estructural fuera de este sistema.

## 4. Modelo conceptual

```text
FurnitureDefinition
├─ furnitureId
├─ prefab/model reference
├─ finishProfile
│  ├─ semantic zones
│  └─ compatible finish families
├─ defaultVariantId
└─ variants[]
   └─ FurnitureFinishVariant
      ├─ variantId
      ├─ displayName
      ├─ swatch/thumbnail
      └─ bindings[]
         ├─ zoneId
         └─ finishId
```

Cada instancia de `FurnitureFinishVariant` referencia acabados canónicos; no almacena copias innecesarias de texturas o materiales.

## 5. Zonas semánticas de acabado

Cada asset define una sola vez sus zonas de acabado con nombres comprensibles. Ejemplos:

- Silla: `Structure`, `Seat`, `Back`, `Metal`.
- Mesa: `Top`, `Structure`, `Metal`.
- Sofá: `Frame`, `Fabric`, `Legs`.
- Lámpara: `Shade`, `Structure`, `Cable`, `BulbHousing`.

Una zona puede mapear uno o varios Material Slots y, cuando proceda, máscaras internas de un material.

La UI de autoría nunca debe obligar a trabajar con nombres opacos como `Element 0` o `Material.003` una vez exista clasificación semántica.

## 6. Familias de acabado

La biblioteca central clasifica los acabados al menos en:

- Wood
- Fabric
- Leather
- Metal
- Stone
- Glass
- Paint
- Plastic
- Ceramic
- Other

Cada zona declara las familias compatibles. La herramienta filtra automáticamente opciones incompatibles para evitar asignaciones absurdas.

## 7. Definición de un acabado

Un acabado no es únicamente un color. Puede contener:

```text
FinishDefinition
├─ finishId
├─ displayName
├─ family
├─ material/shader profile
├─ baseColor / albedo
├─ normal
├─ roughness or smoothness
├─ metallic
├─ height (optional)
├─ ambient occlusion (optional)
├─ emission (optional)
├─ texture scale
├─ physical scale metadata
└─ tags
```

Los campos concretos dependen del pipeline de render de Bistro Builder; el contrato semántico permanece estable.

## 8. Ventana de autoría

Ruta propuesta: **Bistro Builder → Mobiliario → Acabados y Variantes**.

La ventana se divide en tres áreas:

1. **Variantes / biblioteca**, a la izquierda.
2. **Preview 3D**, en el centro.
3. **Propiedades y zonas**, a la derecha.

Debe permitir seleccionar un FurnitureDefinition, inspeccionar sus zonas, crear variantes, duplicarlas, asignar acabados, validar y publicar.

## 9. Flujo de creación manual

1. Seleccionar el mueble maestro.
2. Ver las zonas semánticas detectadas.
3. Pulsar **+ Nueva variante** o **Duplicar variante**.
4. Elegir acabados compatibles por zona.
5. Previsualizar el resultado en tiempo real.
6. Guardar la variante como borrador.
7. Validar.
8. Publicar cuando esté aprobada.

Duplicar una variante debe copiar únicamente sus bindings; después puede modificarse una o varias zonas sin alterar la variante original.

## 10. Vinculación de zonas

La herramienta permite vincular temporalmente varias zonas para aplicarles el mismo acabado en una sola acción.

Ejemplo: `Seat + Back` → `Fabric_Olive`.

Esta vinculación es una ayuda de autoría y no obliga a que ambas zonas permanezcan ligadas para siempre.

## 11. Preview 3D

El visor debe ofrecer como mínimo:

- orbit;
- zoom;
- pan;
- reset de cámara;
- iluminación neutra reproducible;
- fondo neutro;
- vista del asset completo;
- resaltado de la zona seleccionada;
- comparación **Original / Propuesta / Aplicado**.

La preview no modifica el asset publicado hasta ejecutar una acción de aplicación o publicación.

## 12. Miniaturas

El sistema puede generar miniaturas automáticamente con cámara, encuadre, iluminación y fondo estandarizados.

Las miniaturas son derivados regenerables; nunca son la fuente de verdad del acabado.

## 13. Acabado Automático

Cuando el sistema detecta una o más zonas sin acabado válido, muestra:

**Acabado Automático · N zonas**

Regla principal:

> **Acabado Automático solo completa zonas o canales que falten. Nunca sustituye una zona ya resuelta salvo orden explícita del creador.**

El flujo es:

`Analizar → Proponer → Previsualizar → Aplicar/Descartar`.

No existe guardado ni publicación automática tras generar una propuesta.

## 14. Qué se considera missing

El análisis distingue al menos:

- Material inexistente / `None`.
- Referencia de material perdida.
- Shader roto o no soportado.
- Material placeholder marcado como temporal.
- Slot obligatorio sin asignar.
- Base Color faltante.
- Normal faltante.
- Roughness/Smoothness faltante.
- Metallic faltante cuando sea necesario.
- Otros canales obligatorios según el perfil del material.

Una ausencia completa se presenta como **Sin acabado**. Una ausencia de mapas/canales se presenta como **Acabado incompleto**.

## 15. Completar frente a sustituir

Si una zona no tiene material, **Acabado Automático** propone un acabado completo.

Si ya existe un material válido pero faltan canales, **Completar acabado** conserva lo válido y completa únicamente los canales ausentes.

La sustitución total de un acabado existente requiere una acción explícita distinta.

## 16. Orden de resolución automática

El sistema intenta resolver un missing en este orden:

1. Identificar la semántica de la zona.
2. Consultar el resto del mismo asset.
3. Consultar la familia del mueble y sus reglas.
4. Buscar un acabado compatible en la biblioteca canónica.
5. Buscar una coincidencia visual/semántica suficientemente fiable.
6. Solo si no existe una solución reutilizable adecuada, ofrecer generar un acabado nuevo.

**Reutilizar antes que generar** es una regla vinculante.

## 17. Señales para la propuesta automática

El motor puede considerar:

- nombre semántico de la pieza;
- clasificación procedente de Assets4All;
- Material Slots;
- materiales ya presentes en zonas relacionadas;
- nombres y metadatos de materiales importados;
- textura/base color existente;
- familia de mobiliario;
- tags estilísticos;
- acabados usados por variantes hermanas;
- referencia visual disponible;
- compatibilidad técnica con el shader de Bistro Builder.

Estas señales producen una propuesta; no convierten a la automatización en autoridad.

## 18. Incertidumbre semántica

Si una zona se llama, por ejemplo, `Part_017` y no existe evidencia suficiente para saber si es madera, metal, tela u otra superficie, el sistema no asigna un acabado a ciegas.

Debe mostrar **Tipo de superficie incierto** y solicitar una clasificación breve:

`Madera | Metal | Tela | Cuero | Piedra | Vidrio | Plástico | Otro`.

La clasificación confirmada se guarda como metadato del asset para futuras operaciones.

## 19. Generación de acabado nuevo

Si no existe un acabado suficientemente apropiado en la biblioteca, la herramienta puede ofrecer **Generar acabado nuevo**.

El generador debe producir, según el perfil requerido, mapas PBR y parámetros necesarios, preferentemente tileables y técnicamente compatibles con el pipeline del juego.

La generación puede usar texto, referencia visual u otros datos de entrada, pero siempre produce un borrador revisable.

El proveedor o tecnología concreta de IA no forma parte del contrato V1 y puede cambiar sin afectar a los datos canónicos.

## 20. Protección del original

Antes de aplicar una propuesta automática se conservan:

- referencia al estado original;
- propuesta temporal;
- diff de zonas/canales afectados.

Las acciones mínimas son **Original**, **Propuesta automática**, **Aplicar** y **Descartar**.

Después de aplicar, Undo debe poder recuperar el estado anterior.

## 21. Resolver todo automáticamente

Cuando existen varias incidencias puede mostrarse **Resolver todo automáticamente**.

Esta acción sigue exactamente las mismas restricciones: solo toca missing/incompletos, prioriza reutilización, no publica y genera una única propuesta revisable con el conjunto de cambios.

## 22. Validación previa a publicación

Una variante no puede publicarse si incumple requisitos obligatorios. La validación incluye al menos:

- zonas obligatorias resueltas;
- referencias existentes;
- shader compatible;
- ausencia de materiales Missing;
- IDs únicos y estables;
- finish families compatibles;
- bindings válidos;
- preview renderizable;
- miniatura disponible o regenerable;
- ausencia de cambios espaciales no declarados.

Los errores bloquean publicación. Los avisos no bloqueantes deben quedar diferenciados.

## 23. Guardar y publicar

**Guardar** conserva un borrador de autoría.

**Publicar** registra la variante como contenido canónico consumible por el resto de Bistro Builder.

La publicación no decide cómo la variante se presenta al jugador. Agrupar variantes en una sola tarjeta, mostrarlas como artículos separados o determinar su precio pertenece a los sistemas con autoridad sobre catálogo/UI/economía.

## 24. Integración con Assets4All

Assets4All puede proporcionar:

- zonas/piezas semánticas;
- Material Slots;
- materiales detectados;
- confianza de clasificación;
- metadatos de pieza;
- referencias visuales;
- VariantSet existente.

BBFFVAS consume esos datos, pero puede corregir o completar la clasificación de acabado sin redefinir la segmentación geométrica que pertenezca a Assets4All.

## 25. Integración con BBSIS y Modo Edición

Cambiar un acabado no altera:

- Spatial Contracts;
- footprint;
- collider;
- Work Edges;
- Ports;
- Seat Bays;
- claims;
- navegación;
- reglas de colocación.

Si una modificación exige cambiar alguno de esos contratos, deja de ser una simple variante de acabado.

## 26. Integración con BBPLFS

BBPLFS puede consumir IDs de FurnitureDefinition y Variant/Finish ya publicados para producir propuestas estilísticamente coherentes.

BBFFVAS no decide layouts, distribución ni lógica procedural del local.

## 27. Integración con catálogo, economía y persistencia

BBFFVAS publica identidad y apariencia; no decide experiencia de compra.

- Catálogo/UI decide presentación al jugador.
- Economía decide precios, cobros y devoluciones.
- Persistence conserva IDs estables según los contratos canónicos.
- Un cambio de nombre visible nunca sustituye al ID persistente.

## 28. Estrategia Unity

Para simples diferencias visuales se prioriza compartir geometría y datos de mueble.

Los **Material Variants**, materiales maestros/derivados y parámetros compatibles pueden utilizarse como implementación técnica cuando aporten herencia útil.

Los **Prefab Variants** no son la solución primaria para simples cambios de acabado; se reservan para variaciones que realmente necesiten overrides estructurales de prefab.

Nunca se modifica accidentalmente un material compartido de forma que cambien assets no seleccionados.

## 29. Patrones de industria adoptados

El diseño sigue patrones consolidados:

- **Cities: Skylines II:** mesh único con zonas/máscaras y combinaciones de color para multiplicar variedad sin duplicar geometría.
- **Unity:** herencia de Prefab Variants y Material Variants para evitar copias desconectadas.
- **Unreal Engine:** Material Instances parametrizadas derivadas de materiales maestros.
- **House Flipper 2:** objetos con múltiples materiales/acabados reutilizables.
- **Substance / Unity AI Material Generator:** generación o reconstrucción asistida de materiales PBR.

BBFFVAS añade una capa propia: detección de missing + reutilización prioritaria + generación opcional + preview + aprobación humana.

## 30. Criterios de aceptación V1

V1 se considera funcional cuando:

1. Puede abrir un mueble existente y leer sus zonas/materiales.
2. Puede crear y duplicar variantes sin duplicar geometría.
3. Puede reutilizar acabados desde una biblioteca central.
4. Filtra acabados incompatibles por tipo de superficie.
5. Detecta zonas y canales missing/incompletos.
6. **Acabado Automático** modifica exclusivamente lo que falta.
7. Puede completar canales sin destruir mapas válidos existentes.
8. Ante semántica incierta solicita clasificación en lugar de adivinar.
9. Prioriza acabados existentes antes de generar nuevos.
10. Permite preview Original/Propuesta/Aplicado.
11. Aplicar/Descartar y Undo/Redo funcionan.
12. Valida y bloquea publicación ante errores críticos.
13. Publica IDs estables consumibles por los sistemas canónicos.
14. No altera BBSIS, navegación, colliders ni reglas espaciales.
15. No expone esta herramienta al jugador.

## 31. Frontera de autoridad

Este sistema posee la **autoría y definición visual de acabados/variantes de mobiliario**.

No posee geometría base, segmentación 3D primaria, gameplay, BBSIS, navegación, catálogo de jugador, economía, layouts BBPLFS ni persistencia global.

Si durante la implementación aparece una decisión perteneciente a otro sistema, se documenta la dependencia y se consume su contrato público; no se redefine desde BBFFVAS.

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

Las condiciones de aparición, estados semánticos de espera y contrato de UI de estas acciones se centralizan en `docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md`. Los tiempos no se hardcodean en Presentation ni se duplican por acción. Mesa/Cliente reutiliza los contadores canónicos existentes para `WaitingForWaiter`, `WaitingForFood` y `WaitingForBill`. `TakeOrder` usa un perfil configurable; `FoodDelivery` deriva sus umbrales del tiempo esperado real de la comanda, calculado a partir de los tiempos de preparación efectivos de la carta de la partida y con fallback al catálogo canónico; `BillDelivery` conserva su perfil configurable. `Explicar demora` se habilita desde Demora y `Disculpa` desde Incidencia/Crítico o por fallos explícitos recuperables; ninguna de las dos acelera físicamente la tarea. `Agilizar cuenta` sigue siendo exclusiva de la cuenta y eleva únicamente la tarea real `DeliverBill` a través de `WaiterTaskCoordinator`. Recuperaciones, incidencias y priorización sobreviven a Save/Load sin crear autoridades paralelas.

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
| Autoría | Acabados y Variantes de Mobiliario (BBFFVAS) | IMPLEMENTACIÓN TÉCNICA V1 VALIDADA | herramienta interna; variantes visuales, biblioteca, Acabado Automático, miniaturas, publicación e importación de pipelines existentes |

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

## SOURCE: docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md

Category: CANONICAL

# Bistro Builder — Catálogo canónico de acciones contextuales

**Estado:** diseño vinculante en construcción. Este documento fija el contrato de UI y gameplay de las acciones contextuales del modo normal/servicio. Las acciones concretas se ampliarán por contexto sin duplicar lógica de dominio.

## Principio de la barra inferior

En modo normal/servicio, la barra horizontal inferior contiene de forma permanente **Velocidad**, `Caja` y **Climatología**. A continuación dispone de una zona de **acciones contextuales**.

La zona contextual no es un menú fijo ni debe llenarse por defecto. Solo muestra acciones que tengan sentido para el elemento seleccionado y para su estado actual. Como regla de diseño, se priorizan aproximadamente **3–4 acciones visibles simultáneamente**. Si no existe una intervención útil, la zona puede permanecer vacía.

Las acciones contextuales no saltan por encima de los sistemas de gameplay. La UI emite intención/comandos; camareros, cocina, comandas, satisfacción, finanzas u otras autoridades siguen resolviendo el resultado.

## Estados canónicos de espera/servicio

Las esperas se interpretan mediante estados semánticos comunes:

| Estado | Significado | Consecuencia de UI |
|---|---|---|
| **Normal** | La fase está dentro del tiempo razonable esperado. | No se ofrece una intervención por demora. |
| **Atención** | La fase se acerca al límite razonable. | Puede ofrecerse una acción preventiva si existe una decisión útil. |
| **Demora** | Se ha superado el tiempo esperado de la fase. | Se habilitan acciones de gestión de la demora. |
| **Incidencia** | La demora ya es grave o se ha producido un fallo explícito. | Se habilitan acciones de recuperación y consecuencias de satisfacción. |
| **Crítico** | Problema grave, repetido o muy deteriorado. | Alta prioridad visual/operativa y recuperación urgente. |
| **Resolución** | La causa ha sido resuelta y el sistema está cerrando la incidencia. | Las acciones dejan de ofrecerse cuando ya no tienen objeto. |

Una **incidencia** puede originarse de dos formas:

1. **Por tiempo:** una tarea supera de forma suficiente su margen razonable.
2. **Por evento:** ocurre un fallo real aunque no haya transcurrido un tiempo largo, por ejemplo un plato incorrecto o una atención fallida.

Los contadores empiezan cuando existe realmente la necesidad: una mesa está lista para pedir, una petición de camarero ha sido emitida, una cuenta ha sido solicitada, etc. No se cronometra una fase antes de que exista su obligación operativa.

## Fuente única de tiempos

No se deben hardcodear umbrales independientes en cada pantalla, acción o sistema. La intención es disponer de una fuente canónica configurable, conceptualmente **ServiceTimingCatalog**, consultada por UI y gameplay.

Para fases generales del servicio puede definir:

- atención/recepción inicial;
- toma de comanda;
- entrega de bebida;
- petición de camarero;
- retirada/atención posterior;
- entrega de cuenta;
- cobro;
- otras fases equivalentes que se ratifiquen.

Cada entrada podrá expresar al menos un objetivo y umbrales para **Atención**, **Demora** e **Incidencia**. Los valores concretos son datos de balance y no se consideran cerrados hasta probarlos en juego.

## Cocina y carta incompleta

No es requisito disponer ahora de un tiempo definitivo para cada plato. El sistema debe separar infraestructura de contenido de balance.

La resolución de tiempo esperado de un plato seguirá esta jerarquía:

`tiempo específico del plato -> perfil de preparación -> valor global por defecto`

Se prevé un concepto **DishPreparationProfile** para agrupar platos por comportamiento de preparación. Ejemplos de categorías como Rápido/Estándar/Lento son perfiles de diseño, no valores definitivos todavía.

Reglas:

- No inventar tiempos individuales para platos que aún no están diseñados.
- Todo plato debe poder funcionar aunque solo herede el perfil/default.
- Más adelante un plato puede sobrescribir su tiempo cuando exista una razón de diseño.
- Los umbrales de Atención/Demora/Incidencia se derivan del tiempo esperado; no se duplican manualmente dentro de cada plato.
- La estimación puede incorporar la carga/cola real de Cocina cuando exista una previsión fiable.
- El balance final se valida jugando; no se cierra únicamente sobre números teóricos.

## Contexto Mesa / Cliente

### Acciones ya ratificadas

| Acción | Condición semántica de aparición | Efecto de diseño |
|---|---|---|
| **Disculpa** | Existe una **Incidencia** o un evento negativo concreto que admite recuperación. | Intervención de recuperación de satisfacción. No elimina la causa del problema. |
| **Explicar demora** | Existe una **Demora** activa sobre una necesidad relevante de la mesa. | Gestiona la expectativa/impacto de la espera mientras la causa persiste. No acelera físicamente el servicio. |
| **Agilizar cuenta** | La mesa ha solicitado la cuenta, existe una tarea real de cuenta/cobro pendiente y la espera ha alcanzado al menos el estado **Atención**. | Eleva la prioridad operativa de las tareas relacionadas con preparar/entregar/cobrar la cuenta. |

Comportamiento esperado:

- Una mesa recién sentada y atendida dentro de tiempos normales no muestra estas acciones.
- **Explicar demora** puede aparecer antes que **Disculpa**: es una intervención preventiva cuando ya existe demora pero todavía no una incidencia grave.
- **Disculpa** aparece cuando el problema ya ha producido una incidencia o existe un fallo explícito.
- **Agilizar cuenta** no aparece en estado **Normal**. Se ofrece a partir de **Atención**, para evitar convertirla en una acción rutinaria que el jugador pulse en todas las mesas.
- En **Atención** se presenta como opción preventiva sin tratamiento de alarma; en **Demora** se destaca visualmente; en **Incidencia** puede coexistir con **Disculpa**.
- Una vez aplicada la priorización, no se permiten pulsaciones repetidas sobre la misma necesidad de cuenta.
- Cuando un camarero ya ha asumido efectivamente la tarea de cuenta, la acción deja de estar disponible y la UI puede mostrar un estado informativo como `Cuenta en camino`.
- **Agilizar cuenta** desaparece cuando ya no existe una tarea de cuenta/cobro pendiente.
- Cuando la causa desaparece, la acción asociada deja de ofrecerse; la UI no conserva botones obsoletos.

#### Vertical runtime: esperas de mesa

La integración runtime de Mesa/Cliente cubre ya **toma de comanda**, **espera de comida** y **espera de cuenta**, reutilizando los mismos estados semánticos y las acciones ratificadas `Explicar demora`, `Disculpa` y, exclusivamente para la cuenta, `Agilizar cuenta`. No crea cronómetros paralelos ni modifica la autoridad del sistema avanzado de camareros.

Tuning provisional de prueba para `BillDelivery`:

| Referencia | Tiempo |
|---|---:|
| Objetivo | 90 s |
| **Atención** | 120 s |
| **Demora** | 210 s |
| **Incidencia** | 300 s |
| **Crítico** | 420 s |

Estos valores son **datos provisionales de balance**, no cifras definitivas de diseño. Deben permanecer configurables en `ServiceTimingCatalog` y ajustarse mediante playtests.

Tuning provisional de prueba para `TakeOrder`, usando el contador canónico existente mientras la mesa/grupo permanecen en `WaitingForWaiter`:

| Referencia | Tiempo |
|---|---:|
| Objetivo | 10 s |
| **Atención** | 20 s |
| **Demora** | 35 s |
| **Incidencia** | 50 s |
| **Crítico** | 70 s |

Para `FoodDelivery` no se usa un tiempo fijo universal. La referencia es el **tiempo esperado real de la comanda**, resolviendo para cada plato el tiempo de preparación efectivo de la carta de esa partida (`BistroBuilderRestaurantMenuService`) y usando la definición canónica del plato solo como fallback cuando corresponda. La comanda toma como referencia el mayor tiempo efectivo entre sus líneas activas. Los umbrales provisionales se derivan dinámicamente:

- Objetivo = tiempo esperado.
- **Atención** = 1,15 × tiempo esperado.
- **Demora** = 1,35 × tiempo esperado + 4 s.
- **Incidencia** = 2 × tiempo esperado.
- **Crítico** = 3 × tiempo esperado + 30 s.
- Los umbrales se fuerzan a ser monotónicos para que nunca retrocedan aunque el tiempo esperado sea muy corto.

`Explicar demora` queda disponible desde **Demora** tanto en `TakeOrder` como en `FoodDelivery`; `Disculpa` desde **Incidencia/Crítico**. Ambas son de una sola aplicación por necesidad activa, no aceleran la tarea física y su estado se conserva en `reputation.runtime`. El tuning provisional de recuperación se mantiene en **1500 pb** para explicación y **2500 pb** para disculpa. `Priorizar atención` continúa **sin implementar y pendiente de ratificación**.

La espera canónica de cuenta se lee del seguimiento de experiencia ya existente mientras el grupo permanece en `WaitingForBill`; no se crea un segundo cronómetro. La acción eleva la tarea real `DeliverBill` de la cola autoritativa de camareros a prioridad urgente únicamente mientras sigue pendiente. Si un camarero ya la ha asumido, la acción desaparece y la UI puede indicar `Cuenta en camino`.

Si el jugador ha aplicado `Agilizar cuenta` y realiza un guardado de servicio activo mientras la necesidad sigue vigente, el estado de priorización debe conservarse y rehidratarse al cargar; no puede perderse ni duplicar tareas.

`Explicar demora` aparece **desde Demora**, no en Atención. No acelera la tarea física de cuenta. Es de una sola aplicación por necesidad activa y puede coexistir con `Agilizar cuenta`. Su efecto es mitigar parte del impacto de la espera en satisfacción mientras la causa sigue existiendo. La mitigación inicial de prueba queda en **1500 pb (15 % de la penalización de espera de cuenta recuperable)**, configurada en `ServiceTimingCatalog`; es un valor **provisional de balance**, no una cifra definitiva. El estado explicado se persiste dentro de `reputation.runtime` para sobrevivir a Save/Load.

`Disculpa` aparece cuando la espera de cuenta alcanza **Incidencia/Crítico** o cuando la visita tiene una incidencia explícita recuperable procedente de la comanda canónica. En esta primera integración se consideran fallos del restaurante: `WrongDish`, `DuplicateOrder`, `KitchenError`, `QualityIssue`, `AllergyRisk`, `MissingItem` y `ServiceError`. `CustomerChange` no se considera fallo del restaurante y no habilita `Disculpa` por sí solo.

`Disculpa` **no resuelve la causa** ni acelera físicamente ninguna tarea: recupera solo parte del impacto de satisfacción. Para la incidencia temporal de cuenta, la recuperación inicial de prueba es **2500 pb (25 % de la penalización restante recuperable)**. Para incidencias explícitas, el tuning inicial de prueba es **1000 pb de penalización por incidencia** y **500 pb recuperados por cada incidencia cubierta por una disculpa**. Todos estos valores son **provisionales y configurables** en `ServiceTimingCatalog`.

Una misma incidencia temporal de cuenta no admite disculpas repetidas. Las incidencias explícitas se contabilizan en la visita y la disculpa cubre las pendientes; si aparece una incidencia explícita adicional después, `Disculpa` puede volver a estar disponible. El estado de recuperación e incidencias se persiste en `reputation.runtime`.

La UI de esta vertical admite hasta **tres acciones simultáneas** sin solaparse con fecha/hora ni controles de velocidad. Tras priorizar, `Agilizar cuenta` desaparece; tras explicar, `Explicar demora` desaparece; tras cubrir las incidencias disponibles, `Disculpa` desaparece. El panel contextual conserva feedback informativo (`Cuenta priorizada`, `Demora explicada`, `Disculpa realizada`, `Cuenta en camino`) sin mantener botones obsoletos.

### Acciones pendientes de ratificación

Estas acciones son propuestas y **no se consideran todavía cerradas**:

- **Ver comanda**: navegación directa al detalle de la comanda activa de la mesa.
- **Priorizar atención**: elevar temporalmente la prioridad de una tarea de camarero pendiente sin asignar ni teletransportar manualmente a un camarero.

No incorporar todavía como acciones canónicas sin diseño adicional:

- Cobrar ahora.
- Servir ahora.
- Limpiar mesa.
- Cambiar de mesa.
- Llamar refuerzos.
- Ofrecer compensación económica.

Estas opciones podrían saltarse autoridades existentes o requieren reglas económicas/espaciales adicionales.

## Contexto Cocina

Acciones ya aprobadas por el sistema de servicio:

- **Reducir entrada**.
- **Pausar nuevas comandas** por plato.
- **Priorizar comanda**, con un máximo de 3 prioridades simultáneas.

Su aparición exacta en la barra contextual deberá derivarse del estado y selección de Cocina/Comanda, sin duplicar las reglas de la autoridad de cocina.

## Regla de implementación incremental

No implementar de una vez carta completa, incidentes, satisfacción, tiempos, prioridades y UI. El orden acordado es:

1. Cerrar el catálogo de acciones y sus **condiciones semánticas**.
2. Implementar el contrato/configuración de tiempos generales de servicio.
3. Preparar **DishPreparationProfile** y el fallback global, sin completar todavía toda la carta.
4. Asignar perfiles/tiempos a los platos a medida que la carta se diseña.
5. Ajustar umbrales y tiempos mediante pruebas de juego.
6. Integrar las acciones con sus sistemas reales, sin crear lógica paralela en Presentation.

La ausencia temporal de tiempos específicos por plato no debe bloquear el desarrollo ni obligar a introducir datos ficticios.

---

## SOURCE: docs/30_UI_UX/NEW_GAME_APPROVED.md

Category: CANONICAL

# Nueva partida — marfil clásico aprobado

Aprobado por el usuario el 24/09/2026: la primera de las tres últimas imágenes, en versión interactiva. La referencia queda en [approved-reference.png](../UI/NewGame/approved-reference.png); la [maqueta ejecutable](../UI/NewGame/index.html) se abre en un navegador.

## Diseño vinculante
- Marco marfil opaco con borde latón y logo centrado en una franja propia.
- Título y subtítulo a la izquierda; nombre editable del restaurante a la derecha, con separación suficiente.
- Desde cero y Lo esencial en la primera fila, separadas; Últimos retoques centrada debajo. Las tres tarjetas tienen exactamente el mismo ancho y alto en cada resolución. En pantallas estrechas se apilan.
- Desde cero: local vacío, diseño libre. Lo esencial: distribución y equipo básicos. Últimos retoques: restaurante casi terminado para personalizar antes de abrir. Son grados de preparación, no tamaños del local.
- Botones Al pase: Atrás con flecha grande de latón; Crear restaurante con campana. Texto e iconos centrados. Respuesta al pasar, pulsar y usar teclado; movimiento reducido cuando se solicita.
- Tipografía de producción: Recoleta en títulos e Inter en controles. La maqueta usa alternativas del sistema sin distribuir fuentes comerciales.

## Integración en el juego — 24/09/2026
Pantalla nativa uGUI/TMP conectada al servicio de apertura existente. No se ejecuta un navegador dentro de Unity. Nombre editable, selección única, validación de nombre vacío y bloqueo de doble creación. Atrás conduce a un menú con Nueva partida, Continuar partida y Salir. Crear abre el modo edición; allí se conservan Guardar recuperación y Validar y continuar.

Desde cero usa el perfil Empty existente. Lo esencial añade Essentials (valor 4), que conserva dos mesas y sus asientos asociados, cocina y equipamiento funcional de la escena base y retira decoración. Últimos retoques añade FinishingTouches (valor 5), que conserva la escena equipada. Ambos perfiles se guardan y cargan; los valores anteriores 0–3 permanecen compatibles. No se cambia el tamaño del terreno. El número de mesas básicas es un parámetro de autoría del servicio.

Las miniaturas ilustradas son orientativas de preparación; el mobiliario real procede del contenido ya integrado. El recurso aprobado se reutiliza como atlas visual para logo, miniaturas y botones, con coordenadas normalizadas sobre la referencia original. Textos, controles y paneles son nativos y escalan con la resolución. Se usan Recoleta e Inter del proyecto. La preferencia existente de movimiento reducido desactiva los movimientos de los iconos.

La maqueta web se conserva como referencia de diseño; sus botones siguen siendo demostraciones, mientras que la versión Unity sí crea y recupera partidas.
## Archivos y comprobación
- `docs/UI/NewGame/index.html`: versión autónoma; recursos visuales incluidos.
- `docs/UI/NewGame/new-game.fragment.html`: fuente editable de la maqueta.
- `docs/UI/NewGame/approved-reference.png`: imagen elegida.
- `docs/UI/NewGame/verify.cjs`: verificación con Playwright y Edge. Ejecutar `node docs/UI/NewGame/verify.cjs`; requiere el módulo `playwright` o su ruta en `PLAYWRIGHT_MODULE`.

Verificado sin red a 1920, 1280, 1024, 736 y 320 px: tarjetas iguales, sin desbordamiento horizontal, selección única, nombre obligatorio, respuesta de botones, teclado y movimiento reducido. No constituye una prueba del juego ni una build Windows.

## Validación nativa
BistroBuilderOpeningIvorySelfTest.Run: PASS en creación por botón, Atrás/reapertura, nombre vacío, selección única, tres tarjetas iguales y guardar/cargar los tres perfiles. Resultado: 0 artículos/0 mesas, 6 artículos/2 mesas y 38 artículos/10 mesas respectivamente en la escena canónica. Panel completo y texto sin desbordamiento a 1920×1080, 1280×720 y 3440×1440, con capturas renderizadas e inspección visual. Pruebas usan ranuras libres 901–999 y eliminan únicamente los guardados que generan.

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
- Barra horizontal inferior operativa para el modo normal/servicio: integra de forma permanente **Velocidad**, `Caja` y **Climatología**, además de las acciones del contexto actual cuando procedan. El catálogo canónico de estas acciones vive en `CONTEXTUAL_ACTION_CATALOG.md`.
- La zona superior queda reservada a la navegación global y a los elementos superiores ya definidos; **Velocidad, `Caja` y Climatología no se ubican en la barra superior** ni en un menú lateral permanente.

## Interacción
- Seleccionar una mesa recentra suavemente la cámara **sin zoom automático**.
- Doble clic en mesa abre detalle/comanda cuando corresponda.
- Clic en evento de Actividad centra y selecciona su objetivo.
- Clic en camarero, cocina o barra centra y abre su panel contextual.
- `Esc` o clic en vacío limpia la selección/cierra contexto apropiado.
- Cambiar selección debe transicionar el contexto sin reconstruir visualmente toda la interfaz.

## Estados visuales
HUD operativo por estados **Normal / Atención / Demora / Incidencia / Crítico / Resolución**. Verde = correcto; ámbar = atención/demora; rojo se reserva para incidencia/crítico; azul/gris = neutro. Las condiciones semánticas y las acciones asociadas se detallan en `CONTEXTUAL_ACTION_CATALOG.md`. Notificaciones agrupadas, sin spam ni modales rutinarios. `Actividad` muestra aproximadamente 5–8 eventos útiles.

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

## Nueva partida — decisión 24/09/2026
La composición aprobada es marfil clásico panorámico, con tres opciones de preparación del restaurante y botones Al pase. Véase [diseño aprobado y alcance](NEW_GAME_APPROVED.md). Pantalla nativa integrada en Unity: tres preparaciones, creación de partida, menú Atrás/Continuar y acciones de diseño inicial.

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
| D-013 | VIGENTE | UI de Servicio: navegación horizontal superior; Actividad izquierda; contexto derecha; barra inferior operativa. La ubicación de indicadores globales se rige por D-033. |
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
| D-027 | VIGENTE | Modo Edición adopta como dirección gráfica la Propuesta C revisada: catálogo claro vertical a la izquierda, viewport central, inspector claro a la derecha con Reglas de colocación y franja inferior de herramientas/acciones. |
| D-028 | VIGENTE | BBFFVAS es una herramienta interna de autoría; no es una mecánica ni interfaz destinada al jugador. |
| D-029 | VIGENTE | Una variante puramente visual comparte geometría, footprint, collider y contratos espaciales; los acabados no justifican duplicar prefabs. |
| D-030 | VIGENTE | Acabado Automático solo completa zonas/canales missing o incompletos; nunca sobrescribe trabajo válido salvo orden explícita. |
| D-031 | VIGENTE | Toda propuesta automática requiere preview y aplicación explícita; reutilizar acabados canónicos tiene prioridad sobre generar nuevos. |
| D-032 | VIGENTE | Si la semántica de una superficie es incierta, el sistema solicita clasificación y no asigna materiales a ciegas. |
| D-033 | VIGENTE | En modo normal/servicio, la barra horizontal inferior integra **Velocidad**, `Caja` y **Climatología**, además de las acciones contextuales que correspondan. Estos tres elementos no se colocan en la barra superior. |
| D-034 | VIGENTE | Los tiempos y estados de espera del servicio se definen desde configuración canónica compartida; no se hardcodean por pantalla/acción. Cocina admite `tiempo específico -> perfil de preparación -> default global`, por lo que la carta puede completarse progresivamente sin inventar tiempos de platos aún no diseñados. |
| D-035 | VIGENTE | Las acciones contextuales aparecen solo cuando existe una condición semántica válida del objeto seleccionado; no forman un menú fijo. `Disculpa`, `Explicar demora` y `Agilizar cuenta` están ratificadas para Mesa/Cliente; nuevas acciones permanecen como propuestas hasta decisión explícita. |
| D-036 | VIGENTE | `Agilizar cuenta` no aparece desde que se solicita la cuenta: se ofrece a partir del estado **Atención**. En Demora se destaca, en Incidencia puede coexistir con `Disculpa`, no admite pulsaciones repetidas sobre la misma necesidad y desaparece cuando la cuenta ya está siendo atendida o resuelta. |
| D-037 | VIGENTE | `Explicar demora` se ofrece desde **Demora** en adelante y solo una vez por necesidad activa. Mitiga de forma configurable la penalización de satisfacción atribuible a la espera, pero no reduce el tiempo real, no cambia el estado semántico y no altera la prioridad de la tarea; el valor concreto de balance permanece provisional. |
| D-038 | VIGENTE | `Disculpa` se ofrece cuando una necesidad alcanza **Incidencia/Crítico** o existe un fallo explícito recuperable de la comanda canónica. No elimina la causa ni acelera el servicio: recupera solo parte del impacto de satisfacción. `CustomerChange` no cuenta como fallo del restaurante. La aplicación no es repetible sobre la misma incidencia ya cubierta, puede reaparecer ante nuevas incidencias explícitas y todo su tuning de recuperación permanece configurable/provisional. |
| D-039 | VIGENTE | El timing contextual de Mesa/Cliente se extiende a `TakeOrder` y `FoodDelivery` reutilizando los contadores canónicos existentes. `TakeOrder` usa perfil configurable; `FoodDelivery` deriva Atención/Demora/Incidencia/Crítico del tiempo esperado real de la comanda, resolviendo los tiempos de preparación efectivos de la carta de la partida y usando el catálogo canónico solo como fallback, sin cronómetro paralelo ni tiempo fijo universal. `Explicar demora` entra desde Demora y `Disculpa` desde Incidencia/Crítico. `Priorizar atención` permanece pendiente de ratificación y no se implementa. |

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

La barra usa el catálogo SVG y los efectos de `feature/21b-iconography-system` (`34694c7`). Incluye Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing y Reputación, conectados a sus pantallas existentes. La selección usa dorado, subrayado y fondo. La versión visual aprobada el 23/09/2026 aplica un hover cálido e iluminado por recuadro y una microanimación continua propia a cada icono mientras el puntero permanece encima; Opciones mantiene un giro corto de engranaje y el resto combina elevación, balanceo o desplazamiento según su semántica.

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

## Referencia gráfica aprobada · Propuesta C revisada

La composición aprobada para implementación toma la **Propuesta C revisada** como referencia visual principal:

- Panel izquierdo claro, vertical y redondeado para `Catálogo de artículos`, con búsqueda, categorías por icono/texto, filtros y tarjetas en dos columnas.
- Paleta del editor: marfil/crema como superficie, carbón para texto, verde oliva para selección/validez y acentos cálidos discretos para favoritos; sombras suaves y profundidad moderada.
- Tarjeta seleccionada con contorno verde y check discreto; favoritos con estrella; artículos bloqueados desaturados con candado y requisito.
- Viewport central prioritario, cuadrícula visualmente integrada con el suelo y ghost translúcido con huella/contorno verde cuando la posición es válida.
- Inspector derecho claro con preview grande, título, descripción, precio, ámbito interior/exterior, variantes de color y dimensiones.
- El inspector incorpora una sección **Reglas de colocación** antes del estado final. Para una silla estándar de suelo, la representación de referencia muestra: `Se puede colocar en suelos`, `Requiere espacio libre` y `Apto para interior y exterior`, siempre derivados de metadatos reales.
- Debajo de las reglas, caja de estado contextual: `Listo para colocar` + explicación breve cuando sea válido; en error mantiene la misma estructura y comunica el motivo.
- Barra inferior clara: contextos/herramientas de edición separados de acciones sobre el objeto (`Eliminar`, `Rotar`, `Duplicar` cuando proceda). No alberga un segundo catálogo.
- Barra superior conserva la identidad general de Bistro Builder y muestra de forma inequívoca que se está en `Modo Edición`.
- La interfaz no replica el estilo oscuro de la propuesta B; de B se adopta únicamente el patrón funcional de `Reglas de colocación` dentro del inspector de la propuesta C.

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
| Paredes | Prefabs de 0,5 / 1 / 2 / 4 m, altura objetivo 2,5 m, grosor 0,12 m; meshes propios |
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

## SOURCE: docs/SAVIC.md

Category: SUPPORTING

# SAVIC — Sistema de Autoría, Validación e Integración de Contenido

**Proyecto:** Bistro Builder
**Motor:** Unity 6000.3.19f1
**Estado:** diseño funcional y técnico V1 cerrado para iniciar implementación
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

## 17. Colliders

Objetivo: suficientemente precisos para gameplay y baratos para Unity.

[V1] Preferencia por colliders simples o compuestos.

[V1] Evitar MeshCollider complejo por defecto.

[V1] Generar collider a partir de piezas/volúmenes cuando la familia lo permita.
[V1] Recalcular collider si cambian escala, geometría o pivot relevantes.

[V1] Validar que collider y visual ocupan volúmenes coherentes.

[R] Convex decomposition controlada para formas donde realmente aporte valor.

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

### Bloque 7 — Batch, recovery y rendimiento
[V1 antes de declarar estable]

- 100/500/2.000;
- checkpoint;
- resume;
- cancellation;
- incremental invalidation;
- time slicing;
- freeze budgets.

### Bloque 8 — Decoración y equipamiento
[R]

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

## SOURCE: docs/UI/Servicio/ActivityApprovedIcons.md

Category: SUPPORTING

# Bistro Builder — Iconos aprobados de ACTIVIDAD

Estado: EN CURSO
Fecha de actualización: 2026-09-23
Rama canónica: `integration/ui-combined-final-20260917-v2`
Relacionado: `ActivityIconFamilies.md`

Este documento registra exclusivamente iconos aprobados por el usuario. Cada selección queda vinculada a su imagen canónica en la Library de ChatGPT para evitar pérdidas o sustituciones accidentales.

| Familia | Estado | Archivo canónico | Library file id |
|---|---|---|---|
| `activity.people.arrival` | APROBADO | `BB_Activity_People_Arrival.png` | `libfile_d9e6525d45e481918792a363f74cc771` |
| `activity.table.seated` | APROBADO | `BB_Activity_Table_Seated.png` | `libfile_aac9c1428e10819194a296dd203942dc` |
| `activity.table.attention` | APROBADO | `BB_Activity_Table_Attention.png` | `libfile_ecdfd3955ddc81919ff55442f49e52f6` |
| `activity.wait.alert` | APROBADO | `BB_Activity_Wait_Alert.png` | `libfile_4624b479bc988191b66681635de5f57b` |
| `activity.table.bill` | APROBADO | `BB_Activity_Table_Bill.png` | `libfile_46d5751477208191a1eccb4eedc545ee` |
| `activity.table.complete` | APROBADO | `BB_Activity_Table_Complete.png` | `libfile_216cca66746881919ad6f865f23cce8f` |
| `activity.order.new` | APROBADO | `BB_Activity_Order_New.png` | `libfile_302a2cb5b2b08191b8661655a25ab539` |
| `activity.order.ready` | APROBADO | `BB_Activity_Order_Ready.png` | `libfile_d558d5cd27fc8191b64366e7c43c15d7` |
| `activity.order.priority` | APROBADO | `BB_Activity_Order_Priority.png` | `libfile_ec8eab9e1d488191809f97039d6b6568` |
| `activity.order.error` | APROBADO | `BB_Activity_Order_Error.png` | `libfile_891e4e2837c88191af56a4ae913c3039` |
| `activity.dish.problem` | APROBADO | `BB_Activity_Dish_Problem.png` | `libfile_d3a4fb0d41b081918bb064b057ddca04` |
| `activity.dish.trending` | APROBADO | `BB_Activity_Dish_Trending.png` | `libfile_89806ccb247081918e56bbbb67398e99` |
| `activity.kitchen.state` | APROBADO | `BB_Activity_Kitchen_State.png` | `libfile_29824b54f5148191bb594082af093c14` |
| `activity.kitchen.equipment` | APROBADO | `BB_Activity_Kitchen_Equipment.png` | `libfile_244f232574508191b26d63e460bb6e5e` |
| `activity.flow.state` | APROBADO | `BB_Activity_Flow_State.png` | `libfile_1e5f4871a05c81918ca096ae28480d69` |
| `activity.wait.group` | APROBADO | `BB_Activity_Wait_Group.png` | `libfile_6e4810e4ab5881918b3b21ecb034c4de` |
| `activity.table.available` | APROBADO | `BB_Activity_Table_Available.png` | `libfile_837a13b93bb88191bec7d876c89f7c15` |
| `activity.bar.state` | APROBADO | `BB_Activity_Bar_State.png` | `libfile_73da123c2dd08191a2ec313674cc6509` |
| `activity.staff.state` | APROBADO | `BB_Activity_Staff_State.png` | `libfile_f0b0e5b7f86c8191b395c2ff6eeb1f20` |
| `activity.reservation` | APROBADO | `BB_Activity_Reservation.png` | `libfile_ca5be238c7e48191a29c5e163c1aaf5f` |
| `activity.reservation.group` | APROBADO | `BB_Activity_Reservation_Group.png` | `libfile_59cad6c6567481918972c9059bd83010` |
| `activity.guest.special` | APROBADO | `BB_Activity_Guest_Special.png` | `libfile_94b9418a06788191830da05e89684254` |
| `activity.stock.state` | APROBADO | `BB_Activity_Stock_State.png` | `libfile_d53e1c5009a48191bf9de3f6400c7e58` |
| `activity.supplier` | APROBADO | `BB_Activity_Supplier.png` | `libfile_af0832038c588191be3413305a2d5531` |
| `activity.reputation` | APROBADO | `BB_Activity_Reputation.png` | `libfile_2e9400e9a9648191bab7c78b86e87f58` |

Ruta Library:
`/BistroBuilder/UI/Referencias/Servicio/PanelActividad/IconosAprobados/`

## Cierre de familias

| `activity.marketing` | APROBADO | `BB_Activity_Marketing.png` | `libfile_b5292e4ba01081919c076fcb8fe7e4d9` |
| `activity.opportunity` | APROBADO | `BB_Activity_Opportunity.png` | `libfile_822a5718c0f08191967d8a1f42445ed6` |
| `activity.trend.up` | APROBADO | `BB_Activity_Trend_Up.png` | `libfile_7d64f35b6e148191b26cdf53efd8f934` |

## Regla de aprobación

- Una familia solo entra aquí después de aprobación explícita.
- No reemplazar una imagen aprobada por una variante posterior sin nueva aprobación.
- La familia debe conservar su `IconKey` estable aunque cambie la implementación técnica.
- Las 28 familias quedan aprobadas.
- Estado actual: **28 de 28 familias aprobadas**.

---

## SOURCE: docs/UI/Servicio/ActivityEventCatalog.md

Category: SUPPORTING

# Bistro Builder — Catálogo canónico de eventos de ACTIVIDAD

Estado: DISEÑO CERRADO
Versión: 1.0
Fecha: 2026-09-23
Documento padre: `PanelActividad.md`

## Propósito

Este catálogo define qué frases puede generar el panel ACTIVIDAD, qué datos variables utiliza, qué icono corresponde, cómo se prioriza y qué ocurre al pulsar cada entrada.

Regla principal: ningún sistema de gameplay debe escribir frases completas en la UI. Los sistemas emiten eventos estructurados y ACTIVIDAD compone el texto mediante plantillas localizables.

## Modelo de datos

Cada definición contiene:
- `EventId`: identificador estable y único.
- `Category`: Incident, Opportunity, Event o Reservation.
- `Severity`: Info, Positive, Attention o Critical.
- `IconKey`: familia visual canónica.
- `TitleKey` y `BodyKey`: claves de localización.
- `TargetType`: entidad que se selecciona al pulsar.
- `Lifetime`: Transient, StickyUntilResolved o ServiceSession.
- `Aggregation`: regla para evitar spam.
- `FeatureGate`: sistema requerido, si procede.

## Pipeline de runtime

1. El sistema de gameplay detecta un cambio real.
2. Emite un evento semántico con IDs y datos, nunca texto final.
3. `ActivityFeedService` resuelve la definición en `ActivityEventCatalog`.
4. Se valida deduplicación, prioridad, agrupación y vigencia.
5. `ActivityTemplateFormatter` resuelve título y cuerpo desde Unity Localization.
6. `ActivityPanelController` inserta o actualiza la fila.
7. Al pulsar, `ActivityTargetRouter` centra/selecciona el objetivo y alimenta Contexto.

La UI no puede modificar el estado de gameplay directamente por mostrar un evento.

## Presentación

- Máximo visual simultáneo: 8 filas en la referencia aprobada.
- El resto permanece disponible mediante scroll.
- Orden: Critical > Attention > Opportunity relevante > cronología.
- Un Critical no resuelto permanece fijado.
- El filtro de cabecera ofrece: Hoy, Incidencias, Oportunidades y Reservas.
- La hora procede del reloj de juego, no del reloj real.
- Título de fila: Inter SemiBold.
- Texto secundario y hora: Inter Regular.
- Cabecera ACTIVIDAD: Recoleta.
- Cada entrada puede mostrar un marcador semántico adicional a la derecha.

## Reglas anti-spam y determinismo

- Un mismo `EventId + TargetId` no crea duplicados dentro de su ventana de deduplicación.
- Los eventos de estado solo se generan cuando cambia el estado.
- Stock bajo/crítico/agotado se vuelve a emitir únicamente tras abandonar y volver a entrar en ese estado.
- Reservas generan un único evento por transición.
- Eventos positivos repetitivos tienen rate-limit.
- Incidencias resueltas dejan de estar fijadas; su registro histórico puede permanecer.
- Eventos agregables conservan los TargetIds afectados para poder navegar entre ellos.
- Guardar/cargar no vuelve a ejecutar efectos ni recompensas.
- Tras cargar, las referencias inválidas se descartan de forma segura.

## Catálogo — Mesas y clientes

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `table.group_arrived` | **Nuevo grupo** · {guests} personas | Info | `activity.people.arrival` | Group |
| `table.group_seated` | **Mesa {table}** · Grupo sentado | Info | `activity.table.seated` | Table |
| `table.attention_needed` | **Mesa {table}** · Necesita atención | Attention | `activity.table.attention` | Table |
| `table.waiting_excessive` | **Mesa {table}** · Espera demasiado sus platos | Critical | `activity.wait.alert` | Table |
| `table.bill_requested` | **Mesa {table}** · Ha pedido la cuenta | Info | `activity.table.bill` | Table |
| `table.finished` | **Mesa {table}** · Servicio finalizado | Info | `activity.table.complete` | Table |
| `table.customer_unhappy` | **Cliente descontento** · Mesa {table} | Attention | `activity.reputation.negative` | Table |
| `table.customer_recovered` | **Incidencia resuelta** · Mesa {table} recuperada | Positive | `activity.reputation.recovered` | Table |

## Catálogo — Comandas, platos y cocina

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `order.created` | **Nuevo pedido** · Mesa {table} · {lines} platos | Info | `activity.order.new` | Order |
| `order.ready` | **Pedido listo** · Mesa {table} | Info | `activity.order.ready` | Order |
| `order.priority_set` | **Comanda prioritaria** · Mesa {table} | Attention | `activity.order.priority` | Order |
| `order.error` | **Error de pedido** · Mesa {table} | Attention | `activity.order.error` | Order |
| `dish.cold_or_poor` | **Problema de plato** · Mesa {table} | Attention | `activity.dish.problem` | Order |
| `dish.blocked_stock` | **Plato no disponible** · {dish} | Critical | `activity.stock.blocked` | Dish |
| `dish.trending` | **Plato destacado** · {dish} · {count} pedidos | Positive | `activity.dish.trending` | Dish |
| `kitchen.state_loaded` | **Cocina cargada** · Aumenta la cola | Attention | `activity.kitchen.loaded` | Kitchen |
| `kitchen.state_saturated` | **Cocina saturada** · {pending} comandas pendientes | Critical | `activity.kitchen.saturated` | Kitchen |
| `kitchen.state_blocked` | **Cocina bloqueada** · Requiere actuación | Critical | `activity.kitchen.blocked` | Kitchen |
| `kitchen.state_recovered` | **Cocina fluida** · Ritmo recuperado | Positive | `activity.kitchen.recovered` | Kitchen |
| `kitchen.equipment_issue` | **Incidencia de cocina** · {equipment} | Critical | `activity.kitchen.equipment` | Kitchen |
| `kitchen.priority_limit` | **Prioridades completas** · Máximo alcanzado | Attention | `activity.order.priority` | Kitchen |

## Catálogo — Entrada, sala, barra y espera

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `foh.state_waiting` | **Entrada con espera** · {groups} grupos | Attention | `activity.wait.queue` | Entrance |
| `foh.state_saturated` | **Sala saturada** · Requiere ajuste | Critical | `activity.zone.saturated` | Zone |
| `foh.state_slowed` | **Ritmo reducido** · Entrada controlada | Attention | `activity.flow.slowed` | Entrance |
| `foh.state_recovered` | **Entrada fluida** · Ritmo recuperado | Positive | `activity.flow.recovered` | Entrance |
| `waitlist.group_added` | **Nuevo grupo en espera** · {guests} personas | Info | `activity.wait.group` | WaitTicket |
| `waitlist.long_wait` | **Espera elevada** · {minutes} min | Critical | `activity.wait.alert` | WaitTicket |
| `waitlist.table_available` | **Mesa disponible** · Grupo en espera puede sentarse | Opportunity | `activity.table.available` | WaitTicket |
| `waitlist.sent_to_bar` | **Espera en barra** · {guests} personas | Info | `activity.bar.wait` | WaitTicket |
| `waitlist.group_left` | **Grupo perdido** · Abandona la espera | Attention | `activity.wait.left` | WaitTicket |
| `bar.saturated` | **Barra saturada** · No absorbe más espera | Critical | `activity.bar.saturated` | Bar |
| `zone.staff_shortage` | **Falta personal** · {zone} | Critical | `activity.staff.shortage` | Zone |
| `table.needs_reset` | **Mesa pendiente** · Limpieza/preparación | Attention | `activity.table.reset` | Table |
| `table.ready` | **Mesa preparada** · Disponible de nuevo | Positive | `activity.table.available` | Table |

## Catálogo — Reservas

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `reservation.arriving_soon` | **Reserva próxima** · {guests} pax · {minutes} min | Reservation | `activity.reservation.soon` | Reservation |
| `reservation.arrived` | **Reserva llegada** · {guests} pax | Reservation | `activity.reservation.arrived` | Reservation |
| `reservation.seated` | **Reserva sentada** · Mesa {table} | Reservation | `activity.reservation.seated` | Table |
| `reservation.large_group` | **Grupo grande próximo** · {guests} pax | Attention | `activity.reservation.group` | Reservation |
| `reservation.special_guest` | **Cliente especial** · Reserva próxima | Opportunity | `activity.guest.special` | Reservation |
| `reservation.peak_window` | **Pico de reservas** · {count} entradas próximas | Critical | `activity.reservation.peak` | ReservationGroup |

Regla vigente: no se generan eventos de no-show ni de retraso de clientes reservados.

## Catálogo — Inventario y proveedores

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `inventory.low` | **Inventario bajo** · {ingredient} | Attention | `activity.stock.low` | Ingredient |
| `inventory.critical` | **Stock crítico** · {ingredient} | Critical | `activity.stock.critical` | Ingredient |
| `inventory.out` | **Stock agotado** · {ingredient} | Critical | `activity.stock.out` | Ingredient |
| `inventory.updated` | **Inventario actualizado** · {source} | Info | `activity.stock.updated` | Inventory |
| `supplier.delivery_received` | **Entrega recibida** · {supplier} | Positive | `activity.supplier.delivery` | Supplier |
| `supplier.issue` | **Problema de suministro** · {supplier} | Attention | `activity.supplier.issue` | Supplier |

## Catálogo — Personal y operación

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `staff.overloaded` | **Empleado saturado** · {employee} | Attention | `activity.staff.overloaded` | Employee |
| `staff.zone_uncovered` | **Zona sin cobertura** · {zone} | Critical | `activity.staff.shortage` | Zone |
| `staff.support_needed` | **Apoyo requerido** · {zone} | Attention | `activity.staff.support` | Zone |
| `staff.upsell_opportunity` | **Venta sugerida** · Mesa {table} | Opportunity | `activity.opportunity.upsell` | Table |

## Catálogo — Reputación, marketing y oportunidades

| EventId | Texto visible | Severity | IconKey | Target |
|---|---|---|---|---|
| `reputation.good_review` | **¡Buena reseña!** · “{excerpt}” | Positive | `activity.reputation.positive` | Reputation |
| `reputation.bad_review` | **Reseña negativa** · “{excerpt}” | Attention | `activity.reputation.negative` | Reputation |
| `reputation.word_of_mouth` | **Boca a boca** · Demanda orgánica al alza | Positive | `activity.reputation.word_of_mouth` | Reputation |
| `marketing.campaign_started` | **Campaña activa** · {campaign} | Info | `activity.marketing.campaign` | Marketing |
| `marketing.demand_spike` | **Demanda al alza** · +{percent}% prevista | Opportunity | `activity.trend.up` | Marketing |
| `marketing.capacity_risk` | **Demanda excesiva** · Capacidad en riesgo | Critical | `activity.marketing.risk` | Marketing |
| `opportunity.regular_guest` | **Cliente habitual** · Mesa {table} | Opportunity | `activity.guest.regular` | Table |
| `opportunity.special_guest` | **Cliente importante** · Mesa {table} | Opportunity | `activity.guest.special` | Table |
| `opportunity.drink` | **Oportunidad de bebida** · Mesa {table} | Opportunity | `activity.opportunity.drink` | Table |
| `opportunity.dessert` | **Oportunidad de postre** · Mesa {table} | Opportunity | `activity.opportunity.dessert` | Table |
| `opportunity.walkin_group` | **Mesa aprovechable** · Grupo espontáneo de {guests} | Opportunity | `activity.opportunity.group` | Group |
| `opportunity.bar_wait_sale` | **Espera rentable** · Grupo puede pasar a barra | Opportunity | `activity.opportunity.bar` | WaitTicket |
| `opportunity.high_demand` | **Día de gran afluencia** · {guests} comensales (+{percent}%) | Opportunity | `activity.trend.up` | Restaurant |

## Eventos condicionados por sistemas futuros

Estos IDs quedan reservados pero no forman parte del lote de iconos V1 mientras el canal correspondiente no esté activo:
- `online.order_problem` — pedido online problemático.
- `online.paused` — canal online pausado.
- `online.capacity_risk` — online agravando saturación de cocina.

## Agrupaciones canónicas

- Varias `table.waiting_excessive` en 60 s → **Espera elevada · {count} mesas necesitan atención**.
- Varias `zone.staff_shortage` simultáneas → **Falta de personal · {count} zonas afectadas**.
- Varias reservas próximas en una misma ventana → `reservation.peak_window`.
- Varias oportunidades de upselling de la misma familia pueden agruparse por zona.
- Las incidencias Critical nunca se ocultan dentro de una agrupación sin conservar acceso a cada objetivo.

## Persistencia

ACTIVIDAD es una proyección de UI, no autoridad de gameplay.
Al guardar un servicio activo se conservan:
- eventos visibles recientes;
- eventos StickyUntilResolved;
- timestamp de juego;
- EventId;
- TargetRef;
- parámetros de plantilla.

Al cargar:
- no se repiten efectos;
- se validan referencias;
- se rehidratan únicamente entradas todavía relevantes.

## Contrato de implementación

Tipos previstos:
- `ActivityEventDefinition`
- `ActivityEventInstance`
- `ActivityEventCatalog`
- `ActivityFeedService`
- `ActivityFeedAggregator`
- `ActivityTemplateFormatter`
- `ActivityTargetRouter`
- `ActivityPanelController`

Los productores de gameplay solo conocen un contrato de emisión, por ejemplo:
`Publish(ActivityEventId.TableBillRequested, payload)`.

No deben depender de prefabs, TextMeshPro, sprites ni jerarquías UI.

## Criterios de aceptación

- Un evento idéntico no se duplica por polling o reentrada.
- Las transiciones de estado son deterministas.
- Critical permanece visible hasta resolución.
- El filtro no altera ni destruye eventos.
- Cargar partida no duplica mensajes.
- Un clic siempre resuelve a un Target válido o falla de forma segura.
- Las frases se localizan sin recompilar gameplay.
- Cambiar un icono no modifica la lógica del evento.
- El panel sigue siendo secundario respecto al restaurante.

Este documento es la fuente canónica para implementar las frases del panel ACTIVIDAD.

---

## SOURCE: docs/UI/Servicio/ActivityIconFamilies.md

Category: SUPPORTING

# Bistro Builder — Familias de iconos de ACTIVIDAD

Estado: DISEÑO CERRADO
Versión: 1.0
Fecha: 2026-09-23
Relacionado: `ActivityEventCatalog.md`

## Objetivo

Definir el lote visual mínimo necesario para cubrir el catálogo completo sin crear un icono distinto para cada frase.

Regla: el icono identifica el concepto; el color/insignia identifica estado o gravedad.

## Lenguaje visual

- Misma familia estética que los iconos aprobados de la barra superior.
- Volumen 3D contenido, no caricaturesco.
- Base crema, madera, grafito, dorado suave y colores semánticos puntuales.
- Sin fondos cuadrados propios; deben funcionar sobre las tarjetas crema del panel.
- Lectura clara a tamaño pequeño.
- Siluetas distintas entre familias.
- Evitar texto dentro del icono salvo elementos naturales del objeto.
- Mantener coherencia de iluminación y perspectiva.

## Semántica de color

- Info: dorado/crema neutro.
- Positive: verde controlado.
- Opportunity: dorado vivo.
- Attention: ámbar.
- Critical: coral/rojo.
- Reservation: coral suave o dorado según contexto.

El color no sustituye al símbolo: todas las variantes deben seguir siendo reconocibles sin depender únicamente del color.

## Lote V1 — 28 familias

1. `activity.people.arrival` — grupo llegando.
2. `activity.table.seated` — mesa/grupo sentado.
3. `activity.table.attention` — mesa necesita atención.
4. `activity.wait.alert` — espera excesiva.
5. `activity.table.bill` — cuenta solicitada.
6. `activity.table.complete` — mesa finalizada.
7. `activity.order.new` — nueva comanda.
8. `activity.order.ready` — pedido listo.
9. `activity.order.priority` — comanda prioritaria.
10. `activity.order.error` — error de pedido.
11. `activity.dish.problem` — plato con problema.
12. `activity.dish.trending` — plato destacado.
13. `activity.kitchen.state` — estado cocina.
14. `activity.kitchen.equipment` — avería/bloqueo cocina.

15. `activity.flow.state` — entrada/sala: fluida, espera, saturada, ritmo reducido.
16. `activity.wait.group` — grupo en lista de espera.
17. `activity.table.available` — mesa disponible/preparada.
18. `activity.bar.state` — espera en barra / barra saturada.
19. `activity.staff.state` — falta, saturación o apoyo de personal.
20. `activity.reservation` — reserva próxima/llegada/sentada.
21. `activity.reservation.group` — grupo grande / pico de reservas.
22. `activity.guest.special` — habitual/importante/VIP.
23. `activity.stock.state` — bajo/crítico/agotado/bloqueo.
24. `activity.supplier` — entrega/problema de suministro.
25. `activity.reputation` — reseña positiva/negativa/recuperación.
26. `activity.marketing` — campaña/demanda/riesgo.
27. `activity.opportunity` — bebida/postre/upsell/barra.
28. `activity.trend.up` — gran afluencia/tendencia positiva.

## Variantes por familia

Las variantes se resuelven con insignias pequeñas y color semántico, no rehaciendo el icono desde cero.

Ejemplos:
- `kitchen.state`: gorro/cocina base + punto o aura de estado.
- `stock.state`: caja base + flecha abajo / ! / X.
- `reservation`: calendario o cartel base + reloj / llegada / check.
- `reputation`: estrella/bocadillo base + sonrisa / alerta.
- `staff.state`: persona base + ! / apoyo / saturación.
- `bar.state`: barra/copa base + espera / alerta.

## Iconos que NO necesitan familia propia

No crear iconos separados para:
- cada número de mesa;
- cada plato;
- cada ingrediente;
- cada proveedor;
- cada empleado;
- cada nivel de cocina;
- cada minuto de espera;
- cada tamaño de grupo;
- cada campaña;
- cada texto de reseña.

Esos datos son variables de la entrada, no conceptos visuales nuevos.

## Prioridad de producción gráfica

Primera tanda:
- table.bill
- order.new
- reservation
- reputation
- stock.state
- dish.trending
- wait.alert
- kitchen.state
- staff.state
- trend.up

Segunda tanda:
- people.arrival
- table.seated
- table.attention
- table.complete
- order.ready
- order.priority
- order.error
- dish.problem
- kitchen.equipment

Tercera tanda:
- flow.state
- wait.group
- table.available
- bar.state
- reservation.group
- guest.special
- supplier
- marketing
- opportunity

## Exportación recomendada

Para cada familia aprobada:
- PNG con transparencia para referencia y fallback.
- SVG cuando la forma lo permita sin perder el acabado.
- 256×256 master.
- 128×128 runtime de alta densidad.
- 64×64 runtime estándar.
- nombre estable según `IconKey`.

Convención:
`BB_Activity_<Family>_<Variant>`

Ejemplo:
`BB_Activity_Stock_Critical.png`

## Regla canónica

No diseñar decenas de iconos redundantes. Toda frase nueva debe intentar reutilizar primero una familia existente. Solo se añade una nueva familia cuando el concepto no pueda leerse correctamente con las 28 actuales.

## Registro de aprobaciones

Las selecciones visuales aprobadas se registran en `ActivityApprovedIcons.md`.
Ese documento es la autoridad para saber qué variante exacta de cada familia está cerrada.

Estado a 2026-09-23: **28 de 28 familias aprobadas**.

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
