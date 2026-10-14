# Editor V2 R9 + SAVIC: integración conjunta — 2026-10-14

## Entorno y resultado

PASS sobre Unity 6000.3.19f1, Windows/D3D11. Proyecto vivo: `C:\Users\mruperez\ProyectoBB\BB_EditorV2_SAVIC`, junction a `editor-v2-galeria-fiel/BistroBuilder`, rama `codex/editor-v2-galeria-viva`. MASTER continúa siendo `integration/master-current-20260918`; esto no cierra los bloques pendientes de V2.

## Evidencia funcional

- Blender real: analizar/aprobar/exportar dos revisiones, identidad y diez piezas estables, colores incluidos, rechazo de contenido alterado. `deliveries/public-flow-result.json`.
- Unity real en copia aislada: primera entrega, actualización r1→r2 y reinicio PASS; mantiene identidad, referencias, datos y acabado manual protegido.
- Colocación mediante autoridad V2, Apply, guardar r1 y cargar después de publicar r2: PASS, IDs estables, cero errores de consola. Partidas diagnósticas eliminadas; respaldo del único intento anterior interrumpido.
- Rollback: fallo tras publicación recupera bytes y fuente anterior; journal interrumpido recuperado.
- Galería conjunta: **465 checks PASS** en instalación real. Mantiene controles, filtros, tarjetas, barra, tipografía, transformaciones y ronda de guardado/carga con identidad.
- B8 selección múltiple/grupos: **75 OK, 0 fallos**, incluidos estrés de 10.000 poses, cancelación y Undo/Redo.
- Huella física compuesta: cavidad, paredes, holgura, candidato, escala y compatibilidad rectangular PASS.
- Vinculación de `sourceCatalogs`: PASS; una escena actualizada conservando el resto de V2.
- Build Windows conjunto: BUILD=Succeeded; ERRORS=0; WARNINGS=13. Ejecución GPU/capturas se registran en `GalleryVivaQA` del build.

## Delta instalado

Merge de contratos compartidos mediante ancestro común y comprobación SHA; sin sustituir escena/interfaz R9 por la escena anterior de SAVIC. SAVIC Editor, glTFast 6.19.0, manifiestos, archivo fuente y catálogo de producción incorporados. Datos diagnósticos y ajustes ficticios de la copia aislada excluidos.

Se corrige `Heading` para clonar atlas/material junto con la fuente mediante `CloneForRuntime`, conservando todo el bloque Recoleta DEMO, fallback, tamaños y métricas R9. Las seis fotos R9/meta y originales tipográficos conservan sus SHA, salvo `Inter-Regular-SDF.asset`: Unity serializa distintos valores internos `m_FeatureLookupFlags`; la comparación completa excluyendo solo ese campo confirma idénticos atlas, glifos y métricas. Se conserva el contenido vivo R9 sin restaurar la fuente antigua de HEAD. Las fotos adicionales usan el servicio canónico y respetan imágenes manuales; QA exige 1024 solo a derivados generados y verifica centrado para todas las tarjetas.

## Límites y uso

SAVIC recibe nuevas entregas **dentro de Unity con Play detenido**. El EXE contiene el catálogo publicado al compilar; necesita una nueva build para contenido posterior. Recibir no coloca automáticamente un objeto. `BB_GlassCorner_01` permanece `NEEDS_REVIEW` por falta de módulo automático para su tipo; se conserva la revisión original.

Respaldo y logs: `C:\Users\mruperez\Assets4All_LocalGate\v029\tmp\editor-savic-integration-20261013` (`installed.json`, `live-backup`, `final-validation.json`). La evidencia R9 independiente se conserva. Uso básico: [Proyecto unificado](../PROYECTO_UNIFICADO_EDITOR_V2_SAVIC.md).
