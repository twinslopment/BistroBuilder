# Editor V2 — B3 Selección común

**Fecha:** 2026-10-06
**Estado:** PASS
**Rama:** `feature/editor-v2`
**Base inmediata:** B2 Coordinator PASS

## Objetivo

Unificar el concepto de selección de Editor V2 sin sustituir las autoridades que ya seleccionan y editan mobiliario o arquitectura.

B3 introduce un único idioma de selección para que UI, inspector y acciones puedan preguntar:

- qué familia está seleccionada;
- qué tipo de entidad es;
- cuál es su identidad;
- qué nombre debe presentar;
- qué acciones admite;
- si su identidad es persistente.

B3 no crea una segunda selección de dominio ni ejecuta por su cuenta reglas espaciales, económicas, estructurales o de persistencia.

## Principio de autoridad

Las autoridades especializadas se conservan:

- mobiliario: `RestaurantEditInteractionController`;
- arquitectura: `ArchitectureSelection` dentro de `BistroBuilderConstructionAuthoringRuntimeTool`;
- superficies: reutilizan la selección arquitectónica de habitación;
- Editor V2: proyecta el estado anterior mediante `BistroBuilderEditorV2SelectionCoordinator`.

La selección común es una proyección observable. No es el lugar donde vive la geometría ni el estado canónico del restaurante.

## Modelo común

Archivo:

`Assets/Scripts/Application/Restaurant/EditMode/EditorV2/BistroBuilderEditorV2Selection.cs`

### Tipos de selección

`BistroBuilderEditorV2SelectionKind`:

- None;
- Furniture;
- Wall;
- Opening;
- Room;
- Surface.

### Capacidades

`BistroBuilderEditorV2SelectionCapability` es un conjunto de flags:

- Inspect;
- Move;
- Rotate;
- Delete;
- Duplicate;
- ApplySurface.

El consumidor no infiere acciones por nombres, modos o GameObjects. Pregunta directamente a la selección común mediante `Supports(...)`.

### Identidad

`BistroBuilderEditorV2Selection` expone:

- `family`;
- `kind`;
- `stableId`;
- `displayName`;
- `capabilities`;
- `persistentIdentity`.

Para mobiliario se usa `RestaurantPlaceableObject.InstanceId` cuando existe.

Los objetos antiguos sin identidad persistente reciben únicamente una identidad runtime `runtime:<instanceId>` y quedan marcados con `persistentIdentity=false`. No se inventa una identidad persistente.

Paredes, huecos y habitaciones usan los `BistroBuilderEditId` existentes.

## Fuentes de selección

Los adaptadores B2 implementan además `IBistroBuilderEditorV2SelectionSource`.

### Furniture

Proyecta la selección existente de `RestaurantEditInteractionController`.

Capacidades:

- Inspect;
- Delete;
- Move solo si `RestaurantEditableObject.CanMove`;
- Rotate solo si `RestaurantEditableObject.CanRotate`;
- Duplicate solo si existe `ItemDefinition`.

Limpiar delega en `RestaurantEditInteractionController.ClearSelection()`.

### Construction

Proyecta `SelectedKind` y `SelectedId` de Construction Authoring.

Pared:

- Inspect;
- Move;
- Rotate;
- Delete;
- Duplicate.

Puerta/ventana:

- Inspect;
- Delete;
- Duplicate.

Habitación:

- Inspect.

Limpiar delega en la selección arquitectónica existente.

### Surfaces

No crea selector propio.

Solo una habitación arquitectónica puede proyectarse como `Surface`.

Capacidades:

- Inspect;
- ApplySurface.

Una pared o abertura seleccionada no se fuerza a convertirse en superficie.

## Coordinador de selección

`BistroBuilderEditorV2SelectionCoordinator`:

- registra exactamente una fuente por familia;
- rechaza dos fuentes para la misma familia;
- mantiene una única proyección común;
- publica `SelectionChanged`;
- mantiene revisión de selección;
- la lectura es idempotente;
- limpia selecciones incompatibles al cambiar de autoridad;
- limpia toda selección al salir del modo edición.

### Regla Construction ↔ Surfaces

Construction y Surfaces comparten la misma autoridad arquitectónica.

Por tanto:

- habitación → Surfaces: se conserva si sigue siendo válida;
- pared/abertura → Surfaces: se limpia;
- abandonar arquitectura hacia Furniture: se limpia la selección arquitectónica.

No se conserva una selección incompatible por comodidad de UI.

## Seleccionar no modifica

Se añadieron dos APIs públicas controladas a Construction Authoring:

- `TrySelectArchitecture(...)`;
- `ClearArchitectureSelection()`.

Ambas operan sobre `ArchitectureSelection`.

`TrySelectArchitecture`:

- valida identidad;
- resuelve la entidad contra el Draft actual;
- selecciona mediante la autoridad existente;
- refresca visuales;
- no ejecuta un comando de edición.

La prueba runtime confirma que seleccionar una pared no altera:

- fingerprint del Draft;
- `DraftRevision`;
- estado dirty previo.

En mobiliario, seleccionar un artículo no:

- abre una colocación;
- mueve el objeto;
- rota el objeto.

## Integración de UI

La barra de edición existente deja de decidir la disponibilidad de Eliminar, Rotar y Duplicar deduciendo manualmente si está en Furniture o Construction.

`BistroBuilderUiShell.EditModeChrome` consume ahora `BistroBuilderEditorV2SelectionCoordinator` y sus capacidades.

Reglas relevantes:

- Mover se habilita para una selección Furniture con capacidad Move y sin colocación provisional;
- Eliminar depende de `Delete`;
- Rotar depende de `Rotate` o de una colocación Furniture ya activa;
- Duplicar depende de `Duplicate`.

La ejecución sigue delegada:

- Furniture → servicios existentes de placement/deletion;
- Construction → Construction Authoring.

Por tanto, la UI comparte inspector lógico y acciones disponibles sin convertirse en autoridad de dominio.

Undo/Redo sigue especializado en B3. Su coordinación global pertenece expresamente a B4.

## Bootstrap runtime

`BistroBuilderEditorV2RuntimeBootstrap` compone ahora:

- Coordinator B2;
- Furniture Adapter;
- Construction Adapter;
- Surfaces Adapter;
- Selection Coordinator B3.

Garantías:

- scene-scoped;
- idempotente;
- un único Selection Coordinator;
- no `DontDestroyOnLoad`;
- no crea autoridades de geometría, economía o persistencia.

## Gate B3

Test:

`BistroBuilderEditorV2B3SelectionSelfTest.cs`

Resultado final:

**28 OK / 0 fallos — Unity exit 0.**

Casos cubiertos:

1. una fuente por familia;
2. entrada mediante autoridad existente;
3. proyección Furniture;
4. lectura idempotente;
5. capacidades Furniture exactas;
6. limpieza al abandonar Furniture;
7. capacidades de pared;
8. selección compatible Construction → Surfaces;
9. selección incompatible Construction → Surfaces;
10. rechazo de fuentes duplicadas;
11. limpieza al salir;
12. bootstrap real;
13. bootstrap idempotente;
14. tres proyecciones runtime;
15. activación Furniture real;
16. colocable real encontrado;
17. selección Furniture real;
18. selección Furniture no abre transacción ni modifica pose;
19. capacidades derivadas de `RestaurantEditableObject`;
20. Furniture → Construction limpia selección incompatible;
21. Draft arquitectónico mediante autoridad existente;
22. pared temporal de prueba solo dentro del Draft cuando la escena no contiene una;
23. selección de pared mediante Construction;
24. seleccionar pared no modifica Draft ni revisión;
25. capacidades comunes de pared;
26. Construction → Surfaces limpia pared incompatible;
27. salida limpia;
28. consistencia total del gate.

La pared temporal del test, cuando es necesaria, se crea exclusivamente en el Draft de prueba mediante el comando arquitectónico existente y se descarta. Nunca se publica en la escena ni en Save/Load.

## Regresión final sobre el árbol B3

Después de conectar también la barra de edición al contrato común:

- B3 Selection: **28/28 OK**, exit 0;
- B2 Coordinator: **22/22 OK**, exit 0;
- Core: **84/84 OK**, exit 0;
- Scene Integration: **77/77 OK**, exit 0;
- Runtime Lifecycle: **20/20 OK**, exit 0;
- Queen Test: **PASS**, exit 0.

El primer intento de regresión Scene devolvió exit 1 porque el comando usó por error una clase inexistente, `BistroBuilderEditBlock18SceneValidation`. No fue un fallo del producto. Repetido con la entrada real `BistroBuilderEditBlock18SceneValidator.RunFromCommandLine`, el resultado fue 77/77 y exit 0.

## Decisión

**B3 = PASS.**

Editor V2 dispone ya de:

- B2: coordinación común de herramientas;
- B3: selección común y capacidades consumibles por UI.

**B4 — Undo/Redo global queda desbloqueado.**
