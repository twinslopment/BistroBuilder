# Integración SAVIC y presentación — 03/10/2026

## Base y alcance

Integración solicitada sobre `feature/bb-presentation-interaction-quality-v1`, base `b595fd99`, con SAVIC `ef1fcbb7a539cd6a02a1af05fe938bdfe586d3d8`. Se prueba una copia nueva en `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation`, conservando los cambios locales de las carpetas originales. No se integra en master.

Se combinan el catálogo de persistencia de presentación (sincronización dinámica y eventos) y las fuentes canónicas SAVIC; se conservan las definiciones de escena anteriores. Los clientes mantienen paleta y cápsula de respaldo de presentación y añaden el perfil Humanoid canónico. La materialización de paredes conserva geometría continua, vecinos, acabados y aperturas y resuelve módulos de construcción SAVIC por definición.

## Regresiones demostradas y correcciones

- La primera prueba de construcción detectó que elegir solo el materializador de presentación perdía los módulos publicados SAVIC (`savic-presentation-first-closure.log`, exit 1). La combinación corregida pasa 26/26 y la regresión nativa posterior.
- Un checkout nuevo carecía de los GLB SourceMirror referenciados por los prefabs. Se versionan los 18 GLB con sus GUID existentes mediante Git LFS, compartiendo objetos SHA con el archivo canónico. Los 18 objetos LFS (540 MB) están transferidos. No se versionan las fuentes sintéticas de diagnósticos.
- La normalización LF de Git alteraba los bytes de ProviderMetadata y su SHA almacenado, impidiendo verificar la identidad completa de la campana (`integration-hood-current-candidate.log`, exit 1). Se restauran los siete archivos originales y se establece `-text` para preservar su evidencia byte a byte. Verificados los hashes de todos los archivos/mirrors de los 18 publicados y de cada metadato adjunto.
- La primera aceptación de barra hizo colocación y SaveGame, pero rechazó una excepción de Unity Search durante el arranque del Editor nuevo (`integration-bar-main-runtime.log`, exit 1). Se desactiva únicamente el indexado de arranque en UserSettings de esta copia de pruebas, no versionado. Los verificadores siguen rechazando Error/Exception/Assert; no se filtró la excepción. La repetición estricta termina exit 0.

## Aceptación funcional real

| Ejecución local | Resultado |
| --- | --- |
| integration-bar-main-runtime-second.log | exit 0; catálogo principal/SaveDefinition, registro, servicio/leases/rutas, SaveGame con identidad estable y nuevas instancias, cleanup/Console |
| integration-stool-current-candidate.log | exit 0; aceptación de dependencias de representación combinadas, sin cambiar catálogo ni estados |
| integration-stool-main-strict.log | exit 0; tres taburetes, asociación, cliente Humanoid sentado, Navigation/ocupación/lease, seis cargas SaveGame, nuevas instancias/links estables, cleanup/Console |
| integration-hood-preserved-evidence-candidate.log | exit 0; fuente/perfil propios, área Kitchen, cuerpo elevado/BBSIS/paso humano/claims, dos cargas reales |
| integration-hood-main-strict.log | exit 0; catálogo principal/SaveDefinition exactos, mismos contratos y dos cargas, cleanup/Console hasta Editor |
| integration-final-native-regression.log | exit 0; Closure Gate 26/26, core 84/84, Navigation 22/22, barra 59/59, BBSIS 2B 18/18; proofs actuales verificados |

Auditoría de esta copia: **03/10/2026 16:33:56 UTC; 18 únicos, 18 publicados, 17 catálogo placeables, cero NEEDS_REVIEW, cero FAILED y cero inbox**. El lote solicitado de 14 GLB está incluido. La diferencia con los 27 únicos de la carpeta original son los nueve jobs históricos locales sin fuente original; se conservan allí y no se fabrican registros en esta copia nueva.

Copias exactas del inventario, gate y cinco informes estrictos: `SAVIC/Verification/PresentationIntegration_20261003`. Los informes mantienen sus SHA de los manifiestos y mencionan Actual MainCatalog; las rutas runtime canónicas siguen en Library. No se usa el directorio de evidencia como autoridad de publicación.

## Presentación: comprobación pendiente

`integration-topbar-responsive.log` termina **exit 1**: la comprobación de igualdad física de altura entre barra superior e inferior falla a 1920×1080. Se conserva `responsive-ui-failure.txt`. Las dos implementaciones de layout y este test son idénticos a `b595fd99`: la superior usa 9% (84–102 unidades) y la inferior 8,5% (78–108). Este fallo no se presenta como aceptación visual positiva ni se retira su assert para pasar. La integración funcional no certifica el cierre visual de 21A; el usuario debe poder evaluar la combinación.

No se afirma reconocimiento universal de fuentes futuras, jornada IA completa, recuperación de un servicio ocupado al cargar ni extracción/ventilación simulada. Los checkpoints de taburetes son desocupados y la campana sigue pasiva conforme a D-003.
