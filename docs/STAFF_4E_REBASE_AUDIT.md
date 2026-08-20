# Bistro Builder — 4E Personal / Rebase y auditoría estática

Estado: implementado parcialmente / pendiente de compilación y validación Unity.

## Base autoritativa

La rama `feature/4e-staff-persistence-v2` nace de `2c40d717c4d665c14c4fd6a0b398e076f43e57ae`, HEAD endurecido de 4D. La rama anterior `feature/4e-staff-persistence` quedó divergida y no debe usarse como base de nuevas modificaciones.

## Secciones de Save incluidas

- `staff.state`: autoridad persistente Employee; no guarda Waiter ni tareas.
- `staff.recruitment`: mercado de candidatos y metadatos de refresco; no guarda empleados ni dinero.
- `staff.session.runtime`: binding EmployeeId ↔ WaiterId y métricas de la sesión; no guarda GameObjects ni sustituye `service.runtime`.

## Orden de carga vinculante

- `staff.state`: Apply 400.
- `staff.recruitment`: Apply 425.
- `service.runtime`: Apply 500 (autoridad existente).
- `staff.session.runtime`: Apply 550.

El cambio respecto al 4E anterior es deliberado: el binding de Personal se restaura después de que `service.runtime` haya reconstruido el estado operativo de los Waiter. Esto evita que Personal active/desactive agentes antes de que la autoridad de servicio termine de aplicar su snapshot.

Prepare mantiene la relación inversa necesaria para desmontar con seguridad:

- `service.runtime`: Prepare 9000.
- `staff.state`: Prepare 9050.
- `staff.recruitment`: Prepare 9075.
- `staff.session.runtime`: Prepare 9100.

Finalize:

- `staff.state`: 10500.
- `staff.recruitment`: 10600.
- `service.runtime`: 11000.
- `staff.session.runtime`: 11100.

## Compatibilidad con partidas antiguas

Las tres secciones son opcionales. `staff.state` se vacía durante Prepare para impedir contaminación entre partidas. `staff.recruitment` restaura un snapshot vacío y, si la sección no existía, genera un mercado nuevo determinista para el `DayIndex` cargado. `staff.session.runtime` descarta bindings anteriores y, si el save legacy declara servicio activo, solicita a 4D reconstruir una sesión contra los Waiter ya restaurados por `service.runtime`.

## Endurecimiento 4D exigido por 4E

Existen dos helpers de seguridad separados de las autoridades operativas:

- `BistroBuilderStaffEligibilityBatch`: aplica cambios globales de elegibilidad como lote transaccional y restaura los valores previos si cualquier Waiter rechaza la operación.
- `BistroBuilderStaffSessionClosePreflight`: exige que todos los WaiterId ligados existan, sigan elegibles y estén realmente libres antes de consolidar XP/rendimiento o desactivar agentes.

`BistroBuilderStaff4DHardeningSelfTest` prueba ambos contratos y además inspecciona el wiring real de `BistroBuilderStaffSessionService`. El instalador 4E v2 ejecuta este gate **antes de modificar la escena**. Mientras el servicio siga usando el bucle antiguo o no invoque el preflight de cierre, 4E fallará deliberadamente y hará cero cambios.

## Gate aún pendiente antes de cerrar 4D/4E

Los helpers y su enforcement ya existen, pero `BistroBuilderStaffSessionService` todavía debe cablear explícitamente:

1. `BistroBuilderStaffEligibilityBatch.TryApply(...)` dentro de `TrySetAllWaitersEligible`.
2. `BistroBuilderStaffSessionClosePreflight.TryValidate(...)` al comienzo de `TryFinalizeClosedSession`, antes de `FinalizeObservedWorkCycle` y antes de cualquier llamada a `TryApplyServiceResult`.

Hasta que esos dos puntos estén integrados y Unity compile/ejecute los gates, 4D y 4E no deben marcarse como cerrados.
