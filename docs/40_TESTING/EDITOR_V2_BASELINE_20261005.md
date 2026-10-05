# Editor V2 — Baseline técnica B0

**Fecha de cierre:** 2026-10-05
**Estado B0:** PASS
**Proyecto:** Bistro Builder
**Unity:** 6000.3.19f1
**Escena de referencia:** `Assets/Scenes/Prototype_Restaurant.unity`

## 1. Objetivo

B0 fija una fotografía reproducible del Modo Edición antes de desarrollar Editor V2.

B0 no mejora ni corrige el editor. Su misión es permitir distinguir, en cualquier bloque posterior, entre:

- comportamiento que ya existía antes de Editor V2;
- regresión introducida por Editor V2;
- mejora real respecto a la baseline.

## 2. Punto de partida inmutable

### MASTER del proyecto

- Alias humano: **MASTER**
- Rama: `integration/master-current-20260918`
- Commit de código auditado: `3b279d717dac843dfb3294c2efa37afebc7d95d9`
- Worktree de referencia: `C:\Users\mruperez\ProyectoBB\BistroBuilder_MasterIntegration_20260918`
- Estado al iniciar B0: limpio.

### Editor V2

- Alias humano: **EDITOR V2**
- Rama: `feature/editor-v2`
- Commit inicial de B0: `ac3e3389dea689970fce56231d67272ae8064509`
- Worktree: `C:\Users\mruperez\ProyectoBB\BistroBuilder_EditorV2`

El commit inicial de Editor V2 añade únicamente documentación respecto a MASTER.

Comprobación realizada:

`git diff --name-only integration/master-current-20260918..feature/editor-v2 -- . ":(exclude)docs/**"`

Resultado: **vacío**.

Por tanto, al cerrar B0 no existe ninguna diferencia funcional de código, Assets o ProjectSettings atribuible a Editor V2.

## 3. Equipo de referencia

- SO: Windows 11 Professional, build 10.0.26200.
- CPU: Intel Core i7-1355U, 10 núcleos / 12 hilos.
- RAM física: 15,7 GB.
- GPU: Intel Iris Xe Graphics.
- Driver GPU: 32.0.101.7088.
- Memoria de adaptador reportada por Windows: 2 GB.

Las métricas de este documento son baseline de este equipo. No se extrapolan automáticamente a otro hardware.

## 4. Validación ejecutada

### 4.1 Core Self Test — PASS

Método:

`BistroBuilderEditBlock18CoreSelfTest.RunFromCommandLine`

Resultado:

- **84 OK**
- **0 fallos**
- exit code 0.

Cubre, entre otros:

- Draft/Baseline;
- comandos de arquitectura;
- Undo/Redo estructural;
- openings;
- persistencia JSON;
- superficies y zonas;
- materialización;
- commit económico;
- Finance;
- BBSIS;
- Navigation;
- Save/Load;
- validación externa;
- rollback e idempotencia de publicación.

Log local:

`Logs/EditorV2_B0_Core.log`

### 4.2 Scene Validator — PASS

Método:

`BistroBuilderEditBlock18SceneValidator.RunFromCommandLine`

Resultado:

- **77 OK**
- **0 fallos**
- **0 pendientes de producción**
- exit code 0.

Verifica las dependencias reales de escena, materializador, contratos espaciales, Navigation, Finance, SaveGame, serializador y documento comprometido.

Log local:

`Logs/EditorV2_B0_Scene.log`

### 4.3 Runtime Lifecycle — PASS

Método:

`BistroBuilderEditRuntimeLifecycleSelfTest.RunFromCommandLine`

Resultado:

- **20 OK**
- **0 fallos**
- exit code 0.

Verifica:

- disponibilidad de edición según estado de servicio;
- protección ante Draft obsoleto;
- bloqueo de commit durante servicio;
- autorización económica;
- limpieza del workspace al cargar;
- Save/Load guard durante sesión semántica;
- confirmación única.

Log local:

`Logs/EditorV2_B0_RuntimeLifecycle.log`

### 4.4 Queen Test integrada — PASS

Método:

`BistroBuilderEditBlock18QueenTest.RunFromCommandLine`

Resultado canónico:

**PASS — commit económico, BBSIS, Navigation, Save/Load, servicio y rollback verificados.**

Escena:

`Assets/Scenes/Prototype_Restaurant.unity`

Log local:

`Logs/EditorV2_B0_Queen.log`

### 4.5 Placement / estructura espacial de mobiliario — baseline válida

Durante el Play Mode de la sonda de rendimiento se verificó el estado real de la escena:

- miembros espaciales: **42 / 42 correctos**;
- sin asignar: 0;
- fuera de áreas: 0;
- capacidad incompatible: 0;
- huella fuera de área: 0;
- huellas registradas por Placement: **38**;
- colocaciones correctas: **38 / 38**;
- errores de área: 0;
- solapamientos físicos: 0;
- separación insuficiente: 0;
- reglas funcionales incumplidas: 0;
- errores de sistema: 0;
- topología de asientos: 28 sillas asociadas, 0 sin asociación.

Existe además `RestaurantPlacementTransactionSmokeTest`, pero actualmente es un MonoBehaviour de desarrollo y no un gate CLI instalado en la escena canónica. B0 no modifica producción para forzarlo. Su conversión futura a gate automatizable puede hacerse como hardening de tests, no como requisito para alterar esta baseline.

## 5. Baseline de rendimiento

### 5.1 Medición completa

Probe:

`BistroBuilderEditPerformanceProbe.RunBatch`

Resultado:

- `topologyMs`: **33,31 ms**
- `assessmentMs`: **30,57 ms**
- `seatingMs`: **4,44 ms**
- `fullHealthMs`: **37.043,37 ms**
- `deleteCallMs`: **130,41 ms**
- mayor frame tras borrado: **20,00 ms**
- `loadMs`: **5.484,66 ms**
- mayor frame de carga/estabilización: **20,00 ms**
- rebuilds de Navigation tras borrado: 1
- evaluaciones completas de salud tras borrado: 0
- rebuilds de Navigation durante carga: 2
- evaluaciones completas de salud durante carga: 0.

El diagnóstico completo de circulación tarda aproximadamente **37,0 segundos** en esta medición. No forma parte del hot path normal de borrar/cargar y debe seguir siendo bajo demanda hasta ser rediseñado u optimizado.

Artefacto local:

`Logs/EditPerformance-baseline.json`

### 5.2 Gate de rendimiento presupuestado — FAIL PREEXISTENTE

Se ejecutó el modo oficial `-bistroPerfFinal`, que aplica los límites definidos por el propio proyecto.

Resultado:

- `topologyMs`: **65,52 ms**
- `assessmentMs`: **43,23 ms**
- `seatingMs`: **4,73 ms**
- `fullHealthMs`: -1, correctamente omitido en este gate
- `deleteCallMs`: **40,47 ms**
- mayor frame tras borrado: **20,00 ms**
- `loadMs`: **5.676,06 ms**
- mayor frame de carga/estabilización: **20,00 ms**
- diagnósticos completos por borrado: 0
- diagnósticos completos por carga: 0.

Resultado del gate:

**FAIL — `Load exceeded five seconds`.**

Límite histórico: **< 5.000 ms**.
Medición actual: **5.676,06 ms**.

Esto es una incidencia de la baseline previa a Editor V2. No es una regresión de Editor V2.

Artefacto local:

`Logs/EditPerformance-final.json`

## 6. Incidencias conocidas de la baseline

### B0-KI-01 — Load por encima del presupuesto

**Severidad:** alta para rendimiento, no corrupción.

El Load tarda 5,676 s en el gate presupuestado y supera el límite de 5 s.

Debe entrar como evidencia en B1, especialmente al investigar el camino de materialización/rebuild de Load detectado en la auditoría técnica.

### B0-KI-02 — Diagnóstico completo de circulación extremadamente costoso

**Severidad:** alta si llegase a hot path; actualmente bajo demanda.

`EvaluateCirculationHealth` midió aproximadamente 37 s.

Editor V2 no debe disparar este diagnóstico en drag, borrado, colocación, Load ni por frame. B11/B14 deberán diseñar diagnóstico y rendimiento teniendo esta baseline presente.

### B0-ENV-01 — Unity Search Index

En una ejecución Play Mode apareció:

`ArgumentOutOfRangeException` en `UnityEditor.Search.SearchDatabase` durante `SearchInit.IndexationOnStartup`.

No procede de código Bistro Builder y las pruebas funcionales continuaron. Se conserva como anomalía ambiental de esta baseline.

### B0-ENV-02 — servicios externos de Unity

Se observaron de forma aislada:

- timeout de UnityConnect;
- avisos de token/licencia que posteriormente resolvieron entitlement;
- un primer lanzamiento presupuestado inválido porque Unity Package Manager no arrancó.

Ninguno de esos intentos se utilizó para declarar PASS/FAIL funcional. El gate de rendimiento definitivo se repitió correctamente y produjo el FAIL real de B0-KI-01.

## 7. Qué NO se ha cambiado

B0 no ha modificado:

- Placement;
- Construction Authoring;
- BBSIS;
- Navigation;
- Finance;
- Save/Load;
- Universal Preview;
- gameplay;
- escenas;
- ProjectSettings;
- assets de UI;
- fuentes.

Unity reserializó temporalmente algunos assets y reportes durante las pruebas. Se inspeccionaron y restauraron deliberadamente antes del cierre.

El worktree quedó limpio después de las pruebas.

## 8. Criterio de cierre B0

B0 se declara **PASS** porque:

- existe un commit de código exacto de referencia;
- MASTER estaba limpio;
- Editor V2 parte de ese código sin diferencias funcionales;
- las pruebas estructurales y de integración están registradas;
- existe baseline reproducible de rendimiento;
- los fallos preexistentes están identificados en vez de ocultados;
- existe un punto de retorno inequívoco;
- no se ha introducido comportamiento nuevo.

PASS de B0 **no significa que el editor actual sea perfecto**. Significa que sabemos exactamente desde qué estado partimos.

## 9. Próximo gate

No iniciar B1 todavía.

Orden vinculante:

**B0 PASS → cerrar Assets4All → SAVIC → validar ese gate → comenzar B1 Editor V2.**

B0-KI-01 y B0-KI-02 quedan registrados como entradas de hardening para las fases posteriores.
