# BB Living Architecture — Estado de implementación

## LA1 — Kernel topológico
Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

Incluye:
- IDs estables de edificio, nivel, vértice, pared, apertura, región y operación.
- Punto planar propio del dominio, sin dependencia de Transform/GameObject.
- Modelo canónico Building → Levels → Vertices/Walls → Openings.
- Aperturas parametrizadas por `CenterT` sobre su `WallId`.
- `ArchitectureSnapshot` versionado con `DeepClone` profundo.
- Fingerprint SHA-256 determinista e independiente del orden de listas.
- Validador de invariantes LA1: IDs, referencias, longitud, espesor, altura y dominio de aperturas.
- Self-test de Editor sin escena para DeepClone, fingerprint y rechazo de estados inválidos.

Gates pendientes que requieren Unity real:
- compilación C# del proyecto completo;
- ejecución del menú `Bistro Builder/Living Architecture/LA1/Run Self Test`;
- Console 0 errores.

No se declara LA1 validado/cerrado hasta superar esos gates.

## LA2 — Motor de regiones emergentes
Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

Incluye:
- regiones derivadas de la topología, sin `Room GameObject` ni entidad persistente duplicada;
- recorrido determinista de medias aristas sobre grafos planares de paredes rectas;
- gate explícito de planaridad: un cruce o contacto entre paredes sin `VertexId` compartido se rechaza con `LA2_NON_PLANAR_CROSSING`;
- descarte de la cara exterior y de ciclos degenerados;
- `RegionId` derivado de `LevelId + WallIds` mediante SHA-256, estable ante reordenación de colecciones;
- contorno, paredes/vértices delimitadores, área y centroide de cada región;
- consulta espacial `FindContaining` y pertenencia inclusiva sobre borde;
- soporte de múltiples recintos y componentes desconectados;
- self-test puro de 10 casos: región rectangular, área/centroide, interior/exterior/borde, división, identidad estable, grafo abierto, recintos desconectados y rechazo de cruce no topológico.

Principio de autoridad: LA2 **no persiste habitaciones**. La región emerge siempre del kernel LA1; al cambiar topología, se reconstruye. Esto evita estados donde pared y habitación discrepen.

Gates pendientes que requieren Unity real:
- compilación C# del proyecto completo;
- ejecución del self-test LA2 desde el runner acumulativo que se integrará en los siguientes hitos;
- Console 0 errores.

No se declara LA2 validado/cerrado hasta superar esos gates.

## LA3 — Operaciones arquitectónicas transaccionales
Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

Incluye:
- flujo puro `A → propuesta B → validación → commit`, siempre sobre `DeepClone`, sin mutar el snapshot base durante el cálculo;
- `ArchitectureOperationId`, tipo y etiqueta de operación en cada transacción;
- detección de propuestas obsoletas mediante fingerprint base (`LA3_STALE_PROPOSAL`);
- commit atómico con registro completo A/B para Undo/Redo semántico;
- rollback natural: cualquier excepción, invariante LA1 o gate topológico LA2 rechaza B y deja A intacto;
- primitivas V1 para crear pared, mover pared, dividir pared, eliminar pared y mover vértice compartido;
- `SplitWall` conserva el `WallId` del primer tramo, crea identidad estable para el segundo y remapea aperturas por coordenada paramétrica;
- un split que atraviesa una apertura se rechaza explícitamente, sin reparar ni desplazar silenciosamente la puerta/ventana;
- mover una pared opera sobre sus vértices canónicos, por lo que las conexiones compartidas siguen siendo conexiones topológicas reales y no aparecen grietas por duplicación de puntos;
- self-test puro de 12 casos: pureza de propuesta, commit, rollback, stale conflict, Undo, Redo, identidad tras split, remapeo de apertura, rechazo de split sobre apertura, delete reversible, create + región y conectividad compartida al mover pared.

Principio de autoridad: LA3 **no toca GameObjects, meshes, economía, seating ni circulación**. Solo transforma snapshots canónicos. Los impactos externos y la preservación avanzada de intención entran en LA4–LA6.

Gates pendientes que requieren Unity real:
- compilación C# del proyecto completo;
- ejecución acumulativa LA1+LA2+LA3;
- confirmar 12/0 en el self-test LA3;
- Console 0 errores.

No se declara LA3 validado/cerrado hasta superar esos gates.

## LA4 — Restricciones e intención
Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

Incluye:
- `ArchitectureIntent` como declaración explícita de lo que una reforma intenta preservar, separada de la mutación LA3;
- restricciones tipadas V1 para vértice/ancla, longitud de pared, ángulo, apertura centrada, distancia física de apertura desde el extremo inicial y área de región;
- severidad `Hard` frente a `Advisory`: una restricción dura impide la propuesta si no puede preservarse; una advisory informa sin adquirir autoridad;
- correcciones locales deterministas para ancla, longitud, ángulo y relaciones de apertura, sin solver global ni rediseño silencioso;
- preservación de distancia física de una apertura al cambiar la longitud de su pared mediante recálculo de `CenterT`;
- área de región como gate verificable: LA4 puede preservarla si la operación propuesta ya lo permite, pero no inventa una redistribución global para forzarla;
- evaluaciones estructuradas por restricción con código, valor real, objetivo y diagnóstico, reutilizables por Presentation/Impact;
- integración sobre el flujo puro LA3: A permanece intacto y solo B recibe las correcciones autorizadas;
- rechazo explícito cuando una preservación dura no es resoluble, sin cambios ocultos sobre el snapshot real;
- self-test puro LA4 de 9 casos: longitud, ángulo, ancla, apertura centrada, offset físico, área compatible, área incompatible + pureza de A, advisory y determinismo.

Principio de autoridad: LA4 **expresa y verifica intención; no decide gameplay ni impacto externo**. No toca GameObjects, colocables, seating, circulación ni economía. El snap ofrecerá candidatos en LA5 y los impactos externos entran en LA6.

Gates pendientes que requieren Unity real:
- compilación C# del proyecto completo;
- ejecución acumulativa LA1+LA2+LA3+LA4;
- confirmar 9/0 en el self-test LA4;
- Console 0 errores.

No se declara LA4 validado/cerrado hasta superar esos gates.

## LA5 — Snap inteligente
Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

Incluye:
- `ArchitectureSnapService` puro y sin mutaciones, separado de operaciones, solver y Presentation;
- candidatos geométricos a vértice y proyección sobre pared dentro de tolerancia configurable;
- primeros snaps semánticos V1: paralelo, perpendicular, igualdad de longitud y continuidad desde extremos arquitectónicos;
- cada candidato devuelve tipo, punto propuesto, entidad fuente, `ReasonCode`, distancia, score, nivel de confianza y, cuando aplica, ángulo/longitud objetivo;
- niveles de confianza `Low/Medium/High` derivados de la cercanía respecto a la tolerancia, para que LA9/LA10 puedan distinguir coincidencias fuertes de aproximaciones débiles;
- ranking determinista por score, tipo, entidad y posición; mismo nivel + mismo gesto = misma secuencia de candidatos;
- exclusión explícita de una pared en edición para evitar auto-snap circular;
- las relaciones angulares usan orientación axial, por lo que una pared de 0° y otra de 180° se consideran paralelas sin duplicar semántica;
- ningún candidato se aplica automáticamente: LA5 **ofrece opciones** y la herramienta/usuario decide cuál aceptar;
- self-test puro LA5 de 10 casos: vértice, proyección, paralelo, perpendicular, igual longitud, continuidad, exclusión, tolerancia, determinismo y ausencia de mutación.

Principio de autoridad: LA5 **no construye ni modifica arquitectura**. Tampoco decide validez de gameplay. Es un generador read-only de candidatos reutilizable por LA9 y visualizable por LA10.

Gates pendientes que requieren Unity real:
- compilación C# del proyecto completo;
- ejecución acumulativa LA1+LA2+LA3+LA4+LA5;
- confirmar 10/0 en el self-test LA5;
- Console 0 errores.

No se declara LA5 validado/cerrado hasta superar esos gates.
