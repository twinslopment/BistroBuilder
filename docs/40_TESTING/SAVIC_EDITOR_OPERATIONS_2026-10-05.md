# SAVIC: operaciones de Editor — 05/10/2026

## Alcance comprobado

Base ba77d32a, rama de revisión `codex/presentation-review-fixes`. Destino autorizado: `feature/bb-presentation-interaction-quality-v1`. Las fuentes y autorías existentes se preservan; las fixtures se eliminan por sus identidades y hashes de prueba. Ninguna recuperación Git/stash ni nuevo registro de catálogo, reservas o navegación.

| Prueba | Evidencia local en `BB_Review/Logs` | Resultado |
|---|---|---|
| Importar GLB real desde ventana y cerrar con job Processing persistido | `savic-verified-BeginColdRestartFromCommandLine.log` | exit 0 |
| Reanudar desde pestaña Cola, publicar, reintentar y actualizar; GUID/precio/catalog singleton; duplicado antiguo y rechazo de GLB malformado | `savic-verified-PrepareRevisionColdRollbackFromCommandLine.log` | exit 0 |
| Abrir un proceso nuevo y restaurar revisión cuyo marcador final se interrumpió | `savic-verified-RecoverRevisionAfterColdRestartFromCommandLine.log` | exit 0 |
| Botón de verificación individual de barra, MainCatalog y SaveGame reales | `savic-functional-ui-RunBarFromCommandLine.log` | exit 0 |
| Botón de verificación de un taburete, cliente sentado y dos cargas reales | `savic-functional-ui-RunStoolFromCommandLine.log` | exit 0 |
| Botón de verificación de campana, cocina/paso inferior/claims y dos cargas | `savic-functional-ui-RunHoodFromCommandLine.log` | exit 0 |
| Actualizar fuente de barra; candidato → publicación → segunda prueba desde MainCatalog; identidad y GUID conservados | `savic-functional-revision-final-RunBarRevisionFromCommandLine.log` | exit 0 |
| Gate, inventario real, cola vacía y proofs actuales | `savic-verified-RunFinalVerificationFromCommandLine.log` | exit 0, gate 27/27 |

La activación de botones es mediante `NavigationSubmitEvent` nativo de UI Toolkit; no se presenta como una prueba con ratón humano ni como revisión visual del usuario. Las pruebas funcionales conservan los gates de los runners existentes y registran Console limpia hasta Editor. No se ha jugado una jornada ni se necesita para este alcance de SAVIC.

## Causa y solución

El UI carecía de acciones operativas por asset. La deduplicación normal conocía la fuente actual, pero no una revisión anterior. Actualizar bytes sin un contrato explícito creaba otra identidad o entraba en conflicto con la inmutabilidad SourceHash/SavicId del repositorio.

Las acciones nuevas delegan en las autoridades existentes. Una revisión explícita conserva la identidad y su historial, archiva los nuevos bytes por SHA y procesa normalmente. Antes de cambiar el manifiesto se guarda una transacción con copias íntegramente verificadas de la publicación anterior. Las revisiones funcionales quedan sin aceptar hasta pasar runtime real; un error o reinicio incompleto restaura la versión válida anterior. No se cambia un estado para reducir cifras.

La primera prueba funcional de revisión falló de forma segura: `savic-functional-ui-real-revision-second.log`, exit 1. La fuente nueva aún heredaba PUBLISHED, así que SourceProcessing preservaba el snapshot anterior al encontrar `BAR_RUNTIME_ACCEPTANCE_PENDING`; no quedaba un candidato continuable. La revisión empieza ahora INGESTED y la transacción explícita conserva la restauración anterior. La reprueba completa de candidato, publicación y MainCatalog pasa, sin quitar Matches.

Los jobs fallidos anteriores permanecen en Historial. Solo un trabajo posterior de la misma identidad puede superarlos en la vista activa; los fallos huérfanos y las validaciones del manifiesto/inventario conservan su visibilidad. El autotest previo de UX contaba tres revisiones aunque la fixture tenía cuatro incidencias distintas; se corrigió la expectativa y se incorporó el test al gate.

## Auditoría final aislada

`Library/BistroBuilder/SAVIC/Logs/canonical-content-inventory.json`, 05/10/2026 11:54:49 UTC:

- 18 únicos, 18 publicados y 17 placeables en catálogo.
- 0 NEEDS_REVIEW, 0 FAILED, 0 inbox, 0 huérfanos.
- Cola sin pendientes y proofs de todos los publicados funcionales actuales.
- `SavicV1ClosureGateReport.json`: 27 PASS, 0 FAIL.

Las revisiones de ensayo modifican un marcador JSON de GLB reales, manteniendo su geometría. El reinicio se prueba con procesos distintos; la ausencia del marcador final COMMITTED se inyecta en una transacción real y se declara como tal. Los slots de SaveGame diagnósticos se eliminan. Los checkpoints de servicio son desocupados, sin promesa de restaurar servicio activo.

## Uso y límites

Abrir `Tools > Bistro Builder > SAVIC > Open Control Center`. Importar una carpeta con GLB o usar la carpeta existente `ContentInbox/DropHere`. Las acciones de ficha bloquean operaciones concurrentes del mismo asset y verifican SHA antes de actuar. Guardar las escenas antes de actualizar o verificar funcionamiento.

Actualizar conserva una función compatible, identidad, GUID y valores manuales; una fuente ambigua o de otra función se rechaza conservando lo válido. Esta acción admite GLB y FBX autocontenidos, no GLTF con dependencias externas. Los valores de instalación/normalización siguen perteneciendo a los perfiles canónicos. No se añade extracción ni ventilación D-003. Assets4ALL no se conecta en este cambio.
## Cierre en el proyecto de entrega

Las dos actualizaciones remotas de Carta `efd07b6e` y `f94ec55c` se integraron en `4c43284d`, antes de probar la entrega `BB_SavicPresentation`. La comparación SHA-256 conserva los **94 archivos existentes** capturados antes del merge; no se han incluido cambios de otros trabajos en este cierre.

La primera comprobación de entrega terminó exit 1: los tres proofs de taburete conservaban hashes antiguos del prefab de cliente y del catálogo de Animation. El botón **Verificar funcionamiento** usa ahora la revalidación candidata existente para la identidad seleccionada, mantiene la evidencia de fuente/plan/perfil/autoría e inicia después una segunda comprobación estricta desde MainCatalog. Los tres taburetes pasan ambas fases con cliente Humanoid sentado, llegada/ocupación/leases, dos cargas SaveGame por fase, IDs estables/nuevas instancias, slot eliminado y Console limpia. No se quitó Matches ni se sustituyó una comprobación por un estado. Evidencia: `savic-stale-stool-ui-verified-1.log`, `-2.log`, `-3.log`, los tres exit 0 en `BB_SavicPresentation/Logs`.

La prueba de revisión archivada descubrió también que el historial de un DuplicateExact guardaba el SHA actual del propietario, en lugar de los bytes antiguos entrantes. Se conserva ahora el SourceRecord de la revisión duplicada, sin cambiar la fuente vigente. Reprueba en dos aperturas reales: `savic-revision-history-final-BeginColdRestartFromCommandLine.log` y `savic-revision-history-final-CompleteAfterColdRestartFromCommandLine.log`, exit 0, **29 comprobaciones**.

Otro gate de entrega terminó exit 1 al restaurar `BBIconCatalog.asset`: Windows rechazó copiar un archivo que Unity mantenía mapeado. La transacción común libera ahora los handles de AssetDatabase antes de restaurar archivos; el rollback de revisión lo hace antes de cualquier borrado/copia. Si falla la restauración, las copias se conservan y se informa su ubicación. El catálogo diagnóstico se reconstruyó con su instalador canónico y su diff volvió a quedar vacío. `savic-delivered-rollback-verified-RunFinalVerificationFromCommandLine.log`, exit 0: **gate 27/27**, inventario **05/10/2026 12:33:14 UTC**, **18 publicados / 17 placeables / 0 revisiones / 0 fallidos / 0 inbox / 0 huérfanos**, cola vacía y proofs actuales. Los fallos anteriores permanecen en los logs; no son ejecuciones declaradas PASS.

Los logs finales `savic-editor-final-release-verification.log` vuelven a comprobar el código final de Review y Delivery después de retirar el helper puntual del catálogo. Sus archivos `.exitcode.txt` y el inventario fresco son la evidencia de liberación. Esta sección describe exclusivamente las operaciones de Editor; el trabajo Assets4ALL de otro chat conserva sus archivos y decisiones por separado.

Liberación final confirmada: ambos procesos `savic-editor-final-release-verification.log` terminaron **exit 0**, gate **27/27** y proofs actuales. Auditoría de Delivery **05/10/2026 12:39:50 UTC**: **18 únicos,18 publicados,17 placeables,0 NEEDS_REVIEW,0 FAILED,0 inbox,0 huérfanos**, cola vacía.
