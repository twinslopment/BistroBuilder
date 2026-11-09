# Índice consolidado — menú superior, Modo Juego

**Fecha de consolidación:** 09/11/2026.
**Rama documental:** `feature/ui-topbar-v21-approved-20261105`.
**Naturaleza:** inventario de **decisiones funcionales** y brechas verificadas frente a código existente. **NO es certificado de implementación, pruebas visuales ni build Unity actualizada.**

## Contratos visuales inmutables en esta fase

- Las **diez entradas de la barra superior V21** permanecen: **Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación, Opciones**.
- Mantener intacta la **barra inferior de Modo Juego E5.5** (Caja del servicio, Satisfacción, Velocidad y Climatología), sus iconos y reglas de interacción aprobadas.
- El **Modo Edición** tiene navegación y barras propias; no debe mostrar la barra inferior normal ni el menú de servicio del Modo Juego.
- Aplicar posteriormente una sola identidad visual coherente según `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md` y `BARRA_SUPERIOR_V21_V30_APROBADA.md`. No resucitar diseños antiguos ni crear pantallas estilo CRM por reutilizar el código existente.

## Mapa funcional del menú superior

| Menú V21 | Estructura acordada | Estado funcional | Documento fuente |
|---|---|---|---|
| **Actividad** | Acción directa al restaurante 3D; sin submenú | **Aprobado** | `SUBOPCIONES_MODO_JUEGO_ACTIVIDAD.md` |
| **Personal** | Plantilla · Candidatos · Horarios | **3/3 aprobadas** | `SUBOPCIONES_MODO_JUEGO_PERSONAL.md` |
| **Carta** | Cartas y reglas de activación · Platos | **2/2 aprobadas** | `SUBOPCIONES_MODO_JUEGO_CARTA.md` |
| **Inventario** | Existencias · Alertas de stock · Movimientos · Recepciones | **4/4 aprobadas** | `SUBOPCIONES_MODO_JUEGO_INVENTARIO.md` |
| **Proveedores** | Proveedores · Catálogo · Compra inteligente · Pedidos | **4/4 aprobadas** | `SUBOPCIONES_MODO_JUEGO_PROVEEDORES.md` |
| **Reservas** | UNA pantalla Agenda y gestión | **1/1 aprobada** | `SUBOPCIONES_MODO_JUEGO_RESERVAS.md` |
| **Economía** | Resumen · Resultados · Caja · Históricos · Financiación | **5/5 aprobadas** | `SUBOPCIONES_MODO_JUEGO_ECONOMIA.md` |
| **Marketing** | Catálogo de campañas · Campañas activas | **2/2 aprobadas** | `SUBOPCIONES_MODO_JUEGO_MARKETING.md` |
| **Reputación** | UNA pantalla Valoración y opiniones | **1/1 aprobada** | `SUBOPCIONES_MODO_JUEGO_REPUTACION.md` |
| **Opciones** | Partida · Audio · Vídeo · Jugabilidad · Interfaz · Controles · Accesibilidad · Idioma · Créditos/Legal | **2/9 aprobadas, 7/9 propuestas pendientes de aprobación conjunta** | `SUBOPCIONES_MODO_JUEGO_OPCIONES.md` |

**Total documental:** **24 de 31 apartados/categorías aprobados**, siete propuestas de Opciones preparadas para una **única validación**, y Actividad aprobada como **acción directa sin submenú**. No inferir que «aprobado» equivale a terminado en el juego.

## Última decisión registrada y aprobaciones pendientes

- `D-062`: **Opciones → Audio**, aprobada tras `D-061` (Partida).
- **Pendientes de aprobación** (no registrar como vigentes hasta aceptación del usuario): **Opciones → Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal**.
- Las siete están descritas en `SUBOPCIONES_MODO_JUEGO_OPCIONES.md`, **cada una con funciones, código verificado, límites y trabajo de integración**.
- Una vez validadas estas siete, registrar las decisiones correspondientes y cerrar documentalmente Opciones; con ello terminará la **definición funcional** de las diez entradas V21, pero **no el desarrollo de las pantallas**.

## Brechas de implementación a priorizar después de las decisiones

1. **Acciones de riesgo y confirmaciones:** Marketing → Catálogo inicia campañas inmediatamente sin preconfirmación (exigida); Proveedores → Pedidos cancela sin confirmación y puede mostrar cancelaciones no permitidas; Opciones → Partida necesita nomenclatura inequívoca de «menú de pausa» frente a «menú principal».
2. **Navegación e integración visual:** cada opción debe abrir su panel/acción correcta sin solaparse, y las barras **V21/E5.5** deben mantenerse idénticas. Integrar enlaces profundos a otras pantallas solo si los servicios reales los soportan.
3. **Consultas que aún son propuestas de UI:** filtros/búsquedas/detalles adicionales en Pedidos, Inventario, Reservas, Marketing activo y movimientos financieros; ficha completa de préstamos; comparativa de Históricos por servicios; reseñas de Reputación con «sin datos suficientes».
4. **Datos y autoridad:** un solo ledger financiero; reservas y mesas canónicas; campañas y modificadores de Marketing reales; coste de producto reconocido sin inventar COGS; ninguna UI altera de forma directa los servicios propietarios.
5. **Audio/Vídeo/Accesibilidad:** audio solo maestro, sin canales por grupo; vídeo sin selector de resolución; control `ReducedMotion` único y compartido entre Interfaz y Accesibilidad; controles PC informativos, sin reasignación; idioma español únicamente.
6. **Publicación y licencias:** comprobar derechos de distribución de los recursos y de `Recoleta Regular DEMO` antes de una build comercial; mantener avisos legales exactos.
7. **Pruebas posteriores obligatorias, NO realizadas con esta documentación:** compilación y Play Mode, interacción de botones/paneles, guardado/carga, persistencia, idempotencia de operaciones monetarias, navegación teclado/ratón, resolución **1920×1080 y 1280×720**, captura visual de las dos barras y regresión en Modo Edición.

## Siguiente hito

Solicitar **una aprobación en bloque** de las siete propuestas pendientes de Opciones. Tras la validación, cerrar formalmente el alcance funcional del menú V21 y programar por etapas con pruebas de fidelidad visual y funcionalidad real, utilizando estos documentos como contratos de aceptación.
