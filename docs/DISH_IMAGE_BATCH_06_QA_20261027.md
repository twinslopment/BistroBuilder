# Bistro Builder — Validación de imágenes, lote 06 (27/10/2026)

- Rama de destino: `feature/dish-images-catalog`.
- Desarrollo y validación en worktree aislado `BB_DishImages_20261027`; no se ha modificado el worktree principal ocupado por el desarrollo de animaciones.
- Estado previo: 29 fotografías; estado nuevo: 35 fotografías (6 añadidas).
- Nuevos archivos: `croquetas_de_jamon.jpg`, `salmorejo_cordobes.jpg`, `paella_valenciana.jpg`, `gambas_al_ajillo.jpg`, `albondigas_en_salsa.jpg`, `bacalao_al_pil_pil.jpg`.
- Importador real: `BistroBuilderDishImageCatalogImporter.RebuildFromCommandLine` con Unity `6000.3.19f1` (batchmode). Resultado: `BB_DISH_IMAGES_PASS|IMAGES=35|WARNINGS=0` y terminación de Unity con código 0.
- `dishes_manifest.csv`: 35 registros, nuevos seis con estado `IMAGE_ONLY`, sin recetas ni precios inventados. `DishImageCatalog.asset`: 35 entradas con referencias a las seis fotografías.
- Los seis `.meta` tienen `textureType: 8`, `spriteMode: 1`, `enableMipMap: 0` (Sprite Single sin mipmaps).
- Claves de Carta V3: compilada y ejecutada con .NET 10 la clase real `BistroBuilderDishImageKeyUtility.cs` de la rama de asociación `feature/carta-v3-dish-photo-mapping-20261027`; las seis comparaciones de claves de nombres y archivos dieron PASS. Esta comprobación verifica compatibilidad de nombres; no supone un merge de ramas ni sustituye una prueba visual de Carta V3 integrada.
- SHA-256: 0 duplicados binarios en 35 fotografías. dHash de 64 bits: 0 coincidencias cercanas (distancia <= 7) frente a las fotografías anteriores y a las otras nuevas.
- Comprobación adicional: se conservan `dish_fabada_asturiana`, `dish_aceitunas_alinadas` y `dish_tarta_queso` como identificadores jugables existentes.
- Auditoría estática sobre manifiesto, referencias YAML del catálogo, 6 importadores Sprite, 6 estados de asociación, unicidad y nombres: `FAILURES=0`.
- Las atribuciones y licencias de fotografías externas figuran en `docs/DISH_PHOTOS_ATTRIBUTION_20261027.md`.

No se han tocado scripts de juego ni definiciones jugables en este lote. El commit solo debe incluir las seis imágenes, sus seis metas, el manifiesto, el catálogo y estos documentos.
