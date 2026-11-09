# Bistro Builder — Design System V1 · Actividad
**Fecha:** 2026-11-09
**Estado:** PROPUESTA DE COMPONENTES; solo el layout compacto de Actividad está APROBADO.
**Alcance:** exclusivamente dirección artística. No modifica Unity, gameplay, barras ni navegación.

## Referencia vinculante
- Guía visual global V4: videojuego de gestión premium, cálido, adulto y sin aspecto CRM.
- Barra superior V21 + animación V30, iconos originales, diez secciones y orden aprobado: Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación, Opciones. No rediseñar.
- Barra inferior E5.5 con retoques posteriores: Caja, Satisfacción, Velocidad y Climatología. No rediseñar ni cambiar iconos.
- Actividad: único panel izquierdo de Modo Juego con filtros Hoy, Incidencias, Oportunidades, Reservas, cierre con pulsación repetida, selección/localización en restaurante 3D y panel Contexto a la derecha cuando proceda. No hay pantalla independiente de alertas ni submenú. No aparece en Modo Edición.

## Base aprobada
Los dos paneles laterales compactos sustituyen el diseño inicial sobredimensionado. El restaurante 3D conserva la mayor superficie y el protagonismo visual. El diseño se aplica después a todos los paneles: misma tipografía, botones, iconografía, estados, sombras, bordes y transiciones.

## Tokens de diseño propuestos (pendientes de aprobación)
| Token | Hex | Uso |
|---|---|---|
| Grafito | #252323 | Fondos de panel oscuro |
| Piedra | #3B3A36 | Profundidad y controles |
| Marfil | #F2EADF | Fondos claros, lecturas |
| Bronce | #C19A68 | Selección e interacción |
| Verde | #6E8B57 | Correcto y confirmado |
| Rojo | #B94A44 | Criticidad y destrucción |
Derivaciones de superficie propuestas: #FFF6E8, #EDE0CC. Títulos Recoleta; textos y controles Inter. Las imágenes de demostración usan una serif de sustitución porque Recoleta no está disponible: no cambia la familia elegida.

## Biblioteca de componentes propuesta
- `BB-Button/Primary`: normal bronce, hover con luz/borde moderados, pressed hundido, disabled sin brillo.
- `BB-Button/Secondary`: marfil, borde cálido, hover sin saltos de layout.
- `BB-Button/Danger`: rojo solo para acciones destructivas.
- `BB-FilterTabs`: una pestaña activa; color bronce para selección, grafito para inactivas; indicador de alerta crítico pequeño donde proceda.
- `BB-EventRow`: normal claro, hover suave, selected contorno bronce, critical con insignia roja distinta de selected.
- `BB-StatusChip`: estados con texto/icono además del color.
- `BB-Panel/Dark`: fondo grafito/piedra y borde metálico suave.
- `BB-Panel/Light`: marfil, sombras discretas, texto legible.
- `BB-ActionGroup`: acciones disponibles según entidad, estado y autoridad canónica, sin añadir gameplay.
- `BB-Icon`: iconografía adulta de trazo consistente.
- `BB-Focus`: anillo de teclado de 2 px siempre visible.

## Microinteracciones propuestas, no ratificadas
- Hover 150 ms: variar luz, borde y sombra, no geometría.
- Pressed 90 ms: sensación de presión leve.
- Panel 220 ms: opacidad y desplazamiento lateral de 12 px.
- Crítico: contador rojo discreto en icono Actividad si existe evento crítico pendiente; sin destellos invasivos.
- Respetar modo de movimiento reducido. V30 y E5.5 siguen con sus tiempos/comportamientos propios: estas propuestas NO los sustituyen.

## Distribución y variantes
- 1920×1080: lateral izquierdo aprox. 415 px, contextual derecho aprox. 403 px, ambos entre barras.
- 1280×720: recomposición con laterales aprox. 302 y 301 px, cuatro filas visibles y scroll interno; no mera escala.
- Demostraciones: `BistroBuilder_DesignSystem_V1_Estados_1920x1080.png`, `BistroBuilder_Actividad_Hoy_1920x1080.png`, `BistroBuilder_Actividad_Oportunidades_1920x1080.png`, `BistroBuilder_Actividad_Reservas_1920x1080.png`, `BistroBuilder_Actividad_Incidencias_Responsive_1280x720.png`.
- Las imágenes son bocetos visuales, no capturas runtime ni pruebas funcionales. Sus escenas y datos son ilustrativos.

## Contrato funcional
Consultar `docs/30_UI_UX/UI_UX_DEFINITIVE.md` y `docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md`. `Explicar demora` solo desde Demora; `Disculpa` desde Incidencia/fallo recuperable; `Agilizar cuenta` solo ante espera real de cuenta desde Atención. `Priorizar comanda` pertenece a Cocina/Comanda, no debe aparecer automáticamente desde Mesa. Los diseños de Oportunidades y Reservas no introducen acciones jugables. Evitar promociones, compensaciones, refuerzos o nuevas acciones no ratificadas.

## Aprobación e integración
La aprobación del layout compacto NO aprueba estos hovers ni medidas. Revisar diseño visual y estados en próximas iteraciones, después implementar componentes compartidos en Unity y validar 1080p/720p, selección, scroll, teclado, reduced motion y accesibilidad. No integrar esta rama antes de aprobación.
**Discrepancia documental:** en `integration/master-current-20260918`, la clave D-036 de `DECISION_REGISTER.md` corresponde a Agilizar cuenta, no a Actividad; reconciliar la referencia antes de integrar sin sobrescribir decisiones.
**Gestión documental:** actualizar documentos canónicos y regenerar índice y knowledge bundle conforme a `AGENTS.md` al fusionar.
