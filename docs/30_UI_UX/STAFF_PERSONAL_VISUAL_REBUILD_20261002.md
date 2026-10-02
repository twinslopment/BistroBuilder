# Personal — reconstrucción visual · 02/10/2026

## Referencia y alcance
- Referencia obligatoria: captura aportada `Captura de pantalla 2026-10-01 093447(1).png` (marco marfil/latón, tabla Sala/Cocina, retrato y ficha derecha).
- Mantener los servicios existentes de Personal, contratación, formación, Horarios y confirmaciones; sin autoridad duplicada ni navegador integrado en Unity.
- Previsualización independiente y navegable HTML/CSS/JS: `Personal_BistroBuilder_PREVIEW.html` (entregada como artefacto de la conversación, no archivo del repositorio).
- Cinco retratos originales con bordado conceptual del emblema Bistro Builder en el paquete separado `Personal_Portraits_Unity6.zip`.

## Cambios implementados en Unity
- `BistroBuilderStaffPlayerScreen.ApprovedV1.cs`: mayor presencia del marco, raíles de latón, cabecera descriptiva con fondo, tabla compacta, secciones de 77 unidades, filas de 52, huecos de contratación de 51, columnas alineadas y botón Contratar en la columna Estado.
- Pie informativo independiente en Plantilla para evitar que la última fila quede parcialmente oculta.
- Ficha: marco con relieve, insignia de nivel, acceso `Ver horarios`, conserva datos reales, XP y cuatro habilidades.
- `BistroBuilderStaffVisuals.cs`: paleta de marfil/latón revisada, sombras sutiles de controles y misma identidad visual para Candidatos/Horarios.
- Corrección importante: las filas de empleados no pasan por el estilo de botón genérico, que centraba indebidamente la primera etiqueta.
- `BistroBuilderStaffPortraitImporter.cs`: importa como Sprite únicamente las imágenes ubicadas en `Assets/Resources/BistroBuilder/UI/StaffPortraits/{waiter,cook}/`.
- La selección de Sprite se ordena explícitamente por nombre y usa colecciones de avatar coherentes con los nombres simulados del catálogo de contratación (variantes `waiter_f_`, `waiter_m_`, `cook_f_`, `cook_m_`); conserva la misma imagen entre candidatura y plantilla.

## Validación obtenida
- Unity 6000.3.19f1: compilación correcta, `BistroBuilderStaffApprovedRuntimeProbe.RunBatch` **11 PASS / 0 FAIL**, proceso EXIT 0.
- Previsualización HTML: navegación/selección/horarios sin errores JavaScript en 1920, 1313 y 800 px.
- La prueba Unity verifica estructura e interacción, NO una coincidencia visual píxel a píxel con la captura.
- Los JPEG de los retratos siguen en el paquete de entrega; deben incorporarse físicamente al proyecto para que el juego los cargue. No declarar los retratos instalados.
- Pendiente aceptación visual de la previsualización y captura comparativa de Unity en 1920 × 1080, 1280 × 720 y 800 × 600.
- Horarios/principio de servicio/agentes físicos son otro bloque: **no modificados en esta reconstrucción visual**.

## Tipografía V4, implementada (02/10/2026)
- Los dos recortes enviados por el usuario son la referencia concreta para el título PERSONAL y la tipografía tabular. Se elimina la mezcla Inter/Cambria/Recoleta dentro de Personal.
- `BistroBuilderStaffVisuals.StaffRegular` y `StaffBold` resuelven Georgia Regular/Bold mediante TextMesh Pro en Windows, solo para Personal/Candidatos/Horarios, sin modificar `BistroBuilderTypography` ni otras pantallas. El recurso serif existente es fallback cuando el sistema no tenga Georgia.
- `Label` y `TextStyle` usan esa misma familia; `ApprovedRowCells` fuerza el nombre del empleado en negrita; título a 41 unidades con sombra discreta. Las estrellas y marcas tipográficas no compatibles se sustituyen por sprites del catálogo (`CustomerVip`, `EconomyReport`).
- Preview HTML autocontenida `Personal_BistroBuilder_PREVIEW_V4.html`: Georgia regular/negrita coherente en las tres pestañas y en los paneles laterales; accesible como artefacto del chat.
- Validación Unity 6000.3.19f1 sobre escena real: `BistroBuilderStaffApprovedRuntimeProbe.RunBatch` **12 PASS / 0 FAIL, EXIT 0**, incluyendo comprobación explícita de `faceInfo.familyName == Georgia` y fuente Georgia Bold aplicada realmente al título y al nombre. Sin warnings de caracteres decorativos ausentes.
- V4 sigue pendiente de aceptación visual subjetiva del usuario; no declarar igualdad píxel a píxel con el original.
