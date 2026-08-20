# Bistro Builder — 4E Personal / Rebase y auditoría estática

Estado: implementado estáticamente / pendiente de compilación y validación Unity.

## Base autoritativa

`feature/4e-staff-persistence-v2` es actualmente una extensión lineal de `feature/4d-staff-service-binding` y su merge-base es el HEAD canónico 4D `42d397846855324693e676f84bc6bed9d976d768`. La rama anterior `feature/4e-staff-persistence` quedó divergida y no debe usarse como base de nuevas modificaciones.

Los gates 4D de elegibilidad transaccional y preflight de cierre ya viven en la rama canónica 4D; 4E v2 los hereda sin duplicar su autoridad.

## Secciones de Save incluidas

- `staff.state`: autoridad persistente Employee; no guarda Waiter ni tareas.
- `staff.recruitment`: mercado de candidatos y metadatos de refresco; no guarda empleados ni dinero.
- `staff.session.runtime`: binding EmployeeId ↔ WaiterId y métricas de la sesión; no guarda GameObjects ni sustituye `service.runtime`.

## Orden de carga vinculante

### Prepare — orden DESCENDENTE

`BistroBuilderSaveGameService` ordena `PrepareForLoad` de **mayor a menor**. Por ello el desmontaje seguro queda:

1. `service.runtime`: Prepare 9000 — limpia primero tareas, flujos y asignaciones operativas.
2. `staff.session.runtime`: Prepare 8950 — desmonta bindings y elegibilidad después del runtime operativo.
3. `staff.recruitment`: Prepare 8900 — limpia mercado temporal.
4. `staff.state`: Prepare 8850 — vacía la plantilla al final, cuando ya no quedan bindings runtime vivos.

Este orden corrige un defecto detectado en auditoría estática: los valores anteriores 9050/9075/9100 asumían erróneamente un sort ascendente y habrían ejecutado Personal antes que `service.runtime`.

### Apply — orden ASCENDENTE

1. `staff.state`: Apply 400.
2. `staff.recruitment`: Apply 425.
3. `service.runtime`: Apply 500 (autoridad existente).
4. `staff.session.runtime`: Apply 550.

El binding de Personal se restaura después de que `service.runtime` haya reconstruido el estado operativo de los Waiter. Esto evita que Personal active/desactive agentes antes de que la autoridad de servicio termine de aplicar su snapshot.

### Finalize — orden ASCENDENTE

1. `staff.state`: 10500.
2. `staff.recruitment`: 10600.
3. `service.runtime`: 11000.
4. `staff.session.runtime`: 11100.

El validador y el instalador 4E v2 comprueban explícitamente las tres relaciones de orden.

## Compatibilidad con partidas antiguas

Las tres secciones son opcionales. `staff.state` se vacía durante Prepare para impedir contaminación entre partidas. `staff.recruitment` restaura un snapshot vacío y, si la sección no existía, genera un mercado nuevo determinista para el `DayIndex` cargado. `staff.session.runtime` descarta bindings anteriores y, si el save legacy declara servicio activo, solicita a 4D reconstruir una sesión contra los Waiter ya restaurados por `service.runtime`.

## Endurecimiento 4D exigido por 4E

Los dos helpers de seguridad están integrados en `BistroBuilderStaffSessionService` y existen en la rama canónica 4D:

- `BistroBuilderStaffEligibilityBatch`: `TrySetAllWaitersEligible(...)` delega en este lote transaccional y restaura los valores previos si cualquier Waiter rechaza la operación.
- `BistroBuilderStaffSessionClosePreflight`: `TryFinalizeClosedSession(...)` lo ejecuta antes de `FinalizeObservedWorkCycle(...)` y antes de cualquier `TryApplyServiceResult(...)`, exigiendo que todos los WaiterId ligados existan, sigan elegibles y estén realmente libres.

`BistroBuilderStaff4DHardeningSelfTest` prueba ambos contratos y además inspecciona el wiring real de `BistroBuilderStaffSessionService`. El instalador 4E v2 ejecuta este gate **antes de modificar la escena**.

## Gate de serialización real

Se añade `BistroBuilderStaff4EJsonRoundTripSelfTest`, ejecutado también por el instalador antes de modificar la escena. Usa directamente `BistroBuilderJsonSaveSerializer` (`unity-json-v1`) para serializar y deserializar:

- `staff.state`, comprobando EmployeeId, contrato, skills y responsabilidades;
- `staff.recruitment`, comprobando secuencia, día de refresh, CandidateId y salario esperado;
- `staff.session.runtime`, comprobando SessionId, EmployeeId ↔ WaiterId, métricas y mesas atendidas;
- snapshot de sesión inactivo, verificando que no sobrevivan residuos;
- operationId de rendimiento, verificando que sigue siendo estable tras el round-trip.

Después de cada deserialización se vuelven a ejecutar los validadores canónicos de dominio. Este gate no sustituye una prueba real de Save/Load: únicamente detecta incompatibilidades de modelo/JsonUtility de forma temprana y determinista.

## Gates aún pendientes antes de cerrar 4D/4E

No queda un gate estático conocido de wiring 4D bloqueando 4E. Continúan pendientes los gates que requieren el proyecto real:

1. Compilación limpia en Unity.
2. Instalación acumulativa sobre la escena canónica.
3. Validador y autotests 4D/4E en Unity, incluido JSON round-trip.
4. Round-trip Save/Load real durante servicio activo, verificando el mismo `EmployeeId ↔ WaiterId`, ausencia de duplicados, tareas coherentes y métricas sin doble aplicación.

Hasta superar esos gates, 4D y 4E no deben marcarse como cerrados ni validados.
