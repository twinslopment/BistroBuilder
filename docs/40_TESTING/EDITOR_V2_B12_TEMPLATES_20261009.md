# Editor V2 — B12 Plantillas y composiciones: núcleo técnico

**Fecha:** 2026-10-09
**Rama:** `feature/editor-v2`
**Estado:** NÚCLEO TÉCNICO VALIDADO EN PLAY MODE; integración gráfica final pendiente. QA extendida: `EDITOR_V2_B12_HARDENING_20261009.md`.

## 1. Qué se ha implementado

- `BistroBuilderEditorV2TemplateLibrary` guarda una selección de 1 a 64 artículos en una biblioteca independiente de la partida. Solo serializa `itemId` canónico, posición relativa al centro de la composición y rotación de cada miembro. Nunca almacena ni duplica las mallas, prefabs, texturas, ni identificadores de instancias originales. La misma plantilla puede usarse en otras partidas.
- Persistencia versionada **JSON schema=1** bajo `Application.persistentDataPath/EditorV2/editor_v2_templates_v1.json`. Escribe primero fichero temporal y utiliza sustitución del archivo con copia `.bak` en Windows. Biblioteca corrupta o de versión desconocida: modo seguro, bloquea escritura, conserva el original. Hasta 100 plantillas, con GUID por plantilla.
- Consultas `TryFind` devuelven copias profundas. `TryQuote` resuelve el catálogo y suma el coste en céntimos, rechazando artículos descatalogados antes de colocar.
- `TryPlace` resuelve todos los IDs y sus posiciones/rotaciones respecto al pivote. La nueva operación `RestaurantPlaceableCreationService.TryCreateTemplateBatch` recorre **las mismas autoridades reales** de lifecycle, validación espacial, Finanzas e historial que B8; valida todos los miembros provisionales antes de activarlos y revierte todo si falla una activación, coste o comando histórico.
- Instalación idempotente en `BistroBuilderEditorV2RuntimeBootstrap`; prohibidas las operaciones fuera del modo edición.

## 2. Evidencia de Play Mode

Informe: `docs/40_TESTING/EDITOR_V2_B12_RUNTIME_PASS.txt` (copia versionada de `EditorV2_B12_Runtime_Report.txt`). **Regresión completa Editor V2 B4–B10: 267/267 PASS, exit 0** (evidencia `docs/40_TESTING/EDITOR_V2_B12_REGRESSION_PASS.txt`). Banco: `BistroBuilderEditorV2B12RuntimeSelfTest.RunFromCommandLine`, en `Prototype_Restaurant.unity`, con 38 muebles reales y una biblioteca temporal de QA aislada del usuario.

Verificado:
- rechaza guardar fuera de edición;
- guarda y relee archivo real con artículo canónico y presupuesto;
- coloca una instancia independiente de una mesa del restaurante en espacio real válido, preserva pose y genera ID nuevo; rechaza otras posiciones cuando bloquean uso, no desactiva validación;
- deshacer, rehacer y deshacer dejan 38 muebles y el historial exactamente como al principio;
- dos muebles seleccionados: guardar/cargar y preservar distancia relativa y coste conjunto, **sin crear copias al guardar**;
- borrar una plantilla y volver a cargar confirma su eliminación;
- biblioteca JSON corrupta: no sobrescribe los datos y comunica error.

**Build Windows x64 (hardening final):** `Logs/B12_Hardening_FinalWindowsBuild.log`, `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, exit 0, 13 avisos de compilación no bloqueantes; ejecutable `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`. **Regresión B4–B10 267/267 PASS** con los reintentos necesarios por concurrencia de Unity documentados en `EDITOR_V2_B12_HARDENING_REGRESSION_PASS.txt`. **Save/Load general real PASS** (`EDITOR_V2_B12_SAVELOAD_AUTHORITY_PASS.txt`).

## 3. Límites que impiden declarar el bloque totalmente cerrado

1. **Mesa y cuatro sillas: PASS en Play Mode real.** Se captura la composición existente, guarda/carga JSON, borra temporalmente la fuente mediante la autoridad B8, coloca cinco instancias nuevas en el mismo espacio y reconstruye las cuatro asociaciones a la nueva mesa. Undo/Redo y recuperación del original: PASS, 38 muebles intactos.
2. **Rollback económico: PASS.** Fallo inyectado en el tercer cargo después de dos pagos; 2/2 reembolsos, sin miembros ni historial nuevos.
3. **64 componentes: prueba limitada.** El conjunto de 64 piezas con huellas coincidentes es rechazado en 99 ms, sin mutaciones. No equivale a aceptar ni medir una composición de 64 colocaciones válidas.
4. **Persistencia del mundo no ensayada específicamente para los cinco objetos nuevos después de Save/Load completo.** La biblioteca se recarga y la propia colocación/Undo/Redo están validados. El sistema general de guardado tiene pruebas previas independientes.
5. **Interfaz visual definitiva pendiente.** No se han creado paneles IMGUI improvisados. Capturar/guardar/colocar con previsualización, presupuesto, validaciones y responsividad debe diseñarse según Galería Viva.
6. El controlador de selección legado de la escena de pruebas no selecciona todos los objetos; QA usa el Selection Set canónico de B8. No afirmar que todas las interacciones visuales están listas.

## 4. Criterio de cierre B12

**Cierre técnico condicionado a regresión y build final:** la prueba de mesa y cuatro sillas, las relaciones, los cambios financieros con rollback, la persistencia de la biblioteca y el Undo/Redo cumplen el criterio de motor. Queda pendiente una prueba Save/Load específica del mundo con los cinco nuevos muebles, el caso válido de 64 componentes y la aprobación de UI Galería Viva. No declarar terminado el apartado visual hasta que exista interfaz responsive y pruebas manuales.

No se fusiona en `integration/master-current-20260918` ni se tocan iconos, tipografía o assets visuales de otros agentes.
