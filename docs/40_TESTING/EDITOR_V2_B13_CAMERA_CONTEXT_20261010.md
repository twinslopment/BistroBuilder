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

## Fase técnica 2 — descubrimiento geométrico seguro (2026-10-10)

- Incorporada API `TryFindOccludingCandidates(cameraPosition, targetPosition, allowedCandidates, results)`. Utiliza `Physics.RaycastNonAlloc` únicamente por solicitud, no consulta cada frame y no hace `Physics.SyncTransforms` de forma global.
- Solo propone renderers **visibles y presentes en la lista explícita autorizada** si su collider, o el de uno de sus hijos, intersecta el segmento entre cámara y objetivo. No se basa en `Renderer.bounds` ni acepta un collider ascendente del edificio entero como prueba para ocultar una pared concreta.
- Los triggers, elementos por detrás del objetivo, fuera del segmento, duplicados y elementos fuera de la lista no producen candidatos. Los datos NaN, el modo servicio y un resultado de raycast saturado (128 impactos en buffer) devuelven falso y vacían la lista, evitando decisiones con resultados incompletos.
- **Ningún candidato se oculta automáticamente.** La API es de lectura; la autorización del usuario, selección de paredes reales y el efecto visual/fade todavía requieren diseño y aprobación de la interfaz. La cámara 369A/B/C sigue intacta.
- Prueba `BistroBuilderEditorV2B13CameraRuntimeSelfTest.RunFromCommandLine`, log `Logs/B13_Occlusion_Saturation02.log`, informe `EditorV2_B13_Camera_Report.txt`. PASS real Play Mode: obstáculo físico autorizado correcto, exclusion de off-axis/trigger/behind, whitelist estricta, modo normal bloqueado, NaN rechazado, saturación de 140 colliders falla con seguridad, restauración al salir y vista cenital intacta.
- El escenario de 140 colliders se genera exclusivamente en Play Mode y se destruye; no se modifican escenas, assets visuales, finanzas ni navegación.
- **Pendiente para PASS integral de B13:** selección automática/semiautomática de paredes reales con criterio probado, fade de pieza individual aprobado visualmente, pruebas en build Windows 1920×1080 y 1280×720. Por ahora B13 continúa EN DESARROLLO.
