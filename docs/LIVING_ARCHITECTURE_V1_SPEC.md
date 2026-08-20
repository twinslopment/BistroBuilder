# Bistro Builder — Arquitectura Viva / BB Living Architecture

Estado: **V1 aprobada para implementación / pendiente validación Unity**.

## 1. Norte de diseño

> En Bistro Builder no editas un conjunto de paredes. Reformas un restaurante que entiende qué estás intentando conservar y qué consecuencias tendrá cada cambio.

La construcción arquitectónica debe ser reconocible como Bistro Builder por su comportamiento, no por copiar herramientas de otros simuladores.

## 2. Identidad diferencial vinculante

No se considerarán diferenciadores por sí solos: gridless, paredes curvas, medidas exactas, snapping, Undo/Redo, previews o generación procedural. Son capacidades útiles con precedentes en otros constructores.

La identidad propia se apoya en seis pilares:
1. Arquitectura que entiende relaciones, no solo meshes/XYZ.
2. Arquitectura conectada con la simulación real del restaurante.
3. Preservación explícita de la intención durante una reforma.
4. Consecuencias explicadas antes de confirmar, con correcciones mínimas sugeribles cuando existan.
5. Reformas transaccionales, reversibles y comparables como unidades completas.
6. Inteligencia invisible cuando no hace falta: una operación sencilla debe seguir siendo sencilla.

### Test de diferenciación
Toda gran feature deberá responder: **¿hace que Bistro Builder entienda mejor la intención, la arquitectura o las consecuencias del restaurante?** Si no, deberá justificarse por necesidad funcional y no venderse como innovación.

## 3. Autoridades y separación

La V1 se divide en capas estrictas:
- **Architecture Domain/Kernel**: verdad topológica y geométrica abstracta. Sin GameObjects.
- **Architecture Operations/Solver**: propone y valida transformaciones sobre snapshots.
- **Architecture Impact**: consulta sistemas existentes y describe consecuencias; no toma autoridad sobre ellos.
- **Architecture Runtime Adapter**: materializa el estado canónico en Unity.
- **Architecture Presentation**: herramientas, preview, medidas y feedback; nunca fuente de verdad.
- **SaveGame universal**: única persistencia. No crear un segundo Save.

El mesher/render no decide topología. El feedback no decide validez. Circulación, seating, economía y colocables conservan sus autoridades canónicas.

## 4. Modelo V1

### Identidad estable
- `BuildingId`
- `LevelId`
- `VertexId`
- `WallId`
- `RegionId`
- `OpeningId`
- `ArchitectureOperationId`

Los IDs no dependen de nombre, índice, posición, GameObject ni orden de creación.

### Grafo arquitectónico
Un nivel contiene vértices y tramos de pared. Una pared conecta dos vértices estables, tiene espesor/altura y puede contener aperturas. Las regiones cerradas se **derivan** de la topología; no son GameObjects creados a mano.

Una apertura pertenece a una pared mediante coordenada paramétrica/relación estable, no por posición mundial independiente.

## 5. Alcance funcional V1

### Incluido
- Un edificio y un nivel jugable, con modelo preparado para varios niveles.
- Paredes rectas gridless.
- Unión topológica L/T/X y vértices compartidos.
- Espesor y altura de pared como datos canónicos.
- Detección determinista de regiones cerradas.
- Suelo lógico derivable de regiones.
- Aperturas rectas para puertas/ventanas como parte de la pared.
- Crear, mover, dividir y eliminar pared.
- Mover vértice/esquina compartida sin generar grietas topológicas.
- Mover una pared conservando relaciones seleccionadas.
- Medidas editables.
- Snap geométrico + primeros snaps semánticos: paralelo, perpendicular, alineación, igual longitud y continuidad.
- Restricciones V1: fijar vértice, longitud, ángulo, apertura y área cuando la operación lo soporte.
- Preview de operación antes del commit.
- Operaciones atómicas con rollback completo.
- Undo/Redo semántico por operación arquitectónica.
- IDs preservados cuando conceptualmente sigue siendo el mismo elemento.
- Consultas: región que contiene punto, paredes de región, regiones separadas/conectadas por pared/apertura.
- Impact report previo sobre colocables/circulación mediante adaptadores read-only.
- Save/Load universal y migración versionada.

### Fuera de V1, pero previsto por contratos
- Paredes curvas.
- Varios pisos jugables.
- Escaleras.
- Desniveles/plataformas.
- Tejados.
- Terreno.
- Generación automática de edificios.
- Solver global que rediseñe el restaurante por el jugador.
- Costes/reformas económicas avanzadas.

## 6. Preservación de intención V1

Cada operación podrá declarar restricciones/preservaciones explícitas, por ejemplo:
- conservar apertura centrada o a distancia fija de extremo;
- conservar vértice/ancla;
- conservar longitud o ángulo;
- conservar área de una región cuando sea resoluble;
- mantener un colocable asociado si su autoridad acepta la transformación propuesta.

El solver no debe adivinar silenciosamente cambios grandes. Las inferencias de baja confianza se presentan como propuesta, nunca como mutación automática irreversible.

## 7. Análisis de impacto V1

Antes del commit, una propuesta puede devolver incidencias estructuradas:
- colocable quedaría fuera/solapado;
- acceso o circulación potencialmente bloqueado;
- seating afectado;
- región creada, eliminada, dividida o fusionada;
- apertura invalidada;
- relación preservada o imposible de preservar.

Formato conceptual: `Severity + SourceSystem + EntityId + ReasonCode + HumanMessage + OptionalSuggestedDelta`.

La V1 debe poder decir **qué rompe**. Cuando exista una corrección local determinista, puede sugerir el cambio mínimo, pero el jugador decide.

## 8. Transacción arquitectónica

Flujo vinculante:
1. Capturar snapshot base A.
2. Construir intención/operación.
3. Resolver propuesta B sin mutar A.
4. Validar invariantes topológicas.
5. Consultar impactos externos read-only.
6. Presentar preview B + consecuencias.
7. Confirmación del jugador.
8. Commit atómico A→B.
9. Notificar adaptadores/Presentation.
10. Registrar una única entrada Undo con A/B y OperationId.

Si falla cualquier gate antes del paso 8, A permanece intacto. Si falla materialización después del commit, debe existir recuperación transaccional definida; nunca medio edificio actualizado.

## 9. Invariantes V1

- No existen `WallId`, `VertexId`, `OpeningId` duplicados.
- Ninguna pared referencia vértices inexistentes.
- Longitud de pared > epsilon canónico.
- Aperturas pertenecen a una pared existente y caben dentro de su dominio útil.
- Vértices compartidos son realmente compartidos, no puntos casi coincidentes.
- Regiones son derivadas reproducibles del mismo snapshot.
- Una operación rechazada no cambia el snapshot.
- Undo restaura exactamente A; Redo restaura exactamente B.
- Save/Load conserva IDs y topología.
- GameObject/mesh nunca es fuente de verdad.

## 10. Plan de implementación

### LA1 — Kernel topológico
IDs, snapshots, vértices, paredes, aperturas, niveles, validación y DeepClone.

### LA2 — Motor de regiones
Grafo planar V1, ciclos/regiones deterministas y consultas espaciales.

### LA3 — Operaciones transaccionales
Crear/mover/dividir/eliminar pared y mover vértice; propuesta pura A→B; rollback.

### LA4 — Restricciones e intención
Medidas, ángulos, anclas, aperturas y primeras preservaciones.

### LA5 — Snap inteligente
Geométrico + paralelo/perpendicular/alineación/igualdad; confianza y candidatos sin autoridad.

### LA6 — Impacto Bistro Builder
Adaptadores read-only a colocables, seating y circulación. Reporte previo estructurado.

### LA7 — Runtime/Mesher
Materialización Unity separada del kernel, junctions y aperturas; incremental cuando sea seguro.

### LA8 — Persistencia
Sección versionada dentro de SaveGame universal, orden de fases, rollback y migración.

### LA9 — Herramienta jugable V1
Dibujar pared, seleccionar, mover, editar medida, preview, confirmar/cancelar, Undo/Redo.

### LA10 — BB Edit Feedback System
Preview translúcida, válido/inválido, guías, snapping visual, reveal/pulso/ghost y audio mínimo. No decide construcción.

### LA11 — Queen Test
Construcción → región → apertura → reforma preservando intención → impacto → Save/Load → Undo/Redo → rollback integral.

## 11. Gates de calidad

- Kernel probado sin escena Unity.
- Determinismo: mismo snapshot + operación = mismo resultado.
- Fuzz/property tests sobre topología básica.
- Ningún GameObject como identidad.
- Ninguna dependencia Domain→Presentation/Unity runtime visual.
- Operaciones rechazadas dejan hash/fingerprint del snapshot idéntico.
- Save/Load round-trip conserva fingerprint topológico.
- Queen Test reversible.
- Presupuesto de interacción: cálculo de preview debe aspirar a responder dentro del mismo frame para operaciones V1 normales; cualquier trabajo pesado debe medirse antes de optimizar.

## 12. Principio visual

No se representarán obreros/NPCs construyendo o reformando. La materialización será directa, elegante y abstracta/procedural mediante el BB Edit Feedback System.

## 13. Regla de evolución

La V1 prioriza una topología recta extremadamente robusta sobre acumular features vistosas. Paredes curvas, plantas múltiples y tejados solo entrarán cuando puedan añadirse sin romper IDs, operaciones, regiones, Save/Load ni contratos de impacto.
