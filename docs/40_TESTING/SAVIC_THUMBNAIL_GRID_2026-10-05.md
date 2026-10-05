# SAVIC · cuadrícula de miniaturas — 05/10/2026

## Función implementada

Control Center → **Inventario** abre por defecto una cuadrícula compacta. **Miniaturas / Lista** permite alternar vista conservando la identidad seleccionada. Las tarjetas usan `preview.catalog` y, cuando falta ese artefacto, `preview.large`; mantienen proporciones con ScaleToFit. Incluyen nombre, tipo/estado, tooltip completo, borde de selección y navegación por flechas. Pulsar una tarjeta abre su ficha y preview grande.

Se conserva el inventario canónico, sus filtros y las autoridades de publicación. Un filtro vacío limpia la ficha; la selección válida se mantiene al refrescar/filtrar. Las filas se virtualizan con ListView FixedHeight; unbind elimina imagen, título y referencia anterior. Los PNG pertenecen a AssetDatabase. Un asset sin preview muestra **Sin miniatura**, sin asignarle una imagen ajena. No se genera nueva geometría ni se altera publicación/materiales.

## Prueba nativa con curl

`SavicThumbnailGridProbe.StartFromCommandLine` abre la ventana SAVIC real y una fixture de tamaño variable del mismo componente nativo. El servicio temporal se enlaza exclusivamente a loopback 127.0.0.1:19057; no arranca en uso normal. Finaliza con /finish y tiene timeout. El layout se mide mediante worldBound real en UI Toolkit, no mediante CSS ni una simulación de Unity.

| Ancho de fixture | Columnas reales | Assets | Tarjetas realizadas, incluido overscan |
|---:|---:|---:|---:|
| 132 px | 1 | 18 | 5 |
| 330 px | 2 | 18 | 10 |
| 460 px | 3 | 18 | 15 |
| 740 px | 5 | 18 | 18 |

**16 solicitudes curl aprobadas**: salud; cuatro layouts; búsqueda con resultados y búsqueda vacía; selección por identidad; seis imágenes PNG reales; imagen inexistente HTTP404; cierre con comprobaciones nativas. Los layouts prueban tamaños finitos, ausencia de solapes y ScaleToFit. La selección/ficha y el cambio Lista/Miniaturas se prueban aparte en la ventana SAVIC real mediante NavigationSubmitEvent. También se prueba búsqueda real, fallback, borrado de datos reciclados y contrato de virtualización para 10.000 entradas sintéticas explícitas, sin crear assets canónicos.

Evidencia en `BB_SavicPresentation/Logs/savic-thumbnail-grid-curl-native-second.log`, **exit0**; respuestas y resultado `Library/BistroBuilder/SAVIC/Logs/ThumbnailGrid`: `result.json` statusPASS, curlRequests16, consoleErrors0. Los PNG de ejemplo se descargaron por curl y se inspeccionaron visualmente. Una galería HTML independiente muestra seis imágenes reales; es una presentación de ejemplos, no una captura de la ventana Unity.

El primer intento no pasó compilación: referencias adelantadas a los botones de vista y uso de una propiedad de inventario inexistente. Se corrigieron a botones previamente declarados y TryLoadPersisted; el log inicial se conserva como fallo, no se declara PASS.

## Regresión final

La comprobación de miniaturas se añade al autotest de UX existente: imagen canónica real, rebind de imagen válida a imagen ausente, limpieza, selección preferida y filtrada, vacío y 10.000 entradas virtualizadas. `savic-thumbnail-grid-final-regression.log`, **exit0**, **ClosureGate27/27 PASS** y proofs funcionales actuales. Auditoría **05/10/2026 15:47:36 UTC**: **18 únicos,18 publicados,17 placeables,0 NEEDS_REVIEW,0 FAILED,0 inbox,0 huérfanos**, cola vacía. No se han repetido las pruebas de jornada ni se afirma clasificación universal.