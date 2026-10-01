# Bistro Builder — Personal · Presencia operativa V1 (01/10/2026)

**Estado:** implementado estáticamente en `feature/bb-presentation-interaction-quality-v1`; **PENDIENTE de compilación Unity, Play Mode y Save/Load real**. No fusionar a master sin esos gates.

## Problema corregido
`staff.state` persistía contratos, pero 4D solo ligaba camareros contra el número fijo de componentes `Waiter` existentes en la escena. Contratar tres empleados nuevos no creaba tres agentes. Cocina seleccionaba cocineros disponibles lógicamente, incluso cuando no tenían turno; además no había un avatar físico reutilizable verificado.

## Solución sin autoridad duplicada
- `BistroBuilderStaffWaiterPopulation` es una **fábrica/gestor de agentes de escena**, nunca un gestor de empleados, turnos, tareas o Save paralelo. Se obtiene en `BistroBuilderStaffSessionService.CacheDependencies` y se aprovisiona antes de que 4D construya los bindings. Número solicitado: camareros activos/disponibles **programados explícitamente para el servicio**; con ausencia de planificación, se conserva el fallback legacy de 4D.
- Clona únicamente un arquetipo `Waiter` authored (no otro clon) que contenga el paquete completo `WaiterMovementView`, `WaiterTableServiceFlow`, `FoodDeliveryServiceFlow`, `BillServiceFlow`, `TableCleaningServiceFlow`; el nuevo clon se configura inactivo con un `WaiterId` único, `BistroBuilderStaffGeneratedWaiter`, `BistroBuilderAnimationActorBinding` con actorId único, y después se registra con el `WaiterTaskCoordinator` autoritativo. Fallo de configuración o registro revierte clones, nunca añade Employee.
- `BistroBuilderStaffWaiterVisualPresence` controla exclusivamente renderers, movimiento y colliders según el binding activo: sin turno/asignación no hay personaje ni obstáculo invisible. Se conserva el GameObject de `Waiter` para que el índice de 4D y `service.runtime` puedan resolver el ID. Restauración del baseline del arquetipo al clonar un source oculto.
- `BistroBuilderActiveServiceSaveSectionProvider.ApplyState` restaura primero todas las identidades de `BistroBuilderWaiterRuntimeSaveRecord`, antes de `BuildWaiterIndexAndRestoreTransforms`, pedidos y de `staff.session.runtime` (ApplyOrder 550). Desactiva y retira los agentes gestionados sobrantes; no destruye arquetipos authored. Las posiciones/órdenes son autoridad de `service.runtime`, **no se asignan por índice de empleado**.
- `BistroBuilderStaffScheduleSessionBridge` filtra solamente `operationalAdapterId=waiter.agent` al vincular EmployeeId ↔ WaiterId. Los cocineros del mismo turno permanecen asignados lógicamente a Cocina, nunca se convierten en `Waiter`.
- `BistroBuilderAdvancedKitchenService.AssignCook` usa los EmployeeId realmente programados para día/servicio actual. La primera partida programa al cocinero inicial junto al camarero inicial.
- `BistroBuilderStaffCookPresence` es una vista efímera por EmployeeId, derivada de Personal/Horarios/servicio. Solo aparece dentro de `RestaurantArea` activa, de tipo `kitchen`, y solo sobre posiciones validadas con `ContainsPosition`. No crea tareas/colas/Staff alternativo; la cocina avanzada mantiene la atribución real `WorkItem.CookEmployeeId`. Retira avatar al cerrar o perder elegibilidad y reconstruye tras Load con los datos canónicos.

## Requisito artístico real, no ocultar al usuario técnico
`CookPresence` **NO crea un primitivo ni reutiliza un camarero como cocinero**. Para ver al chef debe existir un prefab de personaje aprobado en:
`Assets/Resources/BistroBuilder/Characters/CookPresence.prefab`
(`Resources.Load<GameObject>("BistroBuilder/Characters/CookPresence")`), o asignarlo mediante inspector al componente. Debe ser **solo visual**, no contener `Waiter`, `CustomerGroup` ni `KitchenSystem`. Actualmente no se ha verificado un prefab canónico de cocinero reutilizable en GitHub; si falta, el componente registra una advertencia y NO finge visuales. Esto sigue PENDIENTE.

## Gates obligatorios (sin sustituir el Queen Test 4G/5F)
1. `Tools/Bistro Builder/Personal/V1 - Verificar agentes de sala y cocina` — preflight estático / identidad de agente inactivo; no cambia escena guardada.
2. Compilación Unity 6000.3.19f1: 0 errores.
3. Con restaurante cerrado contratar y programar, por ejemplo, 3 camareros; abrir y comprobar 3 WaiterId distintos y 3 agentes reales registrados (o la cantidad programada). Desprogramados fuera de vista y navegación.
4. Cocineros activos/programados: selección para tareas exclusivamente desde el turno de cocina; prefab aprobado SOLO dentro del área `kitchen`; sin cartel para el jugador.
5. Save activo A, trabajo real y posiciones B, Load A: mismos WaiterIds únicos, mismos bindings, órdenes y puestos, sin duplicados ni objeto extraño. Repetir con guardado cerrado y servicio nuevo.
6. Testar contratación/despido entre servicios, ausencia de turno, perfil Legacy sin plan y configuración sin chef prefab (error visible en Console y nunca un chef improvisado).
7. Inspección de navegación/colliders y colisiones de spawn en la escena final. El espaciado inicial es 1,15 m, pero **solo Unity puede validar la geometría y obstáculos reales**.
8. Probar 1920, 1280 y 800, FPS integrado Intel, sin búsquedas por frame para poblar plantilla.

## Fronteras
`Staff` conserva contrato, `Schedule` planificación, `4D` binding, `WaiterTaskCoordinator` colas, `AdvancedKitchenService` preparación y `service.runtime` checkpoints. Ningún componente nuevo escribe una sección de Save ni crea falsos empleados.
