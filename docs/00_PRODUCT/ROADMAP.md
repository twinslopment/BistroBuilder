# Bistro Builder — roadmap vivo

**Corte documental:** 12/09/2026. `CERRADO` significa cierre dentro del alcance declarado; una regresión posterior puede abrir hardening sin invalidar todo el diseño.

| Área | Estado canónico | Nota |
|---|---|---|
| 365B/C/D Seating, snapping e indicadores | CERRADO | PASS |
| 366/366B Save/Load estructura + `game.general` | CERRADO | PASS |
| 367A–F Carta, comandas, preparación, consumo, compartidos/pases | CERRADO | PASS en su alcance |
| 2.2 Inventario/Almacén V1 | CERRADO | FEFO, lotes, recepciones, mínimos/previsión, UI |
| 2.3 Proveedores V1 | CERRADO | mercado, formatos/ofertas, pedidos, storage, compra Inteligente |
| 3 Economía y Finanzas | CERRADO | cierre formal 28/08/2026 |
| 4 Personal | CERRADO | arquitectura laboral separada de agentes operativos |
| 5 Horarios y Turnos | CERRADO | cierre posterior prevalece sobre auditorías antiguas pendientes |
| 6 Reservas | CERRADO | capacidad, disponibilidad, servicio, persistencia, UI, Queen |
| 7 Marketing / 8 Reputación / 9 Progresión | CERRADO V1 | rama consolidada 7–9 |
| 10 Clientes / 11 Comandas / 12 Cocina / 13 Camareros avanzados | CERRADO V1 | ramas específicas integradas/hardened |
| 14 Entrada, Sala y Barra | CERRADO V1 | espera virtual general; barra con espera física |
| 15 Fin de servicio/día | CERRADO V1 | rama específica |
| 16 Nueva partida/apertura | CERRADO V1 + hardening | existe V2 de apertura |
| 17 Navegación V1 | CERRADO V1 + hardening | Crowd Flow transversal continúa integración/regresiones |
| 18 Modo Edición/Construcción | CORE V1 CERRADO; EDITOR V2 B0 PASS; GATE A4A→SAVIC PASS; B1 PASS; B2 PASS; B3 PASS | Selección común validada 28/28 y consumida por la barra de edición; regresiones B2/Core/Scene/Lifecycle/Queen PASS; B4 Undo/Redo global desbloqueado |
| 21A UI/UX definitiva | EN DESARROLLO | diseño vinculante y rama propia; cierre aún no ratificado |

## Sistemas transversales
- **BBSIS v1:** COMPLETO, VALIDADO Y CERRADO; hardening solo ante regresión real.
- **Interaction & Reservation v1:** IMPLEMENTADO, VALIDADO Y CERRADO; auditoría destructiva futura antes de vertical slice/beta.
- **Character & Interaction Animation v1:** INTEGRADO, VALIDADO Y SUBIDO; futuras ampliaciones son V2/hardening.
- **Navigation & Crowd Flow:** contratos de autoridad fijados; implementación/hardening activo sin duplicar BBSIS ni Animation.
- **BBPLFS:** desarrollo activo en `feature/bbplfs-v1`.
- **Climate & Weather:** implementación V1 existente; cierre final pendiente de validación/ratificación.
- **BB Furniture Finishes & Variants Authoring System (BBFFVAS):** IMPLEMENTACIÓN TÉCNICA V1 ACTIVA; núcleo, autoría, Acabado Automático, miniaturas, publicación, runtime binding e importación Smart Assets/Asset Studio BB validados en Unity 6000.3.19f1. Integración en producción pendiente.

## Integración actual
`playtest/all-current-20260912` contiene trabajo posterior a `integration/chat-final-20260911` y, al crear esta documentación, tenía conflictos de merge sin resolver. Esta rama documental se aisló deliberadamente para no tocar esa integración.

## Actualización 24/09/2026 — Nueva partida
Diseño marfil clásico de tres opciones aprobado y maqueta interactiva incorporada en integration/master-current-20260918. Pantalla nativa integrada con tres preparaciones, creación/carga y menú de retorno; no cambia el cierre funcional del servicio de apertura. Fuente: [Nueva partida](../30_UI_UX/NEW_GAME_APPROVED.md).

## Actualización UI — 25/09/2026
Barra superior del modo normal adaptada a la preview V3 en la rama codex/topbar-responsive-approved: composición nativa compacta, iconos originales independientes y hover. 66 comprobaciones PASS en siete resoluciones (800×600 a 3840×2160, incluido ultrawide). Candidata a revisión visual del usuario antes de integrar.

## Revisión visual UI — 26/09/2026
La revisión del usuario detectó nitidez insuficiente en la build por preferencias antiguas de resolución (1280×720 ampliado a pantalla completa). Corregido el arranque y la transición a pantalla completa para solicitar píxeles nativos; retirado también el dock antiguo superpuesto. Regresión responsive: 67 comprobaciones PASS. La revisión visual final sigue pendiente antes de integrar.

## Revisión UI — 28/09/2026
Ampliado el área útil de los iconos sin aumentar la barra, mejorado el filtrado al reducirlos y reforzado el texto. Hover y pulsación responden desde su evento, con asentamiento de 90 ms. 77 comprobaciones PASS; pendiente de conformidad visual del usuario e integración.

## Revisión UI — 29/09/2026
La candidata `codex/topbar-responsive-approved` incorpora `f86f97da` de `integration/master-current-20260918` para conservar las secciones actuales de edición, las miniaturas y las correcciones de paredes. Se corrigen solapamientos, selectores sin texto, contraste de Reputación y duplicación de acciones iniciales. La integración de esta candidata continúa pendiente de conformidad visual; no se declara cerrado 21A. [Evidencia y alcance](../40_TESTING/UI_VIDEO_AUDIT_2026-09-29.md).
## Integración UI autorizada — 29/09/2026
El usuario aprueba subir las correcciones de `9b70eafc` a `integration/master-current-20260918`. Se integra con el cambio remoto `702b7175` de Universal Preview V2 conservando ambos trabajos. El recorrido de UI combinado mantiene 305 comprobaciones PASS. Esto sustituye el estado pendiente de aprobación de la candidata; no declara cerrado el conjunto de 21A.

## Plan Editor V2 — 05/10/2026
Se aprueba el plan maestro de evolución del Modo Edición sin reescritura total. La implementación queda secuenciada por gates: primero B0 (baseline/protección), después cierre de la conexión Assets4All → SAVIC y, una vez superado ese gate, B1–B16 de Editor V2. BBSIS, Navigation, Finance, Save/Load, SAVIC, Placement, Construction Authoring, cámara 369 y Universal Preview conservan sus autoridades; Editor V2 añadirá coordinación común sin duplicarlas. Fuente: [Editor V2 Master Plan](../20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md).

## Editor V2 B3 - 06/10/2026
B3 Selección común queda cerrado en PASS. Furniture, Construction y Surfaces proyectan su selección especializada a un contrato común con identidad, tipo y capacidades; la barra de edición usa ese contrato para habilitar acciones sin duplicar autoridades. Gate B3 28/28; regresiones B2 22/22, Core 84/84, Scene 77/77, Lifecycle 20/20 y Queen exit 0. B4 Undo/Redo global queda desbloqueado. Fuente: [Editor V2 B3 Selección común](../40_TESTING/EDITOR_V2_B3_COMMON_SELECTION_20261006.md).

## Editor V2 — Galería Viva nativa — 10/10/2026

Implementado el catálogo nativo a partir de la referencia detallada del usuario, con búsqueda, categorías, favoritos, recientes, ámbito, precios y ordenación reales sobre B9. Se desactivan los renderizadores anteriores del catálogo y el diagnóstico IMGUI provisional. B8/B10 y colocación provisional conservan sus autoridades. Validación nativa: 61 comprobaciones PASS; B9: 42/42 con 10.000 definiciones; capturas GPU en 1920 × 1080 y 1280 × 720 y build Windows independiente. La aprobación visual del conjunto y los productos fotografiados de la maqueta continúan pendientes. [Evidencia y alcance](../40_TESTING/EDITOR_V2_GALERIA_VIVA_NATIVE_20261010.md).
## Editor V2 — revisión gráfica R2 — 11/10/2026

La entrega visual del 10/10 queda rechazada por el usuario. R2 corrige medidas, marcos anidados, herramientas inferiores cortadas y coexistencia de inspectores B8/arquitectura. Ficha nativa compartida, acciones iniciales en menú V2, tipografía SDF y filtros canónicos; 106 comprobaciones Play Mode PASS. Vista de revisión HTML/CSS/JavaScript con controles funcionales y capturas completas en 4K. No se declara identidad visual por píxel ni se publican las ocho mesas de la imagen en el catálogo real; escena 3D y aceptación gráfica siguen pendientes. Trabajo aislado sin integración a principal. [Evidencia R2](../40_TESTING/EDITOR_V2_REFERENCE_R2_20261011.md).

## Editor V2 — R3 y pantalla completa — 11/10/2026

Disponible una nueva revisión separada con resolución nativa a pantalla completa, F11, catálogo medido desde la referencia completa, marcos sin restos de texto dibujado, fuentes SDF y selección de tarjetas coherente con B8. 106 comprobaciones nativas y 16 de navegador PASS; prueba GPU de pantalla completa y vuelta a ventana. R3 no cierra la igualdad visual ni sustituye los modelos del prototipo por los de la ilustración. [Evidencia R3](../40_TESTING/EDITOR_V2_REFERENCE_R3_20261011.md).


## Editor V2 — R4 Windows nativo — 12/10/2026

Destino de ejecución aclarado: EXE Unity de Windows; la revisión HTML es únicamente un banco de comparación. R4 incorpora medidas, iconos vectoriales SDF, pie sin divisores añadidos, tarjeta crema, corrección del recorte de precios y ficha ornamental en la interfaz nativa. 134 comprobaciones Play Mode y regresión B9 42/42 PASS; build sin errores (13 advertencias), capturas GPU, arranque directo y pantalla completa nativa 1920 × 1080. Igualdad visual absoluta y contenido artístico de la referencia siguen abiertos; rama sin integrar. [Evidencia R4 Windows](../40_TESTING/EDITOR_V2_REFERENCE_R4_WINDOWS_20261012.md).


## Editor V2 — R5: barra inferior compacta — 12/10/2026

Petición gráfica del usuario aplicada al EXE nativo: pie de 85 unidades, unido al borde inferior y al ancho de pantalla, papel opaco bajo las leyendas, títulos de 16 y botones de 54. 147 comprobaciones Play Mode PASS; build sin errores (13 advertencias), capturas reales GPU, pantalla completa 1920 × 1080 y regreso a ventana verificados. R4 se conserva como historia y la rama permanece sin integrar. [Evidencia R5 Windows](../40_TESTING/EDITOR_V2_FOOTER_R5_WINDOWS_20261012.md).


## Editor V2 — R6: cabecera y selector de luz — 12/10/2026

Petición gráfica aplicada al EXE nativo: cabecera al borde superior/ancho completo, papel opaco con marco encima y logo contenido. Corregido el enlace a la etiqueta de Día/Atardecer/Noche que antes sobrescribía el glifo del sol. 184 comprobaciones Play Mode PASS; build sin errores (13 advertencias), capturas reales de los tres presets y regreso a Día en 1920 × 1080, tamaños de ventana y retorno desde pantalla completa verificados. Se conserva el pie R5. Rama sin integrar. [Evidencia R6 Windows](../40_TESTING/EDITOR_V2_HEADER_R6_WINDOWS_20261012.md).


## Editor V2 — R7: botones de barra inferior — 13/10/2026

Mejora solicitada de los once botones: relieve fino, alineación común y estados de hover/pulsación/selección. Eliminar utiliza burdeos y una nueva papelera SDF, con estado desactivado legible y diferenciado. Se conserva la autoridad de disponibilidad/acciones y la geometría de las barras R5/R6. 293 comprobaciones Play Mode PASS; build Windows sin errores (13 advertencias), capturas GPU de los estados y fullscreen físico 1920 × 1080. Rama sin integrar. [Evidencia R7 Windows](../40_TESTING/EDITOR_V2_TOOLBAR_R7_WINDOWS_20261013.md).


## Editor V2 — R8: paneles, edición de ficha y scroll de Asientos — 13/10/2026

Título Catálogo, doce iconos SDF originales con relieve, fuentes aprobadas y etiquetas uniformes. Ficha centrada con centímetros, proporciones, presets y XYZ activos sobre creación/placement/BBSIS/historial/guardado canónicos. La nueva corrección de Asientos añade tarjetas uniformes, nombres multilínea, precios visibles, viewport dentro del marco y scroll de latón. 381 comprobaciones Play Mode PASS; regresión espacial PASS y B8 75/75 PASS; build Windows sin errores y revisión de capturas reales, fullscreen físico 1920 × 1080 con F11. EXE R8 separado; la rama sigue sin integrar y no se certifica igualdad absoluta de la referencia. [Evidencia R8](../40_TESTING/EDITOR_V2_PANELS_R8_WINDOWS_20261013.md).


## Editor V2 — R9: fotografía de sillas — 13/10/2026

Mejora solicitada tras aceptar el formato R8: cámara elevada, asiento visible, encuadre uniforme y seis miniaturas reales 1024 × 1024 centradas. Evolución del generador canónico SAVIC, GUID/prefabs/materiales preservados y reparación demostrada de la ficha Roja enlazada a otra silla. 56 comprobaciones de render y 393 de UI Play Mode PASS; build Windows sin errores y captura GPU de seis fichas, scroll, 1280 × 720 y fullscreen. Entrega EXE R9 independiente; rama sin integrar. [Evidencia R9](../40_TESTING/EDITOR_V2_CHAIR_PHOTOS_R9_WINDOWS_20261013.md).


## Proyecto conjunto Editor V2 + SAVIC — 2026-10-14

La ruta de revisión viva es `C:\Users\mruperez\ProyectoBB\BB_EditorV2_SAVIC`, un enlace al worktree `editor-v2-galeria-fiel/BistroBuilder` de `codex/editor-v2-galeria-viva`. Integra R9 y el receptor Assets4All–SAVIC mediante un delta contrastado y respaldado; ambos chats deben continuar sus cambios sobre este mismo worktree. La evidencia R9 independiente se conserva. MASTER continúa siendo `integration/master-current-20260918`.

Instrucciones para el usuario: [Proyecto unificado](../PROYECTO_UNIFICADO_EDITOR_V2_SAVIC.md). Evidencia conjunta: [Validación](../40_TESTING/EDITOR_V2_SAVIC_UNIFIED_20261014.md).
