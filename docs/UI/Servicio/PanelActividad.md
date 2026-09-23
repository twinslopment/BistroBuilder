# Bistro Builder — Panel ACTIVIDAD

Estado: APROBADO
Fecha de cierre: 2026-09-23
Ámbito: partida normal / durante el servicio

## Objetivo

El panel ACTIVIDAD es el panel compacto situado en la parte izquierda durante el servicio. Su función es mostrar únicamente información operativa relevante sin convertir la pantalla principal en un panel de estadísticas.

La pantalla principal debe seguir siendo el restaurante. El panel debe ser estrecho, legible y no tapar innecesariamente el local.

## Contenido canónico

El panel muestra cuatro tipos principales de información:
- Incidencias.
- Oportunidades.
- Eventos importantes.
- Alertas críticas, que deben seguir siendo visibles incluso a velocidad alta.

Las fuentes de eventos pueden ser:
- Mesas.
- Cocina.
- Barra.
- Entrada y espera.
- Reservas.
- Inventario.
- Proveedores.
- Marketing.
- Reputación.

## Estructura visual y funcional

Cabecera:
- Título: `ACTIVIDAD`.
- Selector compacto: `Hoy / Incidencias / Oportunidades / Reservas`.

Cada fila debe mostrar:
- Hora.
- Icono.
- Origen o elemento relacionado.
- Mensaje breve.
- Estado o prioridad cuando corresponda.

Orden de prioridad:
1. Alertas e incidencias críticas.
2. Incidencias que requieren atención.
3. Oportunidades.
4. Eventos importantes recientes.

No deben mostrarse finanzas profundas durante el servicio, salvo avisos críticos directamente relacionados con la operación.

## Interacción

Al pulsar una entrada:
- se selecciona o centra el elemento relacionado cuando exista;
- se actualiza el panel contextual derecho con el detalle;
- las acciones disponibles deben ejecutarse sin sacar al jugador del flujo del servicio cuando sea posible.

## Criterios visuales

- Estética coherente con la UI aprobada de Bistro Builder.
- Colores cálidos crema/beige con acentos dorados sobre el lenguaje visual actual.
- Recoleta para títulos o encabezados principales.
- Inter Regular para texto de cuerpo y detalles.
- Inter SemiBold para pestañas, etiquetas y nombres con jerarquía.
- Diseño compacto, limpio, profesional y gastronómico.
- Evitar aspecto barroco, infantil, recargado o de dashboard empresarial.
- El restaurante debe seguir siendo visualmente protagonista.

## Regla canónica

Este documento es la referencia funcional vigente del panel ACTIVIDAD. No rediseñar su estructura, contenido o jerarquía sin aprobación explícita.

## Referencia visual aprobada

Referencia visual canónica aprobada el 2026-09-23:
- ChatGPT Library: `/BistroBuilder/UI/Referencias/Servicio/PanelActividad/BB_PanelActividad_APROBADO_2026-09-23.png`
- Library file id: `libfile_d809ca05da508191976815d3a0c7c410`

Elementos visuales fijados:
- Icono de ACTIVIDAD: portapapeles con gráfica ascendente roja.
- Título `ACTIVIDAD` con la tipografía aprobada para encabezados.
- Selector `Hoy` en cabecera.
- Fondo crema/beige, marco dorado y tarjetas internas claras.
- Mantener esta composición como referencia visual vigente salvo aprobación explícita de un cambio.
