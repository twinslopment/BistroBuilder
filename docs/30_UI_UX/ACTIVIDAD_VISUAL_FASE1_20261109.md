# Bistro Builder — Actividad · Diseño visual Fase 1
**Fecha:** 2026-11-09
**Estado:** base compacta APROBADA por el usuario. Las vistas adicionales y las medidas son propuestas pendientes de validación; no hay implementación Unity.

## Decisión visual aprobada
Se aprueba el diseño **compacto** de los dos paneles laterales de Actividad (izquierdo) y Contexto (derecho), sustituyendo la versión anterior sobredimensionada. El restaurante 3D será protagonista. El diseño debe pertenecer al mismo sistema visual que Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación y Opciones: grafito, piedra, marfil, bronce discreto, verde semántico y rojo para incidentes críticos/acciones destructivas; titulares de estilo Recoleta, textos estilo Inter, iconografía adulta, sombra, borde y estados hover/focus/pressed consistentes. Evitar estética CRM.

## Barras bloqueadas — no rediseñar
- Superior **V21** con animación **V30**: negro, logotipo original y diez opciones en este orden: Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación, Opciones.
- Inferior **E5.5** y retoques posteriores: Caja, Satisfacción, Velocidad y Climatología, con iconos propios y sus comportamientos. Referencia de prototipo gráfico: `BistroBuilder_BarraInferior_Animada.html`.
- Las barras se recuperaron de prototipos HTML anteriores. Los montajes son imagen conceptual, no capturas de Unity.

## Composición y estados a desarrollar
- Referencia: **1920×1080**, adaptación a **1280×720**. Mantener los paneles dentro del hueco entre las barras, con el mayor centro 3D posible. Anchos orientativos a comprobar: Actividad 380–440 px, Contexto 340–405 px a 1920; no aumentar sin nueva revisión.
- Al pulsar Actividad desde gestión: volver al restaurante y mostrar lateral izquierdo. Al repetir pulsación: ocultar **ese panel**.
- Cuatro filtros: **Hoy, Incidencias, Oportunidades y Reservas**. No crear submenú de Actividad ni pantalla de alertas.
- Feed de acontecimientos útiles. Fila seleccionada con contorno bronce/ámbar, selección y localización del elemento en la escena y panel contextual a la derecha cuando proceda.
- Criticidad: señal roja discreta en el feed y el icono superior mientras haya incidencia crítica pendiente. Sin modal rutinario.
- Contexto: mostrar acciones solo si encajan con la entidad y su estado. Un panel oculto no debe dar lugar a un control flotante inventado.
- En Modo Edición, Actividad y Contexto de Modo Juego no aparecen.

## Contrato de acciones (no inventar gameplay)
Los textos de acciones que figuraban en las primeras ilustraciones no suponían aprobación funcional. Ver `docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md`: para Mesa/Cliente están ratificadas `Explicar demora` desde Demora y `Disculpa` desde Incidencia o fallo recuperable; `Agilizar cuenta` se limita a una tarea real de cuenta desde Atención. `Ver comanda` y `Priorizar atención` siguen pendientes de ratificación, mientras que `Priorizar comanda` corresponde a Cocina/Comanda.

## Evidencias de diseño
Montajes conceptuales entregados en el paquete `BistroBuilder_Actividad_Fase1_Visual_2026-11-09.zip`:
- `actividad_incidencia_abierta_1920x1080.png`: incidencia seleccionada, barra superior e inferior verificadas como recursos originales.
- `actividad_panel_cerrado_1920x1080.png`: lateral izquierdo oculto; coexistencia con contexto como variante a confirmar.
- `actividad_incidencia_abierta_1280x720.png`: inspección gráfica, no prueba responsive en Unity.
La aprobación ratifica la **base compacta**, no implica aceptación automática de estas capturas adicionales ni de todas sus etiquetas.

## Trazabilidad y discrepancia
Vincular esta decisión con `docs/30_UI_UX/UI_UX_DEFINITIVE.md`, `docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md` y la especificación funcional indicada por el usuario `SUBOPCIONES_MODO_JUEGO_ACTIVIDAD.md` (pendiente de localizar en la rama). **No reutilizar ni sobrescribir D-036**: en la rama fuente `integration/master-current-20260918`, el registro `docs/90_DECISIONS/DECISION_REGISTER.md` aplica D-036 a `Agilizar cuenta`, no a Actividad. Reconciliar antes de integrar.
 
**Antes de fusionar esta documentación con la rama de integración:** incorporar las imágenes aprobadas al repositorio y regenerar `ALL_MARKDOWN_INDEX.md` y `PROJECT_KNOWLEDGE_BUNDLE.md` según `AGENTS.md`. Esta rama de diseño no altera código ni runtime.
