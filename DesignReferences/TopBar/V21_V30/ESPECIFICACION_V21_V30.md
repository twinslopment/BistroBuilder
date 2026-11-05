# Bistro Builder — V21 + V30 mejorada

**Estado:** prototipo funcional en HTML/CSS/JavaScript. **Unity no se ha modificado.**

## Identidad
- Barra **V21** aprobada: negro satinado, logotipo dorado 3D original exacto, diez opciones visibles, iconos SVG blancos, separadores y selección luminosa.
- El archivo `assets/Logotipo_Bistro_Builder_Original.png` es copia binaria del original proporcionado por el usuario; no se transforma ni se recorta.

## Secuencia única de animación al hacer clic
- Duración normal: **280 ms**.
- 0 %: tamaño 100 %, brillo habitual.
- 20 %: compresión al **94 %**, bajada de 1 px.
- 47 %: recuperación al **103 %**, elevación de 0,65 px.
- 53 %: destello blanco moderado en el SVG.
- 72 %: amortiguación al 99,4 %.
- 100 %: icono al 100 %, sin desplazamiento ni brillo transitorio.
- Se reproduce con cada clic, incluso clics repetidos y rápidos en la misma opción.
- `prefers-reduced-motion: reduce` desactiva el movimiento. El demostrador también incorpora una opción manual «Sin animación».
- Modo «Lenta · 840 ms» disponible para inspeccionar la curva del movimiento; no es el ajuste recomendado del juego.

## Navegación
Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación y Opciones. Sin accesos adicionales ni menús inventados.

## Comprobación
- Probar 1280×720 y 1920×1080, sin solapamientos de la barra.
- Verificar que las diez secciones son pulsables, que el clic repetido reinicia la animación, teclado (← → Inicio Fin), ausencia de errores JS y respeto a movimiento reducido.
- Este prototipo no se conecta aún a Unity. En Unity la interacción deberá vincularse a los launchers reales de `BistroBuilderUiShell`, evitando crear otra navegación paralela.
