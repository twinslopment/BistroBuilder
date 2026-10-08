# Editor V2 — B9 Catálogo escalable

**Fecha:** 2026-10-08
**Rama:** `feature/editor-v2`
**Estado:** PASS — B9 validado en Unity y build Windows 64 bits

## Propósito

Sustituir la creación masiva de GameObjects UI por virtualización real sin inventar otro
catálogo ni cambiar las autoridades de SAVIC, Placement, Finance o Save/Load. El diseño
gráfico final de Editor V2 sigue sujeto al documento UI/UX y a aprobación del usuario.

## Implementación

- Índice canónico de artículos válidos por ItemId y búsqueda multi-término, normalizada
  para mayúsculas y diacríticos, filtrada por categoría, subcategoría, ámbito,
  favoritos y recientes, con orden estable y modos de precio y nombre.
- Consulta por ItemId del servicio original cambiada a diccionario O(1);
  importante porque el inspector de colocación la utiliza cada frame.
- ScrollRect con altura virtual y tarjetas UI en pool limitado a viewport + overscan:
  no se instancian miles de tarjetas ni se fuerzan LayoutGroups sobre resultados ocultos.
- Rebinding correcto al reciclar cards, botón de selección y favorito enlazados con
  definición **actual**, no con la definición capturada anteriormente en una lambda.
- Tamaños del grid adaptativos: dos columnas en viewport suficiente; una columna
  con viewport compacto, sin recreación masiva.
- Sprites de catálogo se asignan solo a las tarjetas vinculadas/visibles; memoria
  de referencias de thumbnails cacheada con límite de 64 y invalidación en republicación.
- Cambios publicados por CatalogChanged reconstruyen índice e invalidan solo enlaces
  visuales relevantes: nunca reconstruyen miles de GameObjects.
- Variantes por FurnitureFinishProfile se indexan sin ocultar ItemIds independientes;
  la futura forma visual de elegir variantes depende de la aprobación UI/UX.

## Límite explícito de las miniaturas

Las definiciones actuales exponen Sprite serializado en `CatalogIcon`. B9 aplaza la
**vinculación visual** al entrar en viewport y limita su caché, pero no descarga ni
transmite texturas de forma asíncrona desde disco. Un streaming real de texturas
requiere que SAVIC publique un proveedor/ruta de miniaturas apta para carga diferida.
No se inventa tal contrato ni se ocultan artículos por ello.

## Pruebas realizadas

Selftest: `BistroBuilderEditorV2B9ScalableCatalogSelfTest.RunFromCommandLine`.

- Dataset sintético 10.000 ScriptableObject de catálogo con ItemIds estables.
- Búsqueda multi-término, sin acentos y con mayúsculas; ámbitos y categorías.
- Favoritos, recientes, filtros combinados y tres tipos de ordenación.
- Índice reconstruido con entradas repetidas, retiradas y reincorporaciones.
- 100 búsquedas alternas en 10.000 registros.
- ScrollRect Unity real con 10.000 items, posición final, reciclaje y callback exacto.
- Dos y una columnas mediante anchura del viewport; recuento pool acotado.
- Tarjetas bloqueadas durante operación de colocación.
- 10.000 frames estables sin trabajo extra ni GC.
- Escena real `Assets/Scenes/Prototype_Restaurant.unity`: catálogo y servicio canónicos,
  inicialización del virtualizador, filtros sin resultados y restauración sin rebuild.

Resultados de la batería ampliada B9: **42 OK / 0 fallos**.

Métricas orientativas (varían según carga del PC):
- Indexación 10.000: ~402 ms.
- Búsqueda 10.000: ~11 ms.
- 100 búsquedas 10.000: ~564 ms.
- 1.000 scroll updates: ~7 ms en el escenario sintético.
- 10.000 frames sin scroll: ~2–5 ms y 0 bytes GC tras calentamiento.
- 300 desplazamientos del panel real con 10.000 fichas persistentes de prueba y ambos skins: ~17,9 ms en total, 5.346 rebinds, pool estable.
- La prueba de skins descarta expresamente el falso positivo del viewport 0×0 en batchmode y exige >=100 rebinds.

Estas cifras miden pruebas automatizadas del Editor y escenarios sintéticos;
no equivalen a FPS de una build Windows.

## Regresión e integración

**B9:** 42/42 en tres ejecuciones consecutivas después de ampliar el test al reciclaje real de tarjetas.
**B8:** 74/74 y 59/59 en las dos últimas ejecuciones. Se reforzó únicamente el fixture de prueba de duplicación para no depender de la primera pareja de colocables de una escena poblada. Cada destino rechazado verifica rollback sin estado parcial.
**Regresión:** B1 20/20; B2 22/22; B3 28/28; B4 63/63; B5 48/48; B6 37/37; B7 41/41; Core 84/84; Lifecycle 20/20; Scene Validator 77/77; Queen Test PASS. Todos finalizaron con exit code 0.

**Build de Windows 64 bits:** PASS. `BistroBuilderPlaytestBuild.BuildWindowsPlaytestFromCommandLine` devuelve exit 0 y `BB_PLAYTEST_BUILD_PASS` en `Logs/B9_WindowsBuild.log`. Artefacto generado: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe` (contenido total reportado por Unity: 181.337.959 bytes, 13 warnings). El ejecutable se inició en Windows de manera headless durante 12 segundos sin excepciones registradas y después se cerró de forma controlada. Esto valida arranque, **no** FPS gráficos ni navegación manual.

La integración visual definitiva de Editor V2 sigue pendiente de aprobación UI/UX. No se han dado por medidos FPS del catálogo en una sesión de juego renderizada, ni se ha declarado streaming asincrónico de miniaturas sin contrato SAVIC.

## Entrega

Los archivos ajenos al Editor V2 y las aprobaciones gráficas del chat UI/UX
no forman parte del commit B9.
