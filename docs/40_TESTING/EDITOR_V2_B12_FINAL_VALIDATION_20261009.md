# Editor V2 B12 — aceptación técnica y repetición independiente

**Fecha:** 2026-10-09. **Rama aislada:** `feature/editor-v2`. **Unity:** 6000.3.19f1.

## Cambios y garantía de integridad

- Creación transaccional de plantillas (1–64 miembros) con catálogo, geometría real, validación de área y espacios operativos, finanzas, Undo/Redo y restauración de fallos.
- Optimización segura de `NotifyBatchRelationshipRebuild`: para lotes previamente validados se omite la reevaluación espacial duplicada, pero no las notificaciones funcionales.
- Resolución de asientos por `RestaurantSeatRegistry` una vez por mesa y por consulta semántica, eliminando las búsquedas globales por cada plaza; desempate conservado por ID de instancia y fallback compatible.
- B8: se conserva la búsqueda adversarial de parejas en el restaurante real, con rollback obligatorio en cada intento fallido. Para evitar que un comedor lleno invalide una prueba positiva, hay una segunda prueba con área de comedor temporal y dos muebles canónicos: duplicar, verificar identificadores, geometría, historial, Undo/Redo, volver al inventario inicial. La prueba positiva se ejecuta aun cuando la escena principal encuentre un hueco.
- Runner nuevo `Tools/BistroBuilder/RunEditorV2B12FinalQA.ps1`: Unity se invoca **secuencialmente**, se espera el proceso principal y se verifica **exit code + informe de esta ejecución**, en lugar de confiar en archivos antiguos. BBSIS se valida por su log nuevo porque su reporte histórico no se actualiza.

## Pruebas reales terminadas

- **B12 Runtime:** PASS con IDs canónicos, catálogo, biblioteca JSON versionada, rechazo de conflictos espaciales internos, rechazo 65 miembros, Undo/Redo, restauración del mundo y fallo financiero inducido en el tercer cargo (2 cargos compensados).
- **B12 cinco piezas + Save/Load real:** PASS; mesa y cuatro sillas asociadas, JSON, slot QA 970–989, persistencia del mundo y restauración de exactamente las cuatro asociaciones y sus identidades. Slot temporal eliminado.
- **B12 válido 64:** 3/3 PASS en **dos series independientes** en la misma escena de prueba. En la última serie: creación 240 / 262 / 354 ms y reconstrucción 283 / 271 / 312 ms, respectivamente; 64 objetos con identificadores únicos, 1 Undo agrupado y Undo/Redo/Undo sin residuos en registro.
- **BBSIS Phase 2C Play Mode:** PASS, exit 0, log actual `Logs/B12_SEQ_BBSIS_PHASE2C.log`.
- **B8 regresión intermitente corregida:** tres ejecuciones consecutivas de la prueba mejorada PASS (en una prueba se midieron 60/60 verificaciones, otras cuentan según reintentos adversariales). Log final `Logs/B12_B8_Fixture_Retest04.log`.
- **Build Windows x64:** PASS, `Logs/B12_SEQ_WINDOWS_BUILD.log`, `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, exit 0, 13 warnings no bloqueantes; binario `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

## Interpretación correcta de métricas y límites

- Los tiempos de 64 corresponden a una **zona QA temporal amplia**, cuadrícula 8×8 de mesas a 9 m, no a un restaurante saturado con decenas de agentes. Son valores medidos, no un contrato máximo de latencia.
- `directAllocBytes=0` / `reloadAllocBytes=0` procede de una consulta de asignaciones del hilo; **no prueba cero asignaciones de Unity**. Se mantiene la necesidad de profiler para garantías de memoria.
- La mesa del fixture tiene coste de catálogo 0 céntimos. El rollback de pago se verifica con un gate de QA que rechaza el tercer cargo: no es una prueba real con caja insuficiente configurada en partida.
- B12 **no está cerrado visualmente**: falta integrar su interfaz de gestión y previsualización de plantillas según Galería Viva y aprobarla en una build responsive (1920×1080 y 1280×720). Ningún cambio de arte/UI de otros agentes se ha incluido como parte del núcleo.
- No se modifica `integration/master-current-20260918` ni se guardan cambios sobre escenas de usuario.

## Evidencia y ejecución

- `Logs/B12_Final_Sequential_Suite.txt`: QA secuencial B12, cinco miembros, 64×3 y BBSIS. Históricamente se detectó un false negative por leer un informe BBSIS del 7/10, y fue corregido para comprobar el log del mismo proceso.
- `Logs/B12_SEQ_B12_RUNTIME.log`, `Logs/B12_SEQ_B12_FIVESAVELOAD.log`, `Logs/B12_SEQ_B12_VALID64_A.log`, `..._B.log`, `..._C.log`.
- `EditorV2_B12_Runtime_Report.txt`, `EditorV2_B12_FiveSaveLoad_Report.txt`, `EditorV2_B12_Valid64_Report.txt`.
- Los runners Play Mode **no** usan `-quit`; los runners Edit Mode y build sí.
