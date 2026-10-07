# Editor V2 — B7 Snapping contextual — Validación 2026-10-07

## Resultado

**PASS — 41 OK / 0 fallos.**

## Cobertura validada

- providers/perfiles extensibles, sin hardcode por asset individual;
- silla ↔ mesa con plazas reales del sistema de seating;
- plazas ocupadas visibles pero nunca ofrecidas como candidato;
- contratos Floor, Wall, Surface y Ceiling;
- puertas/ventanas eligen host geométricamente compatible;
- relaciones de conjunto mediante `relationshipKey` explícita;
- socket relacional no captura targets genéricos;
- un asset no puede hacerse snap a un target de su propia jerarquía;
- target Blocked nunca se captura y una captura previa se libera si pasa a Blocked;
- histéresis Capture/Release estable;
- Alt permite ignorar completamente la sugerencia;
- snapping no muta el Transform real;
- perfil canónico resoluble desde `RestaurantPlaceableItemDefinition` / contenido SAVIC;
- Placement/BBSIS conserva autoridad final y puede rechazar una pose sugerida;
- integración con Universal Preview B6 conservada.

## Pruebas adversariales y de propiedades

- provider adversarial: candidato más cercano cambia de Available a Blocked y el núcleo conmuta al candidato seguro;
- 5.000 poses fuzz: 0 NaN/Infinity;
- 5.000 poses fuzz: 0 mutaciones del objeto;
- empates de hosts resueltos de forma determinista.

## Rendimiento

- 10.000 resolves contextuales: **71 ms** en la ejecución final registrada;
- asignaciones tras warmup: **0 bytes**.

## Repetibilidad

Tres ejecuciones finales consecutivas de B7:

- B7_A: 41/41, exit 0;
- B7_B: 41/41, exit 0;
- B7_C: 41/41, exit 0.

## Regresiones

- B6: 37/37, exit 0;
- B5: 48/48, exit 0;
- B4: 63/63, exit 0;
- B3: 28/28, exit 0;
- B2: 22/22, exit 0;
- B1: 20/20, exit 0;
- Construction Authoring: 32/32 escenarios, 341 assertions, 0 failures, exit 0.

## Gate

La regla se mantiene: **Snapping propone. Placement/BBSIS valida.**

B7 queda cerrado. B8 — Multiselección y grupos queda desbloqueado.