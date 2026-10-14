# Editor V2 — miniaturas de sillas R9 Windows — 13/10/2026

## Petición y resultado

Tras aceptar el formato y el scroll R8, el usuario pide que las miniaturas de las sillas se vean profesionales y que no se fotografíen desde abajo o de lado. R9 genera fotos reales de las seis definiciones publicadas con la cámara elevada, vista frontal de tres cuartos y encuadre uniforme de la familia Asientos. El asiento, respaldo y patas quedan completos; la imagen se centra en su caja sin alterar la altura de tarjeta, etiquetas ni scroll R8.

Las fotos se renderizan en una escena aislada desde los prefabs reales con el servicio canónico `BistroBuilderCatalogThumbnailService`, evolucionado según SAVIC §22. No hay catálogo paralelo, mobiliario generado por IA ni sustitución de materiales o geometría. La cámara anterior restaba una dirección con Y positivo a los bounds, lo que colocaba la cámara debajo del objeto. El perfil de Asientos usa dirección cámara→centro con Y negativo y ajuste de los ocho vértices del bounds al frustum perspectivo. FOV 30°, margen 1,10, elevación aproximada 28° y azimut 40°. La iluminación prueba dos exposiciones de estudio y conserva la mejor según `BistroBuilderCatalogThumbnailQualityService`.

Se publican PNG de 1024 × 1024 con alpha, GUID conservado, textura sin compresión y mipmaps con filtro trilineal para reducir el parpadeo de líneas finas al mostrarlas pequeñas. Es una mejora por familia, sin casos de render hardcodeados para una silla concreta. Los iconos manuales se preservan por defecto. Esta revisión comparte la foto mejorada con las fichas existentes; no declara implementado el builder independiente de Detail Preview previsto por SAVIC.

## Regresión comprobada y reparación

La definición Roja enlazaba su `inspectorPreview` al PNG de la silla bistró de madera mientras su `catalogIcon` correspondía a la silla roja. Se conserva el asset original en `Logs/ChairPhotosR9/Before` y se repara la referencia por la política común: solo una previsualización generada de Asientos vinculada a otro artículo se reasigna a su derivado propio. Una previsualización manual conserva su autoridad.

Unity serializa dos defaults vacíos previamente ausentes (`snapProfile` y `catalogSubcategory`) al guardar esa definición antigua. El validador admite únicamente la referencia de presentación y esos defaults exactos; el resto del YAML debe ser idéntico. Prefabs, mallas, materiales y datos de economía/placement conservan su SHA256 o contenido original. Las otras cinco definiciones no se reescriben y las imágenes de otras familias permanecen idénticas.

## Validación real

- `Logs/editor_v2_r9_chair_photos_generate_3.log` y `Logs/ChairPhotosR9/generation.txt`: **56 comprobaciones PASS**, seis renders reales GPU Intel Iris Xe; todos reciben calidad **Good** del servicio canónico. Verifica resolución, GUID, importación, ficha propia, contenido canónico, prefabs/materiales y demás imágenes intactas.
- `Logs/editor_v2_r9_chair_photos_qa.log` y `Logs/GalleryViva/functional.txt`: **393 comprobaciones Play Mode PASS**, salida 0. Las 381 regresiones de filtros, lista B9, scroll, ficha/transformación, historial y persistencia R8 más doce checks de foto nativa a 1024 px y centrado en las seis tarjetas.
- `Logs/editor_v2_r9_chair_photos_build.log` y `BUILD_REPORT.txt`: Windows x64 Unity 6000.3.19f1, **BUILD=Succeeded, 0 errores, 13 advertencias**.
- `Logs/editor_v2_r9_chair_photos_player.log` y `GalleryVivaQA/chairs.txt`: **CAPTURE_COMPLETE**, todas las fichas usan su propia foto 1024 × 1024 centrada y permanece un solo inspector. Catálogo arriba y abajo, seis fichas, ventana 1280 × 720 y regreso a fullscreen físico 1920 × 1080. Las capturas 3840 × 2160 proceden de supersampling 2× y no certifican un monitor físico 4K.

Los recortes nativos del catálogo y las seis imágenes se inspeccionan visualmente, además de las comprobaciones. Se conservan PNG y definiciones anteriores para comparar y reproducir el cambio.

## Intentos y alcance

El intento 1 detectó reserialización innecesaria del asset al regenerar su PNG; ahora reimportar el mismo GUID no marca la definición dirty. El intento 2 detectó la referencia incorrecta de la ficha roja y calidad insuficiente en los acabados oscuros. Se conserva la evidencia FAIL de ambos y solo el tercer intento completo recibe PASS. La exposición de relleno elimina la clasificación de calidad insuficiente sin cambiar los materiales publicados.

No cambian los sistemas espaciales o la autoridad de transformación ya validados en R8. Los materiales y la malla publicados de la silla contemporánea conservan su aspecto y sus limitaciones de acabado; el nuevo ángulo no es un remodelado del objeto. Se conserva la advertencia preexistente de script ausente en GameSystems y la excepción del indexador Unity Search del editor, sin atribuirlas a las miniaturas. No se certifica igualdad absoluta de toda la maqueta ni cierre del Editor V2.

La entrega independiente `Builds/Windows/EditorV2_R9_20261013/` contiene EXE nativo, fotos y capturas GPU, fuentes, evidencia y manifiesto SHA256; R8 permanece disponible. El arranque interactivo R9 abre Asientos a pantalla completa, con F11 para alternar ventana, sin desactivar entrada física ni ejecutar el recorrido de QA. La rama permanece sin integrar.
