# Revisión de presentación — 04/10/2026

## Alcance y estado

Cinco incidencias adicionales reportadas sobre el vídeo del usuario, corregidas en Unity 6000.3.19f1. Destino autorizado: `feature/bb-presentation-interaction-quality-v1`; proyecto del usuario: `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation`. Revisión aislada: `C:\Users\mruperez\ProyectoBB\BB_Review`. No se sube a master.

## Causas demostradas y correcciones

| Incidencia | Evidencia de causa | Corrección |
|---|---|---|
| Miniaturas y mesa provisional como bloque | La captura tomaba el prefab primitivo sin la presentación de tablero/patas. A DPI 125 %, el target real de PreviewRenderUtility era 320×320 y ReadPixels de 256×256 recortaba la imagen. Preview de mesa de cuatro plazas apuntaba al icono de dos. | Renderer compartido de mesa, también en CreationStarted; geometría en coordenadas locales, escala del prefab preservada, cámara superior y reducción del target completo. Dos PNG inspeccionados y referencia corregida. Sin cambios de autoridad física. |
| Solo dos colores de silla visibles | Cuatro definiciones y prefabs reales en MainCatalog; tarjetas colocadas en una única fila horizontal pese a tener solo scroll vertical. | Conversión del layout heredado a GridLayoutGroup de dos columnas y ContentSizeFitter; cuatro colores comprobados con scroll nativo. |
| Inspector inconsistente al arrastrar | Early return al mantener modo compacto y alturas fijas de contenido/estado no respetaban los textos. | Refresh de datos y visibilidad, reglas durante drag, alturas medidas y límites del canvas; restauración de preview al cancelar. |
| Carta no cerraba | EventSystem alcanzaba el modal antes que el cierre: cabecera elevada con Canvas propio sin GraphicRaycaster. Reconfiguraciones acumulaban sortingOrder. Editor de platos quedaba debajo de navegación. | Raycaster en Canvas sticky, herencia desde Canvas padre, sorting estable, target mínimo 44 y safe-area/viewport canónicos del editor. Tres cierres reales con press/release del Input System; no se llama Close como aceptación. |
| Barra/taburetes antiguos reaparecían | Fixture serializada, instalador y recreación visual legacy. Al retirarla, BarServiceRegistry rechazaba cero plazas y deshabilitaba el servicio. | Retirada nativa guardada, eliminación de sus rutas de creación, guard de compatibilidad por identidad, registro vacío válido con capacidad cero. Alta dinámica de barra SAVIC preservada. |

## Aceptación real

Todos los procesos siguientes finalizaron con código de salida real **0**. Logs locales en `BB_Review/Logs`; los helpers de ejecución quedan versionados.

| Ejecución | Resultado | Evidencia |
|---|---|---|
| Autoría nativa e iconos | Dos mesas, cuatro colores existentes; primera retirada=1, repetición=0; render completo 320→256 | `presentation-native-repair-full-render.log`, `presentation-review-authoring.txt` |
| Play Mode de cinco incidencias | **72 PASS**, Console sin Error/Exception/Assert hasta Editor | `presentation-review-five-issues-final.log`, `presentation-review-runtime.txt`, timestamp 09:21:47 UTC |
| Regresión canónica | SAVIC **26/26**, edición **84/84**, Navigation17 **22/22**, NavigationV1 **44/44**, servicio de barra **59/59**, BBSIS2B **18/18** | `presentation-review-final-canonical-regression.log`, `presentation-review-final-regression.txt`, timestamp 09:32:07 UTC |
| Barra publicada, catálogo principal real | Colocación, binding, ruta GridFallback, lease automático/liberación/ocupación, SaveGame save/load, mismos IDs y nueva instancia; slot eliminado y Console limpia | `presentation-review-published-bar-runtime.log`, proof `barCounterRuntime` verificado 09:32:35 UTC |
| Inventario canónico | **18 únicos, 18 publicados, 17 catálogo placeables, 0 NEEDS_REVIEW, 0 FAILED, 0 inbox, 0 orphaned** | `Library/BistroBuilder/SAVIC/Logs/canonical-content-inventory.json`, 09:32:07 UTC |

Ejecución reproducible: `BistroBuilderPresentationReviewRepair.Run()` prepara autoría; `BistroBuilderPresentationReviewPlaytest.RunFromCommandLine()` verifica los cinco flujos; `BistroBuilderPresentationReviewRepair.RunFinalRegression()` verifica autoridades y genera inventario. La prueba publicada usa `BistroBuilder.Editor.Savic.SavicPublishedTableRuntimePlaytest.RunPublishedBarCounterFromCommandLine()`. Ejecutar sobre una copia limpia y sin otro Editor abierto en ese proyecto.

## Fallos encontrados durante la verificación

Los primeros intentos no se presentan como aceptación. La prueba aislada encontró pointers LFS sin materializar: se verificaron 36 rutas sin cambios locales, sus tamaños y los SHA-256 de 18 objetos ya disponibles, y se materializaron exclusivamente esas rutas. No hubo descarga ni recuperación Git/stash. La prueba BBSIS antigua exigía una barra fija; ahora verifica ausencia de capacidad/asignación en layout vacío y mantiene las pruebas nativas de semánticas cuando existen barras. La barra SAVIC publicada se prueba separadamente.

El test de ratón necesitaba consumir eventos Dynamic porque el bootstrap mantiene la simulación pausada; se verifica estado de Mouse y primer hit de EventSystem antes de enviar cada press/release. Los fallos previos de Canvas/raycaster y de navegación superpuesta demostraron los defectos de producto y se corrigieron sin sustituir el cierre por una llamada directa. También se eliminaron búsquedas de UI durante teardown de componentes desactivados, que producían Assert al salir del Editor.

## Límites

Las capturas nativas del proceso batch son de 640×480; los PNG de miniatura se inspeccionaron a 256×256. Esta evidencia certifica los flujos descritos, no todas las resoluciones del HUD. Los clientes de comedor con pose sentada fuera de la silla y la desigualdad de altura entre barras siguen pendientes. Se conservan los cuatro colores existentes; no se amplía el sistema de acabados ni se inventan materiales de Meshy. No se afirma jornada completa de IA ni recuperación de servicio ocupado tras cargar.
