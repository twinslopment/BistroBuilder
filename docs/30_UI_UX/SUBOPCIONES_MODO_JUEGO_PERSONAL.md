# Personal — subopciones del Modo Juego (propuesta en revisión)

**Estado:** PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN (09/11/2026).
**Regla:** no convertir esta propuesta en decisión vinculante hasta recibir aprobación explícita del usuario. No modificar código de Unity por una propuesta.

## Requisito visual transversal aprobado (09/11/2026)
**Reutilizar el sistema de Personal no implica reutilizar su diseño visual.** Las vistas Plantilla, Candidatos y Horarios, sus iconos, botones, filtros, tarjetas, colores y estados deberán ser completamente rediseñados con la identidad gráfica global aprobada de Bistro Builder. No se trata de una excepción de Personal: se aplica a **TODOS LOS PANELES**. Referencia vinculante: [`SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`](SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md).

## Punto de partida verificado en código

- `BistroBuilderStaffPlayerScreen`: pantalla jugable actual de Personal con pestañas `Staff` (Plantilla) y `Candidates` (Candidatos), sin pestaña integrada de Horarios en esta clase.
- `BistroBuilderStaffSchedulePlayerScreen`: pantalla de Horarios independiente ya desarrollada.
- `BistroBuilderStaffPlayerFacade`: read-models y comandos a través de servicios de Staff, Recruitment, Development y Session, sin estado de dominio duplicado en Presentation.
- `BistroBuilderStaffPlayerUiSnapshot`: empleados, candidatos, estado de asignaciones; cada empleado expone nombre, rol, situación laboral, disponibilidad, salario por servicio, antigüedad desde alta, XP, nivel, habilidades, rendimiento y estado/asignación de sesión.
- `BistroBuilderStaffPlayerTrainingPanel`: funciones de formación mediante fachada.
- `BistroBuilderStaffRoleCatalog`: incluye los roles `waiter` (camarero/a) y `cook` (cocinero/a). Respetar que los cocineros operan solo en cocina; no confundir identidades persistentes de empleados con agentes de servicio.
- La UI actual requiere confirmar contrataciones y despidos; el ascenso no forma parte de la primera fase aprobada del diseño de Personal.

## Estructura propuesta de navegación — NO APROBADA EN ESTE DOCUMENTO

El botón **Personal** abre una pantalla central de gestión con pestañas internas:
1. **Plantilla** (vista inicial)
2. **Candidatos** (contratación)
3. **Horarios** (planificación de turnos, aprovechando el sistema del Bloque 5)

La propuesta evita crear tres sistemas de empleados. La tercera pestaña requiere integrar la pantalla de Horarios existente en una experiencia visual coherente, conservando servicios separados.

## Primera propuesta a discutir: Personal → Plantilla

**Finalidad:** consultar y gestionar a los empleados que ya pertenecen al restaurante.

- Listado de empleados con nombre, puesto, disponibilidad/estado de servicio, nivel y salario por servicio.
- Filtros visuales recomendados por puesto (Todos / Camareros / Cocineros) y estado, sin inventar otro registro de empleados.
- Seleccionar un empleado muestra ficha con contrato, fecha/día de contratación, habilidades, XP/nivel, rendimiento y asignación actual cuando exista.
- Acciones que ya tienen soporte técnico: cambiar disponibilidad, formación y despedir con confirmación.
- Para contratar, llevar a **Candidatos**, sin duplicar la contratación dentro de Plantilla.
- **Ascenso:** oculto en la primera fase, según la decisión previa de diseño.
- **Cocineros:** deben aparecer en la plantilla con su rol correcto; solo trabajan en cocina.
- Horarios/turnos completos se gestionan en la pestaña **Horarios**, no en una gestión de planificación duplicada en Plantilla.
- La pantalla deberá adoptar la identidad visual general premium de Bistro Builder cuando se diseñe; esta propuesta no aprueba aún un mockup.

## Pregunta pendiente de aprobación

¿Aprobar el modelo «Personal abre una sola pantalla, con Plantilla como pestaña inicial; ficha de empleado y acciones reales existentes», manteniendo Candidatos y Horarios como pestañas de la misma experiencia?

## Condiciones de integración futura

- Conservar `EmployeeId` y autoridades canónicas de Personal.
- Resolver correctamente la visibilidad de cocineros.
- Garantizar que los cambios de disponibilidad, formación y despido se ejecutan por los servicios actuales, no por cambios directos de la UI.
- Confirmar en Unity la experiencia de 3 pestañas y validar su convivencia con la barra V21 y el modo de juego.
