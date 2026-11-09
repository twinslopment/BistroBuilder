# Opciones — definición funcional por categorías (Modo Juego)

**Fecha:** 09/11/2026.
**Estado:** **Opciones → Partida y Audio APROBADAS FUNCIONALMENTE (09/11/2026)**. **Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal: PROPUESTAS FUNCIONALES DOCUMENTADAS EN BLOQUE, PENDIENTES DE APROBACIÓN**. No se ha modificado Unity ni realizado rediseño visual.
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

## Opciones → Partida — FUNCIONALIDADES APROBADAS

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

### Límites aprobados

No tocar lógica de `BistroBuilderSaveGameService` ni sobrescribir ranura 999 desde la selección manual. No ofrecer nube, múltiples perfiles de usuario, logros, backups remotos, borrado de partidas, importación/exportación o recuperación garantizada de guardados dañados si no existe soporte comprobado. Todos los cambios que impliquen perder progreso requieren confirmación clara.

## Opciones → Audio — FUNCIONALIDADES APROBADAS

**Objetivo:** dar al jugador control directo y comprensible sobre el sonido general de Bistro Builder en PC, sin fingir canales o contenidos sonoros que todavía no están implementados.

1. **Control de volumen general entre 0 y 100 %**, mediante un deslizador continuo con el porcentaje numérico visible. La escala visual corresponde al rango real `AudioListener.volume = 0..1`.
2. **Aplicar el cambio inmediatamente** mientras el jugador mueve el deslizador, sin necesitar un botón «Aplicar» ni reiniciar la partida. Esto controla todos los sonidos que pasen por el `AudioListener` de Unity; no inventar mezclas por canal.
3. **Volumen inicial al 100 %** cuando el usuario no haya modificado la opción previamente. Al volver a entrar, el deslizador debe reflejar la preferencia guardada en lugar de volver a un valor predeterminado.
4. **Permitir silencio total ajustando el volumen a 0 %** y recuperarlo al mover de nuevo el control. Un botón de silenciar/desilenciar independiente sería una mejora adicional que requeriría comportamiento de restauración y prueba, no imprescindible en esta fase.
5. **Guardar la preferencia por instalación/equipo** con la clave `BB.Options.Volume` de `PlayerPrefs`, independiente de las partidas guardadas; así cargar otra partida no debería cambiar el volumen elegido en el equipo. Garantizar que se persiste en el cierre normal mediante `PlayerPrefs.Save()` y que se aplica en `Awake`.
6. **Mostrar claramente que es un volumen maestro**, no una mezcla de música/efectos/ambiente ni ajuste de volumen del sistema operativo. Respetar el volumen y dispositivo elegidos por el usuario en Windows.
7. **Permitir uso con ratón y teclado** mediante el control de Unity y proporcionar respuesta visual clara de valor actual, foco e interacción. El perfeccionamiento de accesibilidad/estilo corresponde a su sección y al diseño global.
8. **No prometer escucha de muestra ni reproducción de prueba** si no existe un recurso sonoro instalado y comprobado. Cuando se incorpore audio real, evaluar una muestra reproducible y pruebas auditivas para verificar que el volumen funciona en la build.
9. **Restablecer al 100 %** puede ofrecerse como acción explícita de comodidad en una futura adaptación de UI, con cambio inmediato y persistido; la pantalla actual **no incorpora ese botón**, por lo que se trata de mejora propuesta, no funcionalidad ya presente.

### Estado técnico verificado

- `BistroBuilderOptionsScreen.ShowPage(1)` construye una página con un solo `SliderRow("Volumen general", ...)`, con rango **0..1** y texto numérico **0..100 %**.
- Al mover el deslizador llama a `AudioListener.volume = value` y `PlayerPrefs.SetFloat("BB.Options.Volume", value)`. `Awake()` recupera la preferencia mediante `GetFloat(..., 1)`, y `Close()` llama a `PlayerPrefs.Save()`.
- La búsqueda en scripts versionados no mostró controladores de mezcla por grupos ni referencias a `AudioMixer`; tampoco aparecen recursos `.wav`, `.mp3`, `.ogg` o `.mixer` versionados en la ruta revisada. **La existencia de audio reproducible en una build o assets fuera del repositorio no queda acreditada por esta revisión.**
- No existe un control específico de **Música, Efectos, Ambiente, Voces** o **Dispositivo de salida** en la página actual. Añadirlos exigiría primero definir/implementar sus canales reales, conectarlos al control de audio y ejecutar pruebas.
- No se ha ejecutado Unity ni una prueba auditiva en esta fase; la aprobación funcional no valida sonido ni apariencia visual.

### Reglas de alcance aprobadas

No crear controles decorativos que no modifiquen un canal real, ni afirmar que hay banda sonora, sonidos de cocina, voces o volumen ambiental si aún no están integrados. La gestión de dispositivos, mezclas individuales, subtítulos, audio espacial, soporte para varios altavoces o prueba sonora se estudiará cuando existan requisitos y sistema de audio adecuados.

## Opciones → Vídeo — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** cambiar el modo de visualización y limitar el rendimiento de un juego de PC de forma sencilla, sin ofrecer botones de resolución/calidad que aún no controlan el motor.

1. **Pantalla completa** mediante el modo real `FullScreenMode.FullScreenWindow` (ventana sin bordes, no exclusiva). Mostrar al jugador que es pantalla completa sin bordes; aplicar al pulsar y conservar su preferencia.
2. **Modo ventana** mediante `FullScreenMode.Windowed`, con cambio inmediato. Evitar afirmar que existe selección de tamaño de ventana o resolución si no está programada.
3. **VSync activado/desactivado**, aplicando `QualitySettings.vSyncCount` a 1 o 0. Explicar que sincroniza la presentación al monitor y puede determinar los FPS reales aunque se haya elegido un límite diferente.
4. **Límite de FPS** seleccionable entre **30, 60 y 120**, valores ya implementados mediante `Application.targetFrameRate`. El jugador debe poder ver cuál está seleccionado: el código actual tiene botones y texto de estado pero no resalta de forma persistente el límite vigente.
5. **Conservar opciones por equipo** mediante `PlayerPrefs`: `BB.Options.FullScreen`, `BB.Options.VSync`, `BB.Options.FrameRate`, restauradas al iniciar. No guardarlas como propiedad económica o propia de la partida.
6. **Evitar conflictos con el modo Edición y otros límites internos**: existe un `BistroBuilderConstructionAuthoringPerformanceGuard` que puede limitar `Application.targetFrameRate` cuando no hay VSync. El valor solicitado por el jugador y la velocidad efectiva de cada modo deben reconciliarse y probarse; la UI no debe prometer «120 FPS garantizados».
7. **Controles accesibles, persistentes y sin duplicar**: la sección debe poder identificar ventana/pantalla completa, VSync y FPS actuales. Los nuevos controles de resolución, monitor, escalado, antialiasing, sombras, texturas o perfiles gráficos se estudiarán **solo tras programar los ajustes efectivos**.
8. **No presentar el selector de resolución como capacidad actual.** Si se implementa más adelante, enumerar únicamente resoluciones admitidas en el monitor y proponer confirmación/reversión de cambios que puedan dejar la imagen inaccesible.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(2)` ofrece dos botones (pantalla completa sin bordes/ventana), un toggle VSync y tres límites FPS. El `Awake` recupera preferencias si hay claves guardadas. Una opción de línea de comandos `-screen-fullscreen` puede prevalecer sobre la preferencia guardada de pantalla.
- No se encuentra `Screen.SetResolution` en los scripts revisados ni una interfaz para elegir monitor, resolución o calidad gráfica.
- `ToggleRow` guarda VSync en `PlayerPrefs`; `ShowPage(2)` y `Awake` aplican el valor. El código muestra un aviso sobre prioridad de VSync respecto al límite FPS.
- El resaltado permanente del valor elegido, la reconciliación con límites internos y una eventual confirmación/reversión al cambiar resolución requieren trabajo posterior; **no se ha probado la build aquí**.

## Opciones → Jugabilidad — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** ajustar el comportamiento temporal del juego sin provocar pérdida de progreso ni alterar la simulación mediante mecanismos ajenos al reloj canónico.

1. **Pausar automáticamente al abrir Opciones**, activado por defecto, mediante un bloqueo de simulación `GameClock.AcquireSimulationLock("Opciones")`. El jugador puede desactivar este comportamiento expresamente.
2. **Liberar el bloqueo propio de Opciones al cerrar**, sin forzar una reanudación si otro sistema (por ejemplo Guardado/Carga u otra pausa legítima) mantiene bloqueada la simulación. Los bloqueos del reloj son acumulables.
3. **Elegir velocidad de juego ×1, ×2 o ×3** mediante `GameClock.SetSpeedMultiplier`. La elección debe reflejar el mismo estado que los controles de velocidad **definitivos de la barra inferior E5.5**, sin cambiar sus iconos, animaciones ni normas aprobadas.
4. **Mostrar la velocidad vigente** y sincronizar los indicadores aunque el usuario la cambie desde la barra inferior. Elegir una velocidad no debe deshacer automáticamente una pausa del juego: velocidad y estado de pausa son cosas diferentes.
5. **Persistencia de velocidad según el estado real de la partida**: el esquema general de guardado ya contiene `clockSpeedMultiplier`; no tratar la velocidad como preferencia global de `PlayerPrefs` sin decisión adicional. En cambio, `PauseOptions` sí es preferencia de este equipo.
6. **No permitir valores arbitrarios de tiempo** (×10, simulación instantánea, reloj manual, avance de días o reinicio de servicio) desde esta pantalla. No duplicar el simulador ni introducir acciones de desarrollo/debug en el juego.
7. **Evitar confusiones de control**: botón de `Pausa` de la barra inferior, pausa temporal del overlay y velocidad seleccionada tienen funciones distintas. La UI futura debe mostrar su estado con claridad.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(3)` ya incluye el toggle `PauseOptions` (por defecto activado) y los tres botones de velocidad ×1, ×2 y ×3.
- `GameClock` posee `SpeedMultiplier` y `AcquireSimulationLock`, con varios bloqueos temporales independientes; `SpeedButtonController` lee el mismo reloj y escucha `SpeedChanged`.
- El selector de velocidad en Opciones actualmente **no refleja claramente la opción seleccionada** como un conjunto exclusivo ni identifica todos los bloqueos activos; sincronizar su presentación con E5.5 requiere integrar los componentes en la nueva UI.
- `BistroBuilderGeneralGameSaveSectionProvider` guarda/restaura el multiplicador temporal dentro del estado de la partida; no asumir que la preferencia persistente equivale al juego guardado.

## Opciones → Interfaz — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** controlar aspectos de presentación y animación de la interfaz sin deshacer la identidad artística definitiva de Bistro Builder.

1. **Reducir animaciones de botones e iconos**, con un interruptor `ReducedMotion` inicial desactivado, conservado mediante `PlayerPrefs`.
2. **Aplicar el cambio a las animaciones que ya consultan esa preferencia**, incluyendo `BBIconButton`, `BistroBuilderUiMotion` y `BistroBuilderV30ButtonMotion`, eliminando movimientos/escala decorativos cuando corresponda.
3. **Mantener información visual importante** aun con movimiento reducido: estados de selección, disponibilidad, confirmación, error y foco no deben depender exclusivamente de una transición animada.
4. **Preferencia única compartida con Accesibilidad:** el interruptor `ReducedMotion` aparece en ambas categorías, pero es **la misma clave**, no dos modos distintos. Cualquier cambio debe verse reflejado coherentemente en las dos páginas y no crear un estado duplicado. Recomiendo una sola autoridad del ajuste y acceso de lectura/control desde la otra categoría.
5. **Mantener intactas las barras V21 y E5.5 aprobadas** y su composición; reducir movimientos no equivale a reemplazar iconos, cambiar fuentes, alterar colores artísticos ni convertir el juego en una aplicación de gestión empresarial.
6. **No introducir todavía escalado general de UI, tamaño de letra, temas claro/oscuro, densidad configurable o recoloreo automático** sin una implementación global que respete 1920×1080 y 1280×720 y pruebas de legibilidad.
7. **Proporcionar confirmación visual accesible** para interruptores y opción elegida, guardando de inmediato la preferencia local.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(4)` crea un toggle `ReducedMotion`, además de notas informativas sobre borde de selección y foco de teclado.
- `BBIconButton`, `BistroBuilderUiMotion` y `BistroBuilderV30ButtonMotion` consultan `BistroBuilderOptionsScreen.ReducedMotion`. **No se ha comprobado que todos los elementos animados de todas las pantallas respeten la opción**: requiere auditoría de integración y pruebas visuales.
- La opción es también visible en **Accesibilidad** con exactamente la misma clave. Cambiar el estado en una pantalla y abrir la otra debe ofrecer lectura fresca de `PlayerPrefs`.
- Nada en el código revisado ofrece controles de tamaño de tipografía, escala global, color o idioma desde Interfaz.

## Opciones → Controles — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** mostrar instrucciones claras de ratón y teclado para las acciones disponibles en cada modo de Bistro Builder, sin prometer un sistema de reasignación de teclas que aún no existe.

1. **Guía consultiva de controles de PC**: seleccionar/colocar con clic izquierdo, arrastrar elementos cuando el modo lo permita, zoom con rueda, desplazamiento de cámara con botón central, giro con botón derecho y navegación básica por botones con teclado.
2. **Controles contextualizados por modo**: diferenciar la cámara y operaciones del **Modo Juego** de las herramientas del **Modo Edición**, porque no todas las acciones son posibles en ambas pantallas. Mostrar la disponibilidad real, no un listado universal ambiguo.
3. **Navegación de interfaz** con Tab / Mayús+Tab, activación con Intro y cierre de Opciones con Esc, siempre que los componentes realmente estén enlazados a la navegación del EventSystem.
4. **Leyendas de acciones importantes**, como pausa, velocidades, selección de objetos y confirmaciones, solo si los comandos han sido comprobados en los subsistemas correspondientes.
5. **No permitir reasignar teclas o mandos como función existente.** En esta versión la sección es solo informativa; remapeo, perfiles de control, compatibilidad con mando y restablecimiento de asignaciones se estudiarán cuando exista un mapa de acciones central compatible.
6. **Evitar instrucciones contradictorias con el Modo Edición**: cualquier cambio de entrada en cámara/editor debe actualizar la ayuda y validarse en una build, no quedar como texto fijo obsoleto.
7. **Legibilidad y uso con teclado** según el sistema visual global: organizar por categoría de acción y mostrar iconos discretos de teclado/ratón, no un catálogo interminable de combinaciones.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(5)` únicamente escribe instrucciones estáticas para clic izquierdo, arrastre, rueda, clic central/derecho, Tab, Mayús+Tab, Intro y Esc. **No modifica mapeos ni expone una pantalla de configuración de entradas**.
- `BistroBuilderProfessionalCameraInput` consulta botones reales del ratón mediante el nuevo Input System; la verificación por modo de todos los controles exige pruebas de integración.
- `BistroBuilderOptionsScreen.Update` cierra Opciones con Esc cuando está abierto; no extrapolar esta pulsación a un sistema global de pausa o navegación de escenas.
- No crear ajustes de sensibilidad, inversión de ejes ni controles de gamepad sin propiedades conectadas al motor y pruebas.

## Opciones → Accesibilidad — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** mejorar la comodidad visual y reducir molestias de animación, preservando una interfaz legible, operable con ratón y teclado y de estilo premium.

1. **Movimiento reducido** mediante la misma preferencia `ReducedMotion` de la sección Interfaz, con efecto inmediato sobre los componentes que la consultan.
2. **No duplicar la configuración**: debe existir una sola opción persistente y el usuario podrá llegar a ella desde Interfaz o Accesibilidad con un estado coherente.
3. **Foco visible de teclado**, estado de seleccionado, botones deshabilitados e información relevante comunicados por **texto, icono y contraste**, no exclusivamente por color o animación.
4. **Recorridos de navegación comprensibles** con teclado para menús y confirmaciones; verificar operabilidad de los controles existentes antes de anunciar accesibilidad completa por teclado.
5. **Respetar la reducción de movimiento en las animaciones ya compatibles**; no deshabilitar por ello la progresión lógica de clientes, el tiempo de juego o las transiciones imprescindibles para entender acciones.
6. **Notas explicativas breves** sobre qué modifica el ajuste; no presentar toggles de daltonismo, subtítulos, narrador, tamaños de letra o alto contraste que aún no tienen implementación.
7. **Registrar como trabajo posterior** una auditoría de legibilidad en 1920×1080 y 1280×720, estados de foco, tabulación, tamaños de controles, avisos sonoros sin alternativa visual y accesibilidad visual global. La aprobación de la funcionalidad no certifica cumplimiento de estándares.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(6)` ofrece únicamente el toggle `ReducedMotion` y un texto descriptivo sobre foco azul, iconos y estados deshabilitados.
- Se usa `BB.Options.ReducedMotion`, que también controla Interfaz. El código de `BBIconButton` evita desplazamientos y cambios de escala con el ajuste; otros componentes lo comprueban parcialmente.
- La documentación existente menciona estados de foco y navegación, pero **no se han verificado sistemáticamente todas las pantallas del juego**. No declarar modos de alto contraste, lector de pantalla o localización de subtítulos ya disponibles.
- Si un futuro modo de alto contraste modifica la identidad visual aprobada, deberá diseñarse como alternativa accesible específica, no reemplazar el estilo normal.

## Opciones → Idioma — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** informar honestamente del idioma de la versión y preparar un camino de internacionalización sin mostrar un selector que no funciona.

1. **Mostrar Español como idioma disponible** y actualmente activo, con una etiqueta clara de que es el idioma soportado en la versión presente.
2. **No mostrar idiomas no disponibles como opciones seleccionables**, ni cambiar textos parcialmente, ni prometer cambio dinámico de idioma mientras no exista servicio de localización integrado.
3. **Mantener toda la interfaz jugable en español**: denominaciones, menús, mensajes de error, calendarios y ayuda de usuario, coherentes con las aprobaciones de este proyecto. Los identificadores técnicos y los nombres de API pueden permanecer internos en inglés.
4. **Preparar futuro soporte multilingüe separadamente** con recursos localizados, tipografías compatibles, formatos de número/fecha, ampliación de texto, pluralización y pruebas en pantallas reales. No convertir este punto en rediseño ni añadir idiomas sin plan de contenido.
5. **No confundir idioma con región, divisa o país**: no establecer ajustes económicos, horarios o mecánicos por elegir una lengua. El idioma no modifica la lógica del restaurante.
6. **Evitar una opción vacía o engañosa**: mientras solo exista español, la categoría puede conservarse como ficha informativa sin selector; la decisión de ocultarla en un futuro requeriría redefinir la navegación de nueve categorías aprobadas.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(7)` muestra literalmente que solo **Español** está disponible; no existe desplegable ni `SetLanguage` en este componente.
- Los textos jugables examinados están codificados directamente en C#; no acreditar un sistema de traducción completo ni configuraciones multilingües que no se hayan auditado.
- Localización futura será ampliación de producto, no una función implementada por la interfaz actual.

## Opciones → Créditos / Legal — PROPUESTA FUNCIONAL PENDIENTE DE APROBACIÓN

**Objetivo:** mostrar información fiel de autoría, versión, motor y licencias de terceros sin inventar derechos de distribución ni presentar una demostración como producto definitivo.

1. **Mostrar el nombre Bistro Builder**, su condición de videojuego de gestión de restaurantes y la **versión instalada** obtenida dinámicamente mediante `Application.version`.
2. **Consultar créditos de tecnologías/recursos** según licencias y materiales efectivamente integrados: Unity, tipografías, iconografía y otros paquetes cuando existan.
3. **Presentar atribuciones legales exactas**: identificar el titular/licencia de cada material de tercero y facilitar el texto o enlace a la licencia cuando las condiciones de distribución lo exijan. No sustituir las licencias por una frase genérica.
4. **Verificar antes de distribuir la licencia comercial de cada fuente y recurso**, especialmente cualquier fuente identificada como **DEMO**. El código actual muestra `Recoleta Regular DEMO — Latinotype`, lo cual **no demuestra autorización comercial**; esta comprobación constituye un requisito de publicación, no una licencia concedida.
5. **Mantener créditos y aviso legal en consulta sin modificar gameplay**: sin botones de compras, salidas, cambios de versión ni accesos de actualización ficticios.
6. **Permitir consultar la información sin conexión** cuando sea necesaria para cumplir licencias; un futuro enlace a condiciones o privacidad solo se añadirá si existe una URL verificada, texto legal aplicable y necesidad real.
7. **Actualizar la página y sus menciones al publicar nuevas versiones**, porque texto legal desactualizado o atribuciones inexistentes generan confusión.

### Contraste con código y límites

- `BistroBuilderOptionsScreen.ShowPage(8)` lista Inter — SIL Open Font License, Recoleta Regular DEMO — Latinotype, Lucide — ISC License, iconos de Opciones propios, Unity y `Application.version`.
- En los assets versionados aparece `Assets/Resources/BistroBuilder/UI/Typography/Inter-LICENSE.txt`; los derechos de Recoleta DEMO y cualquier otro recurso deben verificarse **por separado** antes de afirmar que la distribución comercial está autorizada.
- No hay en la página actual visor extenso de licencias, términos, privacidad o hipervínculos jurídicos. Atribución completa y auditoría de licencias son trabajo posterior, no funcionalidades probadas.

## Estado general y próximo paso

**Aprobado en este ciclo:** Opciones → Audio, junto con Partida aprobada previamente. **Preparadas pero NO aprobadas todavía:** las siete propuestas **Vídeo, Jugabilidad, Interfaz, Controles, Accesibilidad, Idioma y Créditos / Legal**.

Se solicita **una aprobación en bloque** de estas siete propuestas, o los cambios concretos que se quieran introducir. Si se aprueban, se podrán registrar las siete decisiones y **cerrar Opciones y toda la definición funcional del menú superior de Modo Juego**. Las funciones nuevas de UI, rediseño y pruebas Unity seguirán siendo trabajo posterior.

**Nota de precisión:** el siguiente trabajo no debería ser abrir más submenús por costumbre, sino contrastar este inventario aprobado de pantallas con las barras V21/E5.5 y priorizar los huecos de implementación en la rama de integración.
