# PERSONAL V5 — implementación de la referencia izquierda (02/10/2026)

> HISTÓRICO: las decisiones tipográficas y de rejilla han sido sustituidas por `STAFF_PERSONAL_REFERENCE_V6_20261002.md` (Recoleta + Inter). Mantener de V5 únicamente la referencia compositiva que no contradiga V6.

## Referencia visual vinculante
- Captura aportada por el usuario: panel PERSONAL a la izquierda de la comparación `image(20261002-093222).png`, equivalente al diseño original `Captura de pantalla 2026-10-01 093447.png`.
- Una sola cabecera PERSONAL + texto descriptivo; no se reserva una fila permanente para Plantilla, Candidatos y Horarios.
- Selector contextual sobre el título PERSONAL: muestra las tres acciones reales al pulsarlo y vuelve a ocultarse después de escoger un destino.
- Composición principal expandida directamente bajo la cabecera. Plantilla mantiene 56% de tabla Sala/Cocina + 43% de ficha derecha, sin scroll global decorativo.
- Alturas compactas de cabecera de departamento (69 unidades), fila real (40), puesto libre (52); icono/contador y separadores alineados.
- Ficha derecha: retrato, profesión, XP, datos con iconos dedicados, cuatro habilidades y acciones operativas. Salarios reales por servicio (no se copian los importes ficticios mensuales de la imagen).
- Selección con miel suave, no naranja saturado. Georgia Regular/Bold solo para Personal; sin cambiar la topbar global.

## Assets ya instalados
- SVG con importación `svgType: 0`: `Assets/Resources/BistroBuilder/UI/StaffIcons/{role,salary,assignment,state}.svg`.
- Cinco retratos con logo bordado, extraídos sin intervención del usuario de la preview V4 autocontenida que ya existía en su carpeta Downloads y convertidos de WebP a PNG.
- Destinos: `Assets/Resources/BistroBuilder/UI/StaffPortraits/waiter/waiter_f_01.png`, `waiter_f_02.png`, `waiter_m_01.png`, `cook/cook_f_01.png` y `cook_m_01.png`.
- El importador dedicado prepara los PNG como Sprite; `Portrait()` conserva selección determinista, separada por rol/colección.

## Validación y límites
- Preview web separada, programada en HTML/CSS/JS, `Personal_BistroBuilder_REFERENCIA_V5.html`; topbar y retratos son assets de usuario, el resto es UI dinámica. Selector de título y cambios de turno funcionales en la demo.
- Navegación web comprobada en 1313/900/800/600 píxeles, sin error JavaScript ni desplazamiento horizontal.
- Test Play Mode sobre `Prototype_Restaurant.unity`: 15 PASS / 0 FAIL incluyendo selector contextual, iconos Sprite, los cinco retratos, Georgia, navegación y cálculo canónico de planificación.
- El resultado anterior no certifica identidad visual píxel a píxel de la ejecución interactiva en Game View. Requiere captura real comparativa antes de declarar diseño final aprobado.
- No se han cambiado salarios del dominio, contratación, generadores de agentes ni lógica del servicio. No fusionar a master sin aceptación visual y gates operativos independientes.
