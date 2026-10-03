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
