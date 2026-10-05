# Editor V2 — Gate Assets4All → SAVIC

**Fecha:** 2026-10-05
**Estado:** VALIDANDO
**Conclusión:** conexión funcional demostrada; gate de cinco familias todavía NO PASS.

## 1. Qué se ha comprobado

La conexión técnica existe en ambos extremos.

### Assets4All

En el worktree activo de Assets4All v0.1.29 existe un exportador específico:

`blender_extension/assets4all/savic_delivery.py`

El contrato externo es:

`schemaId = assets4all.savic-delivery`
`schemaVersion = 1`

La entrega contiene:

- `model.glb`;
- `asset4all.json`;
- `partgraph.json`;
- `delivery.json`.

El exportador verifica antes de entregar:

- WORK aprobado;
- SOURCE intacto;
- autoridad PartGraph válida;
- cobertura completa de WORK;
- piezas disjuntas;
- correspondencia de triángulos exportados;
- identidad persistente del asset;
- hashes SHA-256;
- revisión secuencial;
- fingerprint y parentFingerprint;
- conservación de SOURCE y WORK tras exportar.

Existe además la sonda real:

`tools/blender_savic_delivery_probe.py`

que cubre exportación, repetición idempotente, reapertura, cambio de acabado, cambio de escala/apoyo y rechazo de WORK no aprobado.

### SAVIC / Bistro Builder

En el worktree de integración SAVIC existe el receptor:

- `SavicAssets4AllService.cs`;
- `SavicAssets4AllModels.cs`;
- `SavicAssets4AllEvidence.cs`;
- `SavicAssets4AllWindow.cs`.

SAVIC valida:

- schema y versión;
- UUID;
- revisión;
- sistema de coordenadas;
- hashes de los cuatro artefactos;
- fingerprint;
- PartKeys y membershipDigest;
- nodos y conteos de triángulos;
- PartGraph frente a delivery;
- GLB frente a evidencia;
- actualización de artículo existente;
- conservación de identidad y GUID;
- conservación de ajustes manuales;
- materiales protegidos;
- rechazo de revisión antigua;
- rechazo de paquete corrupto;
- diario y rollback de publicación.

Existe detección automática en `ContentInbox/Deliveries` y UI manual mediante:

`Tools → Bistro Builder → SAVIC → Assets4ALL → Import Delivery`.

## 2. Evidencia real encontrada

La aceptación real documentada utiliza un armario Meshy ya publicado:

`8a5c37cab8eb4366ab675afa66af65ad`

La prueba cubre:

- primera vinculación;
- varias revisiones;
- identidad canónica conservada;
- GUID de catálogo conservado;
- propiedades manuales conservadas;
- material protegido conservado;
- histórico de fuentes;
- importación idempotente;
- rechazo de GLB corrupto;
- rechazo de revisión obsoleta;
- rollback de fallo durante publicación;
- recuperación de diario interrumpido;
- colocación real desde el catálogo;
- SaveGame con una revisión;
- actualización del modelo;
- carga de la misma partida con IDs y acabado conservados.

Helpers presentes:

- `SavicAssets4AllAcceptance`;
- `SavicAssets4AllSaveGameAcceptance`;
- `tools/blender_savic_delivery_probe.py`.

## 3. Por qué el gate todavía no es PASS

El plan canónico de Editor V2 exige una prueba completa con cinco familias:

1. mesa;
2. silla;
3. lámpara;
4. decoración;
5. equipamiento pasivo.

La evidencia encontrada certifica el flujo real con un armario/equipamiento, pero el propio documento de arquitectura del puente indica expresamente que **no certifica todas las familias**.

No se ha encontrado evidencia reproducible equivalente para las otras cuatro familias pasando por el recorrido completo:

`Assets4All → delivery → SAVIC → publicación → catálogo → colocación → Save/Load cuando corresponda`.

Además, el puente todavía aparece como WIP local:

- en Assets4All, `savic_delivery.py`, la guía y las modificaciones de UI están sin versionar en el worktree v0.1.29;
- en Bistro Builder, los archivos `SavicAssets4All*` y documentación relacionada están sin versionar dentro del worktree `codex/savic-presentation-integration`.

Por tanto, hoy puede afirmarse:

**CONEXIÓN FUNCIONAL: SÍ.**
**CONTRATO TÉCNICO: IMPLEMENTADO.**
**ACEPTACIÓN REAL DE UN ASSET: PASS.**
**GATE EDITOR V2 DE CINCO FAMILIAS: AÚN NO PASS.**
**INTEGRACIÓN VERSIONADA/ESTABLE: PENDIENTE.**

## 4. Qué falta exactamente para cerrar el gate

Sin rediseñar el puente:

- versionar el trabajo actual de Assets4All y SAVIC de forma trazable;
- ejecutar la misma ruta real con una mesa;
- ejecutar la misma ruta real con una silla;
- ejecutar la misma ruta real con una lámpara;
- ejecutar la misma ruta real con una decoración;
- mantener el equipamiento ya demostrado o repetirlo con el armario actual;
- verificar para cada familia identidad, definición/prefab resoluble, collider, preview, categoría y contratos de colocación/espaciales;
- confirmar que ambigüedad crítica termina en NEEDS_REVIEW y no en invención silenciosa.

## 5. Regla para Editor V2

B1 permanece bloqueado hasta que este gate cambie a PASS.

No es necesario rehacer la conexión. Lo pendiente es **cerrar cobertura e integración**, no rediseñar el contrato.
