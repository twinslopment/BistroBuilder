# Editor V2 — revisión gráfica R2 — 11/10/2026

## Referencia y rechazo de la entrega anterior

El usuario rechaza la entrega visual de Galería Viva del 10/10: marcos anidados, inspector B8 superpuesto al de arquitectura, barras demasiado altas y herramientas inferiores fuera de la ventana. La referencia completa vinculante pasa a ser `docs/ModoEdicion/EditorV2/ReferenciasRecuperadas/Imagenes_Originales/EditorV2_Completo_Usuario_20261011.png`, de 1672 × 941. La captura rechazada se conserva junto a ella como `EditorV2_Resultado_Rechazado_20261011.png`.

Las pruebas funcionales anteriores no constituyen aprobación visual. Las builds 09/10 y 10/10 quedan superadas para esta revisión y no deben entregarse como resultado R2. Sigue vigente la exigencia del usuario de no mostrar la interfaz anterior del modo edición.

## Cambios nativos en Unity

`BistroBuilderEditorV2ReferenceLayout` unifica las coordenadas del marco superior (74), inferior (100), catálogo e inspector. Marco fino, papel interior separado y sin marcos adicionales en MODO/EDITAR/AYUDAS. Logo recuperado de la referencia completa, badge con herramientas, placa central, iluminación, cámara y opciones. Aplicar/descartar/salir se mantienen en el menú nativo de la reforma y conservan B5.

`BistroBuilderEditorV2ItemDetailsView` comparte la ficha visual entre producto y selección B8: título, favorito, vista previa de 169 px, descripción, dimensiones, rotación, acabados y variantes publicados. Principal/Conjunto solo aparece al abrir expresamente la selección múltiple; no se apila con la ficha. La píldora de selección mide 280 × 43. Las dimensiones de solo lectura se presentan como tales y no inventan capacidades.

B8 solo muestra el inspector de mobiliario para selección Furniture. Las secciones de construcción y superficies son propiedad del inspector arquitectónico. La sustitución B10 tiene prioridad sobre la ficha B8. Se verifica exactamente un inspector visible en cada caso. El contenido arquitectónico respeta el ancho del viewport.

Las acciones flotantes de Diseño inicial se ocultan mientras el modo inicial de edición está activo; guardar recuperación y validar/continuar se presentan dentro del menú V2 y llaman al flujo de apertura existente. El arranque explícito de revisión omite la pantalla de nueva partida; no crea una nueva partida para hacer capturas. La prueba de habitación es un borrador reversible de QA, fuera del restaurante, creado por la autoridad Construction y sin guardado.

Fuentes de encabezado SDF desde Times New Roman Bold de Windows, sin tracking/negrita sintética adicional de TMP; Inter para cuerpo. Recursos de referencia sin compresión ni mipmaps. Fondo decorativo de bistró para el destacado, sin sustituir los productos canónicos. Cámara y cambios reales de mobiliario conservan sus servicios anteriores; no se introduce persistencia ni catálogo de dominio duplicado.

## Vista HTML/CSS/JavaScript

`Tools/EditorV2ReferencePreview/` contiene una revisión gráfica independiente, sin bibliotecas externas. Texto, filtros, campos, botones e iconos de control son elementos reales de código. Emblemas, marcos y fotografías proceden de la imagen del usuario; el restaurante central es una ilustración de referencia. No es una escena 3D ni está conectado a una partida Unity.

La vista permite comprobar búsqueda, ubicación y precio combinados, orden, favoritos, recientes, detalles, variantes de referencia, zoom y plegado. Usa explícitamente los ocho productos visibles en la imagen como datos de revisión. No publica esos artículos en SAVIC ni asigna fotografías de mesas a prefabs ajenos. Las acciones que requieren Unity indican dónde se comprueban, sin simular cambios de partida.

Capturas completas a 1672 × 941 y 3840 × 2160 en `Tools/EditorV2ReferencePreview/evidence/`. Los textos y controles se renderizan a resolución nativa; las fotografías y bordes recuperados conservan el detalle disponible del original de 1672 × 941. Ampliarlos no crea nueva información de imagen.

## Verificación y estado

Unity 6000.3.19f1, Play Mode con eventos reales de raycaster: **106 comprobaciones PASS**, ejecución `Logs/gallery_viva_r2_qa_4.log`, informe `Logs/GalleryViva/functional.txt`. Incluye filtros y callbacks tras reciclar tarjetas, ámbito y precio, consulta coherente en destacado/relacionados, inspector y zoom, exclusión de contenido anterior, geometría de las barras, glifos, selección real de habitación, ausencia de inspectores apilados y multiselección B8.

Navegador de Codex: once comprobaciones registradas en `evidence/browser-checks.json`, con clics y campos nativos. Se validan búsqueda con acentos, precio/ámbito, orden, rechazo del intervalo inválido, favoritos, recientes, zoom, estado vacío, plegado, categoría Exterior y geometría 4K. Sin errores ni warnings de consola observados.

La batería funcional pasa; la igualdad visual por píxel y la aceptación del usuario **siguen abiertas**. La compilación y las capturas GPU definitivas se registran al concluir la ejecución. No se sustituye ese resultado por el PASS del editor headless.

## Límites materiales que siguen pendientes

La escena real mantiene los modelos de prototipo, mesas verdes y entorno gris. El catálogo real mantiene nueve definiciones: dos mesas de prueba, seis sillas y una planta. Las ocho mesas fotografiadas, sus materiales y el restaurante de la referencia no están publicados como contenido jugable. La vista HTML permite revisar esas imágenes, pero no equivale a incorporarlas como modelos 3D.

Hay diferencias de acabado en algunos ornamentos, fondo del destacado y espaciados. Esta intervención corrige la composición, nitidez de controles y duplicidades; no permite afirmar «ninguna diferencia» en el editor completo ni declarar finalizados Halo de Huella/Proyector Inteligente. El trabajo permanece aislado en `codex/editor-v2-galeria-viva`, sin integración ni subida a la rama principal.

El fondo generado y su descripción se registran en `Tools/EditorV2ReferencePreview/README.md`; se conserva el recurso dentro del proyecto. La copia de código de una entrega es un snapshot para revisar, no debe aplicarse sobre el checkout del editor antiguo: el proyecto Unity completo está en el worktree de esta rama.

### Resultado final de la ejecución R2

Windows x64: **Build Succeeded, 0 errores, 13 advertencias**, Unity 6000.3.19f1, UTC `2026-10-11T20:24:08.3333263Z`. Player GPU Intel Iris Xe: **salida 0, CAPTURE_COMPLETE**. Capturas completas a 1672 × 941, 1280 × 720 y selección de superficie a 1908 × 1042. `WorldSelection_4K.png`: 3840 × 2160 mediante supersampling 2× de un viewport real de 1920 × 1080; no se afirma una pantalla física 4K.

El registro confirma cero inspectores al consultar el catálogo sin abrir ficha y exactamente uno en ficha, construcción, superficies, habitación seleccionada y multiselección B8. No hubo capturas negras ni superposición de inspectores en esta ejecución. No se observaron excepciones en el log del Player. Las pruebas del Editor registraron una excepción de UnityEditor.Search.SearchDatabase al inicializar su índice; no forma parte de la prueba gráfica del Player ni se presenta como un arranque del Editor sin avisos.

Entrega separada: `Builds/Windows/EditorV2_R2_20261011/` y `BistroBuilder_EditorV2_R2_20261011.zip`, con lanzadores R2, referencia HTML, capturas actuales, informes y snapshot de código. Se excluyen las capturas antiguas de 1920 del 10/10, en lugar de mezclarlas con el resultado nuevo. La aceptación gráfica y las diferencias materiales descritas arriba siguen abiertas.
