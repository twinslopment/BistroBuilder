# Editor V2 — B2 Coordinator

**Fecha:** 2026-10-06
**Estado:** PASS
**Rama:** `feature/editor-v2`

## Objetivo

Introducir una única capa de coordinación entre las familias del modo edición sin sustituir las autoridades existentes.

El Coordinator conoce únicamente:

- si el modo edición está activo;
- familia activa: Furniture, Construction o Surfaces;
- herramienta activa;
- si existe una operación provisional;
- transición y último error;
- adaptadores registrados.

No decide geometría, validez espacial, dinero, navegación, BBSIS, persistencia ni historial especializado.

## Implementación

### Contrato común

`BistroBuilderEditorV2Contracts.cs`

Define:

- `BistroBuilderEditorV2ToolFamily`;
- `BistroBuilderEditorV2OperationState`;
- `BistroBuilderEditorV2Snapshot`;
- `IBistroBuilderEditorV2ToolAdapter`.

### Coordinator

`BistroBuilderEditorV2Coordinator.cs`

Garantías:

- no activa herramientas fuera del modo edición;
- un solo adaptador por familia;
- cambio de familia solo después de cancelar una operación provisional incompatible;
- si la cancelación falla, el cambio se rechaza y la herramienta anterior permanece;
- activación repetida de la misma herramienta es idempotente;
- si la nueva herramienta falla al activarse, intenta restaurar la anterior;
- al salir de modo edición limpia únicamente estado de coordinación;
- expone snapshot observable para UI futura.

### Adaptadores

`BistroBuilderEditorV2ToolAdapters.cs`

Furniture:

- delega en `RestaurantEditInteractionController`;
- cancelación mediante `CancelActivePlacement()`;
- respeta `RestaurantPlacementTransactionService`.

Construction:

- delega en `BistroBuilderConstructionAuthoringRuntimeTool`;
- reutiliza modos Select, Wall, Room, Door, Window y WallModule;
- no crea geometría por su cuenta.

Surfaces:

- reutiliza la selección de `ConstructionAuthoring`;
- la aplicación real del acabado sigue en el flujo existente;
- no introduce segunda autoridad de superficies.

### Bootstrap

`BistroBuilderEditorV2RuntimeBootstrap.cs`

- instalación scene-scoped;
- idempotente;
- crea un único `BB_EditorV2Coordinator`;
- registra tres adaptadores;
- no persiste mutaciones de escena;
- no usa `DontDestroyOnLoad`;
- no crea ninguna autoridad de dominio.

Construction Authoring continúa usando su bootstrap existente.

## Prueba específica B2

`BistroBuilderEditorV2B2CoordinatorSelfTest.cs`

Resultado final:

**22 OK / 0 fallos — Unity exit 0.**

Casos cubiertos:

1. no activa herramientas fuera de modo edición;
2. usa `RestaurantEditModeService` para entrar;
3. registra exactamente tres familias;
4. activa mobiliario;
5. cancela operación provisional antes de cambiar;
6. bloquea el cambio si la cancelación falla;
7. cambia a superficies cuando la operación previa converge;
8. activación repetida idempotente;
9. limpia coordinación al salir;
10. rechaza dos adaptadores para la misma familia;
11. compone runtime usando los bootstraps reales;
12. bootstrap Editor V2 idempotente;
13. tres adaptadores runtime sin nuevas autoridades;
14. Furniture delega en el controlador existente;
15. Wall delega en Construction Authoring;
16. Surfaces reutiliza Select de Construction;
17. Surface → Door llega al especialista correcto;
18. volver a Furniture restaura input;
19. herramienta inexistente hace rollback al especialista anterior;
20. limpiar herramienta no inventa ni elimina Draft;
21. salida usa la autoridad existente;
22. estado y transiciones quedan observables.

## Ejemplos reales

### Cambio normal

`Furniture → Wall`

Resultado:

- Construction pasa a `Wall`;
- input de mobiliario queda suspendido;
- no se crea una segunda transacción.

### Operación incompatible

Furniture tiene una operación provisional y se solicita Wall:

- Coordinator solicita cancelación al adaptador Furniture;
- si la autoridad cancela: cambia a Wall;
- si la autoridad rechaza: permanece Furniture;
- no hay dos operaciones simultáneas.

### Superficies

`Wall → Surfaces/floor`

Resultado:

- Construction Authoring pasa a `Select`;
- Surfaces no crea un nuevo selector ni una nueva autoridad;
- el acabado continúa aplicándose por el flujo existente.

### Herramienta inválida

Desde Furniture se solicita `invented-tool`:

- Construction rechaza la herramienta;
- Coordinator restaura Furniture;
- el input de mobiliario vuelve a quedar operativo;
- el estado no queda neutro ni corrupto.

## Regresión posterior

Después de introducir B2:

- Core: exit 0;
- Scene Integration: exit 0;
- Runtime Lifecycle: exit 0;
- Queen Test: exit 0.

No se ha detectado regresión del editor existente.

## Decisión

**B2 = PASS.**

B3 — Selección común queda desbloqueado.
