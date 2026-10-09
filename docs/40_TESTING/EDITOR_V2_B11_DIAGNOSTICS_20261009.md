# Editor V2 — B11: diagnóstico del restaurante

**Estado: B11 PASS técnico — 2026-10-09, commit en `feature/editor-v2`.** La presentación IMGUI es provisional y el cálculo total de rutas conserva la limitación de rendimiento documentada.

## Alcance técnico

Diagnóstico explícito, **solo lectura**, del restaurante antes del servicio. Cinco capas filtrables: Circulación, Accesibilidad, Capacidad, Interacción e Incidencias de distribución. B11 no implementa otro sistema de navegación, capacidad ni espacio: reutiliza autoridades existentes y produce resultados homogéneos con severidad, identificador único, explicación, medida recomendada y coordenadas cuando la autoridad ofrece la localización.

Autoridades:
- **BBSIS**: `BistroBuilderSpatialAssessmentService.EvaluateCurrentLayout`, `LastLedger.records`; roles `MobilityEnvelope`, `CarryEnvelope`, `TraversalGate`, `SeatBay`, `Approach`, `WorkZone`, etc.
- **Navigation**: `BistroBuilderNavigationService.ScanCirculationIncrementally(...)` llama a las mismas comprobaciones privadas `CheckConnection` que `EvaluateCirculationHealth()`, repartiendo las rutas cliente/entrada, cocina/mesa y almacén entre pasos; ningún cambio en el algoritmo de rutas.
- **Placement**: `RestaurantPlacementRegistry.RegisteredFootprints`, `RestaurantPlacementValidationService.ValidateCurrentPlacement`; problemas de área, colisión, separación y restricciones.
- **Seating**: `RestaurantSeatRegistry.RegisteredSeatCount`, `RestaurantSeatingTopologyService.UnassociatedSeatCount`.
- **Architecture**: `BistroBuilderEditNavigationValidationProvider.Validate` sobre `BistroBuilderEditDocumentRuntimeService.GetCommittedSnapshot()`, sin publicar cambios.

El componente `BistroBuilderEditorV2DiagnosisService` no utiliza `Update` ni `InvokeRepeating`: analiza bajo demanda con `TryScan(layers, out report, out error)` y, si se solicita circulación, utiliza una única corutina de revisión progresiva. Se cancela al cerrar, cambiar filtros, desactivar el componente o salir de edición. Rechaza peticiones fuera del modo edición o con capas vacías. Los hallazgos repetidos se deduplican por ID y se ordenan de forma determinista (gravedad, capa, ID). Una autoridad ausente se marca explícitamente como *no evaluada*: **nunca equivale a un diagnóstico sin problemas**.

## Acceso de jugador

El `BistroBuilderEditorV2DiagnosisOverlay` se instala idempotentemente en la escena de Editor V2, se muestra con **F8** y contiene capas y botón **Analizar**. Permite consultar explicaciones, soluciones y coordenadas, además de pequeños indicadores sobre la vista 3D para hallazgos localizados. No modifica ni selecciona objetos del mundo, y cerrar el panel deja el diagnóstico inactivo.

**Límite UX:** es una capa funcional de Unity IMGUI para poder comprobar B11, **no el diseño gráfico definitivo Galería Viva**; trasladar el aspecto a la interfaz aprobada de Editor V2 será una tarea visual separada. No altera `docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md`.

## Tests y garantías

- **B11 QA 33/33 PASS**, evidencia `docs/40_TESTING/EDITOR_V2_B11_UNIT_PASS.txt`; QA de DTO reales de BBSIS, Navigation y Seating: paso bloqueado, cocina/mesa sin ruta, sillas sin mesa, sala sin asientos; severidad y soluciones correctas, localización si existe, deduplicación y filtros.
- QA sobre escena real en Editor: entrada en modo edición, rechazo de análisis fuera de edición, falta de capas, idempotencia bootstrap y panel, bloqueo de análisis de fondo y restauración intacta de historia/poses.
- **Play Mode real PASS**, evidencia `docs/40_TESTING/EDITOR_V2_B11_RUNTIME_PASS.txt`; QA con colocables registrados: identidad y pose de **38 objetos**, rutas de navegación, verificación del historial y el registro de plazas, informe completo con todas las capas y otro filtrado a Layout; cronometraje de análisis explícito.
- **Regresión B4/B5/B8/B9/B10 252/252 PASS, exit 0**, evidencia `docs/40_TESTING/EDITOR_V2_B11_REGRESSION_PASS.txt`; protege historial, transacciones de reforma, multiselección, catálogo y sustitución. **Incidencia conocida:** en ocasiones el banco B8 agota la búsqueda de 100 parejas duplicables sin encontrar hueco (`FootprintOutsideCandidateArea`); el mismo banco puede pasar en ejecución aislada y en repetición conjunta. No se cambian las reglas del motor ni se oculta el fallo; puede requerir estabilizar su fixture de QA.
- **Build Windows x64 PASS** — `Logs/EditorV2_B11_FinalWindowsBuild.log`: `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, exit 0, 13 avisos no bloqueantes, 181376327 bytes según la salida de la build. Ejecutable: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

**Evidencia medida (Play Mode, escena real, 2026-10-09):** 38/38 artículos examinados y preservados, 21/21 rutas revisadas, sin cambios de historial, selección de capas correcta, cancelación al cerrar PASS. Diagnóstico inicial **1501 ms**, frente a **28320 ms** de la primera versión síncrona. Revisión exhaustiva de Navigation **29175 ms acumulados en 20 pasos**, no instantánea: pueden producirse pausas perceptibles mientras se revisan rutas. Esta limitación del A* de respaldo queda registrada; no se presenta el trabajo acumulado como 29 segundos ganados. El diagnóstico inicial y las capas distintas de Navigation son rápidos; las rutas progresivas muestran estado **parcial** hasta terminar y jamás anuncian falsamente que todo está correcto.

Informes automáticos generados en raíz del worktree: `EditorV2_B11_Diagnosis_Report.txt`, `EditorV2_B11_RuntimePlay_Report.txt`; logs de Unity en `Logs/EditorV2_B11_*.log`. Los informes versionados que acompañan a este documento se identifican por su nombre de cierre.

## Convivencia / seguridad

Solo rama `feature/editor-v2`, commit exclusivo de archivos B11. No merge a la rama `integration/master-current-20260918` sin integración con el diseño visual y coordinación de trabajo concurrente. No tocar `Assets/Resources/BistroBuilder/UI/Typography`, iconografía ajena ni ajustes gráficos compartidos.
