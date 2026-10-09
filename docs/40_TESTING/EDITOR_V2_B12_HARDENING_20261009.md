# Editor V2 — B12: hardening de composiciones (2026-10-09)

**Rama:** `feature/editor-v2`. Motor Unity 6000.3.19f1. No merge a maestra.

## Incidencias detectadas y corregidas

1. Antes del hardening, la validación secuencial de provisionales no comprobaba explícitamente conflictos **entre miembros de la propia plantilla** que todavía no estaban en el registro. Se añadió prueba de huellas orientadas utilizando `RestaurantPlacementCollisionUtility.EvaluateConflict`, antes de aplicar cambios, cobrar o publicar historial.
2. La validación de mesa con cuatro sillas inicialmente falló con `PlacementConstraintViolation`: las sillas provisionales activas eran interpretadas como obstáculos por las consultas de espacio de uso. Las instancias provisionales se desactivan inmediatamente tras materializarse; la validación y activación se ejecutan en orden: **mesa → otros muebles → sillas**, reutilizando el registro y las autoridades existentes.
3. Antes del cambio, cada artículo se cobraba mientras se activaba. Ahora primero deben superar **todos** la activación y validación espacial y **después** se confirman los gastos, que continúan teniendo rollback específico. Una única entrada de historial compuesta agrupa los cambios. Los modelos originales nunca se alteran.
4. Si cualquier operación falla, se invierte el ciclo de vida de las instancias activadas, se compensan gastos ya confirmados y se destruyen las provisionales. Los límites 1..64 y el permiso de edición se mantienen.

## QA real con escena de 38 muebles

Método: `BistroBuilderEditorV2B12RuntimeSelfTest.RunFromCommandLine`. Archivo de resultado runtime `EditorV2_B12_Runtime_Report.txt`; log `Logs/B12_FinanceRollback02.log`.

- PASS: captura → JSON → recarga → resolución de ID canónico → coste → nueva colocación en posición válida → identidad propia → Undo/Redo/Undo → 38 originales intactos.
- PASS: guardar y reabrir conjunto de dos miembros, conservando la separación geométrica y el precio agregado.
- PASS: fixture de **mesa + cuatro sillas realmente asociadas** por `RestaurantSeatingTopologyService`. Se guarda y recarga el JSON, se libera temporalmente el espacio original con el servicio oficial de borrado grupal, se reconstruyen los cinco en el mismo sitio desde la plantilla. El nuevo grupo presenta cuatro sillas asociadas a la **mesa nueva**, no a la original. Undo/Redo y reversión de ambas transacciones dejan 38 muebles e historial idénticos a los originales.
- PASS: 2 copias con huella coincidente rechazadas, con 0 creadas y 0 mutaciones persistentes.
- PASS: 65 miembros rechazados por límite.
- PASS: 64 miembros provisionales con geometría inviable rechazados, 0 nuevos, 0 historial; **99 ms** en esa ejecución. No equivale a un test de colocación válida de 64.
- PASS: economía de fallo inyectado en el **tercer** miembro del conjunto de cinco. Se verificaron 3 intentos, 2 cargos autorizados y 2 compensaciones, 0 miembros colocados y sin nueva entrada de historial. El gate original se restablece antes de la colocación normal.
- PASS: biblioteca JSON corrupta no se sobrescribe, permisos de modo edición, consultas de ID no existente.

## Límites de la evidencia

- No hay aún una prueba automatizada de **Save/Load del mundo de juego** con los cinco nuevos muebles confirmados; la persistencia comprobada es la biblioteca de plantillas, junto con el historial y las autoridades de colocación existentes.
- No se ha validado una composición válida de 64 artículos en un restaurante real, solo un rechazo de 64; no prometer ausencia universal de picos.
- Falta interfaz visual aprobada (Galería Viva) en las pantallas definitivas, con responsividad y pruebas manuales en Windows 1920×1080 / 1280×720. No se creó una UI gráfica improvisada.
- La prueba destructiva de Economía utiliza un gate controlado de QA para garantizar el fallo en el tercer cargo, no un déficit real de caja configurado en la escena. Confirma reversión transaccional en ese escenario.

La implementación respeta el trabajo paralelo de UI/arte/otras ramas; solo modifica el núcleo B12 y su QA.
