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

## Validación obtenida
- Unity 6000.3.19f1: compilación correcta, `BistroBuilderStaffApprovedRuntimeProbe.RunBatch` **11 PASS / 0 FAIL**, proceso EXIT 0.
- Previsualización HTML: navegación/selección/horarios sin errores JavaScript en 1920, 1313 y 800 px.
- La prueba Unity verifica estructura e interacción, NO una coincidencia visual píxel a píxel con la captura.
- Los JPEG de los retratos siguen en el paquete de entrega; deben incorporarse físicamente al proyecto para que el juego los cargue. No declarar los retratos instalados.
- Pendiente aceptación visual de la previsualización y captura comparativa de Unity en 1920 × 1080, 1280 × 720 y 800 × 600.
- Horarios/principio de servicio/agentes físicos son otro bloque: **no modificados en esta reconstrucción visual**.
