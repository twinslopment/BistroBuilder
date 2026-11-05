# Norma de publicación de ramas de Bistro Builder

**Decisión del proyecto:** todas las ramas que se publiquen a partir de ahora deben poder entenderse rápidamente en español.

## Obligatorio al publicar o actualizar una rama

1. Crear o actualizar `docs/RESUMEN_RAMA.md` **dentro de esa misma rama**, con:
   - **Objetivo:** qué problema resuelve.
   - **Contenido:** archivos, módulos y funcionalidades añadidos o modificados, expresados de manera comprensible.
   - **Pruebas realizadas:** resultados comprobados, no supuestos.
   - **Estado real:** terminado, en desarrollo, pendiente de fusión o integrado en la build.
   - **Pendientes o limitaciones:** lo que todavía no está hecho o probado.
   - **Commit principal** y, si existe, enlace a la solicitud de integración (pull request).
2. Escribir **mensajes de commit en español**, claros y descriptivos. Se admiten identificadores técnicos, rutas y términos habituales como `fix`, `feat`, `UI`, `Unity` o `PR`.
3. Al abrir una pull request, escribir **título y descripción en español**, con resumen, pruebas y riesgos.
4. Al informar sobre una rama publicada, incluir siempre **nombre de la rama, resumen en español, estado y enlace**.
5. Conservar los nombres técnicos de rama sin tildes ni espacios; el texto explicativo debe estar en español.

**Importante:** GitHub no ofrece un campo de descripción general propio para cada rama. El archivo `docs/RESUMEN_RAMA.md`, los commits y la descripción de la pull request son los lugares donde estará disponible esa información.

**No declarar funcionalidades integradas en Unity por el solo hecho de subir el código a GitHub.**
