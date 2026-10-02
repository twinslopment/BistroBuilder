# PERSONAL V8 — tipografía serif en las zonas señaladas · 02/10/2026

## Encargo y referencia
La captura marcada en rojo por el usuario es vinculante: **títulos de columnas, nombres y todos los datos de Sala/Cocina, bloque de habilidades y botones inferiores** deben mostrar el carácter editorial serif, no Inter. La composición aprobada de dos columnas y la lógica real de Personal se conservan.

## Regla definitiva aplicada en Unity
- Recoleta del propio proyecto (nunca Georgia ni una fuente descargada) en títulos, encabezados de columnas, filas y vacantes de Sala/Cocina, ficha principal, datos contractuales, nivel, cuatro habilidades y valores, botones inferiores; también en las zonas equivalentes de Candidatos.
- Recoleta Bold visual mediante el estilo TMP Bold solo en títulos, nombres, columnas, valores destacados y botones; Recoleta Regular para datos ordinarios. La normalización es semántica e idempotente: una segunda llamada no convierte todas las filas en negrita.
- Inter Regular/SemiBold se conserva en frases aclaratorias, notas, ayuda y navegación secundaria; Horarios mantiene su propia distribución tipográfica oficial ya aprobada.
- La lógica de negocio no se modifica: no hay cambios en contrataciones, turnos, nóminas, presencia ni Save/Load.

## Limitación REAL de la Recoleta actual
El `Recoleta-SDF.asset` importado figura como `Regular DEMO` y su tabla de caracteres contiene exactamente los 95 caracteres ASCII (U+0020–U+007E). El `Recoleta.otf` incluye asignaciones para acentos y euro, pero su representación visual DEMO genera marcas en esos caracteres. Una primera prueba de atlas dinámico se descartó al detectar el defecto en la captura real.
- En Unity, `BistroBuilderStaffVisuals.StaffTitle` instancia el SDF oficial conservando sus 95 glifos limpios y registra Inter Regular-SDF como fallback para signos y acentos que realmente faltan. No modifica el asset original.
- Si se instala legalmente una Recoleta completa en el mismo recurso, el código permite crear su variante dinámica (sin la restricción DEMO).
- La preview HTML usa Recoleta local únicamente para segmentos ASCII y agrupa los glifos de Inter dentro de la misma palabra, evitando cortes/espaciados artificiales en `Iván`, `Sofía`, `Formación`, `Asignación` y valores `€`.
- No se han exportado, empaquetado ni redistribuido fuentes.

## Archivos de preview
- `docs/30_UI_UX/previews/Personal_BistroBuilder_PREVIEW_V8.html`: pantalla interactiva con el arte y los retratos incorporados; enlaces relativos a los recursos tipográficos ya presentes en el proyecto. Plantilla, Candidatos y Horarios siguen navegables.
- `docs/30_UI_UX/previews/Personal_V8_Reference_1313.png`: captura real de Chrome con tipografías del ordenador (no generada por IA).
- Copia local en `C:\Users\mruperez\Downloads\Personal_BistroBuilder_PREVIEW_V8.html` (rutas de font-face adaptadas a Downloads) y `Personal_V8_FINAL_1313.png`.

## Verificaciones ejecutadas
- Compilación Unity 6000.3.19f1 correcta, escena `Assets/Scenes/Prototype_Restaurant.unity`.
- `BistroBuilderStaffApprovedRuntimeProbe.RunBatch`: **23 PASS / 0 FAIL**, exit 0; verifica fila/cabeceras/ficha/habilidades/botones en Recoleta, copy secundaria en Inter, que Recoleta DEMO no aporta acentos/euro al atlas estático, presencia de fallback Inter y estabilidad tras normalización repetida.
- Auditoría integral: **137** textos Personal/Candidatos/modal y **45** textos Horarios utilizan únicamente las dos familias oficiales.
- Captura V8 revisada en 1313px: sin los glifos de marca DEMO, sin separación artificial en palabras con acentos.

## Pendiente ajeno a esta tipografía
La similitud tipográfica perfecta para todos los glifos requiere sustituir el `Recoleta.otf` DEMO por la variante completa con licencia. La validación visual de una sesión normal de Game View por el usuario sigue siendo independiente de la regresión en batch.
