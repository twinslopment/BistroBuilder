# Bistro Builder — PERSONAL V1 · Contrato aprobado (01/10/2026)

## Referencia visual vinculante
Pantalla PERSONAL aprobada por usuario el 01/10/2026 (composición tabla/lista izquierda y ficha derecha). Misma familia **marfil, crema, tinta marrón, latón, tipografía y marco** que TopBar modo normal (`BistroBuilderTopBarPlate`). Seria, elegante, sin iconos arcade adicionales. PC responsive 1920×1080 / 1280×720 / 800×600; el contenido puede hacer scroll, nunca los botones críticos fuera de viewport.

## Fuente de verdad
- `BistroBuilderStaffService`: empleado persistente (`EmployeeId`), salario por **servicio**, estado, XP, habilidades.
- `BistroBuilderStaffRecruitmentService`: mercado y CandidateId efímero; 5 ofertas V1; actualización hasta una vez por día de juego; después de contratar desaparece la oferta; confirmar antes de mutar.
- `BistroBuilderStaffDevelopmentService`: progreso de nivel XP y cuatro formaciones del perfil 4C; **no ascenso manual de categoría ni rol en V1**.
- `BistroBuilderStaffScheduleService`: planificación; la pantalla Personal no la sustituye.
- Binding de sesión: liga empleado con agente operativo real; nunca deducir que contratar equivale a estar trabajando; Save/Load sin duplicados.
- Finanzas es autoridad monetaria, Personal solo muestra/proyecta salario en céntimos/servicio.

## Profesiones
V1 jugables: `waiter` (Camarero/a, SALA) y `cook` (Cocinero/a, COCINA). No mostrar puestos históricos/futuros (limpieza, reparto, etc.) hasta que tengan definición activa y adaptador operativo válido. La UI construye grupos/filtros desde el catálogo, sin arrays de profesiones hardcodeadas. Extender metadatos de departamento al catálogo sin cambiar EmployeeId ni la autoridad. Invariante interna: **cocineros contratados solo aparecen físicamente en la cocina**; no escribir avisos de esta restricción al jugador y nunca asignar agentes cook a sala.

## Pestaña Plantilla
Grupos por departamento; fila: identidad/rol, estado, nivel, salario por servicio. Ficha: nombre, rol, estado real, antigüedad, salario/servicio, nivel/XP, habilidades reales Velocidad / Atención / Organización / Trato, asignación si existe y rendimiento real o estado vacío. Acciones: Disponibilidad, Formación y Despedir, respetando precondiciones. Acceso a horarios desde navegación canónica; nunca añadir controles falsos. **Ascender se elimina**. No ofrecer ajuste de salario hasta existir comando aprobado.

## Pestaña Candidatos
Mostrar ofertas reales, filtro Todos / roles activos derivados de las ofertas, consulta de perfil / XP / habilidades / salario/servicio, Contratar con confirmación y Renovar mercado según cooldown canónico. En el mercado V1 con dos roles y 5 plazas deberá existir al menos un candidato de cada rol; sin negociación, entrevistas, coste de alta ni rerolls ilimitados. Separar candidatos de plantilla real (CandidateId != EmployeeId).

## Capacidad de contratación
No inventar cap global `3 camareros` / `2 cocineros`. Plantilla contratada no equivale a plantilla asignada: servicio y horarios determinan elegibilidad/capacidad real. Mostrar empleados activos, candidatos y asignados, no contadores ficticios X/Y. Cuando futuras reglas de infraestructura/progresión definan plazas, mostrar sólo límites derivados de esas autoridades.

## Modales de confirmación
Contratar: «¿Estás seguro de que quieres contratar a {nombre} como {profesión}? Salario: {importe} por servicio.» Botones Cancelar / Sí, contratar.
Despedir: «¿Estás seguro de que quieres despedir a {nombre}? Dejará de pertenecer a la plantilla activa.» Cancelar / Sí, despedir. Despedir con binding activo se rechaza mediante dominio. Oscurecer/bloquear el fondo, capturar identidad seleccionada al abrir, evitar doble envío y no mutar al cancelar/cerrar. Cerrar modal con Escape; foco visible.

## Microinteracciones
ColorTint / glow fino de latón (120–180 ms) para pestañas, filas, botones, filtros; estado seleccionado persistente, sin hover en deshabilitados; leve presión al click. Botón Despedir solo intensifica su rojo muy sutilmente. Uso de animación por Selectable sin recorrido global por frame. Un solo estilo reutilizable.

## Gates de cierre
Compilación Unity 0 errores; mercado waiter+cook, contratación y desaparición CandidateId, modal doble click/cancelar, despido seguro, Plantilla por grupos del catálogo, Formación, Save/Load y binding sin duplicar, cooks solo cocina, 1920×1080 / 1280×720 / 800×600 sin desbordes, control de FPS de equipo PC Intel sin degradar PC final. No declarar PASS runtime ni visual sin ejecutarlo.

## Referencias
`docs/STAFF_BLOCK_4_ARCHITECTURE.md`, `docs/STAFF_4B_DESIGN.md`, `docs/STAFF_4C_DESIGN.md`, `docs/STAFF_BLOCK_5_SCHEDULING.md`, `docs/10_ARCHITECTURE/AUTHORITY_MATRIX.md`.
