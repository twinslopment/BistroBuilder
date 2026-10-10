# Editor V2 / B13 — detección conservadora de paredes canónicas

**10/10/2026 · Unity 6000.3.19f1 · rama aislada `feature/editor-v2-b13-archwhitelist` · base `b2bdbbd4`**

## Resultado técnico reproducible

- **B13 Play Mode: PASS.** `BistroBuilderEditorV2B13CameraRuntimeSelfTest.RunFromCommandLine` en `Prototype_Restaurant.unity`; archivo `EditorV2_B13_Camera_Report.txt`; log `B13_Canonical_Walls_QA.log`.
- **B4 historial global: 63 OK / 0 fallos**, Play Mode real, `EditorV2_B4_GlobalHistory_Report.txt`; log `B13_WallSource_B4Regression.log`.
- **B5 reforma/rollback: 48 OK / 0 fallos**, Play Mode real, `EditorV2_B5_Renovation_Report.txt`; log `B13_WallSource_B5Regression.log`.
- Compilación real C#: Tundra build success, 0 errores de C#; advertencias preexistentes de UI/Progression (no relacionadas con el cambio). Total comprobaciones B4 + B5: **111/111 PASS**.

## Alcance implementado

El materializador de arquitectura mantiene un registro incremental de los **MeshRenderer de cada pared creada desde el documento canónico**. El registro se vacía en `ClearGenerated`/reconstrucción y no recorre toda la escena, ni busca por nombres durante cada consulta.

La cámara de edición consulta esa fuente mediante `TryFindOccludingArchitectureWalls`, reutilizando el filtro de rayos segmentarios y sus protecciones (modo edición, resultados incompletos, distancias inválidas, collider propio, triggers). La salida es **únicamente una propuesta** de los renderers de paredes autorizados: no oculta, no aplica transparencia, no cambia materiales, física, navegación, economía o Save/Load.

La QA construye una pared arquitectónica real con identidad canónica, la intersecta mediante una línea de visión, verifica exclusión del obstáculo de QA no autorizado, excluye la pared deshabilitada, rechaza materializador nulo y comprueba vaciado del registro. También conserva las pruebas anteriores de saturación de 140 colisionadores, cámara única 369A, memorias 369B/C y restauración visual exacta.

## Límites y gate

- No se presenta B13 como cerrado: **fade, experiencia real de selección, aprobación visual del usuario en build 1920×1080 y 1280×720** pendientes.
- No se ha fusionado nada a `feature/editor-v2` ni a `integration/master-current-20260918`; los cambios están aislados para evitar conflictos con el desarrollo paralelo de UI.
- En el **primer arranque del Library nuevo** apareció un `UnityEditor.Search.SearchDatabase.ArgumentOutOfRangeException` durante indexación del editor. No afectó a los PASS posteriores, pero debe investigarse si se exige un gate de consola universal con cero excepciones. También se observó una advertencia Unity `StackAllocator` en apagados batch; no se atribuye a estos cambios sin trazado.
