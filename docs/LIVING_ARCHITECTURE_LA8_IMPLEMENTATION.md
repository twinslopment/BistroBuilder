# BB Living Architecture — LA8 Persistencia

Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

## Alcance implementado

- `architecture.living` se integra como sección versionada del **SaveGame universal existente**; no se crea un segundo sistema de guardado.
- `ArchitecturePersistenceState` usa DTOs serializables explícitos para `BuildingId`, `LevelId`, `VertexId`, `WallId`, `OpeningId`, coordenadas, espesores, alturas y aperturas.
- No se persisten regiones, meshes, GameObjects ni Transforms: son derivados reconstruibles desde la topología canónica.
- `ArchitecturePersistence.Capture` ordena niveles, vértices, paredes y aperturas por ID antes de serializar para mantener determinismo.
- `TryRestore` reconstruye un grafo nuevo, vuelve a pasar el gate canónico `ArchitectureValidator` y solo entrega el edificio si la topología es válida.
- La V1 de persistencia incluye migración explícita `schemaVersion 0 -> 1` para valores legacy de grosor/altura y propietario de apertura.
- Versiones futuras desconocidas se rechazan con fail-safe; nunca se interpretan silenciosamente.
- `ArchitectureStateService` mantiene la autoridad runtime en memoria sin leer identidad desde objetos de escena.
- El proveedor captura un snapshot de rollback en `PrepareForLoad`; si cualquier fase del SaveGame falla después de aplicar Arquitectura Viva, `FinalizeLoad` restaura la arquitectura previa.
- Partidas anteriores a LA8, al no contener `architecture.living`, migran a un edificio/nivel vacío nuevo sin reutilizar accidentalmente la arquitectura de la sesión abierta.

## Orden de carga

- `LoadOrder = 45`
- `PrepareOrder = 45`
- `ApplyOrder = 45`
- `FinalizeOrder = 8500`

La arquitectura se restaura después de estado general e inventario temprano, pero antes de reconciliaciones finales de presentación. LA9 deberá reconstruir su proyección runtime desde `ArchitectureStateService` tras una carga correcta.

## Self-test puro LA8

`ArchitecturePersistenceSelfTest` contiene 10 casos:
1. round-trip conserva fingerprint topológico;
2. conserva IDs persistentes;
3. captura en orden determinista;
4. conserva aperturas y propietario;
5. conserva elevación de nivel;
6. migración legacy v0 -> v1;
7. rechazo de schema futuro;
8. rechazo de topología inválida restaurada;
9. captura no muta el dominio;
10. el grafo restaurado no comparte referencias mutables con el DTO.

Runner Unity Editor: `Bistro Builder/Living Architecture/LA8/Run Self Test`.

## Gates pendientes que requieren Unity real

- compilación C# del proyecto completo;
- verificar que `BistroBuilderJsonSaveSerializer` serializa/deserializa el DTO LA8 mediante JsonUtility en el proyecto real;
- registrar/instalar `ArchitectureStateService` + `BistroBuilderLivingArchitectureSaveSectionProvider` en la composición runtime que corresponda;
- ejecutar acumulativo LA1–LA8 y confirmar **10/0** en LA8;
- guardar una arquitectura con pared + apertura, destruir/modificar estado runtime y cargarla verificando fingerprint idéntico;
- provocar una carga fallida posterior a LA8 y confirmar rollback al fingerprint anterior;
- cargar una partida anterior a LA8 y confirmar migración segura a arquitectura vacía;
- Console 0 errores/excepciones/asserts.

No se declara LA8 validado/cerrado hasta superar esos gates.
