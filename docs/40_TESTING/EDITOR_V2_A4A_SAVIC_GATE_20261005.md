# Editor V2 — Gate Assets4All → SAVIC

**Fecha de cierre:** 2026-10-06
**Estado:** PASS
**Decisión:** B1 de Editor V2 queda desbloqueado.
**Alcance:** gate de dependencia entre Assets4All, SAVIC y Editor V2; no autoriza por sí solo merge a MASTER.

## 1. Contrato verificado

La conexión usa el contrato externo:

`schemaId = assets4all.savic-delivery`
`schemaVersion = 1`

Cada entrega contiene:

- `model.glb`;
- `asset4all.json`;
- `partgraph.json`;
- `delivery.json`.

Assets4All conserva la autoridad sobre SOURCE, WORK y PartGraph. SAVIC conserva clasificación, publicación, catálogo, contratos jugables, BBSIS y persistencia.

El exportador Assets4All verifica:

- WORK aprobado;
- SOURCE intacto;
- autoridad PartGraph válida;
- cobertura completa y disjunta;
- correspondencia de triángulos exportados;
- identidad persistente;
- revisión y fingerprint;
- hashes SHA-256;
- ausencia de mutación física de SOURCE/WORK durante la entrega.

El receptor SAVIC verifica:

- schema y versión;
- UUID y revisión;
- sistema de coordenadas;
- hashes y fingerprint;
- PartKeys y membershipDigest;
- nodos y triángulos;
- coherencia PartGraph ↔ delivery ↔ GLB;
- revisiones antiguas/corruptas;
- conservación de identidad;
- conservación de overrides;
- rollback y recuperación de publicación.

## 2. Cinco familias reales

Se han generado y comprobado entregas reales de revisión 1 para las cinco familias exigidas por Editor V2:

| Familia | UUID Assets4All | Parts | Contrato/hashes |
|---|---|---:|---|
| Mesa | `603271c371ef4148b6dd9b801bdbd595` | 14 | PASS |
| Silla | `f4e03532538e42d0a82d1b7142912d03` | 14 | PASS |
| Lámpara | `01767545ae29470a94b632f0d784c6f1` | 9 | PASS |
| Decoración | `4f9fdcfc8be441f793e8fb300daa3bee` | 6 | PASS |
| Equipamiento pasivo | `be8df6979b8946979eb6264e6bf8f45f` | 14 | PASS |

En los cinco paquetes se comprobó:

- `schemaId=assets4all.savic-delivery`;
- `schemaVersion=1`;
- `revision=1`;
- `coordinateSystem=GLTF2_METERS_Y_UP`;
- presencia de los cuatro archivos obligatorios;
- hashes SHA-256 del modelo, manifiesto y PartGraph coincidentes con `delivery.json`;
- PartGraph no vacío.

Las sondas específicas de silla, lámpara y decoración registran además SOURCE/WORK intactos. Mesa y equipamiento se validan con el mismo exportador/contrato; la aceptación profunda ya existente del armario cubre además revisiones, idempotencia y persistencia.

## 3. Evidencia end-to-end profunda

La aceptación profunda existente utiliza el armario/equipamiento SAVIC:

`8a5c37cab8eb4366ab675afa66af65ad`

Está demostrado:

- primera vinculación;
- varias revisiones;
- identidad canónica estable;
- GUID de catálogo estable;
- overrides manuales conservados;
- material protegido conservado;
- histórico de fuentes;
- repetición idempotente;
- rechazo de paquete corrupto;
- rechazo de revisión obsoleta;
- rollback tras fallo de publicación;
- recuperación de diario interrumpido;
- publicación en catálogo;
- colocación en Modo Edición;
- SaveGame;
- actualización del modelo;
- carga posterior conservando ItemId, InstanceId y acabado.

Helpers canónicos:

- `SavicAssets4AllAcceptance`;
- `SavicAssets4AllSaveGameAcceptance`;
- `tools/blender_savic_delivery_probe.py`.

## 4. Endpoints de familia

Los cinco tipos objetivo ya disponen de contenido publicado/materializado por SAVIC y de sus publishers/validadores correspondientes.

La frontera queda así:

**Assets4All → delivery V1 común → SavicAssets4AllService → clasificación/publicación SAVIC → definición/prefab/catálogo → Editor**

El receptor del paquete no contiene ramas específicas por mesa, silla, lámpara, decoración o equipamiento. La especialización posterior sigue perteneciendo a SAVIC. Por ello, el gate de integración no exige duplicar cinco veces las pruebas destructivas del receptor ya cubiertas end-to-end.

## 5. Versionado del puente

### Assets4All

El exportador `savic_delivery.py` está versionado en el commit:

`fb6c2d3 — Implement Material Studio V2 dressing workflow`

Ese commit está contenido en la rama remota activa `origin/wip/v0.1.29-physicalgraph-v5-20260922`.

### SAVIC / Bistro Builder

El receptor y sus adaptaciones están versionados de forma aislada en:

`feature/savic-assets4all-delivery-v1`

Commit:

`a5efc06f — feat(savic): version Assets4All delivery bridge`

Existe además la rama de validación `validation/a4a-savic-gate-20261006` con el commit `b094ab6c`.

No se requiere integrar estas ramas en MASTER para desbloquear B1. Su integración en la rama acumulativa de SAVIC sigue siendo una operación separada y deberá conservar las regresiones de SAVIC.

## 6. Matriz Unity aislada adicional

Se intentó ejecutar una única prueba batch reimportando las cinco entregas en una copia desechable de Unity 6000.3.19f1.

La copia aislada quedó bloqueada antes de ejecutar el código del gate por el entorno de Package Manager:

- con UPM normal, el servidor local de Package Manager no consiguió abrir su IPC;
- con `-noUpm`, faltaron referencias de UGUI/TMP/Input System;
- una contingencia con DLLs cacheadas no fue válida por dependencias transitivas de assemblies.

Por tanto:

**no se afirma que las cinco entregas hayan sido reimportadas juntas en una única sesión Unity.**

Esta matriz queda como regresión adicional futura y no invalida el gate de dependencia porque:

- las cinco entregas reales y sus hashes están verificadas;
- el contrato es único y común;
- los cinco endpoints de familia existen en SAVIC;
- el receptor está probado end-to-end con revisiones, catálogo, colocación, SaveGame y rollback;
- el puente está versionado en ambos extremos.

No se atribuye el fallo del clon al contrato Assets4All → SAVIC.

## 7. Decisión

**Gate Assets4All → SAVIC = PASS.**

**B1 — Cerrar riesgos de la auditoría = DESBLOQUEADO.**

Este PASS no autoriza:

- merge automático a MASTER;
- mezclar la rama completa de SAVIC;
- borrar worktrees con WIP;
- reducir las regresiones de SAVIC;
- omitir futuras pruebas de la matriz cinco-familias cuando exista un checkout Unity estable.

La dependencia necesaria para comenzar Editor V2 B1 queda suficientemente estable, trazable y demostrada.
