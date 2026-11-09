# Plan de implantación — pantallas del Modo Juego V21/E5.5

**Preparado:** 09/11/2026. **Estado:** planificación inicial tras el cierre funcional, no implementación.
**Fuente normativa:** `INDICE_FUNCIONAL_MENU_SUPERIOR_MODO_JUEGO.md`, los diez `SUBOPCIONES_MODO_JUEGO_*.md` y las decisiones D-034 a D-069 que correspondan. La barra superior V21 y la inferior E5.5 conservan iconos, posiciones y comportamientos aprobados. La identidad visual global es `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Objetivo y límites

Tras aprobar las diez entradas y 31 apartados funcionales de la barra superior, transformar las definiciones en **interfaces PC realmente utilizables**, reutilizando los servicios de dominio existentes. Una aprobación documental **no** demuestra que la pantalla esté integrada, que la build muestre la versión nueva o que se hayan pasado pruebas.

- **No modificar** rama de integración, Unity, assets o código como parte de esta planificación documental. Antes de programar, identificar rama/worktree destino, dependencia entre funcionalidades y baseline actual.
- **No sustituir** código de gameplay ni crear duplicaciones contables, de mesas/reservas, personal, catálogo de platos, marketing o reputación.
- **No modificar** diseño y animaciones aprobadas de las barras V21 y E5.5 por intentar acoplar paneles nuevos. En Modo Edición se aplica la navegación propia del Editor V2.
- **No declarar PASS** sin ejecutar las comprobaciones. Los ensayos históricos documentados no equivalen a una prueba de la build actual.

## Fase 0 — Inventario técnico y baseline verificable (obligatorio)

1. Identificar la rama y escena que realmente consume el ejecutable/build actual; verificar si hay sistemas UI V1 antiguos visibles y cuáles son sus referencias. Revisar diferencias entre worktree V21 y rama de integración antes de mezclar commits.
2. Detectar los diez puntos de entrada de la barra superior y los servicios canónicos usados por cada panel; mapa de referencias de escena, prefabs, rutas de iconos y singleton/eventos.
3. Confirmar los assets exactos de la **barra inferior E5.5**, barra superior **V21** y sistema visual global. Evitar reconstrucciones visuales improvisadas si existen archivos finales aprobados.
4. Ejecutar compilación Unity del estado inicial y obtener captura de 1920×1080 y 1280×720 mostrando ambas barras en el restaurante. Registrar errores, fallos de navegación y diferencias contra el diseño.
5. Clasificar cada sección: **existe y funciona**, **existe pero UI anticuada/incompleta**, **UI no conectada**, **funcionalidad no implementada**. La matriz es la entrada para priorizar cambios y no marca nada como terminado por suposiciones.

**Salida:** matriz pantalla → controlador → servicio → estado actual → riesgos → pruebas; baseline reproducible y rollback.

## Fase 1 — Contenedor y navegación general

1. Integrar la **barra superior V21** con sus diez acciones definitivas y la **inferior E5.5** únicamente en Modo Juego, respetando tamaño, anclajes, iconografía, selección, animación y usos acordados.
2. **Actividad** cierra paneles y devuelve la vista al restaurante 3D **sin abrir pantalla de supervisión**; Reputación y Reservas son pantallas únicas, no menús adicionales.
3. Establecer contrato universal de apertura/cierre de panel: foco, capa, botón cerrar/Esc, bloqueo de interacción con el mundo cuando corresponda y no superponer ventanas contradictorias.
4. Respetar modo Edición: sin menú de servicio ni barra inferior de Modo Juego y sin contaminar su navegación propia.
5. Verificar navegación y layout a **1920×1080 y 1280×720**; que la UI no tape innecesariamente el restaurante ni duplique controles de velocidad o caja.

**Salida:** shell de Modo Juego estable, diez entradas que navegan a destinos correctos, sin paneles antiguos superpuestos.

## Fase 2 — Riesgos de acciones y consistencia de gameplay

**Prioridad de seguridad/funcionalidad antes de embellecer detalles:**

1. **Marketing → Catálogo:** exigir confirmación previa y desglose de costes/efectos, validando otra vez el presupuesto; bloquear campaña duplicada para el mismo objetivo, pero no campañas diferentes legalmente compatibles.
2. **Proveedores → Pedidos:** confirmación de cancelación, respetar estados/autorizaciones de cancelación y volver a validar el precio antes de confirmar una compra.
3. **Opciones → Partida:** aclarar menú de pausa vs menú principal, prevenir pérdida de progreso en cargar/sobrescribir/salir; no tocar la ranura de autoguardado 999 con guardados manuales.
4. **Economía y Caja:** mantener cifras monetarias de los servicios canónicos, sin recontabilizar compras o préstamos desde la UI.
5. **Reservas:** asignación de mesa y llegada de clientes sin duplicaciones; Reputación refleja experiencias reales y nunca permite inventar puntuaciones.
6. **Opciones → Jugabilidad:** sincronizar pausa/velocidad con E5.5 usando un único `GameClock` y liberar exclusivamente los locks de Opciones.

**Salida:** acciones críticas con confirmaciones/revalidación y sin eventos duplicados.

## Fase 3 — Completar cada familia de pantallas sobre servicios existentes

**Orden sugerido, sujeto a auditoría de dependencias:**

| Lote | Pantallas | Enfoque de aceptación |
|---|---|---|
| A | Actividad, Opciones, Reputación, Reservas | Una acción directa, navegación del overlay, consulta de reputación y agenda única, sin paneles duplicados |
| B | Personal (3), Carta (2) | Personal/horarios canónicos; Cartas y Platos conectados con el catálogo maestro, iconos y fichas completas |
| C | Inventario (4), Proveedores (4) | Existencias/alertas/recepciones y compras con propuestas, borradores, confirmaciones, logística real |
| D | Economía (5), Marketing (2) | Métricas canónicas, campañas pagadas/activas, resultados y cuotas sin alterar autoridad de datos |

Dentro de cada lote realizar una pantalla/funcionalidad pequeña, probar y comparar con la referencia visual. No declarar cerrado un lote si sus escenarios esenciales fallan. El objetivo es integrar **el comportamiento aprobado completo**, no solo mostrar una maqueta.

## Fase 4 — Mejoras UI explícitamente pendientes

- Búsqueda, filtros y detalles completos en Inventario, Proveedores, Pedidos y Finanzas, cuando el servicio disponga de datos reales.
- Navegaciones profundas y fichas (receta/escandallo, proveedor, recepción, préstamo, reseña) con fuentes canónicas y sin nuevos sistemas paralelos.
- Transparencia sobre métricas incompletas: COGS, riesgo financiero, satisfacción sin evidencia, campañas sin ROI atribuible y previsiones de entregas inciertas.
- Opciones: selección vigente de FPS, control único de movimiento reducido entre Interfaz/Accesibilidad, volumen maestro y texto de idioma español. No inventar selector de resolución, mezclador, remapeador ni idiomas sin motor compatible.
- Créditos/Legal: validar uso comercial de recursos, en particular Recoleta DEMO, antes de cualquier distribución comercial.

## Fase 5 — Pruebas profesionales y puertas de integración

1. **Compilación y tests unitarios/editor** del proyecto Unity sin errores nuevos.
2. **Play Mode**: apertura/cierre de diez destinos y navegación de todas las subpantallas; foco y Esc; Modo Juego frente a Edición.
3. **Persistencia**: guardar/cargar/auto-guardar y recuperar preferencias; volver a cargar sin duplicar pedidos, gastos, campañas, clientes o reservas.
4. **Transacciones**: compras, cobros, cancelaciones, préstamos y marketing con controles de presupuesto y sin gastos dobles.
5. **Regresión de gameplay**: mesas, reservas, economía, proveedores, horarios, marketing y reputación ante cambios de día, servicio y partida.
6. **Fidelidad visual**, con capturas comparables en **1920×1080 y 1280×720**; barra superior V21 e inferior E5.5 aprobadas y ausentes correctamente en Modo Edición.
7. **Accesibilidad/uso PC**: ratón, navegación por teclado, estados seleccionados y ReducedMotion único.
8. Solo integrar en rama compartida tras pasar todas las pruebas pertinentes; registrar commit, evidencia y limitaciones. Si hay fallos, corregir e iterar antes de fusionar.

## Criterio final de aceptación

La tarea de UI Modo Juego será realmente **IMPLEMENTADA Y VALIDADA**, distinta de **DEFINIDA FUNCIONALMENTE**, únicamente cuando el jugador pueda usar las diez entradas y las 31 secciones/categorías aprobadas donde correspondan, con operaciones reales, barras definitivas sin deformaciones, comportamiento coherente en partida guardada y pruebas técnicas y visuales positivas.

**Estado a 09/11/2026:** la fase funcional está **CERRADA**; las fases 0–5 son **trabajo posterior previsto**, no ejecutado ni aprobado como resultado técnico.
