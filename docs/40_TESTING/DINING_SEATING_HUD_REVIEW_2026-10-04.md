# Comedor y HUD — revisión 04/10/2026

## Causas y solución

El presenter del miembro Humanoid resolvía únicamente plazas de barra; el comedor no conectaba la pose con las sillas registradas. Se amplía el presenter existente, conservando su API y actor de Animation V1. Gameplay sigue concediendo la mesa y CustomerSeatingFlow confirma la llegada. La representación observa esa concesión, estados sentados, topología de RestaurantSeatRegistry y plazas ordenadas por AssociatedSlotIndex. Cada miembro usa un SeatPoint distinto y el eje funcional de su silla; solo cambia su visual, no el CustomerGroup lógico, capacidad, reservas, rutas ni leases. Se rechazan topologías insuficientes/ambiguas y sillas con reserva activa, sin fabricar asientos. Al liberar la mesa se reproduce stand y vuelve el visual a su baseline.

La comprobación de carga encontró que HasReachedDestination no es persistente: service.runtime recrea identidad/pose y restaura el estado del grupo sin esa bandera. El presenter espera a terminar IsRestoring y observa los estados autoritativos posteriores a sentarse. El caso de reconstrucción usa las APIs TryRestoreRuntimeIdentity/TryRestoreRuntimeState con la identidad, estado y pose capturados después de la llegada real; no marca Navigation como llegado ni mueve su raíz como prueba.

La barra inferior recalculaba altura con 8,5 % y límites 78–108 unidades, sobrescribiendo la métrica compartida de la superior. Ahora RefreshNormalBottomBarLayout consume ResolveApprovedTopBarMetrics, también para margen lateral y separación física al borde. Se mantienen las autoridades y diseño de pausa/velocidades/relojes; no se cambia el assert de igualdad de altura para aceptar el defecto.

## Evidencia nativa

Unity 6000.3.19f1 en la copia aislada `C:\Users\mruperez\ProyectoBB\BB_Review`. Entrega en `C:\Users\mruperez\ProyectoBB\BB_SavicPresentation`, rama remota `feature/bb-presentation-interaction-quality-v1`. Se preservan todos los cambios locales ajenos.

| Prueba | Resultado | Evidencia local |
|---|---|---|
| Comedor inicial | 25 PASS: dos clientes del prefab real, asignación de mesa y llegada por Navigation/CustomerSeatingFlow, asiento y liberación | `Logs/dining-seat-first.log` |
| Comedor con reconstrucción | **50 PASS**, Console limpia hasta Editor; dos miembros en sillas distintas, pelvis a SeatPoint+offset con error <0,025 m, facing correcto, muslos sentados; misma identidad y nueva instancia Unity sin bandera de llegada fabricada | `Logs/dining-seat-restoration-final.log`, `Logs/dining-seat-presentation.txt` |
| HUD nativo | **159 PASS**, siete resoluciones y siete asserts de igualdad física de barras; marcos/secciones/controles dentro de pantalla, iconos no estirados y etiquetas superiores sin recortes | `Logs/hud-shared-height.log`, `Logs/TopBarResponsive/result.txt` |
| Taburetes publicados, representación compartida | Revalidación y aceptación estricta de MainCatalog, tres assets y seis cargas SaveGame, IDs/enlaces estables y objetos nuevos, clientes sentados, limpieza/slot eliminado/Console=0 | `Logs/dining-change-stool-revalidation.log`, `Logs/dining-change-stool-main-strict.log`, proof 10:02:38 UTC |

Todos estos procesos terminaron con código real **0**. Las capturas inspeccionadas son `Logs/dining-seated-actual-customers.png`, `Logs/dining-seated-restored-customers.png` y `Logs/TopBarResponsive/bar-1920.png`; el HUD se renderizó también a 800×600, 1024×768, 1280×720, 2560×1440, 3440×1440 y 3840×2160. No se sustituyó el render nativo por una maqueta.

Reproducción: `BistroBuilderDiningSeatPresentationPlaytest.Run()` y `BistroBuilderTopBarResponsiveTest.RunChromeOnly()`. La prueba original completa del HUD sigue disponible; este gate se limita a geometría, controles y contenido del chrome, sin afirmar aceptación de todas las pantallas de gestión. Las pruebas de barra/taburetes conservan el gate estricto de fuente/plan/prefab/cliente/Animation/informe vigente.

## Límites

La reconstrucción del comedor certifica las APIs de identidad/estado/visual del cliente y su asignación; no se presenta como prueba completa de SaveGame de un servicio ocupado. Las seis cargas completas de SaveGame sí se ejecutan para los taburetes publicados en su alcance anterior. No se añade walking, interacción de manos, extracción de campana ni capacidad nueva. La aprobación visual comercial global y jornada IA completa permanecen fuera de este cierre.

## Cierre canónico

Regresión posterior al caso de reconstrucción: `Logs/dining-hud-restoration-canonical-final.log`, código real 0, **SAVIC26/26, edición84/84, Navigation17 22/22, NavigationV1 44/44, barra59/59 y BBSIS2B18/18 PASS**. Inventario nativo 04/10/2026 10:10:54 UTC: **18 únicos, 18 publicados, 17 placeables, 0 NEEDS_REVIEW, 0 FAILED, 0 inbox y 0 orphaned**. Los proofs publicados siguen vigentes; no se cambian estados para cerrar el lote.
