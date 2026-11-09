# Bistro Builder — Actividad · prototipo visual interactivo V1.1
**Fecha:** 2026-11-09
**Estado:** PREVISUALIZACIÓN para revisión, **no aprobada aún** en lo relativo a hover, pressed, focus, halo y duraciones.

## Alcance
Se ha construido un prototipo separado en HTML/CSS/JavaScript para que el usuario pruebe el diseño compacto de Actividad con los paneles grafito cálido y marco fino luminoso bronce (dirección artística ya aprobada). No se modifica Unity, escenas ni arquitectura.

## Componentes cubiertos
- Pestañas Hoy, Incidencias, Oportunidades y Reservas, con estado seleccionado y hover.
- Filas de acontecimientos normal, hover, seleccionado, crítico, en curso, leve y resuelto.
- Panel izquierdo abrible/cerrable desde Actividad; panel contextual derecho cerrable/reabrible.
- Foco visible de teclado, botones principal/secundario/destructivo/deshabilitado; respuestas a pulsación.
- Marco de luz perimetral ajustable desde un control temporal de inspección, iluminación tenue sin efecto neón.
- Acciones ilustrativas de Mesa: Explicar demora y Disculpa, con inhabilitación demostrativa tras pulsación.
- Resolución 1920×1080 y recomposición a 1280×720.
- Toggle de movimiento reducido; muestras de diseño de estados.

## Condiciones de las barras
Las barras no se rediseñan. Se emplean las capturas originales de barra superior V21 y barra inferior E5.5. El botón Actividad de la demo tiene una zona pulsable superpuesta que muestra un destello de inspección, pero **no replica** la animación vectorial V30 definitiva. Los controles reales de velocidad/meteorología no se reprograman.

## Evidencia
Archivo autocontenido generado para usuario: `BistroBuilder_Actividad_Preview_Interactiva.html`, más paquete de código/recursos `BistroBuilder_Actividad_Preview_Interactiva_Codigo.zip`. Las imágenes de evidencia (generadas por Chromium) muestran estado principal a 1920×1080 y 1280×720, hover y panel cerrado. Estos artefactos se entregan en la conversación y no están todavía importados a GitHub/Unity.

## Comprobación
Prueba visual y de interacciones con Chromium:
- carga de imágenes originales superior/inferior: OK
- renderizado de cinco incidencias y selección inicial correcta: OK
- filtros y detalle Reserva: OK
- acción Explicar demora y deshabilitación tras uso: OK
- panel Actividad ocultar/mostrar: OK
- recomposición y cambio de recursos a 1280×720: OK
- restablecimiento de simulación: OK
- errores JavaScript observados: **0**

## Cautelas
La escena de restaurante es una fotografía de referencia estática, no viewport Unity. Los contenidos del feed son datos de ejemplo y las opciones de gameplay deben provenir de las autoridades vigentes de `CONTEXTUAL_ACTION_CATALOG.md`. Recoleta se sustituye por una serif si no está instalada. La aprobación de la dirección artística no aprueba automáticamente todos los estados o duraciones.
