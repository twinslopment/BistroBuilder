# Editor V2 B13 — transparencia contextual de arquitectura, validación técnica

**Fecha:** 2026-10-10. **Unity:** 6000.3.19f1. **Rama:** `feature/editor-v2-b13-archwhitelist` (aislada de maestra y del trabajo UI).

## Implementado
- `BistroBuilderEditorV2WallFadeController`: componente de presentación que hace semitransparentes (opacidad 20 %) únicamente las paredes arquitectónicas realmente interceptadas entre cámara y foco en modo edición. Actualización de geometría bajo demanda cuando cambia cámara/foco o se reconstruye arquitectura; intervalo mínimo 0,15 s; transición 0,20 s.
- Candidatos procedentes exclusivamente de `BistroBuilderArchitectureRuntimeMaterializer`: lista canónica de MeshRenderers con collider propio, sin exploración global del escenario. La instancia existe en `Prototype_Restaurant.unity` (componente con GUID `0661c3f3122509a4d99a322c055a13e9`).
- Se clonan materiales de pared **solo cuando hacen falta**; el material compartido original jamás se modifica. Se conservan textura/color base, colliders, geometría, economía, Navigation y Save/Load. Desactivar, perder visibilidad o abandonar edición restaura materiales y sombras originales y libera clones.
- Fallo seguro con saturación de 128 impactos; solo se admiten shaders con parámetros `_Surface` y `_BaseColor`; shaders incompatibles quedan inalterados. No se crea otro controlador de cámara.
- `BistroBuilderEditorV2RuntimeBootstrap` instala un único componente de fade sobre la misma escena/controlador de cámara.

## Evidencia de pruebas reales
- **B13 real Play Mode PASS**, informe `EditorV2_B13_Camera_Report.txt`, log `B13_Fade_QA_02.log`. Verificado material visible/transparente, opacidad y `_Surface`, idempotencia (mismo material runtime ante repetición), restitución fuera de eje y al salir, restauración de estado original en saturación (140 colisionadores), pared canónica, whitelist, seguridad y memoria de cámara. `controllers=1`.
- **Regresiones 252/252 PASS**, repetidas tras introducir fade; archivo `B13_FinalGates_Results.txt`: B4 historial 63/63, B5 reforma 48/48, B8 grupos 59/59, B9 catálogo 42/42, B10 sustituir 40/40. **Unity exit 0** por cada ejecución; logs separados `B13_Final_B4.log`, etc.
- **Windows x64 BUILD PASS**: `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, proceso Unity exit code 0; log `B13_WindowsFinalBuild.log`. Tamaño 181 509 431 bytes, 13 warnings no bloqueantes. Ejecutable `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`, resolución nativa FullScreenWindow.
- Se conserva la comparación de B11 Navigation (37/37) y la incidencia separada de estrés de 50 NPC; **no** se declara resuelta por B13.

## Gate restante (no exagerar el PASS)
- La **ejecución de Play Mode con renderizado interactivo** y aprobación de interfaz 1920×1080 / 1280×720 siguen pendientes; las pruebas anteriores son técnicas, y el build Windows se ha compilado, no se ha usado como playtest manual de estética.
- No fusionar a `integration/master-current-20260918` hasta aceptación visual e integración B14–B16.
- La ejecución de Unity batch en worktree limpio mostró warnings no bloqueantes del resto del proyecto; no se declaran eliminados. B13 no exige modificar escenas de usuario.
