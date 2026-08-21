# BB Living Architecture — LA7 Runtime / Mesher

Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

## Alcance implementado

- `ArchitectureMeshData` define una salida de geometría 3D pura, independiente de `Mesh`, `GameObject` y `Transform`.
- `ArchitectureWallMesher` proyecta paredes rectas canónicas a geometría determinista usando longitud, grosor, altura y elevación de planta.
- Las aperturas se convierten en huecos geométricos reales mediante partición determinista de la pared; puertas/ventanas siguen perteneciendo exclusivamente al modelo canónico, no a objetos visuales.
- El mesher ordena aperturas por `OpeningId`, por lo que reordenar listas no cambia el resultado.
- El mesher es read-only: no modifica vértices, paredes, aperturas ni snapshots.
- `ArchitectureRuntimePresenter` convierte la salida pura en `Mesh` y `GameObject` de Unity, con un objeto reconstruible por `WallId`.
- El presenter destruye/reconstruye su proyección y conserva el fingerprint canónico antes/después; cualquier mutación accidental dispara `LA7_RUNTIME_MUTATED_CANONICAL_ARCHITECTURE`.
- La escena no puede convertirse en fuente de verdad: la proyección runtime es descartable y reconstruible desde `ArchitectureBuilding`.
- Material y `MeshCollider` son presentación/configuración runtime opcional y no forman parte del estado arquitectónico.

## Autoridad

LA7 mantiene una dirección estricta:

`Snapshot canónico -> MeshData puro -> Mesh/GameObjects Unity`

No existe flujo inverso `Transform/GameObject -> Arquitectura`. Mover o destruir una representación visual no modifica por sí mismo el edificio. Las herramientas de edición de LA9 deberán emitir operaciones LA3 sobre el snapshot canónico y después pedir una nueva proyección.

## Self-test puro LA7

`ArchitectureMesherSelfTest` incluye 10 casos:
1. pared simple produce malla;
2. longitud/grosor/altura respetados;
3. elevación de planta aplicada;
4. apertura crea hueco;
5. apertura elevada conserva antepecho;
6. orden de aperturas no altera resultado;
7. determinismo del mesher;
8. ausencia de mutación del dominio;
9. fallo seguro ante vértice ausente;
10. fallo seguro ante pared degenerada.

Runner Unity Editor: `Bistro Builder/Living Architecture/LA7/Run Self Test`.

## Gates pendientes que requieren Unity real

- compilación C# del proyecto completo;
- ejecutar acumulativo LA1–LA7;
- confirmar **10/0** en `ArchitectureMesherSelfTest`;
- crear una proyección runtime real y verificar meshes, huecos, grosor, alturas y limpieza/rebuild en Play Mode;
- confirmar que `ArchitectureRuntimePresenter` no altera fingerprints;
- Console 0 errores/excepciones/asserts.

No se declara LA7 validado/cerrado hasta superar esos gates.
