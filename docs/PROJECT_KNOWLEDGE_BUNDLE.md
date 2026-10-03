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
- **SAVIC:** lote solicitado cerrado en `feature/savic-v1`. Auditoría 03/10/2026 05:57:01 UTC: **27 únicos, 18 publicados, 17 catálogo placeables, 0 revisiones, 0 fallidos, 0 inbox y 9 históricos sin original** conservados fuera del lote; selected14 = **14 publicados, 0 revisiones**. Armario, lámpara, barra, tres BarStool y campana integrados por contratos canónicos. Campana pasiva elevada con perfil explícito, cuerpo/colocación/BBSIS/Navigation compartidos y lifecycle transaccional; sin extracción D-003 ni techo inferido. Aceptación estricta MainCatalog/SaveDefinitionCatalog 05:53:31 UTC: área Kitchen real, paso humano inferior, claims, dos cargas SaveGame con IDs estables/objetos nuevos, slot eliminado y Console limpia hasta Editor. Barra y taburetes conservan proofs actuales; taburetes con clientes Humanoid sentados y seis cargas reales verificadas 05:10:21 UTC. Regresión final **gate 26/26, core 84/84, Navigation 22/22, barra 59/59 y BBSIS 2B 18/18 PASS**, UnityActualExitCode=0. Evidencia: `docs/SAVIC.md` 84–85, `overhead-final-canonical-verified-regression.log`, `overhead-main-catalog-strict-runtime-verified.log`, `canonical-content-inventory.json`. Este cierre certifica el lote; no afirma clasificación universal futura, recuperación de servicio ocupado ni jornada IA completa.

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

Ampliación SAVIC 03/10/2026: BarStool se publica como Seating funcional con asociación automática a una plaza persistible existente, sin aumentar capacidad. Su aceptación vincula source/plan/prefab/cliente/Animation/informe actuales y demuestra ocupación, lease, llegada, representación sentada y cargas SaveGame repetidas desde el catálogo. La animación no escribe reservas ni desplaza el root de Navigation. La geometría elevada de la campana continúa pendiente; el contador no se reduce convirtiéndola en decoración. Detalle: SAVIC 77–78.
Cambios futuros deben tratarse como hardening/V2 y demostrar que respetan contratos públicos y compatibilidad con sistemas ya integrados.

## Colocables compuestos de SAVIC

La ampliación funcional autorizada de SAVIC conserva un único registro BBSIS. Un sujeto puede declarar un propietario de ciclo de vida espacial mediante `IBistroBuilderSpatialLifecycleOwner`; si opta a esta política, solo es elegible para alta/rebuild mientras el propietario confirme su activación. La barra colocable consulta `RestaurantPlaceableRegistry`, cuya identidad persistida deriva las identidades de cuerpo y plazas. La ausencia de propietario requerido rechaza el alta; los sujetos anteriores conservan su comportamiento.

`BistroBuilderBarBodySpatialAdapter` ofrece los puertos de las plazas hijas durante el preflight de la raíz provisional, pero la recopilación global solo incluye instancias confirmadas. El cuerpo raíz contiene las piezas estáticas medidas y sus plazas conservan el contrato/leases nativos de `work.bar`, sin una caja adicional entre cliente y camarero ni duplicar semánticas globales. Alta/baja del cuerpo y plazas es transaccional; ocupación o lease activo impiden retirada. Navigation usa esta misma elegibilidad para excluir propuestas provisionales de su topología.

Regresión integrada de 02/10/2026: provisional/rebuild, preflight y cuerpos/puertos reales, asignación/lease, conflicto durante registro con rollback, reintento y limpieza; core 84/84, Navigation 22/22 y servicio de barra 59/59 PASS. Evidencia: `bar-body-spatial-native-verified.log`; límites y continuidad en `docs/SAVIC.md`, sección 65. Esta integración no demuestra por sí sola publicación ni SaveGame del mostrador Meshy.

La barra Meshy ya dispone de familia/publicación canónicas con aceptación vinculada a fuente, plan y prefab. La prueba posterior desde el catálogo principal real comprobó colocación, plazas/leases y rutas nativas antes/después de SaveGame con identidad de instancia estable, nueva instancia Unity y slot eliminado; Console limpia hasta regresar al Editor. Se reorganiza únicamente el mobiliario del layout temporal por el ciclo de vida existente, conservando límites, obstáculos y validadores. Evidencia `bar-counter-runtime-strict-acceptance.log`, 02/10/2026 20:49 UTC; no representa todavía una jornada completa de IA. Los taburetes siguen pendientes de su contrato de asiento/plaza, sin convertirlos en asientos de mesa.

Regresión posterior demostrada y corregida: el coordinador operacional debe incorporar las altas/bajas dinámicas de BarServiceRegistry y liberar el lease del cliente cuando la plaza queda libre. El registro nativo sigue siendo la fuente de plazas; BBSIS concede y libera derechos espaciales. La prueba previa falló por alta tardía ignorada (`bar-dynamic-coordinator-before-fix.log`, exit 1). La reprueba real de barra publicada usa ahora concesión/liberación automáticas del coordinador antes/después de SaveGame y conserva Console limpia (`bar-counter-dynamic-coordinator-runtime-acceptance.log`, 02/10/2026 22:24 UTC, exit 0). Gate 21/21, core 84/84, Navigation 22/22, servicio barra 59/59 y fase 2B 18/18 PASS; continuidad en SAVIC 70–72.

La ampliación de asiento de barra permite relacionar un cuerpo de taburete confirmado con una plaza nativa de capacidad 1. El lease de cliente solo excluye el cuerpo asociado mediante `relatedSubjectId`; sigue rechazando cuerpos y reservas ajenos. Occupancy/capacidad pertenecen a la plaza, la aproximación queda en suelo y el SeatFrame es visual. Una fuente sin counter surface autorado, un provisional o una asociación no validada no concede esa excepción. La API y autoría física están probadas; familia, colocación automática, representación y persistencia de taburetes aún pendientes, sin publicación anticipada.

Ampliación posterior verificada (02/10/2026 23:49 UTC): asociación automática desde pose propuesta sin mutación ni reserva durante preflight, con activación opcional transaccional en PlaceableRegistry y rollback de índices/cuerpo/enlace si falla. `seating.bar` forma parte del catálogo espacial. El proveedor semántico candidato relaciona exclusivamente el ID del puerto de cliente de la plaza compatible; no exceptúa los puertos de trabajo/transferencia ni obstáculos ajenos. Occupant/capacidad siguen en el registro nativo y el lease espacial en BBSIS. CounterSurfacePoint es un datum propio del plan de barra, no un marker inferido desde un log.

Regresión demostrada: después de `ResetTransientRuntimeStateAfterLoad`, un ID de lease cacheado no implica lease activo. `BarSpatialAdapter` consulta el lease real BBSIS y el coordinador puede reconciliar/reconcederlo; la prueba negativa previa termina en exit 1 y la posterior en exit 0. Gate 23/23, core 84/84, Navigation 22/22, barra 59/59, fase 2B 18/18 PASS. Los tres sources de taburete pasan asociación y reconstrucción nativa JSON, pero Animation sentada, SaveGame jugable de taburetes y publicación aún están pendientes. Continuidad en SAVIC 73–76.

## Intervalo vertical opcional de cuerpos y claims

La ampliación autorizada de SAVIC conserva BBSIS como autoridad geométrica. `SpatialHeightRange` permite declarar mínimo/máximo Y mundiales de un volumen. Ausencia o datos inválidos nunca prueban separación: siguen representando una columna conservadora. `SpatialProxyPart.hasVerticalExtent/height` autoran espesor local con escala positiva y base horizontal; el puente físico proyecta el mismo intervalo a colocación/Navigation. No se deducen alturas de mallas, de leases legacy ni de objetos ajenos. Un candidato con intervalo autorado inválido se rechaza; el existente conserva el bloqueo de respaldo.

Overlaps exige intersección horizontal y vertical cuando ambos intervalos son conocidos. Un claim sin altura conserva su comportamiento anterior; una consulta humana que declare 0–2 m puede demostrar separación de un cuerpo cuya cara inferior está a 2,2 m. Esto no certifica alturas de cargas/carritos desconocidos ni convierte un lease anterior en acotado. La pose candidata transforma el intervalo sin cambiar el Transform real. Prueba nativa: cuerpo, concesión/liberación de lease inferior y rechazo de intersección de cabeza; detalles y evidencia en SAVIC 81–82. La campana permanece en revisión hasta su propia familia, lifecycle, publicación y SaveGame reales; no hay simulación de extracción D-003.

## Cuerpo pasivo elevado confirmado por PlaceableRegistry

Ampliación verificada **03/10/2026 05:57 UTC**: `BistroBuilderPassiveBodySpatialBinding` participa en la activación canónica y ofrece `IBistroBuilderSpatialLifecycleOwner`. La raíz solo es elegible con registro de colocable y activación funcional confirmados; un provisional no se introduce por Configure, rebuild o Navigation. El cuerpo usa `spatial.passive.placeable.<InstanceId>.body`, conserva referencias runtime a servicios y revierte registro/identidad en rollback o baja. La consulta de leases activos pertenece a BBSIS y permite impedir alta/retirada que intersecten un derecho vigente. Los intervalos desconocidos siguen bloqueando conservadoramente; no se inventan alturas para claims antiguos.

La campana real ya está publicada como caso pasivo de KitchenEquipment. Comparte cuerpo acotado entre collider, colocación, BBSIS y Navigation y conserva el anclaje de suelo/área. La aceptación estricta MainCatalog/SaveDefinitionCatalog prueba paso humano inferior, claims permitidos/rechazados, dos cargas SaveGame con identidad estable y objetos nuevos, rebuild/baja/cleanup y Console limpia. Gate 26/26, core 84/84, Navigation 22/22, barra 59/59 y BBSIS 2B 18/18 PASS; evidencia y límites en SAVIC 84–85. No añade autoridad de servicio ni extracción/ventilación D-003.

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

## Ampliación autorizada SAVIC: clientes en taburetes de barra

El prefab canónico de clientes puede referenciar un perfil Humanoid certificado. Cada miembro visual registra su propio actor en Animation V1; el bootstrap evita registrar también la raíz lógica sobre su Animator. El presentador de barra observa plaza/ocupación/asiento nativos, llegada real de Navigation y lease activo BBSIS, y representa sit/idle/stand mediante Motion Recipes existentes. Alinea solo el visual y la pelvis al SeatFrame con offset común autorado; el root lógico, reservas y resultados permanecen en sus autoridades. Cancelación, baja y rehidratación limpian la sesión visual propia.

Los tres taburetes reales pasan ocupación/llegada/lease, postura Humanoid sentada, root intacto, stand y cleanup; repetidos tras dos cargas SaveGame por asset. Capturas inspeccionadas sin materiales de error. No se certifica un nuevo ciclo walk, asiento de mesa ni jornada completa de IA. Pruebas aíslan otros flujos del cliente y usan ocupación del registro nativo; el checkpoint está desocupado. Evidencia y límites: SAVIC 77–78 y `bar-stool-main-catalog-runtime-acceptance.log` (03/10/2026 01:08 UTC, exit 0).

Reprueba estricta 01:14:31 UTC: MainCatalog/SaveDefinitionCatalog exactos, bindings activados sin reconfiguración diagnóstica y seis cargas reales en total, tres capturas inspeccionadas. `bar-stool-main-catalog-strict-native-acceptance.log`, exit 0. Animation V1 13/13 y clientes 10G PASS en la regresión final; SAVIC 79–80.

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

Los colocables con cuerpo cóncavo pueden optar a proyectar las cajas estáticas de su proxy BBSIS mediante `BistroBuilderSpatialPhysicalFootprintAdapter`. Navigation y colocación consumen la misma geometría; la envolvente exterior sigue controlando límites de área y bloquea conservadoramente cuando la adaptación es inválida. No debe rellenarse un hueco navegable con el bounding box del render ni desactivarse su bloqueo físico para forzar una ruta.

Una propuesta provisional con propietario espacial explícito no entra en la topología global. Navigation consulta `SpatialSubject.IsRegistrationEligible`, cuya política de barra deriva del registro canónico de colocables; no mantiene otra autoridad de activación. El preflight conserva acceso a su geometría y puertos propios.

La aproximación por anillo de docking requiere validar también el enlace final al destino original con la consulta estructural existente. Encontrar ruta hacia un punto cercano no demuestra que el segmento posterior sea transitable. Regresión de SAVIC del 02/10/2026: U sintética con ruta real de 5 m, ninguna pieza física atravesada y destino rechazado al bloquear su interior; Navigation 17 **22/22 PASS**, Edit Mode core **84/84 PASS**. Evidencia y limitaciones en `docs/SAVIC.md`, sección 64.

## Cierre/hardening

La geometría elevada opta al intervalo vertical del mismo proxy BBSIS consumido por colocación. Navigation conserva esas piezas en su topología y compara cada muestra contra una envolvente humana de altura configurada (`defaultAgentHeight`, 2 m por defecto), además del radio/separación existentes. Planner, validación de ruta NavMesh y solver local comparten esta consulta. El dato es autoría explícita del envelope, no una altura inferida del modelo. Altura desconocida conserva el obstáculo planar anterior.

Una pieza con altura autorada requiere clearance completo incluso cerca del destino; no hereda la excepción de aproximación a un endpoint legacy. Una adaptación física inválida también exige clearance completo sobre su envolvente conservadora. Una negativa nativa demostró que un rectángulo inválido pequeño se podía atravesar por estar entero dentro de la tolerancia de docking; se corrigió el caso en la autoridad Navigation, manteniendo las excepciones antiguas de objetos sin opt-in. Ruta humana inferior de 8 m muestreada cada 5 cm, baja altura con endpoint dentro rechazada y datos inválidos bloqueados; SAVIC 81–82. No equivale todavía a la aceptación runtime del GLB de campana.

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

## Asientos dinámicos de barra — SAVIC

`restaurant.structure` v2 añade enlaces mínimos de taburete: InstanceId del asiento, InstanceId de la barra e índice estable de plaza nativa. No duplica ocupación, capacidad ni reservas. Su proveedor migra v1 de forma pura, conservando registros anteriores y una lista vacía cuando no existía el campo. La prevalidación comprueba IDs/roles/índices/duplicados y los frames de prefab en las poses guardadas antes de retirar instancias vivas; rechaza enlaces ausentes o incompatibles. Carga barras antes de taburetes y verifica la asociación resultante; prepara la retirada en orden inverso de dependencia.

Al limpiar estado transitorio, BBSIS sigue siendo autoridad del lease: un ID cacheado en el adaptador no prueba una reserva activa. La consulta y reconciliación de barra comprueban el lease real, incluyendo expiración, antes de declarar readiness o conceder uno nuevo.

Verificado 02/10/2026: migración/negativos y dos reconstrucciones JSON nativas de barra+taburete con cada uno de los tres GLB reales, IDs estables/nuevas instancias/ocupación y leases/cleanup. La barra publicada sí pasó SaveGame real en Play Mode con v2 y Console limpia (`bar-counter-native-seat-foundation-runtime-final.log`, 23:44 UTC). El SaveGame real de taburetes y su representación sentada permanecen pendientes; las pruebas aisladas no autorizan publicación. Detalle y evidencia en SAVIC 73–76.

Actualización 03/10/2026: los tres taburetes ya pasaron dos cargas SaveGame reales por asset, primero como candidatos y luego publicados en catálogo principal. Se reconstruyen ambos muebles con mismos ItemId/InstanceId y plaza/enlace nativos, nuevas instancias Unity y sin residuos; un cliente nuevo ocupa y se sienta mediante Animation antes y después de cada carga. El slot diagnóstico se elimina y Console permanece limpia hasta Editor. Las instantáneas guardadas no tienen ocupante: no se afirma restauración de sesión de servicio activa. Evidencia `bar-stool-main-catalog-runtime-acceptance.log`, exit 0, 01:08 UTC; SAVIC 77–78.

La reprueba estricta posterior resuelve cada ItemDefinition exacto desde MainCatalog y SaveDefinitionCatalog, sin catálogo candidato ni reconfiguración del binding en el fixture. Seis cargas, mismo resultado y Console limpia: `bar-stool-main-catalog-strict-native-acceptance.log`, exit 0, 03/10/2026 01:14:31 UTC; SAVIC 79–80.

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

Los clones de font TMP usados en runtime poseen copias propias de su atlas y material; no comparten esos recursos destruibles con subassets persistentes. El fallback de acentos conserva la tipografía original. Hardening demostrado al verificar SAVIC: el clon superficial de Recoleta emitía errores al salir de Play Mode, porque el destructor TMP intentaba destruir material/atlas del asset. La corrección preserva los recursos fuente; evidencia y aceptación runtime estricta en `docs/SAVIC.md`, sección 69.

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
| D-027 | VIGENTE | Modo Edición adopta como dirección gráfica la Propuesta C revisada: catálogo claro vertical a la izquierda, viewport central, inspector claro a la derecha con Reglas de colocación y franja inferior de herramientas/acciones. |
| D-028 | VIGENTE | BBFFVAS es una herramienta interna de autoría; no es una mecánica ni interfaz destinada al jugador. |
| D-029 | VIGENTE | Una variante puramente visual comparte geometría, footprint, collider y contratos espaciales; los acabados no justifican duplicar prefabs. |
| D-030 | VIGENTE | Acabado Automático solo completa zonas/canales missing o incompletos; nunca sobrescribe trabajo válido salvo orden explícita. |
| D-031 | VIGENTE | Toda propuesta automática requiere preview y aplicación explícita; reutilizar acabados canónicos tiene prioridad sobre generar nuevos. |
| D-032 | VIGENTE | Si la semántica de una superficie es incierta, el sistema solicita clasificación y no asigna materiales a ciegas. |

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

## SOURCE: docs/SAVIC_REALISTIC_TEST_PACK_V1.md

Category: SUPPORTING

# SAVIC — Realistic Test Pack V1

## Objetivo

Dar a las builds de prueba de Bistro Builder una base visual más creíble usando únicamente contenido ya presente en el proyecto y contratos existentes de SAVIC/Placeable Factory.

El pack no sustituye al contenido definitivo. Es una selección curada para pruebas funcionales y visuales.

## Contenido

### Colocables ya existentes y reutilizados

- `factory_test_plant`
- `pf_bb_chair_master_001_olive`
- `pf_bb_chair_master_001_red`
- `pf_bb_chair_master_001_white`
- `pf_bb_chair_master_001_yellow`
- `chair_bistro_01`
- `bb_chair_master_002`
- `table_basic`
- `table_basic_4`
- `bb_table_b90c47bde3e949918ec76a13dc17c61c`

### Variantes nuevas que instala el pack

- `chair_bistro_01_oak_warm` — Silla bistró, roble cálido
- `chair_bistro_01_painted_black` — Silla bistró, negro
- `chair_bistro_01_painted_white` — Silla bistró, blanco
- `chair_bistro_01_sage_green` — Silla bistró, verde salvia
- `chair_bistro_01_walnut_dark` — Silla bistró, nogal oscuro

Estas cinco variantes parten de prefabs visuales ya existentes. El instalador las pasa por `BistroBuilderPlaceableFactoryEngine` con preset `Chair`, por lo que obtiene prefab jugable, `RestaurantPlaceableObject`, `EditableObjectDefinition`, capacidades de seating, collider cuando procede, thumbnail y alta de catálogo.

### Construcción incluida en la validación

- `Pared_0.5m.prefab`
- `Pared_1.0m.prefab`
- `Pared_2.0m.prefab`
- `Pared_4.0m.prefab`
- `Puerta_roble_abierta.prefab`
- `Ventana_marco_grafito.prefab`

## Resultado esperado

- 15 artículos colocables disponibles en el catálogo.
- 6 elementos constructivos válidos.
- 21 piezas curadas utilizables en pruebas.
- Todos los placeables deben resolver prefab, icono/preview y `EditableObjectDefinition`.
- La instalación es idempotente: volver a ejecutarla no debe duplicar artículos.

## Uso

Instalar o reparar:

`Tools > Bistro Builder > SAVIC > Packs > Realistic Test Pack V1 > Install or Repair`

Validar:

`Tools > Bistro Builder > SAVIC > Packs > Realistic Test Pack V1 > Validate`

También dispone de entradas batch:

- `BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.InstallOrRepairFromCommandLine`
- `BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.ValidateFromCommandLine`

Runner de una sola orden:

`Tools/BistroBuilder/RunSavicRealisticTestPackV1.ps1`

El runner instala/repara el pack, ejecuta después la validación y devuelve código de salida distinto de cero si cualquiera de las dos fases falla.

## Alcance

Este V1 usa solo contenido ya almacenado en Bistro Builder. La siguiente expansión deberá centrarse en añadir variedad real de mesas, iluminación, decoración y equipamiento pasivo mediante SAVIC, evitando incorporar assets externos sin licencia/procedencia clara.

---

## SOURCE: docs/SAVIC.md

Category: SUPPORTING

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
