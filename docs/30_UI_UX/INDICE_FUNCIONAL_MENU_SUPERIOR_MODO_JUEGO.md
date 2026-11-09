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
| **Opciones** | Partida · Audio · Vídeo · Jugabilidad · Interfaz · Controles · Accesibilidad · Idioma · Créditos/Legal | **9/9 aprobadas** | `SUBOPCIONES_MODO_JUEGO_OPCIONES.md` |

**Total documental:** **31 de 31 apartados/categorías aprobados**, con **las 10 entradas de la barra superior definidas funcionalmente**. Actividad funciona como **acción directa sin submenú**. **CERRADO EL DISEÑO FUNCIONAL del menú superior V21**; no confundir aprobación con código implementado, integración visual o build validada.

## Cierre de decisiones y alcance

- `D-061` y `D-062`: **Opciones → Partida y Audio**, aprobadas anteriormente.
- `D-063` a `D-069`: **Opciones → Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal**, aprobadas **en bloque el 09/11/2026**.
- **No quedan apartados de la barra superior por definir funcionalmente.** Los diez menús y las 31 secciones/categorías documentadas ya están acordados, y Actividad es una acción directa.
- La única fase cerrada es la **especificación funcional**. Permanecen **pendientes de implementar y probar** los cambios detectados en cada documento, además de la fidelidad visual V21/E5.5.

## Brechas de implementación a priorizar después de las decisiones

1. **Acciones de riesgo y confirmaciones:** Marketing → Catálogo inicia campañas inmediatamente sin preconfirmación (exigida); Proveedores → Pedidos cancela sin confirmación y puede mostrar cancelaciones no permitidas; Opciones → Partida necesita nomenclatura inequívoca de «menú de pausa» frente a «menú principal».
2. **Navegación e integración visual:** cada opción debe abrir su panel/acción correcta sin solaparse, y las barras **V21/E5.5** deben mantenerse idénticas. Integrar enlaces profundos a otras pantallas solo si los servicios reales los soportan.
3. **Consultas que aún son propuestas de UI:** filtros/búsquedas/detalles adicionales en Pedidos, Inventario, Reservas, Marketing activo y movimientos financieros; ficha completa de préstamos; comparativa de Históricos por servicios; reseñas de Reputación con «sin datos suficientes».
4. **Datos y autoridad:** un solo ledger financiero; reservas y mesas canónicas; campañas y modificadores de Marketing reales; coste de producto reconocido sin inventar COGS; ninguna UI altera de forma directa los servicios propietarios.
5. **Audio/Vídeo/Accesibilidad:** audio solo maestro, sin canales por grupo; vídeo sin selector de resolución; control `ReducedMotion` único y compartido entre Interfaz y Accesibilidad; controles PC informativos, sin reasignación; idioma español únicamente.
6. **Publicación y licencias:** comprobar derechos de distribución de los recursos y de `Recoleta Regular DEMO` antes de una build comercial; mantener avisos legales exactos.
7. **Pruebas posteriores obligatorias, NO realizadas con esta documentación:** compilación y Play Mode, interacción de botones/paneles, guardado/carga, persistencia, idempotencia de operaciones monetarias, navegación teclado/ratón, resolución **1920×1080 y 1280×720**, captura visual de las dos barras y regresión en Modo Edición.

## Siguiente hito: pasar a integración real

**No abrir otra ronda de definición de submenús** salvo un cambio solicitado explícitamente. Usar los documentos aprobados como contratos; inspeccionar la rama de integración autorizada, establecer el orden de implementación y desarrollar en pasos aislados, con pruebas técnicas y comparaciones visuales contra el sistema premium aprobado.

Consultar `PLAN_IMPLANTACION_UI_MODO_JUEGO_V21.md` para orden, dependencias, criterios de aceptación y salvaguardas de integración. Ninguna modificación de Unity se ha realizado para registrar este cierre documental.
