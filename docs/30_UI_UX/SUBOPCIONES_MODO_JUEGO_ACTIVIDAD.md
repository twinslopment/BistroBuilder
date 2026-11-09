# Subopciones del menú Actividad — Modo Juego

**Estado:** decisiones funcionales aprobadas por el usuario (09/11/2026).
**Alcance:** navegación del botón **Actividad** de la barra superior V21 del Modo Juego. No afecta al Modo Edición ni modifica la barra V21 aprobada.

## Subopciones vigentes

1. **Vista del restaurante — APROBADA (opción A).**
   - Cierra los paneles de gestión abiertos y devuelve al jugador a la vista principal 3D del restaurante.
   - No abre una pantalla adicional ni un modo avanzado de supervisión.
   - No cambia la partida ni el estado de la simulación.

2. **Actividad reciente — reutilizar sistema existente.**
   - Acceso al panel izquierdo **Actividad**, que presenta los acontecimientos del servicio.
   - Conservar el sistema de eventos, agrupación, prioridades, filtros y navegación hacia objetivos.
   - El formato de apertura/presentación del acceso se concretará durante la definición del menú, sin crear un registro paralelo.

## Subopción expresamente eliminada

- **«Alertas e incidencias» NO es una subopción independiente.**
- Los avisos y problemas siguen existiendo y se consultan mediante el filtro **«Incidencias»** del panel izquierdo Actividad.
- No crear otra pantalla de alertas, un segundo feed ni un sistema paralelo de notificaciones.
- Mantener la prioridad de incidencias críticas, la persistencia de avisos pendientes y la navegación al elemento afectado.
- Cuando proceda, la selección del objetivo mostrará los datos y acciones correspondientes en el **panel contextual derecho**.

## Distribución de responsabilidades

- **Panel izquierdo = Actividad:** feed, novedades, incidencias, oportunidades, reservas y filtro «Incidencias».
- **Panel derecho = Contexto:** información y acciones del elemento seleccionado; no sustituye al registro de actividad.
- **Modo Edición:** mantiene su interfaz propia; no utilizar los paneles de servicio como menús de edición.

## Estado del código comprobado al aprobar la decisión

- En `feature/ui-topbar-v21-approved-20261105` se encuentran `BistroBuilderUiShell.cs` y `BistroBuilderUiShell.TopNavigation.cs`, sin botón específico «Alertas e incidencias» que haya que suprimir.
- El filtro `ActivityFilter.Incidents` ya está codificado en `feature/activity-runtime-current`, concretamente en `Assets/Scripts/Presentation/UI/Activity/ActivityPanelController.cs` y `Assets/Scripts/Application/Activity/ActivityFeedRuntime.cs`.
- **No está verificada** la integración del controlador avanzado de Actividad en la barra superior V21 ni en una build jugable. Esta decisión no equivale a darla por implementada o probada en Unity.

## Reglas para la implementación futura

1. Reutilizar el contrato de eventos y el controlador de Actividad ya desarrollado.
2. No añadir «Alertas e incidencias» al listado de subopciones de Actividad.
3. Conservar «Incidencias» en los filtros internos del panel izquierdo.
4. Probar que los avisos críticos no desaparecen mientras están pendientes, que los filtros no duplican eventos y que los clics navegan al objetivo válido.
5. Mantener intactas las diez opciones superiores V21; esta especificación se aplica **dentro** de Actividad.
