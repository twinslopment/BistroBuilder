# Opciones — definición funcional por categorías (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Opciones → Partida: PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN**. Pendientes de definición/ratificación: **Audio, Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal**.
**Alcance:** aprobación de funciones; no cambiar código de Unity ni rediseño visual en este paso. El diseño definitivo debe conservar la barra superior V21, inferior E5.5 del Modo Juego y el sistema visual global premium.

## Estructura real comprobada

El componente `Assets/Scripts/Presentation/UI/BistroBuilderOptionsScreen.cs` contiene **nueve categorías existentes**:
1. **Partida** — guardar, cargar, autoguardado, salir y submenú de pausa.
2. **Audio** — volumen general.
3. **Vídeo** — pantalla completa/ventana, VSync, límites de 30/60/120 FPS.
4. **Jugabilidad** — pausar mientras se abre Opciones y cambiar velocidad de simulación ×1/×2/×3.
5. **Interfaz** — reducir movimiento de botones e iconos.
6. **Controles** — guía de uso de ratón y teclado; no hay reasignación.
7. **Accesibilidad** — movimiento reducido y explicaciones de foco/estados.
8. **Idioma** — español; no existe selector multilingüe funcional.
9. **Créditos / Legal** — motor, fuentes, licencias, versión.

La pantalla se abre como overlay propio; al abrir normalmente selecciona **Partida**. `docs/OpcionesYCarta.md` describe también las pruebas históricas `BistroBuilderOptionsPlayTest.RunBatch`, pero no se han ejecutado aquí ni acreditan integración visual definitiva.

## Opciones → Partida — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** permitir al jugador gestionar con claridad su progreso sin perder la partida por una acción accidental.

1. **Guardar manualmente la partida actual**, a través del servicio canónico `BistroBuilderSaveGameService`, en una ranura nueva o existente. Mostrar la identificación del restaurante y la marca de fecha/hora de guardado si el resumen la proporciona; no inventar información del tiempo jugado que no exista.
2. **Consultar las partidas guardadas** con nombre visible, ranura y fecha/hora cuando exista; presentar correctamente un listado vacío cuando no haya partidas recuperables.
3. **Sobrescribir con confirmación explícita** una ranura manual ocupada, advirtiendo de que será reemplazada. No sobrescribir automáticamente otras ranuras ni usar ranuras indisponibles.
4. **Cargar una partida con confirmación explícita**, avisando de que los cambios no guardados en la partida actual se perderán; no cambiar el estado del restaurante hasta que la operación de carga sea aceptada por el servicio.
5. **Mostrar progreso y resultado** de guardado/carga con mensajes de proceso, porcentaje cuando exista, éxito o fallo. Evitar repetir órdenes durante operaciones ocupadas, y bloquear salir mientras el servicio guarda/carga, según las protecciones ya existentes.
6. **Autoguardado opcional**, inicialmente **desactivado**, cada **cinco minutos reales** desde el reloj no escalado, utilizando exclusivamente la **ranura 999**, separada de las manuales. Nunca sobrescribir guardados manuales.
7. **Aplicar las restricciones reales del autoguardado:** no ejecutarlo mientras se muestran Opciones, una pantalla de nueva partida/apertura, o si el servicio está ocupado; respetar validaciones del sistema de persistencia. Aclarar que esta función es por intervalo real, no por días del restaurante.
8. **Consultar y cargar el autoguardado** cuando esté disponible; distinguirlo visualmente de los guardados manuales y no presentarlo como un guardado sin condiciones.
9. **Volver a la partida sin guardar** mediante el botón Cerrar/Continuar o `Esc` dentro de Opciones. Si la opción correspondiente está activada (por defecto sí), la simulación se pausa mientras el overlay de Opciones está abierto y se reanuda cuando este libera su bloqueo. No confundir esta pausa con la velocidad normal de simulación.
10. **Mostrar un menú de pausa sencillo** con **Continuar**, **Cargar partida**, **Opciones** y **Salir**; mantener el retorno al juego mediante acción clara.
11. **Confirmar siempre la salida del juego**, con aviso de que los cambios desde el último guardado se perderán. Si el guardado o carga sigue ocupando el servicio, no autorizar una salida accidental a mitad de la operación.
12. **Distinguir «Menú principal» de «Menú de pausa».** El botón histórico «Volver al menú principal» actualmente ejecuta `ShowPage(-1)` y presenta **un menú dentro de la misma pantalla de Opciones**, **no navega a otra escena ni crea una nueva partida**. No llamar «menú principal» a una escena nueva ni prometer función que el código no cumple: propone renombrarlo «Volver al menú de pausa» o implementar posteriormente la navegación a la escena principal de verdad, como decisión independiente.
13. **Seguridad del progreso:** no implementar atajos de reiniciar partida, borrar ranuras, restaurar de fábrica, cerrar con operación pendiente, guardado rápido sin feedback o un sistema de guardado paralelo sin aprobación y validación expresa.

### Código revisado y mejoras de UX pendientes

- `BistroBuilderOptionsScreen.ShowPage(0)` abre **Partida** con Guardar, Cargar, autoguardado, «Volver al menú principal» y Salir; `ShowPage(-1)` dibuja el menú de Continuar/Cargar/Opciones/Salir en el mismo overlay.
- `ListSlots(bool save)` consulta `BistroBuilderSaveGameService.ReadSlotSummariesAsync`, muestra nombre, ranura y hora local; exige confirmación para **sobrescribir** y **cargar**. La creación de un guardado en ranura libre inicia la petición directamente, sin confirmación redundante.
- `RunSave` usa `TrySaveSlot`/`TryLoadSlot`; `Saved` atiende al evento `OperationCompleted` y refleja resultado o error.
- `Awake/Update` inicializan autoguardado cada **300 segundos con `Time.unscaledTime`**, activable mediante `PlayerPrefs` (valor inicial **0**, desactivado), y escriben exclusivamente en ranura **999**. No ejecuta cuando Opciones está abierto, al mostrar apertura de nueva partida ni si `SaveGameService.IsBusy`.
- `Open` puede adquirir un `GameClock.AcquireSimulationLock("Opciones")` (valor por defecto **activado**) y `Close` libera el bloqueo. La opción se configura en Jugabilidad; la pantalla de Partida no debe duplicar este ajuste.
- `ConfirmExit` pide confirmar y llama a `Application.Quit()`, tras comprobar que no hay guardado o carga en curso.
- **Mejoras que requieren programación/UI:** etiquetado inequívoco de la ranura de autoguardado frente a manual, denominación correcta del menú de pausa frente a escena principal, texto y separación visual de acciones destructivas/seguras, adaptación de la pantalla al sistema visual definitivo y pruebas de guardado/carga de la build actual.
- **No hay acción real de «Nueva partida» dentro del menú de pausa `ShowPage(-1)`**. No incluirla ni prometer transición a una pantalla principal real sin una decisión/implementación adicional.

### Límites propuestos

No tocar lógica de `BistroBuilderSaveGameService` ni sobrescribir ranura 999 desde la selección manual. No ofrecer nube, múltiples perfiles de usuario, logros, backups remotos, borrado de partidas, importación/exportación o recuperación garantizada de guardados dañados si no existe soporte comprobado. Todos los cambios que impliquen perder progreso requieren confirmación clara.

## Próximo paso

Solicitar aprobación de **Opciones → Partida**. Después continuar con **Opciones → Audio**, siguiendo las nueve categorías existentes y registrando cada decisión. El rediseño gráfico se realizará separadamente.
