# BB Living Architecture — LA11 Queen Test y hardening final V1

Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

## Objetivo

LA11 no añade una nueva autoridad. Su función es demostrar que las piezas LA1–LA10 forman un único sistema reversible y coherente antes de considerar la V1 candidata a cierre.

El flujo Queen vinculante es:

**construcción → región emergente → apertura → reforma preservando intención → análisis de impacto → commit → Save/Load → reconstrucción derivada → Undo → Redo → rollback integral**.

## Queen Test puro

`ArchitectureV1QueenSelfTest` contiene 12 casos funcionales:

1. Flujo Queen completo de extremo a extremo.
2. Construcción transaccional de un recinto y aparición automática de su región.
3. Apertura con identidad estable y pertenencia real a `WallId`.
4. Reforma que preserva una apertura centrada mediante LA4.
5. Impacto previo con warning/corrección mínima sin mutar A/B.
6. Save/Load con fingerprint exacto e identidad de apertura conservada.
7. Undo/Redo exactos incluso después de reconstruir el snapshot desde persistencia.
8. Operación degenerada rechazada con rollback integral.
9. Propuesta obsoleta rechazada por fingerprint sin tocar el estado vivo.
10. Adaptador externo que intenta mutar snapshots read-only aislado y convertido en error de sistema.
11. Determinismo: mismo A + misma operación = mismo fingerprint B.
12. Barrido property-style de 64 geometrías rectangulares legales e ilegales para comprobar determinismo, regiones y pureza de propuestas/rechazos.

`ArchitectureV1QueenHardeningSelfTest` añade 2 casos de frontera de autoridad:

13. LA10 representa warning/corrección sin mutar el snapshot canónico A ni la propuesta B, y conserva la decisión de confirmabilidad recibida de LA6/LA9.
14. Tras Save/Load, LA7 reconstruye `MeshData` determinista desde el snapshot; descartar o mutar ese dato derivado no puede alterar la arquitectura y la malla puede regenerarse de nuevo desde la autoridad canónica.

El runner Queen de LA11 exige por tanto **14/14**.

## Runner acumulativo

`ArchitectureV1SelfTestSuite` agrega los self-tests puros de **LA2–LA11**, incluido el hardening final. El conjunto esperado actualmente es **109 casos**:

- LA2: 10
- LA3: 12
- LA4: 9
- LA5: 10
- LA6: 12
- LA7: 10
- LA8: 10
- LA9: 12
- LA10: 10
- LA11 Queen: 12
- LA11 hardening: 2

LA1 conserva su runner Editor histórico porque fue creado antes de la suite pura acumulativa y valida directamente kernel/DeepClone/fingerprint/invariantes.

Menús disponibles:

- `Bistro Builder/Living Architecture/LA11/Run Queen Test`
- `Bistro Builder/Living Architecture/LA11/Run Accumulated LA2-LA11`

## Autoridades auditadas

LA11 mantiene las fronteras de la V1:

- snapshot/domain es la verdad arquitectónica;
- regiones siguen siendo derivadas;
- operaciones solo mutan clones hasta commit;
- intención corrige/verifica únicamente lo declarado;
- impacto consulta mediante adaptadores aislados y no toma autoridad;
- meshes/GameObjects siguen siendo presentación runtime derivada y reconstruible;
- SaveGame universal sigue siendo la única persistencia;
- feedback sigue reaccionando a decisiones ajenas y no decide construcción;
- destruir, recolorear o regenerar presentación no puede modificar el fingerprint arquitectónico.

## Qué NO significa este hito

**LA11 implementado no equivale a V1 validada.** Este repositorio remoto no sustituye la compilación ni el runtime de Unity.

Antes de declarar Arquitectura Viva V1 cerrada deben superarse en Unity real, como mínimo:

1. Compilación completa del proyecto: **0 errores**.
2. Ejecutar LA1 self-test y confirmar PASS.
3. Ejecutar `LA11/Run Accumulated LA2-LA11` y confirmar **109/109**.
4. Ejecutar `LA11/Run Queen Test` y confirmar **14/14** de forma independiente.
5. Instalar/componer `ArchitectureStateService`, provider de persistencia, runtime presenter, herramienta LA9 y presenter de feedback LA10 en la composición efectiva del modo Edición.
6. Prueba Play Mode de dibujar recinto → crear región → apertura → reforma → preview de impacto → Confirmar/Cancelar → Undo/Redo.
7. Save real → Load real → comprobar IDs, topología, región reconstruida y geometría runtime.
8. Destruir/recrear la presentación runtime o sus meshes y comprobar que el snapshot/fingerprint no cambia y que la geometría se reconstruye correctamente.
9. Provocar una operación inválida y comprobar que no queda ningún estado parcial ni GameObject huérfano.
10. Revisión visual de openings, snaps, estados válido/warning/inválido, ghost y materialización.
11. Medir latencia de preview en operaciones V1 normales y comprobar objetivo de respuesta dentro del mismo frame cuando sea razonable.
12. Console final: **0 Error / 0 Exception / 0 Assert**.

## Estado de la V1 tras LA11

Con este hito, **LA1–LA11 quedan implementados en código y auditados estáticamente**, con hardening explícito de las fronteras LA7/LA10. Todos conservan el estado **PENDIENTE UNITY REAL** hasta superar los gates anteriores.

La V1 no se declara validada, cerrada ni lista para merge definitivo únicamente por haber completado LA11.
