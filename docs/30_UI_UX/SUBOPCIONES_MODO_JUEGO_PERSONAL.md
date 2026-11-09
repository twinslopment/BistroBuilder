# Personal — definición funcional por pantallas (Modo Juego)

**Actualización:** 09/11/2026.
**Estado:** **Plantilla APROBADA**; **Candidatos y Horarios pendientes de aprobación**. También sigue pendiente la aprobación formal de la organización final de navegación de las tres pestañas.
**Regla:** documentar lo aprobado sin convertir propuestas no aprobadas en decisiones definitivas.

## Requisito visual global, ya aprobado

**TODOS LOS PANELES** deberán rediseñarse con la misma identidad gráfica premium, incluidos iconos, botones, filtros, colores y estados. Reutilizar funcionalidad de código anterior NO equivale a conservar su diseño visual. Este trabajo es exclusivamente de **definición funcional**; no ejecutar ahora rediseños ni modificaciones del runtime Unity. Fuente vinculante: `SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`.

## Punto de partida verificado en código

- `BistroBuilderStaffPlayerScreen` contiene Plantilla y Candidatos.
- `BistroBuilderStaffSchedulePlayerScreen` implementa la gestión de Horarios en una pantalla separada.
- `BistroBuilderStaffPlayerFacade` delega en Staff, Recruitment, Development y Session; no crea otra autoridad de empleados.
- `BistroBuilderStaffPlayerUiSnapshot` expone empleados con rol, disponibilidad, remuneración por servicio, nivel, XP, habilidades, rendimiento, situación de sesión y antigüedad.
- `BistroBuilderStaffPlayerTrainingPanel` permite formar a un empleado mediante los servicios canónicos.
- `BistroBuilderStaffRoleCatalog` incluye camarero/a y cocinero/a, con roles operativos diferenciados.
- `BistroBuilderStaffRecruitmentService` gestiona los candidatos y su contratación; `BistroBuilderStaffRecruitmentEngine` limita la renovación voluntaria del mercado a una vez por día de juego, después de la generación inicial.

## Plantilla — FUNCIONALIDADES APROBADAS

**Propósito:** consultar y gestionar a los empleados que ya pertenecen al restaurante, sin micromanejar las tareas de sala y cocina.

1. **Consultar plantilla:** todos los camareros y cocineros contratados.
2. **Filtrar empleados:** Todos / Camareros / Cocineros; consultar su estado.
3. **Estado:** disponible, trabajando/asignado o no disponible, según datos de juego.
4. **Contrato:** puesto, salario por servicio y antigüedad.
5. **Habilidades:** capacidades y puntos fuertes/débiles del empleado.
6. **Experiencia:** nivel, XP y progreso profesional.
7. **Rendimiento:** información real del desempeño registrada por el sistema.
8. **Formación:** invertir en formación disponible para mejorar habilidades.
9. **Disponibilidad:** marcar empleado disponible o no disponible.
10. **Despedir:** finalizar su relación laboral con confirmación previa.

**Límites aprobados:**
- **Contratar:** exclusivamente desde Candidatos, no desde Plantilla.
- **Turnos:** asignación/planificación desde Horarios, sin duplicar calendario en Plantilla.
- **Ascensos:** ocultos en la primera versión.
- **Cocineros:** deben aparecer correctamente en la lista y realizar únicamente tareas propias de cocina; camareros y cocineros no son intercambiables como agentes.
- **Gestión operativa:** la planificación de tareas durante el servicio queda en los sistemas de cocina, sala y camareros; Plantilla no es una consola de control manual del servicio.

**Estado técnico:** funcionalidades descritas y aceptadas; no equivale a garantizar su integración visual o pruebas en la build actual.

## Candidatos — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Propósito:** consultar perfiles de aspirantes y contratar personal según las necesidades del restaurante.

1. Consultar los candidatos disponibles, diferenciando camareros y cocineros.
2. Filtrar por puesto y, opcionalmente, ordenar por salario, nivel/experiencia o aptitudes.
3. Seleccionar un candidato para consultar nombre, puesto, perfil (equilibrado, rápido, atento, organizado u hospitalario), habilidades, XP y **salario por servicio** solicitado.
4. **Contratar** al candidato elegido con una confirmación que indique claramente su coste salarial por servicio.
5. Tras contratar, el candidato sale del mercado y pasa a Plantilla como empleado persistente, sin duplicación de identidades.
6. **Renovar candidatos** como acción disponible, respetando la regla ya programada de **una renovación por día de juego**, y explicando cuándo volverá a estar disponible.
7. No inventar entrevistas, negociación salarial, tasas de contratación ni un mercado de pago sin aprobación funcional específica.
8. No permitir que la contratación cambie automáticamente un turno ni una asignación del servicio en curso; estas operaciones pertenecen a Horarios y los sistemas de sesión.

## Horarios — PENDIENTE DE DEFINICIÓN FUNCIONAL

Reutilizar la planificación del Bloque 5, sin duplicar empleados o crear otro planificador paralelo. Se definirá después de aprobar Candidatos.

## Estructura de navegación — PROPUESTA, TODAVÍA NO APROBADA

Una pantalla de Personal con pestañas Plantilla, Candidatos y Horarios, accediendo inicialmente a Plantilla y recuperando la funcionalidad de los sistemas ya implementados. La composición visual y la integración de Horarios se diseñarán después de cerrar la definición funcional.

## Próximo paso

Aprobar o ajustar el alcance de **Candidatos**, en especial el comportamiento de **renovar candidatos** y cualquier limitación adicional de contratación.
