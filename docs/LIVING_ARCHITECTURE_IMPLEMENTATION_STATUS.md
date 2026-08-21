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
