# Bistro Builder — roadmap vivo

**Corte documental:** 12/09/2026. `CERRADO` significa cierre dentro del alcance declarado; una regresión posterior puede abrir hardening sin invalidar todo el diseño.

| Área | Estado canónico | Nota |
|---|---|---|
| 365B/C/D Seating, snapping e indicadores | CERRADO | PASS |
| 366/366B Save/Load estructura + `game.general` | CERRADO | PASS |
| 367A–F Carta, comandas, preparación, consumo, compartidos/pases | CERRADO | PASS en su alcance |
| 2.2 Inventario/Almacén V1 | CERRADO | FEFO, lotes, recepciones, mínimos/previsión, UI |
| 2.3 Proveedores V1 | CERRADO | mercado, formatos/ofertas, pedidos, storage, compra Inteligente |
| 3 Economía y Finanzas | CERRADO | cierre formal 28/08/2026 |
| 4 Personal | CERRADO | arquitectura laboral separada de agentes operativos |
| 5 Horarios y Turnos | CERRADO | cierre posterior prevalece sobre auditorías antiguas pendientes |
| 6 Reservas | CERRADO | capacidad, disponibilidad, servicio, persistencia, UI, Queen |
| 7 Marketing / 8 Reputación / 9 Progresión | CERRADO V1 | rama consolidada 7–9 |
| 10 Clientes / 11 Comandas / 12 Cocina / 13 Camareros avanzados | CERRADO V1 | ramas específicas integradas/hardened |
| 14 Entrada, Sala y Barra | CERRADO V1 | espera virtual general; barra con espera física |
| 15 Fin de servicio/día | CERRADO V1 | rama específica |
| 16 Nueva partida/apertura | CERRADO V1 + hardening | existe V2 de apertura |
| 17 Navegación V1 | CERRADO V1 + hardening | Crowd Flow transversal continúa integración/regresiones |
| 18 Modo Edición/Construcción | CORE V1 CERRADO; UX/HARDENING ACTIVO | 84/84 reportados en núcleo; Construction Authoring/18N sigue hasta player-ready |
| 21A UI/UX definitiva | EN DESARROLLO | diseño vinculante y rama propia; cierre aún no ratificado |

## Sistemas transversales
- **BBSIS v1:** COMPLETO, VALIDADO Y CERRADO; hardening solo ante regresión real.
- **Interaction & Reservation v1:** IMPLEMENTADO, VALIDADO Y CERRADO; auditoría destructiva futura antes de vertical slice/beta.
- **Character & Interaction Animation v1:** INTEGRADO, VALIDADO Y SUBIDO; futuras ampliaciones son V2/hardening.
- **Navigation & Crowd Flow:** contratos de autoridad fijados; implementación/hardening activo sin duplicar BBSIS ni Animation.
- **BBPLFS:** desarrollo activo en `feature/bbplfs-v1`.
- **Climate & Weather:** implementación V1 existente; cierre final pendiente de validación/ratificación.
- **BB Furniture Finishes & Variants Authoring System (BBFFVAS):** IMPLEMENTACIÓN TÉCNICA V1 ACTIVA; núcleo, autoría, Acabado Automático, miniaturas, publicación, runtime binding e importación Smart Assets/Asset Studio BB validados en Unity 6000.3.19f1. Integración en producción pendiente.
- **SAVIC:** lote solicitado cerrado en `feature/savic-v1`. Auditoría 03/10/2026 05:57:01 UTC: **27 únicos, 18 publicados, 17 catálogo placeables, 0 revisiones, 0 fallidos, 0 inbox y 9 históricos sin original** conservados fuera del lote; selected14 = **14 publicados, 0 revisiones**. Armario, lámpara, barra, tres BarStool y campana integrados por contratos canónicos. Campana pasiva elevada con perfil explícito, cuerpo/colocación/BBSIS/Navigation compartidos y lifecycle transaccional; sin extracción D-003 ni techo inferido. Aceptación estricta MainCatalog/SaveDefinitionCatalog 05:53:31 UTC: área Kitchen real, paso humano inferior, claims, dos cargas SaveGame con IDs estables/objetos nuevos, slot eliminado y Console limpia hasta Editor. Barra y taburetes conservan proofs actuales; taburetes con clientes Humanoid sentados y seis cargas reales verificadas 05:10:21 UTC. Regresión final **gate 26/26, core 84/84, Navigation 22/22, barra 59/59 y BBSIS 2B 18/18 PASS**, UnityActualExitCode=0. Evidencia: `docs/SAVIC.md` 84–85, `overhead-final-canonical-verified-regression.log`, `overhead-main-catalog-strict-runtime-verified.log`, `canonical-content-inventory.json`. Este cierre certifica el lote; no afirma clasificación universal futura, recuperación de servicio ocupado ni jornada IA completa.

## Integración actual
`playtest/all-current-20260912` contiene trabajo posterior a `integration/chat-final-20260911` y, al crear esta documentación, tenía conflictos de merge sin resolver. Esta rama documental se aisló deliberadamente para no tocar esa integración.

## Actualización 24/09/2026 — Nueva partida
Diseño marfil clásico de tres opciones aprobado y maqueta interactiva incorporada en integration/master-current-20260918. Pantalla nativa integrada con tres preparaciones, creación/carga y menú de retorno; no cambia el cierre funcional del servicio de apertura. Fuente: [Nueva partida](../30_UI_UX/NEW_GAME_APPROVED.md).

## Actualización UI — 25/09/2026
Barra superior del modo normal adaptada a la preview V3 en la rama codex/topbar-responsive-approved: composición nativa compacta, iconos originales independientes y hover. 66 comprobaciones PASS en siete resoluciones (800×600 a 3840×2160, incluido ultrawide). Candidata a revisión visual del usuario antes de integrar.

## Revisión visual UI — 26/09/2026
La revisión del usuario detectó nitidez insuficiente en la build por preferencias antiguas de resolución (1280×720 ampliado a pantalla completa). Corregido el arranque y la transición a pantalla completa para solicitar píxeles nativos; retirado también el dock antiguo superpuesto. Regresión responsive: 67 comprobaciones PASS. La revisión visual final sigue pendiente antes de integrar.

## Revisión UI — 28/09/2026
Ampliado el área útil de los iconos sin aumentar la barra, mejorado el filtrado al reducirlos y reforzado el texto. Hover y pulsación responden desde su evento, con asentamiento de 90 ms. 77 comprobaciones PASS; pendiente de conformidad visual del usuario e integración.

## Revisión UI — 29/09/2026
La candidata `codex/topbar-responsive-approved` incorpora `f86f97da` de `integration/master-current-20260918` para conservar las secciones actuales de edición, las miniaturas y las correcciones de paredes. Se corrigen solapamientos, selectores sin texto, contraste de Reputación y duplicación de acciones iniciales. La integración de esta candidata continúa pendiente de conformidad visual; no se declara cerrado 21A. [Evidencia y alcance](../40_TESTING/UI_VIDEO_AUDIT_2026-09-29.md).

## Integración SAVIC + presentación — 03/10/2026

Combinación funcional verificada sobre `feature/bb-presentation-interaction-quality-v1` desde `b595fd99`, fuente SAVIC `ef1fcbb7`. Copia nueva `BB_SavicPresentation`: auditoría 16:33:56 UTC, 18 publicados/17 placeables, cero revisiones/fallidos; gate26/core84/Navigation22/barra59/BBSIS2B18 PASS y aceptaciones estrictas reales con SaveGame de barra/taburetes/campana. Empaquetado LFS y evidencia Meshy íntegros en checkout. Sigue pendiente el gate visual: prueba responsive falla por alturas distintas de barras, código heredado de la base de presentación. No se declara cerrado 21A. [Informe](../40_TESTING/SAVIC_PRESENTATION_INTEGRATION_2026-10-03.md).

La integración conserva también el avance remoto `0172c0fb` (iconos de encabezado Carta), con su gate uGUI **26/26 PASS**. Regresión funcional repetida después: gate26/core84/Navigation22/barra59/BBSIS2B18 PASS, auditoría final 16:39:39 UTC mantiene cero revisiones y fallidos. La igualdad responsive de altura continúa pendiente; no afecta al alcance del cierre funcional SAVIC ni certifica cierre visual.


## Regresiones de presentación — 04/10/2026

Cinco incidencias del vídeo corregidas y verificadas: miniaturas/ghost de mesas básicas, cuatro acabados existentes de silla accesibles, inspector de arrastre, cierre de Carta y editor con ratón, y retirada permanente de la barra provisional/taburetes antiguos. Play Mode 72 comprobaciones y Console limpia; regresión SAVIC26/core84/Navigation22+44/barra59/BBSIS2B18 PASS. La barra publicada de SAVIC vuelve a pasar colocación, leases, rutas y SaveGame real. Auditoría 04/10/2026 09:32:07 UTC: 18 publicados/17 placeables, cero revisiones/fallidos/inbox/orphans. Se conserva Carta V3 del remoto y se integra en `feature/bb-presentation-interaction-quality-v1`. Clientes sentados fuera de sillas de comedor y alturas desiguales del HUD continúan pendientes; 21A sigue abierto. [Evidencia y límites](../40_TESTING/UI_PRESENTATION_REVIEW_2026-10-04.md).

## Comedor/HUD — 04/10/2026

Resueltos los dos pendientes de la revisión del vídeo: alineación de clientes Humanoid con sillas reales del comedor y altura física común entre barras del HUD. Play Mode de comedor 50 comprobaciones (llegada, asiento, salida y reconstrucción del cliente sin inventar la bandera de Navigation); HUD 159 comprobaciones en siete resoluciones. Tres BarStool publicados reaceptados desde catálogo principal con seis cargas SaveGame, identidad/asociación estables, clientes sentados y Console limpia. No cambia Gameplay, reservas, capacidad ni raíz lógica. No se declara jornada IA completa ni ratificación comercial de 21A. [Causas y evidencia](../40_TESTING/DINING_SEATING_HUD_REVIEW_2026-10-04.md).
