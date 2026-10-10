# Editor V2 — B13: cámara por contexto y visibilidad reversible (fase técnica 1)

**Fecha:** 2026-10-10 · **Rama:** `feature/editor-v2` · **Estado:** EN DESARROLLO; fase 1 implementada y QA de Play Mode.

## Alcance implementado

- `BistroBuilderEditorV2CameraVisibilityContext` se instala desde el bootstrap de Editor V2, sin crear otra cámara ni sustituir controladores: 369A sigue siendo el único controlador, 369B gestiona vistas y 369C la memoria por contexto.
- Suscripción a `RestaurantEditModeService.EditModeEntered/Exited` sin sondeos permanentes; `LateUpdate` solo reintenta si la inicialización de cámara todavía está pendiente. Conserva la cámara de juego y restaura su estado al volver de edición.
- Vista cenital de precisión `TryEnterPrecisionTopDown` y restauración `TryRestorePreviousFreeView` delegadas al servicio 369B, con transición suavizada existente.
- API de visibilidad `TryHideObstruction(Renderer)`, `TryRestoreObstruction(Renderer)`, `RestoreAllObstructions`: solo se altera `Renderer.enabled`, guardando el valor original (incluido false); no modifica colisionadores, mallas, navegación, Save/Load, objetos o materiales. Se restaura al salir de edición, desactivar o destruir el componente. Operación idempotente.
- El modo juego no permite ocultar; tampoco se altera la cámara si el juego arranca ya en un modo de inspección legítimo. El servicio 369C mantiene tres contextos existentes Service/Edit/Inspection.

## QA y límites

Prueba: `BistroBuilderEditorV2B13CameraRuntimeSelfTest.RunFromCommandLine` en `Prototype_Restaurant.unity`. Archivo `EditorV2_B13_Camera_Report.txt`. Log `Logs/B13_Camera_02.log`. Usa únicamente dos renderers provisionales de QA y no escribe la escena.

Prueba real: entrada/salida de edición, guardas de modo, controlador único, vista cenital, restauración del preset, restauración de renderers inicialmente visibles/invisibles, idempotencia, memoria de cámara y salida de edición con la vista cenital activa (limpieza del override de inclinación).

**No declarar B13 cerrado**: falta selección real/automatizada de paredes que obstruyen la vista, política visual de fade por pieza y no por edificio, previsualización con aprobado del usuario y verificaciones manuales a 1920×1080 y 1280×720 en build Windows. La API habilita ocultación contextual, pero todavía no elige paredes automáticamente ni aplica transparencia gradual. No debe ocultar paredes sin autorización y sin criterio de oclusión validado.

Sin modificaciones en `integration/master-current-20260918`; no se han mezclado ficheros de UI de otros agentes.
