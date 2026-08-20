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
