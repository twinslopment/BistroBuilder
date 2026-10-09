# Navigation 17 / B11 — Optimización de cálculo exhaustivo
**Fecha:** 2026-10-09
**Rama:** `feature/editor-v2`
**Ámbito:** motor real de Navigation 17, sin cambiar reglas de paso, colisiones, movilidad ni elección de mejor ruta.

## 1. Diagnóstico del cuello de botella

El test Play Mode de B11 (38 colocables; 21 conexiones entre entrada, mesas, cocina y almacén) tardó **29.175 ms acumulados de Navigation** antes de esta intervención. Perfil por ruta: ocho conexiones invocaban `TryBuildOperationalDockRoute`, que probaba 80 anclajes alternativos además del primer intento de ruta, llegando a **81 llamadas A*** por conexión.

La primera caché local de celdas **no bastó**: una iteración tardó 37.411 ms bajo carga. No se aceptó como solución.

## 2. Mejoras aceptadas y fundamento de equivalencia

1. **Caché local del A***: el predicado estático de transitabilidad para cada celda se evalúa una vez dentro de la misma consulta. Conserva el trato especial del destino, el chequeo de segmentos y la semántica exacta de radio/zona.
2. **Cota inferior admisible** para cada dock: `distanciaRecta(origen, candidato) + 0,35 × distanciaRecta(candidato, destino)`. La longitud de cualquier ruta real es mayor o igual a su distancia recta, y la penalización de congestión no es negativa; si esta cota supera el mejor resultado ya encontrado, buscar la candidata es inútil.
3. **Ordenación de los 80 docks por cota inferior**, conservando el orden original como desempate. La búsqueda se detiene únicamente cuando las candidatas restantes ya no pueden mejorar la puntuación ganadora. No se elimina una candidata que podría mejorar el resultado.
4. **Caché estructural de consulta de docking** con clave exacta `(posición Vector3, radio, agente)`. Se aplica solo al predicado independiente del destino y **nunca** a la excepción de cercanía al extremo. Se inicializa y vacía dentro de un `try/finally` por operación, sin utilizar geometría de consultas anteriores. Límite de memoria: 65.536 claves.

No se redujeron los límites del A*, la precisión de cuadrícula, la distancia de muestreo, las restricciones ni el número total de candidatas consideradas matemáticamente posibles.

## 3. Evidencia de resultado real

| Prueba | Anterior | Optimizado | Integridad |
|---|---:|---:|---|
| Diagnóstico B11, 21 rutas de restaurante | 29.175 ms | 2.462 ms | 21/21, 38 muebles intactos, historial intacto |
| Equivalencia exacta Play Mode, 37 casos | 41.446 ms | 3.107 ms | 37/37 PASS |
| Tres repeticiones completas B11, 21 rutas | — | 2.983 / 2.701 / 2.951 ms | 3/3 PASS; 21/21 rutas en cada ejecución |

**Equivalencia ejecutable:** `BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest.RunFromCommandLine`. Compara, para cada uno de los 21 recorridos reales y 16 variantes de destino/radio, ambos motores en el mismo fotograma: alcanzabilidad, `BistroBuilderNavigationRouteKind`, longitud (±0,001 m), número de puntos y la posición de **cada** waypoint (±0,001 m). 37 coincidencias y ninguna discrepancia; no se desplazó mobiliario ni cambió la revisión de navegación. Evidencia versionada: `docs/40_TESTING/EDITOR_V2_B11_NAV_EQUIVALENCE_PASS.txt`, Unity log `Logs/B11_NavEquivalence_37Cases.log`. Las tres repeticiones se registran en `docs/40_TESTING/EDITOR_V2_B11_NAV_OPT_THREE_RUNS.txt`; mediana Navigation 2.951 ms, máximo 2.983 ms en esas ejecuciones, sin mutaciones ni rutas omitidas.

**Pruebas de componentes:** `BistroBuilderNavigationV1CoreSelfTest` **44/44 PASS** y `BistroBuilderNavigation17SelfTest` **22/22 PASS**. `BistroBuilderNavigation17PlayModeSelfTest` y `BistroBuilderNavigation17SaveLoadSelfTest`, ambos exit code 0. **Regresión Editor V2 B4–B10: 267/267 PASS, exit 0** (B4 63, B5 48, B8 74, B9 42, B10 40); evidencia `docs/40_TESTING/EDITOR_V2_B11_NAV_OPT_REGRESSION_PASS.txt`. **Build Windows x64 PASS**, `Logs/B11_NavOptimize_FinalWindowsBuild.log`, `Build Finished, Result: Success`, `BB_PLAYTEST_BUILD_PASS`, tamaño 181379399 bytes, 13 warnings no bloqueantes. Ejecutable `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.

### Prueba de estrés 50 NPC: incidencia diferenciada

`BistroBuilderNavigation17StressSoakSelfTest` no ha pasado. Primera ejecución optimizada: 48 agentes activos al agotar drenaje, 1 interbloqueo. Comparativa con búsqueda histórica (interruptor exclusivo `UNITY_EDITOR`, restablecido después): **también falló**, con 15 agentes activos y ningún interbloqueo. Repetición optimizada: **también falló**, con 34 agentes activos, 0 interbloqueos; no hubo errores explícitos de ruta (`failed=0`) pero el tráfico no terminó. Evidencias versionadas: `docs/40_TESTING/NAVIGATION17_STRESS50_LEGACY_FAILURE_20261009.txt` y `docs/40_TESTING/NAVIGATION17_STRESS50_OPTIMIZED_REPEAT_20261009.txt`. Por tanto **no existe evidencia para afirmar que el problema sea provocado exclusivamente por la optimización**, pero tampoco para declarar estable el tráfico de 50 NPC. Es una incidencia abierta de convergencia/replanificación del sistema de agentes. No se modificó el banco de estrés ni se rebajaron criterios para lograr un PASS artificial.

### Restricciones conocidas

B11 reparte por corutina las 21 verificaciones y cancela al cerrar; la optimización acelera la propia autoridad Navigation y reduce los picos, pero las mediciones de una escena no equivalen a una garantía universal para cualquier layout. Continúan pendientes pruebas gráficas manuales a las resoluciones definitivas y el traslado del panel provisional a Galería Viva.

## 4. Reproducción

Unity 6000.3.19f1, `Prototype_Restaurant.unity`, Editor V2. Métodos CLI verificables:

- `BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest.RunFromCommandLine` (**sin** `-quit`, entra y sale de Play Mode).
- `BistroBuilderEditorV2B11RuntimePlayModeSelfTest.RunFromCommandLine` (**sin** `-quit`).
- `BistroBuilderNavigationV1CoreSelfTest.RunFromCommandLine` y `BistroBuilderNavigation17SelfTest.RunFromCommandLine` (con `-quit`).
- `BistroBuilderNavigation17PlayModeSelfTest.RunFromCommandLine` y `BistroBuilderNavigation17SaveLoadSelfTest.RunFromCommandLine` (sin `-quit`).

No se ha tocado la rama `integration/master-current-20260918` ni archivos de fuentes/iconografía de otros agentes.
