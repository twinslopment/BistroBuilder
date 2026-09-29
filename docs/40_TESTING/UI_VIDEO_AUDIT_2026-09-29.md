# UI — revisión del vídeo del 28/09/2026

Candidata en `codex/topbar-responsive-approved`, sobre `477e00de`, incorporando la integración `f86f97da`. Pendiente de revisión visual del usuario antes de subir a `integration/master-current-20260918`.

## Correcciones

- Los ocho paneles de gestión reservan la altura real de la barra superior y el HUD inferior al cambiar resolución.
- Actividad y las acciones de diseño inicial se ocultan al abrir gestión. El diseño inicial conserva una sola pareja de acciones; el dock de construcción IMGUI no se muestra junto al shell del jugador.
- El reloj del modo normal se oculta al editar o abrir Nueva partida; la fecha del HUD ya no repite la hora ni contiene un signo de interrogación de separación.
- Los selectores de Carta e Inventario mantienen sus textos: su superficie de entrada transparente queda excluida del tema visual.
- Reputación usa tamaños homogéneos y texto claro en sus tarjetas oscuras; Personal explica el estado sin empleados.
- Actividad conserva el marfil de su diseño y ajusta su altura a las entradas visibles. Sus filtros no reciben iconos genéricos que desplacen el texto.
- El hover de la barra responde desde el evento de entrada del puntero y se asienta en 90 ms; se conservan los iconos originales, el filtrado y las proporciones.
- Se recuperan las siete secciones de edición y las nueve miniaturas desde la integración; los artículos sin coste se identifican como Incluido. Se corrigen caracteres mal codificados en el catálogo de paredes.

## Validación reproducible

- `BistroBuilderTopBarResponsiveTest.Run`: **305 comprobaciones PASS** (log `Logs/ui_audit_validated.log`); recorrido de gestión, capas de entrada, apertura/cierre, acciones iniciales y capturas. Barra a 800×600, 1024×768, 1280×720, 1920×1080, 2560×1440, 3440×1440 y 3840×2160. Gestión capturada a 1280×720 y 1920×1080.
- `BistroBuilderEditSectionsPlayTest.RunBatchAndBuild`: PASS en siete rutas, previsualizaciones, puertas/ventanas, subcategorías, cierre/reapertura, aislamiento del tema, rotación ortogonal, rechazo de cruces y uniones continuas. Capturas a 1280, 1920 y ultrawide.
- `BistroBuilderEditBlock18CoreSelfTest`: 84 OK / 0 fallos.
- Los logs detallados se guardan en `Logs/TopBarResponsive`, `Logs/EditSectionsTest.txt`, `Logs/OpeningAndWallJoinsTest.txt` y `EditBlock18CoreSelfTestReport.txt`.

## Alcance de la evidencia

Las capturas automatizadas verifican disposición y contenido; no sustituyen la conformidad visual del usuario ni una medición de latencia en su monitor. Los cubos de mesas y personajes provisionales siguen siendo los modelos de la escena de prototipo. No se presentan como arte 3D final. Esta revisión no declara cerrados todos los sistemas del juego.

El editor emitió una excepción de indexación de UnityEditor.Search.SearchDatabase durante el batch; no procede del runtime del juego. No se declara una consola global sin incidencias del editor.

Capturas: [barra 1920](../Images/UIAudit20260929/bar-1920.png), [Carta 1280](../Images/UIAudit20260929/Carta-1280.png), [Reputación 1280](../Images/UIAudit20260929/Reputación-1280.png). Resultados: [UI](../Images/UIAudit20260929/ui-results.txt), [construcción](../Images/UIAudit20260929/construction-results.txt).

Build Windows final: **PASS**, Unity 6000.3.19f1, 29/09/2026 11:49 UTC, 181311838 bytes; 13 avisos del build. Log: Logs/ui_verified_windows_build.log. Salida: Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe; arranque a resolución nativa y pantalla completa sin bordes.
