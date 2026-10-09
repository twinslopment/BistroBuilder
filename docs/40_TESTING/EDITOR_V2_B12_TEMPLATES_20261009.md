# Editor V2 — B12 Plantillas y composiciones: núcleo técnico

**Fecha:** 2026-10-09
**Rama:** `feature/editor-v2`
**Estado:** NÚCLEO IMPLEMENTADO Y PROBADO EN PARTE; NO CERRAR B12 COMPLETO AÚN.

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

**Build Windows x64:** `Logs/B12_FinalWindowsBuild.log`, `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, exit 0, 13 avisos de compilación no bloqueantes; ejecutable `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

## 3. Límites que impiden declarar el bloque totalmente cerrado

1. **No se ha validado todavía la colocación real de un conjunto complejo con 4 sillas + mesa y sus vínculos funcionales**. La prueba de 2 miembros verifica guardado y geometría, no implantación física simultánea. La operación ya se apoya en el enlace de grupos de B8, pero hay que probar las reglas reales de Seating/BBSIS cuando se activan todos los miembros.
2. **UI visual definitiva pendiente**. No se han creado paneles IMGUI improvisados; catálogo, capturar/colocar/guardar, previsualización, avisos de precio y área deben integrarse según el diseño visual Galería Viva en el chat de Editor V2.
3. No se ha medido aún el rendimiento de plantillas de 64 artículos ni la prueba destructiva de presupuesto agotado durante la transacción. Requiere fixture separado y rollback efectivo.
4. Con 38 muebles de la escena original, el controlador de selección antiguo no acepta seleccionar algunos objetos que B8 sí registra; la prueba usa la selección canónica de B8. No se debe afirmar que el flujo visual funciona con todos los muebles de la escena.

## 4. Criterio de cierre B12

Completar pruebas **mesa + cuatro sillas** con vinculación real antes/después de Save/Load, costes y Undo/Redo de los cinco, colocación en área válida, rollback de colocación inválida, deshacer después de agotar presupuesto, 64 miembros sin picos, y posterior integración de UI responsive. Sin esos resultados el estado es **EN DESARROLLO (núcleo)**.

No se fusiona en `integration/master-current-20260918` ni se tocan iconos, tipografía o assets visuales de otros agentes.
