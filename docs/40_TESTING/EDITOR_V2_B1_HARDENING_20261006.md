# Editor V2 — B1 Hardening

**Fecha:** 2026-10-06
**Estado:** VALIDANDO
**Rama:** `feature/editor-v2`

## Hallazgos confirmados y corregidos

### B1-01 — Commit arquitectura/economía

Confirmado: el documento arquitectónico podía quedar publicado si Finanzas fallaba durante `Finalize`.

Corrección:

- preflight del candidato antes de efectos externos;
- publicación identificada por `operationId`;
- rollback compensatorio limitado a la misma operación y revisión;
- aborto de autorización económica si Finalize falla;
- retorno de la sesión a `ActiveDirty`;
- journal final `Aborted`;
- rechazo de rollbacks ajenos o repetidos.

Prueba aislada del código de producción:

`12 PASS / 0 FAIL`.

Casos incluidos:

- fallo inyectado en Finance Finalize;
- restauración exacta de revisión/fingerprint;
- aborto económico;
- journal Aborted;
- fallo de journal posterior a Publish sin dejar commit a medias;
- rechazo de rollback con operationId incorrecto.

### B1-02 — Doble materialización en Load

Confirmado: `ReplaceCommittedForLoad` publica `DocumentPublished` y el Save provider llamaba además directamente a `materializer.Rebuild`.

Corrección:

- Persistence deja de materializar;
- `BistroBuilderEditDocumentMaterializationBridge` queda como único propietario de la proyección runtime.

Comprobación estructural:

- llamadas directas a `materializer.Rebuild` desde Save provider: **0**;
- llamadas desde Materialization Bridge: **1**.

Se añadió `RebuildInvocationCount` al materializador para la regresión Unity.

### B1-03 — Physics.SyncTransforms en preview

Confirmado: `TryPreviewPlacement` sincronizaba física en cada invocación aunque posición y rotación fueran idénticas.

Corrección:

- detección explícita `poseChanged`;
- `SetPositionAndRotation` y `Physics.SyncTransforms` solo cuando la pose cambia;
- contador diagnóstico `PreviewPhysicsSyncCount`.

No se ha eliminado la sincronización necesaria durante pausa; solo se elimina trabajo redundante.

## Test Unity preparado

Se añadió:

`Assets/Editor/BistroBuilder/EditMode/BistroBuilderEditorV2B1HardeningSelfTest.cs`

Incluye cinco pruebas de regresión:

1. compensación de mundo si Finance Finalize falla;
2. journal post-Publish no deja commit inconsistente;
3. fallo journal pre-Publish no modifica mundo ni cobra;
4. rollback runtime exacto e idempotencia segura;
5. Load materializa exactamente una vez.

## Limitación actual de validación

Unity 6000.3.19f1 no ha podido ejecutar el self-test batch porque el servidor local de Package Manager termina con código 1 antes de la compilación.

La ejecución con `-noUpm` no es válida como aceptación: faltan UGUI, TMP, Input System y otros paquetes del proyecto.

Por esta razón:

- núcleo transaccional: **12/12 PASS**;
- invariantes de Load/preview: **PASS estructural**;
- regresión Unity completa: **PENDIENTE POR ENTORNO UPM**;
- B1 global: **VALIDANDO**, no PASS.

No existe endpoint HTTP local del editor; por tanto, `curl` no es una herramienta válida para este bloque. Crear un servidor HTTP únicamente para “probar con curl” duplicaría superficie técnica sin probar mejor las autoridades reales. Se ha usado CLI determinista y failure injection sobre el código de producción.

## Verificación adicional — 06/10/2026

Evidencia ejecutada sobre el hardening final:

- `EditorV2_B1_Hardening_navfix.log`: **20/20 OK**, sin `error CS`, sin `FAIL -`, sin excepción funcional y sin retorno 1.
- `EditorV2_B1_CoreRegression.log`: **84/84 OK**.
- `EditorV2_B1_SceneRegression.log`: **77/77 OK**.
- `EditRuntimeLifecycleSelfTestReport.txt`: **20/20 OK**.
- `EditorV2_B1_QueenRegression.log`: PASS y salida batch `return code 0`, sin errores de compilación ni excepciones.
- `EditorV2_B1_PerformanceNavFix.log`: la carga real pasa de **2 rebuilds de topología a 1**; `loadHealthEvaluations=0`.

Comparativa de Navigation durante Load:

- antes del ajuste: `loadTopologyBuilds=2`;
- después del ajuste: `loadTopologyBuilds=1`.

La prueba de rendimiento continúa marcando deuda previa de Load:

- ejecución previa: `loadMs=5622.8961`;
- ejecución posterior: `loadMs=6055.9242`;
- presupuesto histórico: < 5000 ms.

La variación temporal no se atribuye a una segunda reconstrucción de Navigation: el contador demuestra que esa duplicidad quedó eliminada. El presupuesto global de carga permanece abierto para B14 / hardening de rendimiento.

Se confirmó también en código que `BistroBuilderNavigationEditIntegration`:

- conserva una única petición pendiente;
- no reconstruye mientras SaveGame está cargando;
- recibe `OperationCompleted`;
- ejecuta `RebuildNow()` una sola vez si seguía pendiente.

### Estado

La funcionalidad específica de B1 está demostrada. El reintento de ejecutar nuevamente toda la batería sobre el árbol final se encuentra bloqueado por un fallo local reproducible de IPC del Unity Package Manager. El bloqueo ocurre antes de compilar o ejecutar tests y no constituye fallo de B1.

Por rigor, el estado formal continúa **VALIDANDO** hasta que el entorno UPM permita repetir la batería completa sobre el último árbol versionado.
