# Resumen de esta rama — Animación V30 rápida

## Objetivo
Establecer un efecto de pulsación común para los botones de Bistro Builder, de forma que los botones actuales y los que se incorporen en el futuro respondan con la misma animación.

## Qué contiene
- Componente `BistroBuilderV30ButtonMotion.cs` con microanimación de 280 ms: presión al 94 %, rebote al 103 % y retorno al 100 %.
- Instalador global `BistroBuilderV30ButtonInstaller.cs` para aplicar el efecto a botones de Unity UI, incluidos los creados durante la partida.
- Evita superponer la animación de presión antigua al mismo botón.
- Respeta la configuración de movimiento reducido.
- Modo Edición excluido hasta que se decida si debe adoptar el efecto.
- Documento `docs/30_UI_UX/BUTTON_MOTION_V21_V30.md` con especificaciones y criterios de aceptación.
- Prueba técnica `BistroBuilderV30ButtonFeedbackQa.cs`.

## Pruebas y estado
- Compilación de scripts C# en Unity 6000.3.19f1: **correcta** para la primera compilación realizada.
- La importación completa de una copia aislada se detuvo; el test funcional dentro del juego no se dio por aprobado.
- **Pendiente:** ejecutar la prueba técnica en Unity, validar visualmente las interacciones e integrar en la rama principal. No está en la build jugable.

## Referencia
Commit de implementación: `b2fe71f7`. Esta rama contiene la base de animación, no la versión HTML definitiva de la barra superior.
