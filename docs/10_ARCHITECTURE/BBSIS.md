# BB Spatial Interaction System (BBSIS)

**Estado canónico:** V1 COMPLETO, VALIDADO Y CERRADO. No reabrir diseño salvo regresión demostrable.

## Autoridad
BBSIS es la única autoridad de **viabilidad y reserva espacial** para interacciones. No decide intención de gameplay, derechos lógicos, navegación ni animación.

## Conceptos vinculantes
- separación estricta `Render ≠ Spatial`;
- `Adaptive Spatial Proxy` para representación espacial robusta;
- `Spatial Contracts` como requisitos de interacción;
- traits y matriz de compatibilidad;
- `Claims / Spatial Leases` para exclusividad espacial temporal;
- `Spatial Episodes` como ciclo de una interacción espacial;
- `Work Edges`, `Ports` y `Seat Bays` como interfaces de uso;
- `Dynamic Sweep` para volumen requerido durante movimiento/interacción;
- `Mobility Envelope` y `Carry Envelope`;
- `Spatial Gates`, `Critical Routes` y `Flow Quality`;
- LOD lógico y perfiles de tuning data-driven.

## Reglas
- Interaction & Reservation no replica Claims/Leases.
- Navigation puede consultar viabilidad pero conserva autoridad de trayectoria.
- Animation representa la resolución espacial sin modificarla.
- Modo Edición y BBPLFS deben validar contra contratos espaciales comunes.
- Puertas y sillas pueden cambiar su ocupación espacial durante episodios relevantes; no se simulan mediante física por bisagra como autoridad de gameplay.

## Evolución

Ampliación SAVIC 03/10/2026: BarStool se publica como Seating funcional con asociación automática a una plaza persistible existente, sin aumentar capacidad. Su aceptación vincula source/plan/prefab/cliente/Animation/informe actuales y demuestra ocupación, lease, llegada, representación sentada y cargas SaveGame repetidas desde el catálogo. La animación no escribe reservas ni desplaza el root de Navigation. La geometría elevada de la campana continúa pendiente; el contador no se reduce convirtiéndola en decoración. Detalle: SAVIC 77–78.
Cambios futuros deben tratarse como hardening/V2 y demostrar que respetan contratos públicos y compatibilidad con sistemas ya integrados.

## Colocables compuestos de SAVIC

La ampliación funcional autorizada de SAVIC conserva un único registro BBSIS. Un sujeto puede declarar un propietario de ciclo de vida espacial mediante `IBistroBuilderSpatialLifecycleOwner`; si opta a esta política, solo es elegible para alta/rebuild mientras el propietario confirme su activación. La barra colocable consulta `RestaurantPlaceableRegistry`, cuya identidad persistida deriva las identidades de cuerpo y plazas. La ausencia de propietario requerido rechaza el alta; los sujetos anteriores conservan su comportamiento.

`BistroBuilderBarBodySpatialAdapter` ofrece los puertos de las plazas hijas durante el preflight de la raíz provisional, pero la recopilación global solo incluye instancias confirmadas. El cuerpo raíz contiene las piezas estáticas medidas y sus plazas conservan el contrato/leases nativos de `work.bar`, sin una caja adicional entre cliente y camarero ni duplicar semánticas globales. Alta/baja del cuerpo y plazas es transaccional; ocupación o lease activo impiden retirada. Navigation usa esta misma elegibilidad para excluir propuestas provisionales de su topología.

Regresión integrada de 02/10/2026: provisional/rebuild, preflight y cuerpos/puertos reales, asignación/lease, conflicto durante registro con rollback, reintento y limpieza; core 84/84, Navigation 22/22 y servicio de barra 59/59 PASS. Evidencia: `bar-body-spatial-native-verified.log`; límites y continuidad en `docs/SAVIC.md`, sección 65. Esta integración no demuestra por sí sola publicación ni SaveGame del mostrador Meshy.

La barra Meshy ya dispone de familia/publicación canónicas con aceptación vinculada a fuente, plan y prefab. La prueba posterior desde el catálogo principal real comprobó colocación, plazas/leases y rutas nativas antes/después de SaveGame con identidad de instancia estable, nueva instancia Unity y slot eliminado; Console limpia hasta regresar al Editor. Se reorganiza únicamente el mobiliario del layout temporal por el ciclo de vida existente, conservando límites, obstáculos y validadores. Evidencia `bar-counter-runtime-strict-acceptance.log`, 02/10/2026 20:49 UTC; no representa todavía una jornada completa de IA. Los taburetes siguen pendientes de su contrato de asiento/plaza, sin convertirlos en asientos de mesa.

Regresión posterior demostrada y corregida: el coordinador operacional debe incorporar las altas/bajas dinámicas de BarServiceRegistry y liberar el lease del cliente cuando la plaza queda libre. El registro nativo sigue siendo la fuente de plazas; BBSIS concede y libera derechos espaciales. La prueba previa falló por alta tardía ignorada (`bar-dynamic-coordinator-before-fix.log`, exit 1). La reprueba real de barra publicada usa ahora concesión/liberación automáticas del coordinador antes/después de SaveGame y conserva Console limpia (`bar-counter-dynamic-coordinator-runtime-acceptance.log`, 02/10/2026 22:24 UTC, exit 0). Gate 21/21, core 84/84, Navigation 22/22, servicio barra 59/59 y fase 2B 18/18 PASS; continuidad en SAVIC 70–72.

La ampliación de asiento de barra permite relacionar un cuerpo de taburete confirmado con una plaza nativa de capacidad 1. El lease de cliente solo excluye el cuerpo asociado mediante `relatedSubjectId`; sigue rechazando cuerpos y reservas ajenos. Occupancy/capacidad pertenecen a la plaza, la aproximación queda en suelo y el SeatFrame es visual. Una fuente sin counter surface autorado, un provisional o una asociación no validada no concede esa excepción. La API y autoría física están probadas; familia, colocación automática, representación y persistencia de taburetes aún pendientes, sin publicación anticipada.

Ampliación posterior verificada (02/10/2026 23:49 UTC): asociación automática desde pose propuesta sin mutación ni reserva durante preflight, con activación opcional transaccional en PlaceableRegistry y rollback de índices/cuerpo/enlace si falla. `seating.bar` forma parte del catálogo espacial. El proveedor semántico candidato relaciona exclusivamente el ID del puerto de cliente de la plaza compatible; no exceptúa los puertos de trabajo/transferencia ni obstáculos ajenos. Occupant/capacidad siguen en el registro nativo y el lease espacial en BBSIS. CounterSurfacePoint es un datum propio del plan de barra, no un marker inferido desde un log.

Regresión demostrada: después de `ResetTransientRuntimeStateAfterLoad`, un ID de lease cacheado no implica lease activo. `BarSpatialAdapter` consulta el lease real BBSIS y el coordinador puede reconciliar/reconcederlo; la prueba negativa previa termina en exit 1 y la posterior en exit 0. Gate 23/23, core 84/84, Navigation 22/22, barra 59/59, fase 2B 18/18 PASS. Los tres sources de taburete pasan asociación y reconstrucción nativa JSON, pero Animation sentada, SaveGame jugable de taburetes y publicación aún están pendientes. Continuidad en SAVIC 73–76.

## Intervalo vertical opcional de cuerpos y claims

La ampliación autorizada de SAVIC conserva BBSIS como autoridad geométrica. `SpatialHeightRange` permite declarar mínimo/máximo Y mundiales de un volumen. Ausencia o datos inválidos nunca prueban separación: siguen representando una columna conservadora. `SpatialProxyPart.hasVerticalExtent/height` autoran espesor local con escala positiva y base horizontal; el puente físico proyecta el mismo intervalo a colocación/Navigation. No se deducen alturas de mallas, de leases legacy ni de objetos ajenos. Un candidato con intervalo autorado inválido se rechaza; el existente conserva el bloqueo de respaldo.

Overlaps exige intersección horizontal y vertical cuando ambos intervalos son conocidos. Un claim sin altura conserva su comportamiento anterior; una consulta humana que declare 0–2 m puede demostrar separación de un cuerpo cuya cara inferior está a 2,2 m. Esto no certifica alturas de cargas/carritos desconocidos ni convierte un lease anterior en acotado. La pose candidata transforma el intervalo sin cambiar el Transform real. Prueba nativa: cuerpo, concesión/liberación de lease inferior y rechazo de intersección de cabeza; detalles y evidencia en SAVIC 81–82. La campana permanece en revisión hasta su propia familia, lifecycle, publicación y SaveGame reales; no hay simulación de extracción D-003.

## Cuerpo pasivo elevado confirmado por PlaceableRegistry

Ampliación verificada **03/10/2026 05:57 UTC**: `BistroBuilderPassiveBodySpatialBinding` participa en la activación canónica y ofrece `IBistroBuilderSpatialLifecycleOwner`. La raíz solo es elegible con registro de colocable y activación funcional confirmados; un provisional no se introduce por Configure, rebuild o Navigation. El cuerpo usa `spatial.passive.placeable.<InstanceId>.body`, conserva referencias runtime a servicios y revierte registro/identidad en rollback o baja. La consulta de leases activos pertenece a BBSIS y permite impedir alta/retirada que intersecten un derecho vigente. Los intervalos desconocidos siguen bloqueando conservadoramente; no se inventan alturas para claims antiguos.

La campana real ya está publicada como caso pasivo de KitchenEquipment. Comparte cuerpo acotado entre collider, colocación, BBSIS y Navigation y conserva el anclaje de suelo/área. La aceptación estricta MainCatalog/SaveDefinitionCatalog prueba paso humano inferior, claims permitidos/rechazados, dos cargas SaveGame con identidad estable y objetos nuevos, rebuild/baja/cleanup y Console limpia. Gate 26/26, core 84/84, Navigation 22/22, barra 59/59 y BBSIS 2B 18/18 PASS; evidencia y límites en SAVIC 84–85. No añade autoridad de servicio ni extracción/ventilación D-003.
