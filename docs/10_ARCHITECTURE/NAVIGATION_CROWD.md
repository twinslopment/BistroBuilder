# BB Navigation & Crowd Flow System

**Estado canónico:** diseño especializado cerrado en sus fronteras; implementación/hardening transversal activo. El Bloque 17 Navegación V1 figura cerrado, pero Crowd Flow continúa integración y regresiones.

## Autoridad
Responsable de rutas, circulación y tráfico humano. Prioridad de diseño: **robustez → gameplay → rendimiento → naturalidad → simulación**.

## Familias contempladas
Clientes, camareros, cocineros, resto de personal, grupos, repartidores y carritos.

## Responsabilidades
- calcular y actualizar trayectoria;
- velocidad lógica y heading;
- llegada y replanificación;
- separación/circulación y resolución de bloqueos;
- coste de rutas y gestión de tráfico;
- tratamiento coherente de grupos y agentes con distintos envelopes.

## Fronteras vinculantes
- BBSIS decide si el espacio/episodio es físicamente válido y sus Claims/Leases.
- Interaction & Reservation decide derechos lógicos sobre destino/recurso.
- Animation representa locomoción; no decide trayectoria.
- Gameplay/IA decide qué destino/acción persigue el actor.

## Casos espaciales obligatorios
Las puertas ocupan espacio durante apertura y las sillas cambian ocupación al sentarse/levantarse. Navigation debe consumir esa realidad espacial sin convertirse en autoridad de bisagras, asientos o contratos BBSIS.

Los colocables con cuerpo cóncavo pueden optar a proyectar las cajas estáticas de su proxy BBSIS mediante `BistroBuilderSpatialPhysicalFootprintAdapter`. Navigation y colocación consumen la misma geometría; la envolvente exterior sigue controlando límites de área y bloquea conservadoramente cuando la adaptación es inválida. No debe rellenarse un hueco navegable con el bounding box del render ni desactivarse su bloqueo físico para forzar una ruta.

Una propuesta provisional con propietario espacial explícito no entra en la topología global. Navigation consulta `SpatialSubject.IsRegistrationEligible`, cuya política de barra deriva del registro canónico de colocables; no mantiene otra autoridad de activación. El preflight conserva acceso a su geometría y puertos propios.

La aproximación por anillo de docking requiere validar también el enlace final al destino original con la consulta estructural existente. Encontrar ruta hacia un punto cercano no demuestra que el segmento posterior sea transitable. Regresión de SAVIC del 02/10/2026: U sintética con ruta real de 5 m, ninguna pieza física atravesada y destino rechazado al bloquear su interior; Navigation 17 **22/22 PASS**, Edit Mode core **84/84 PASS**. Evidencia y limitaciones en `docs/SAVIC.md`, sección 64.

## Cierre/hardening

La geometría elevada opta al intervalo vertical del mismo proxy BBSIS consumido por colocación. Navigation conserva esas piezas en su topología y compara cada muestra contra una envolvente humana de altura configurada (`defaultAgentHeight`, 2 m por defecto), además del radio/separación existentes. Planner, validación de ruta NavMesh y solver local comparten esta consulta. El dato es autoría explícita del envelope, no una altura inferida del modelo. Altura desconocida conserva el obstáculo planar anterior.

Una pieza con altura autorada requiere clearance completo incluso cerca del destino; no hereda la excepción de aproximación a un endpoint legacy. Una adaptación física inválida también exige clearance completo sobre su envolvente conservadora. Una negativa nativa demostró que un rectángulo inválido pequeño se podía atravesar por estar entero dentro de la tolerancia de docking; se corrigió el caso en la autoridad Navigation, manteniendo las excepciones antiguas de objetos sin opt-in. Ruta humana inferior de 8 m muestreada cada 5 cm, baja altura con endpoint dentro rechazada y datos inválidos bloqueados; SAVIC 81–82. No equivale todavía a la aceptación runtime del GLB de campana.

No declarar cierre transversal definitivo mientras existan fixes activos de deadlocks/separación o integración. Las regresiones deben resolverse en esta autoridad, sin parches de movimiento dentro de Animation, Staff o sistemas de servicio.
