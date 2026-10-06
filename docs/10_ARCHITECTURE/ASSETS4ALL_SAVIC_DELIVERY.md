# Assets4ALL → SAVIC: entrega versionada V1

Contrato implementado en la rama de trabajo `codex/assets4all-savic-exchange`. Assets4ALL conserva la autoridad sobre SOURCE, WORK y la segmentación; SAVIC conserva clasificación, catálogo, autoría jugable, BBSIS y SaveGame.

## Guía básica de usuario

Para seguir el procedimiento sin conocimientos de programación, consultar la [guía de Assets4ALL y SAVIC](../GUIA_USUARIO_ASSETS4ALL_Y_SAVIC.md). Contiene ilustraciones aproximadas, no capturas reales. Los PDF separados están en [manual de Assets4ALL](../manuales/Manual_Usuario_Assets4ALL.pdf) y [manual de SAVIC](../manuales/Manual_Usuario_SAVIC.pdf).

Para vincular por primera vez una corrección a un artículo existente, exportar inicialmente fuera de `ContentInbox/Deliveries` y seleccionar el artículo mediante Import Delivery antes de activar la detección automática. Una vez vinculado, las siguientes revisiones del mismo UUID pueden usar la carpeta de entregas automática.

## Flujo de uso

1. En Blender, crear la sesión, analizar, esperar a PartGraph READY y aprobar WORK.
2. Pulsar **ENTREGAR A SAVIC** y elegir una carpeta. Para detección automática, usar `ContentInbox/Deliveries` del proyecto Unity.
3. En Unity, abrir **Tools → Bistro Builder → SAVIC → Assets4ALL → Import Delivery** y seleccionar la carpeta de revisión.
4. Para corregir un artículo ya publicado, vincularlo explícitamente en su primera entrega. Después, las revisiones del mismo UUID actualizan ese artículo automáticamente.
5. Guardar el `.blend`: contiene el UUID y el historial de revisiones. Una nueva importación independiente de otro GLB no infiere la identidad del artículo anterior.

## Archivos y verificaciones

La carpeta final `<assetUuid>_r<revision>` se publica mediante rename tras completar `model.glb`, `asset4all.json`, `partgraph.json` y `delivery.json`. Las carpetas `.pending-*` quedan fuera de la ingesta.

`delivery.json` usa `schemaId=assets4all.savic-delivery`, versión 1, UUID independiente del SHA del modelo, revisión secuencial, fingerprint padre, SHA-256 de cada archivo, hash físico de WORK, generación y revisión de PartGraph, y correspondencia `PartKey → nodeName/sourceUid/triangleCount`. El fingerprint es SHA-256 del objeto JSON canónico con las claves `asset4all.json`, `model.glb`, `partgraph.json` y sus hashes.

Blender exporta clones de las caras exactas de cada pieza, conserva materiales/UV de BMesh, verifica cobertura completa y disjunta y compara los triángulos emitidos. SOURCE y WORK físicos se verifican antes y después. Las normales de exportación se recalculan; no se certifica la preservación de normales personalizadas. Los extras globales de escena se excluyen del GLB para que repetir una entrega no incorpore su propio historial.

SAVIC verifica hashes, contrato, PartGraph, nodos y triángulos del GLB y del modelo importado. Archiva el paquete y la fuente. La evidencia Assets4ALL tiene un registro propio; no se presenta como metadatos Meshy. La membresía se conserva, mientras los roles inciertos siguen como hipótesis y no crean contratos jugables. Una familia sin suficiente evidencia puede quedar en revisión.

## Actualizaciones y protección del trabajo

La publicación de cada revisión pasa por los módulos y gates existentes. Un candidato usa repositorio separado y conserva `savicId`, `canonicalContentId`, GUID de catálogo y fuentes anteriores. Solo un candidato publicado puede sustituir el hash de la fuente canónica. La sustitución consume la API canónica `ReplaceSourceRevision` y su historial `sourceRevisions`, conservando la actualización de GLB/FBX que ya estaba en desarrollo. `assets4AllRevisions` retiene la evidencia externa de cada paquete. El GLB de una revisión histórica vuelve a la misma identidad al pasar por la ingesta convencional.

Se conservan nombre, descripción, precio, condiciones económicas y materiales editados sobre nodos Assets4ALL. La pérdida de una pieza con material protegido bloquea la actualización; V1 solicita resolver su linaje. Las miniaturas de artículos genéricos se generan con el material protegido aplicado. No se afirma implementación completa de BBFFVAS ni reconocimiento universal de familias.

Un diario durable en `SAVIC/Transactions/Assets4All` protege los archivos publicados y catálogos. Ante fallo se restauran los bytes anteriores; al arrancar se recuperan transacciones incompletas. Los recibos se escriben en `SAVIC/Receipts/Assets4All`; distinguen publicación, repetición sin cambios y revisión MODEL/GAME. La detección automática respeta la pausa de SAVIC y no trabaja durante Play Mode o compilación.

## Cómo llega al repositorio y al juego distribuido

El recorrido es **Assets4ALL → entrega local → SAVIC → catálogo del proyecto Unity → Git → integración → build → distribución**. El estado `PUBLISHED` significa publicación en el proyecto local; no implica un push a GitHub ni una actualización del ejecutable de los jugadores.

### Proyecto y carpetas de esta instalación

- Proyecto Unity conectado: `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation`.
- Destino de exportación automática desde Blender: `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation\ContentInbox\Deliveries`.
- Archivo del paquete y fuentes: `ContentSource/Assets4All` y `ContentSource/SHA256` dentro del proyecto.
- Manifiestos canónicos: `SAVIC/Manifests`.
- Artefactos publicados: `Assets/Generated/BistroBuilder/SAVIC/Published`, con sus dependencias y archivos `.meta`.
- Catálogo principal de colocables: `Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset`.
- Remoto Git configurado: [twinslopment/BistroBuilder](https://github.com/twinslopment/BistroBuilder).

Con Unity abierto, fuera de Play Mode, sin compilación en curso y con SAVIC sin pausar, la carpeta de entregas se procesa automáticamente. Para la primera vinculación a un artículo existente, usar **Tools → Bistro Builder → SAVIC → Assets4ALL → Import Delivery**, seleccionar la subcarpeta `<assetUuid>_r<revision>` y elegir el artículo. Las siguientes revisiones conservan esa vinculación. Guardar el `.blend` conserva UUID e historial.

`PUBLISHED` permite encontrar el artículo en el catálogo de Modo Edición. `NEEDS_REVIEW` requiere resolver la causa indicada antes de publicarlo; aprobar WORK en Assets4ALL no certifica por sí solo todos los contratos jugables de SAVIC.

### Entrega por Git y generación del ejecutable

1. Revisar y hacer commit de los archivos correspondientes a la entrega: fuentes archivadas, manifiestos, artefactos publicados, dependencias, `.meta` y modificaciones de los catálogos afectados. Acotar el commit al cambio validado y conservar los GUID; no incluir en bloque trabajos ajenos ni carpetas temporales.
2. Hacer push de la rama al remoto del juego. `.gitattributes` configura Git LFS para los GLB de `ContentSource` y de `Assets/Generated/BistroBuilder/SAVIC/SourceMirror`; la subida y la descarga requieren también sus objetos LFS.
3. Integrar el cambio validado en la rama acumulativa vigente del juego, siguiendo la [política de integración](../00_PRODUCT/BRANCH_AUDIT_20260918.md). Un push publica la rama; no modifica por sí solo otras ramas ni otros checkouts locales.
4. Abrir el proyecto integrado y generar la build con **Tools → Bistro Builder → Build → Windows Playtest**. El [script de build](../../Assets/Editor/BistroBuilder/Build/BistroBuilderPlaytestBuild.cs) genera `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe` respecto a la raíz del proyecto desde el que se ejecuta.
5. Distribuir la carpeta completa de la build, incluido el ejecutable y sus datos, por el canal elegido. Un `.exe` generado anteriormente conserva el contenido de su build y necesita una nueva versión para incorporar los cambios.

El puente instalado automatiza la entrega y publicación local. No ejecuta commit, push, integración de ramas, generación de builds ni subida a una plataforma de distribución. `Builds/` está excluido de Git; versionar el proyecto y distribuir su build son operaciones distintas.

## Cómo aplicar cambios de código de SAVIC a esta conexión

El receptor pertenece al propio proyecto Unity, en `Assets/Editor/BistroBuilder/SAVIC`. Assets4ALL entrega un paquete de archivos y SAVIC lo interpreta con el código presente en ese checkout. La conexión no contiene una copia independiente de SAVIC dentro del complemento Blender.

| Cambio | Qué actualizar |
|---|---|
| Validación, clasificación o publicación interna de SAVIC, manteniendo sus interfaces y el formato de entrega | Integrar el código en el proyecto Unity conectado. Unity recompila y los siguientes procesados usan la versión nueva. El complemento Assets4ALL puede mantenerse. |
| Interfaces internas utilizadas por el puente, por ejemplo repositorio, historial de fuentes o publicación | Adaptar también los consumidores del puente y comprobar una entrega real y una actualización del mismo artículo. |
| Archivos, campos, unidades o estructura que Assets4ALL debe enviar | Actualizar exportador y receptor de forma coordinada; generar el paquete actualizado del complemento e instalarlo en Blender. Mantener compatibilidad con entregas anteriores o implementar una migración explícita. |
| Comportamiento o datos que deben llegar a jugadores de una build existente | Integrar, validar y generar/distribuir una nueva build. El código situado en `Assets/Editor` se ejecuta en el editor; sus cambios de publicación llegan al jugador mediante los artefactos generados incluidos en la build. |

### Puntos de mantenimiento

- [SavicAssets4AllService.cs](../../Assets/Editor/BistroBuilder/SAVIC/Intake/SavicAssets4AllService.cs): verificación del paquete, importación, revisiones, conservación de ajustes, recuperación y detección de entregas.
- [SavicAssets4AllModels.cs](../../Assets/Editor/BistroBuilder/SAVIC/Intake/SavicAssets4AllModels.cs): modelos de datos del intercambio.
- [SavicAssets4AllEvidence.cs](../../Assets/Editor/BistroBuilder/SAVIC/Intake/SavicAssets4AllEvidence.cs): evidencia de piezas y comprobación del modelo importado.
- [SavicManifestRepository.cs](../../Assets/Editor/BistroBuilder/SAVIC/Core/SavicManifestRepository.cs): `CommitAssets4AllRevision` y la API canónica `ReplaceSourceRevision`.
- Exportador Blender: `C:\Users\mruperez\Assets4All_LocalGate\v029\blender_extension\assets4all\savic_delivery.py`.

El contrato externo actual es `schemaId=assets4all.savic-delivery`, `schemaVersion=1`. El receptor comprueba expresamente esa versión. Cambiarla solo en el exportador provocaría el rechazo de la entrega; una evolución incompatible requiere soporte en el receptor y un plan para los paquetes archivados. La revisión de un artículo (`revision`) y la versión del formato (`schemaVersion`) tienen funciones diferentes.

### Procedimiento de actualización

1. Desarrollar e integrar los cambios en la rama y el checkout que realmente abre Unity. Si SAVIC se modifica en otra carpeta o rama, llevar esos cambios al proyecto conectado mediante Git; no basta con el push del origen.
2. Esperar a la recompilación de Unity y resolver cualquier incompatibilidad del puente. Actualizar e instalar el complemento Blender solo cuando cambie su exportador o el contrato de entrega.
3. Verificar una primera entrega y una nueva revisión con un artículo real: publicación, identidad y GUID conservados, ajustes manuales, rechazo de paquetes inválidos y recuperación ante fallo. Para cambios relevantes de publicación, comprobar además catálogo, colocación y SaveGame y ejecutar las regresiones SAVIC aplicables. Los helpers disponibles se enumeran en la sección de evidencia de este documento.
4. Reprocesar los artículos ya publicados afectados si el cambio debe regenerar sus artefactos o validaciones. Cambiar código no los regenera automáticamente; repetir una entrega idéntica puede devolver `UNCHANGED` y tampoco fuerza ese reprocesado. Utilizar las operaciones canónicas de SAVIC y comprobar el resultado.
5. Versionar el cambio validado, actualizar esta documentación y regenerar índice/bundle. Integrar en la rama del juego y generar una nueva build cuando deba llegar a los jugadores.

## Evidencia real del 05/10/2026

Modelo: armario Meshy previamente publicado, `8a5c37cab8eb4366ab675afa66af65ad`. Blender 5.1.1: exportación, repetición idempotente, reapertura del `.blend`, cambio de acabado y corrección de escala/suelo, SOURCE intacto y diez PartKeys conservadas. Unity 6000.3.19f1: primera vinculación, revisiones 2 y 3, Editor nuevo, reprocesado, ajustes de catálogo y material protegidos, 18 identidades sin duplicados, GLB histórico duplicado, rechazo de GLB corrupto y revisión antigua. Una cuarta entrega real verifica además la integración con la API de revisiones y las dependencias actuales del proyecto de uso.

Prueba Play Mode sobre `Prototype_Restaurant`: colocación mediante Edit Mode y catálogo principal; SaveGame con revisión 1; Editor cerrado; actualización a revisión 3; carga de esa misma partida con ItemId/InstanceId y acabado conservados; eliminación del slot de diagnóstico y Console sin Error/Exception/Assert durante el test y cleanup. Prueba destructiva: fallo de material protegido tras comenzar publicación, restauración de archivos y recuperación de diario interrumpido.

Helpers reproducibles: `tools/blender_savic_delivery_probe.py`, `SavicAssets4AllAcceptance`, `SavicAssets4AllSaveGameAcceptance`. Logs y JSON reales quedan en la copia de aceptación `assets4all-savic`; este documento no certifica todas las familias ni una jornada completa de IA.
