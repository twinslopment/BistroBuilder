# Bistro Builder — instrucciones para agentes

Esta raíz contiene la fuente de verdad técnica y de producto del proyecto. Antes de modificar código, leer `docs/README.md` y los documentos canónicos enlazados desde allí.

## Precedencia documental
1. Decisiones vigentes de `docs/90_DECISIONS/DECISION_REGISTER.md`.
2. Documentos canónicos de `docs/00_PRODUCT`, `docs/10_ARCHITECTURE`, `docs/20_GAME_SYSTEMS` y `docs/30_UI_UX`.
3. Contratos públicos y comportamiento comprobable del código integrado.
4. Documentación histórica/legacy. Nunca prevalece sobre una decisión posterior marcada como vigente.

## Reglas obligatorias
- No reabrir sistemas cerrados salvo regresión demostrable o nueva decisión explícita.
- No duplicar autoridades entre sistemas. Respetar `AUTHORITY_MATRIX.md`.
- Mantener arquitectura modular, data-driven y sin hardcode específico por asset.
- BBSIS decide viabilidad espacial; Interaction & Reservation derechos lógicos; Navigation rutas/tráfico; Animation representación; Gameplay/IA intención y resultado.
- Modo Edición funciona fuera de servicio y no simula obreros construyendo.
- No introducir agua, extracción, gas o ventilación como simulaciones jugables salvo decisión posterior explícita.
- Los cambios deben ser no destructivos, idempotentes cuando corresponda y acompañados de validadores/autotests.
- Ningún PASS se declara solo por compilar: aplicar los gates de `docs/40_TESTING/ACCEPTANCE_AND_VALIDATION.md`.
- Al encontrar contradicciones, conservar evidencia y actualizar la fuente canónica; no borrar historia.

## Estado vivo
`docs/00_PRODUCT/ROADMAP.md` es el índice operativo de bloques y sistemas transversales. Si cambia el estado real de una rama o integración, actualizarlo en el mismo cambio.