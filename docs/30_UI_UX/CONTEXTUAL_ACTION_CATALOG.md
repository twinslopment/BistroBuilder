# Bistro Builder — Catálogo canónico de acciones contextuales

**Estado:** diseño vinculante en construcción. Este documento fija el contrato de UI y gameplay de las acciones contextuales del modo normal/servicio. Las acciones concretas se ampliarán por contexto sin duplicar lógica de dominio.

## Principio de la barra inferior

En modo normal/servicio, la barra horizontal inferior contiene de forma permanente **Velocidad**, `Caja` y **Climatología**. A continuación dispone de una zona de **acciones contextuales**.

La zona contextual no es un menú fijo ni debe llenarse por defecto. Solo muestra acciones que tengan sentido para el elemento seleccionado y para su estado actual. Como regla de diseño, se priorizan aproximadamente **3–4 acciones visibles simultáneamente**. Si no existe una intervención útil, la zona puede permanecer vacía.

Las acciones contextuales no saltan por encima de los sistemas de gameplay. La UI emite intención/comandos; camareros, cocina, comandas, satisfacción, finanzas u otras autoridades siguen resolviendo el resultado.

## Estados canónicos de espera/servicio

Las esperas se interpretan mediante estados semánticos comunes:

| Estado | Significado | Consecuencia de UI |
|---|---|---|
| **Normal** | La fase está dentro del tiempo razonable esperado. | No se ofrece una intervención por demora. |
| **Atención** | La fase se acerca al límite razonable. | Puede ofrecerse una acción preventiva si existe una decisión útil. |
| **Demora** | Se ha superado el tiempo esperado de la fase. | Se habilitan acciones de gestión de la demora. |
| **Incidencia** | La demora ya es grave o se ha producido un fallo explícito. | Se habilitan acciones de recuperación y consecuencias de satisfacción. |
| **Crítico** | Problema grave, repetido o muy deteriorado. | Alta prioridad visual/operativa y recuperación urgente. |
| **Resolución** | La causa ha sido resuelta y el sistema está cerrando la incidencia. | Las acciones dejan de ofrecerse cuando ya no tienen objeto. |

Una **incidencia** puede originarse de dos formas:

1. **Por tiempo:** una tarea supera de forma suficiente su margen razonable.
2. **Por evento:** ocurre un fallo real aunque no haya transcurrido un tiempo largo, por ejemplo un plato incorrecto o una atención fallida.

Los contadores empiezan cuando existe realmente la necesidad: una mesa está lista para pedir, una petición de camarero ha sido emitida, una cuenta ha sido solicitada, etc. No se cronometra una fase antes de que exista su obligación operativa.

## Fuente única de tiempos

No se deben hardcodear umbrales independientes en cada pantalla, acción o sistema. La intención es disponer de una fuente canónica configurable, conceptualmente **ServiceTimingCatalog**, consultada por UI y gameplay.

Para fases generales del servicio puede definir:

- atención/recepción inicial;
- toma de comanda;
- entrega de bebida;
- petición de camarero;
- retirada/atención posterior;
- entrega de cuenta;
- cobro;
- otras fases equivalentes que se ratifiquen.

Cada entrada podrá expresar al menos un objetivo y umbrales para **Atención**, **Demora** e **Incidencia**. Los valores concretos son datos de balance y no se consideran cerrados hasta probarlos en juego.

## Cocina y carta incompleta

No es requisito disponer ahora de un tiempo definitivo para cada plato. El sistema debe separar infraestructura de contenido de balance.

La resolución de tiempo esperado de un plato seguirá esta jerarquía:

`tiempo específico del plato -> perfil de preparación -> valor global por defecto`

Se prevé un concepto **DishPreparationProfile** para agrupar platos por comportamiento de preparación. Ejemplos de categorías como Rápido/Estándar/Lento son perfiles de diseño, no valores definitivos todavía.

Reglas:

- No inventar tiempos individuales para platos que aún no están diseñados.
- Todo plato debe poder funcionar aunque solo herede el perfil/default.
- Más adelante un plato puede sobrescribir su tiempo cuando exista una razón de diseño.
- Los umbrales de Atención/Demora/Incidencia se derivan del tiempo esperado; no se duplican manualmente dentro de cada plato.
- La estimación puede incorporar la carga/cola real de Cocina cuando exista una previsión fiable.
- El balance final se valida jugando; no se cierra únicamente sobre números teóricos.

## Contexto Mesa / Cliente

### Acciones ya ratificadas

| Acción | Condición semántica de aparición | Efecto de diseño |
|---|---|---|
| **Disculpa** | Existe una **Incidencia** o un evento negativo concreto que admite recuperación. | Intervención de recuperación de satisfacción. No elimina la causa del problema. |
| **Explicar demora** | Existe una **Demora** activa sobre una necesidad relevante de la mesa. | Gestiona la expectativa/impacto de la espera mientras la causa persiste. No acelera físicamente el servicio. |
| **Agilizar cuenta** | La mesa ha solicitado la cuenta, existe una tarea real de cuenta/cobro pendiente y la espera ha alcanzado al menos el estado **Atención**. | Eleva la prioridad operativa de las tareas relacionadas con preparar/entregar/cobrar la cuenta. |

Comportamiento esperado:

- Una mesa recién sentada y atendida dentro de tiempos normales no muestra estas acciones.
- **Explicar demora** puede aparecer antes que **Disculpa**: es una intervención preventiva cuando ya existe demora pero todavía no una incidencia grave.
- **Disculpa** aparece cuando el problema ya ha producido una incidencia o existe un fallo explícito.
- **Agilizar cuenta** no aparece en estado **Normal**. Se ofrece a partir de **Atención**, para evitar convertirla en una acción rutinaria que el jugador pulse en todas las mesas.
- En **Atención** se presenta como opción preventiva sin tratamiento de alarma; en **Demora** se destaca visualmente; en **Incidencia** puede coexistir con **Disculpa**.
- Una vez aplicada la priorización, no se permiten pulsaciones repetidas sobre la misma necesidad de cuenta.
- Cuando un camarero ya ha asumido efectivamente la tarea de cuenta, la acción deja de estar disponible y la UI puede mostrar un estado informativo como `Cuenta en camino`.
- **Agilizar cuenta** desaparece cuando ya no existe una tarea de cuenta/cobro pendiente.
- Cuando la causa desaparece, la acción asociada deja de ofrecerse; la UI no conserva botones obsoletos.

#### Primera vertical implementable: espera de cuenta

La primera integración runtime se limita deliberadamente a **espera de cuenta + `Agilizar cuenta`**. No modifica todavía el sistema avanzado de camareros ni introduce tiempos de platos.

Tuning provisional de prueba para `BillDelivery`:

| Referencia | Tiempo |
|---|---:|
| Objetivo | 90 s |
| **Atención** | 120 s |
| **Demora** | 210 s |
| **Incidencia** | 300 s |
| **Crítico** | 420 s |

Estos valores son **datos provisionales de balance**, no cifras definitivas de diseño. Deben permanecer configurables en `ServiceTimingCatalog` y ajustarse mediante playtests.

La espera canónica se lee del seguimiento de experiencia ya existente mientras el grupo permanece en `WaitingForBill`; no se crea un segundo cronómetro. La acción eleva la tarea real `DeliverBill` de la cola autoritativa de camareros a prioridad urgente únicamente mientras sigue pendiente. Si un camarero ya la ha asumido, la acción desaparece y la UI puede indicar `Cuenta en camino`.

Si el jugador ha aplicado `Agilizar cuenta` y realiza un guardado de servicio activo mientras la necesidad sigue vigente, el estado de priorización debe conservarse y rehidratarse al cargar; no puede perderse ni duplicar tareas.

### Acciones pendientes de ratificación

Estas acciones son propuestas y **no se consideran todavía cerradas**:

- **Ver comanda**: navegación directa al detalle de la comanda activa de la mesa.
- **Priorizar atención**: elevar temporalmente la prioridad de una tarea de camarero pendiente sin asignar ni teletransportar manualmente a un camarero.

No incorporar todavía como acciones canónicas sin diseño adicional:

- Cobrar ahora.
- Servir ahora.
- Limpiar mesa.
- Cambiar de mesa.
- Llamar refuerzos.
- Ofrecer compensación económica.

Estas opciones podrían saltarse autoridades existentes o requieren reglas económicas/espaciales adicionales.

## Contexto Cocina

Acciones ya aprobadas por el sistema de servicio:

- **Reducir entrada**.
- **Pausar nuevas comandas** por plato.
- **Priorizar comanda**, con un máximo de 3 prioridades simultáneas.

Su aparición exacta en la barra contextual deberá derivarse del estado y selección de Cocina/Comanda, sin duplicar las reglas de la autoridad de cocina.

## Regla de implementación incremental

No implementar de una vez carta completa, incidentes, satisfacción, tiempos, prioridades y UI. El orden acordado es:

1. Cerrar el catálogo de acciones y sus **condiciones semánticas**.
2. Implementar el contrato/configuración de tiempos generales de servicio.
3. Preparar **DishPreparationProfile** y el fallback global, sin completar todavía toda la carta.
4. Asignar perfiles/tiempos a los platos a medida que la carta se diseña.
5. Ajustar umbrales y tiempos mediante pruebas de juego.
6. Integrar las acciones con sus sistemas reales, sin crear lógica paralela en Presentation.

La ausencia temporal de tiempos específicos por plato no debe bloquear el desarrollo ni obligar a introducir datos ficticios.
