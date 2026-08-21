# Bistro Builder — Living Architecture LA10 — Sistema Universal de Feedback del Modo Edición

Estado: **IMPLEMENTADO / auditado estáticamente / pendiente validación Unity real**.

## Objetivo

LA10 convierte la información ya decidida por LA3–LA9 en una capa universal de presentación visual, cinética y sonora. No decide construcción, geometría, costes, colisiones, circulación, persistencia ni reglas de gameplay.

Principio vinculante: **feedback reacciona; arquitectura decide**.

## Implementación

- `ArchitectureEditFeedbackService`: traductor puro de propuesta, impacto y snaps a un `ArchitectureEditFeedbackFrame` declarativo.
- Estados universales: `Idle`, `Valid`, `Warning`, `Invalid`.
- Cues universales: ghost de preview, puntos/guías de snap, impactos, corrección sugerida, pulso de commit, cancelación, Undo y Redo.
- `ArchitectureEditToolController`: publica `FeedbackFrame` y `FeedbackChanged` sin transferir autoridad a la presentación.
- `ArchitectureRuntimePresenter`: admite un material de feedback reversible sobre la proyección visible sin modificar datos canónicos.
- `ArchitectureEditFeedbackPresenter`: consume frames, aplica material válido/advertencia/inválido, muestra marcadores de snap por nivel de confianza y reproduce audio mínimo opcional.
- No existen obreros/NPCs de obra. La materialización sigue siendo abstracta/procedural.

## Contrato de autoridad

1. LA9 produce preview y mantiene la sesión.
2. LA6 describe consecuencias; LA5 describe candidatos de snap.
3. LA10 únicamente traduce esos datos a señales presentacionales.
4. LA10 no puede llamar a mutaciones, commit, Save/Load ni adaptadores de gameplay.
5. Un fallo o ausencia de materiales/audio no invalida ni altera la arquitectura.

## Feedback de impacto

- `Warning` sigue siendo confirmable.
- `Blocking` o `SystemError` se representan como `Invalid` y no son confirmables porque LA9/LA6 ya lo determinan.
- Una `SuggestedDelta` se expone como `CorrectionHint`; LA10 nunca la aplica automáticamente.

## Snapping

Los candidatos LA5 se presentan sin recalcularlos ni elegir otros distintos. El frame limita los cues visibles para evitar ruido y conserva `Low/Medium/High` como confianza explícita.

## Self-test LA10

`ArchitectureEditFeedbackSelfTest` contiene 10 casos puros:

1. idle sin propuesta;
2. propuesta ready → válido;
3. propuesta rechazada → inválido;
4. warning confirmable;
5. blocking no confirmable;
6. cues posicionados de snap;
7. límite de cues de snap;
8. corrección sugerida expuesta;
9. cue transitorio de commit;
10. garantía de no mutación de inputs.

Entrada Unity Editor: `Bistro Builder/Living Architecture/LA10/Run Self Test`.

Resultado esperado: `PASS 10/10`.

## Gates pendientes de Unity real

LA10 NO se declara validado/cerrado hasta comprobar:

- compilación completa 0 errores;
- self-test LA10 `10/10`;
- acumulativo LA1–LA10 sin regresiones;
- preview visible válida/advertencia/inválida;
- snaps visibles y coherentes con LA5;
- impactos/correcciones legibles desde UI definitiva;
- Confirm/Cancel/Undo/Redo con feedback y sin dobles eventos;
- ausencia de mutación canónica al cambiar materiales o destruir/recrear presentación;
- Console: 0 Error / 0 Exception / 0 Assert;
- inspección de latencia visual para comprobar el objetivo de respuesta dentro del siguiente frame de presentación.

## Fuera de LA10

LA10 no implementa paredes, topología, restricciones, impacto, Save/Load ni input. Tampoco introduce VFX complejos que condicionen el kernel. La presentación puede evolucionar sin migrar partidas ni cambiar identidades arquitectónicas.
