# Catálogo: iconos y tipografía para integrar en Editor V2 R8

Petición directa del usuario, 13/10/2026: cambiar el icono de cada opción y usar la fuente aceptada; unirlo al código del chat «Analiza el Editor V2».

12 vectores originales: libro de catálogo, marcador con corazón, consulta reciente, gabinete general, mesa de pedestal, butaca tapizada, barra con copa, horno, armario, lámpara colgante, jarrón con hojas y parasol exterior. Contorno principal de 1,8 sobre caja de 32. TMP SDF aporta brillo de borde, cara de latón con gradiente y sombra suave; las capas decorativas no interceptan clics. Los controles nativos conservan su acción y su autoridad.

Recoleta-SDF para encabezados; Inter-Regular-SDF para texto e Inter-SemiBold-SDF para las opciones. Las categorías usan 12,5 en todos sus estados y las pestañas 14. «Almacenamiento» mide 101,88 unidades en Inter SemiBold y dispone de 110. El carril pasa de 126 a 158; el contenido editorial pasa de 399 a 366. El grid conserva cuatro columnas de 86, con sus filas altas/compactas y virtualización B9.

`evidence/font-audit.json` verifica las familias reales y sus hashes. El OTF Recoleta suministrado es Regular DEMO: 243 caracteres, incluidos los acentos españoles, comparten exactamente el dibujo del marcador. Solo se retiran esas entradas de la copia TMP de ejecución y se utiliza el fallback Georgia ya previsto por `BistroBuilderTypography`, seguido de Inter para otras ausencias. El OTF y los assets originales quedan intactos. La instalación no genera ni distribuye caracteres nuevos de Recoleta.

## Integración

El chat propietario del worktree debe ejecutar el script. No abrir otra instancia de Unity mientras la build R8 esté en curso.

```powershell
& 'C:\Users\mruperez\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Tools\EditorV2CatalogStyle\integrate_catalog_style.py' --project 'C:\Users\mruperez\.codex\worktrees\editor-v2-galeria-fiel\BistroBuilder' --dry-run
```

Repetir sin `--dry-run` para aplicar. Preflight de todos los anchors, copias previas en `.tmp/catalog-options-r8-before`, escrituras atómicas por archivo, metadatos estables y operación idempotente. Solo se modifican las regiones visuales de `BistroBuilderGalleryViva.cs`, el acceso a fuentes de `BistroBuilderEditorV2ReferenceLayout.cs`, y se añaden tres módulos y la fuente vectorial propia. El título, el inspector y las dimensiones/rotación quedan a cargo del otro chat.

## Validación

Generación de 12 glifos distintos y contornos dentro de caja; medida de la etiqueta más larga; revisión de la imagen `evidence/catalog-style-preview.png`; preflight e idempotencia del parche. La preview es una prueba del arte y las fuentes, no una captura del Player.

Antes del cierre R8, ejecutar `BistroBuilderCatalogOptionStyle.Validate(expanded, out report)` en la QA nativa, recorrer las nueve categorías y las tres pestañas, verificar que no hay textos truncados y capturar el Player a 1920×1080 y 1280×720. Comprobar Recoleta/Inter reales en catálogo, inspector y barras, y la ausencia de marcador DEMO en «Catálogo», «Artículos» e «Iluminación». Añadir los resultados reales a la documentación canónica de UI y al informe de pruebas R8, regenerando índice/bundle según AGENTS.md del worktree.

## Integración confirmada — 13/10/2026

El chat «Analiza el Editor V2» confirmó la instalación en su worktree y el gate integrado de Play Mode: **344 comprobaciones PASS**, incluido este validador sobre las nueve categorías y las tres pestañas, los doce vectores distintos, etiquetas Inter completas, cuatro columnas e interacción nativa. La adopción de las fuentes reales reveló dos ajustes de espacio en las barras: los títulos del pie quedan uniformes en Recoleta 14/Normal y caja 20; la etiqueta Atardecer gana espacio manteniendo Inter 14. VenueName pasa a Regular, evitando negrita sintética. La build Windows y sus capturas GPU quedan a cargo del chat propietario y estaban en curso al confirmar este gate. Este informe no declara completada esa entrega.
