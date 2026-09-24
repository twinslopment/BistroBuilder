# Barra superior del juego

La barra usa el catálogo SVG y los efectos de `feature/21b-iconography-system` (`34694c7`). Incluye Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing y Reputación, conectados a sus pantallas existentes. La selección usa dorado, subrayado y fondo. La versión visual aprobada el 23/09/2026 aplica un hover cálido e iluminado por recuadro y una microanimación continua propia a cada icono mientras el puntero permanece encima; Opciones mantiene un giro corto de engranaje y el resto combina elevación, balanceo o desplazamiento según su semántica.

El nombre del restaurante, el estado del servicio, el calendario y la hora provienen de la partida. El menú de opciones y el desplegable del restaurante permiten entrar en edición, abrir Progreso, Comandas o Cocina, alternar pantalla completa y cerrar paneles. El menú bloquea la interacción con la construcción mientras está abierto.

La prueba `BistroBuilderTopNavigationPlayTest.RunBatch` verifica catálogo, sprites, animación al pasar el ratón, selección, navegación real entre Personal e Inventario, regreso a Actividad, menú de opciones y persistencia de los textos tras reconstruir la barra. Resultado: PASS. Captura de comprobación: `Logs/TopNavigation1920.png`.

Ejecutable: `Builds/Windows/BistroBuilder_Edicion/BistroBuilder.exe`. El lanzador `Jugar_Pantalla_Completa.cmd` abre la versión de Windows a pantalla completa sin bordes.
