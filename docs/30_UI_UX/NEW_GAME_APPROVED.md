# Nueva partida — marfil clásico aprobado

Aprobado por el usuario el 24/09/2026: la primera de las tres últimas imágenes, en versión interactiva. La referencia queda en [approved-reference.png](../UI/NewGame/approved-reference.png); la [maqueta ejecutable](../UI/NewGame/index.html) se abre en un navegador.

## Diseño vinculante
- Marco marfil opaco con borde latón y logo centrado en una franja propia.
- Título y subtítulo a la izquierda; nombre editable del restaurante a la derecha, con separación suficiente.
- Desde cero y Lo esencial en la primera fila, separadas; Últimos retoques centrada debajo. Las tres tarjetas tienen exactamente el mismo ancho y alto en cada resolución. En pantallas estrechas se apilan.
- Desde cero: local vacío, diseño libre. Lo esencial: distribución y equipo básicos. Últimos retoques: restaurante casi terminado para personalizar antes de abrir. Son grados de preparación, no tamaños del local.
- Botones Al pase: Atrás con flecha grande de latón; Crear restaurante con campana. Texto e iconos centrados. Respuesta al pasar, pulsar y usar teclado; movimiento reducido cuando se solicita.
- Tipografía de producción: Recoleta en títulos e Inter en controles. La maqueta usa alternativas del sistema sin distribuir fuentes comerciales.

## Integración en el juego — 24/09/2026
Pantalla nativa uGUI/TMP conectada al servicio de apertura existente. No se ejecuta un navegador dentro de Unity. Nombre editable, selección única, validación de nombre vacío y bloqueo de doble creación. Atrás conduce a un menú con Nueva partida, Continuar partida y Salir. Crear abre el modo edición; allí se conservan Guardar recuperación y Validar y continuar.

Desde cero usa el perfil Empty existente. Lo esencial añade Essentials (valor 4), que conserva dos mesas y sus asientos asociados, cocina y equipamiento funcional de la escena base y retira decoración. Últimos retoques añade FinishingTouches (valor 5), que conserva la escena equipada. Ambos perfiles se guardan y cargan; los valores anteriores 0–3 permanecen compatibles. No se cambia el tamaño del terreno. El número de mesas básicas es un parámetro de autoría del servicio.

Las miniaturas ilustradas son orientativas de preparación; el mobiliario real procede del contenido ya integrado. El recurso aprobado se reutiliza como atlas visual para logo, miniaturas y botones, con coordenadas normalizadas sobre la referencia original. Textos, controles y paneles son nativos y escalan con la resolución. Se usan Recoleta e Inter del proyecto. La preferencia existente de movimiento reducido desactiva los movimientos de los iconos.

La maqueta web se conserva como referencia de diseño; sus botones siguen siendo demostraciones, mientras que la versión Unity sí crea y recupera partidas.
## Archivos y comprobación
- `docs/UI/NewGame/index.html`: versión autónoma; recursos visuales incluidos.
- `docs/UI/NewGame/new-game.fragment.html`: fuente editable de la maqueta.
- `docs/UI/NewGame/approved-reference.png`: imagen elegida.
- `docs/UI/NewGame/verify.cjs`: verificación con Playwright y Edge. Ejecutar `node docs/UI/NewGame/verify.cjs`; requiere el módulo `playwright` o su ruta en `PLAYWRIGHT_MODULE`.

Verificado sin red a 1920, 1280, 1024, 736 y 320 px: tarjetas iguales, sin desbordamiento horizontal, selección única, nombre obligatorio, respuesta de botones, teclado y movimiento reducido. No constituye una prueba del juego ni una build Windows.

## Validación nativa
BistroBuilderOpeningIvorySelfTest.Run: PASS en creación por botón, Atrás/reapertura, nombre vacío, selección única, tres tarjetas iguales y guardar/cargar los tres perfiles. Resultado: 0 artículos/0 mesas, 6 artículos/2 mesas y 38 artículos/10 mesas respectivamente en la escena canónica. Panel completo y texto sin desbordamiento a 1920×1080, 1280×720 y 3440×1440, con capturas renderizadas e inspección visual. Pruebas usan ranuras libres 901–999 y eliminan únicamente los guardados que generan.
