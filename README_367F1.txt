BistroBuilder 367F1 — Out Parameter Compile Guard

Corrección acumulativa de compilación para 367F.

Archivos sustituidos:
- Assets/Scripts/Application/Orders/BistroBuilderCourseAndSharingService.cs
- Assets/Scripts/Simulation/Orders/Courses/BistroBuilderCourseAndSharingRuntime.cs

Corrección:
- Se separan las comprobaciones de referencia nula y TryValidate(out error).
- Todos los caminos de retorno asignan explícitamente el parámetro out error.
- Se conservan los archivos .meta y los GUID originales.

Instalación:
1. Cerrar Unity.
2. Extraer este ZIP sobre la raíz del proyecto BistroBuilder.
3. Confirmar sustitución.
4. Abrir Unity y comprobar 0 errores de compilación antes de ejecutar el instalador 367F.
