# BB Living Architecture — Registro vivo de decisiones

Este archivo conserva no solo qué se decide, sino por qué. No sustituye la especificación V1.

## ADR-LA-001 — El edificio es datos, no GameObjects
**Decisión:** la fuente de verdad será un snapshot/grafo arquitectónico independiente de Unity.
**Motivo:** IDs estables, tests puros, Save/Load robusto, Undo semántico y mesher sustituible.
**Descartado:** usar componentes de pared/mesh como estado canónico.

## ADR-LA-002 — Regiones emergentes
**Decisión:** las habitaciones/regiones se derivan de la topología cerrada.
**Motivo:** dividir/fusionar espacios debe ser consecuencia natural de editar paredes.
**Descartado:** Room GameObjects mantenidos manualmente.

## ADR-LA-003 — Aperturas pertenecen a paredes
**Decisión:** puertas/ventanas arquitectónicas son aperturas parametrizadas sobre WallId.
**Motivo:** sobreviven a transformaciones y expresan la relación real.
**Descartado:** huecos visuales independientes por XYZ.

## ADR-LA-004 — Operaciones propuesta→commit
**Decisión:** toda reforma compleja se resuelve sobre copia/snapshot y solo después se confirma atómicamente.
**Motivo:** preview real, impacto previo, rollback y Undo de alto nivel.

## ADR-LA-005 — Inteligencia no autoritaria
**Decisión:** snaps, sugerencias y correcciones cercanas proponen; el jugador confirma.
**Motivo:** preservar libertad y evitar que un solver 'arregle' el diseño contra la intención.

## ADR-LA-006 — Diferenciación por comprensión
**Decisión:** gridless, curvas, medidas o snapping no se tratarán como identidad propia.
**Motivo:** existen precedentes. Bistro Builder se diferenciará por relaciones arquitectónicas + consecuencias de simulación + preservación de intención + reformas transaccionales.

## ADR-LA-007 — V1 recta antes que curva
**Decisión:** V1 implementa paredes rectas; curvas quedan previstas pero fuera de alcance.
**Motivo:** validar primero topología, regiones, IDs, operaciones, persistencia e impacto sin multiplicar casos geométricos.

## ADR-LA-008 — Un nivel jugable, modelo multinivel
**Decisión:** V1 opera un nivel pero ningún ID/contrato asumirá Z=0 o un único piso para siempre.
**Motivo:** evitar migración estructural al añadir plantas/escaleras.

## ADR-LA-009 — Impacto mediante adaptadores read-only
**Decisión:** Arquitectura pregunta a circulación/seating/colocables; no replica sus reglas.
**Motivo:** autoridad única y ausencia de divergencia entre preview y gameplay.

## ADR-LA-010 — Mesher reemplazable
**Decisión:** topología y renderer/mesher están separados.
**Motivo:** poder mejorar junctions, materiales, LOD o estilo sin migrar partidas.

## ADR-LA-011 — Feedback separado
**Decisión:** BB Edit Feedback System consume el resultado de operaciones; no decide validez ni geometría.
**Motivo:** una presentación universal reutilizable y una autoridad funcional clara.

## ADR-LA-012 — Sin obreros de construcción
**Decisión:** ninguna reforma arquitectónica se representa mediante NPCs/obreros.
**Motivo:** identidad visual directa y abstracta/procedural del modo Edición.

## Plantilla para futuras decisiones
- ID / título
- Estado: propuesta | aceptada | sustituida
- Contexto/problema
- Alternativas consideradas
- Decisión
- Razón
- Consecuencias
- Sistemas afectados
- Gate/prueba que protege la decisión
