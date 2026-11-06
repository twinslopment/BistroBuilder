# Comprobación en ejecutable Windows: V21 + E5.5 — 06/11/2026

## Fuente real y alcance

- Rama: `feature/ui-bottom-e55-20261105`, basada en la barra superior V21 aprobada.
- Unity: 6000.3.19f1.
- Build Windows Standalone: `Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe`.
- Compilación estándar **PASS**: `Logs/E55_StandaloneBuild.log` contiene `BB_PLAYTEST_BUILD_PASS`; 10 advertencias no bloqueantes.
- Build de verificación **PASS**: `Logs/E55_StandaloneBuild_QA.log`, mismo proyecto y escena `Prototype_Restaurant`, con opción de diagnóstico `-bb-e55-visual-qa`.
- Captura a 1920 × 1080 obtenida desde la pantalla principal con el juego realmente ejecutándose: `docs/Images/UI/BottomE55/E55_Standalone_Build_1920x1080_20261106.png`. **No es imagen generada, ni mockup, ni captura de Editor**.

## Resultado visual verificado

- **Ambas barras están presentes simultáneamente**: V21 arriba, E5.5 abajo.
- **E5.5**: se muestran Caja, Satisfacción, Climatología y Velocidad sin superposición entre sus cuatro módulos; pausa y botones 1×/2×/3× visibles.
- **V21**: se muestran los diez accesos de navegación en la barra superior, pero **faltan sus iconos en el ejecutable**. Es un defecto abierto: `BBIconCatalog` existe en el runtime y `BBIconId.NavActivity` tiene asignado `layout-gridSprite`, con el componente `NavigationIcon` habilitado y alpha 1.00. La ausencia no se debe a un catálogo de iconos completamente ausente; falta investigar la causa de renderizado.
- Al abrir la build normalmente, aparecen el menú `Nueva partida` y la franja `CONSTRUCCIÓN`. Estos elementos se superponen al juego e impiden evaluar visualmente las dos barras sin molestias. El argumento **solo de diagnóstico** `-bb-e55-visual-qa` desactiva las dos capas de IMGUI en esta ejecución; no modifica la presentación ni las reglas normales si se omite el argumento.
- **Partida existente**: `Continuar` no ha funcionado en esta rama porque un guardado contiene `restaurant.structure` versión 2, posterior a la soportada. Se creó copia de seguridad antes de intentar la carga y no se ha modificado el guardado original. No realizar migración forzada.
- La captura con bandera QA presenta el estado de preparación de servicio, **no** una partida cargada ni un servicio económico completo. Sus indicadores ilustran el estado real sin datos de ventas.

## Próximos problemas pendientes

1. Investigar y corregir la ausencia de iconos V21 en la build (los sprites existen, pero no se muestran).
2. Validar la barra superior e inferior durante una partida normal compatible, sin overlays de arranque.
3. Comparar la E5.5 visual con su referencia artística antes de aprobar el diseño definitivo.

El parámetro QA está implementado en `Assets/Scripts/Presentation/UI/BistroBuilderE55StandaloneVisualQa.cs`. Está inactivo por defecto y no escribe ni reemplaza partidas guardadas.
