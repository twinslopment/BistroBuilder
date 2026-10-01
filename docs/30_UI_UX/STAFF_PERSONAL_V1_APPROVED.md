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

## Estado de implementación al 01/10/2026
- Contrato aprobado integrado en la rama `feature/bb-presentation-interaction-quality-v1`; añadido al índice canónico `docs/README.md`.
- Presentación PC marfil/latón sobre la pantalla 4F **ya existente** (`BistroBuilderStaffPlayerScreen.ApprovedV1.cs`), agrupación y orden por metadatos del catálogo, iconos canónicos y filtros de candidatos; sin duplicar StaffService ni escenas.
- Mercado configurado con waiter+cook y generación determinista que garantiza al menos una propuesta de cada rol si hay suficientes plazas; refresco diario/5 candidatos permanecen en 4B.
- Confirmaciones con identidad fijada, capa modal bloqueante, doble click desarmado y Cancel/Escape a través de EventSystem; hover por ColorTint 0,16 s; sin botón Ascender.
- Autotest puro de contrato añadido en `Tools/Bistro Builder/Personal/V1 - Verificar contrato visual y mercado`.
- **Pendiente obligatorio:** compilación/Play Mode real, inspección visual en 1920/1280/800 y regresión Save/Load. La presencia operativa ya tiene implementación estática separada en `docs/20_GAME_SYSTEMS/STAFF_OPERATIONAL_PRESENCE_V1.md` (provisionamiento real de camareros, turnos de cocina y reconciliación Save); sigue pendiente Play Mode/Save real y un prefab 3D de cocinero aprobado. No considerar esa parte PASS por añadir la nueva pantalla.

## Corrección de acceso a Horarios (01/10/2026)
- La barra normal 21A oculta los antiguos botones de servicios auxiliares, incluido `OpenScheduleButton`; por tanto, PERSONAL debe mostrar una tercera pestaña `Horarios` al lado de Plantilla/Candidatos. Esa pestaña invoca la pantalla **canónica 5E** (no un segundo planificador) y respeta su validación.
- El panel 5E mostrará camareros y cocineros activos con rol visible, estado `EN TURNO`/`Libre`, y selección de Día + Comida/Cena. La cobertura mínima mostrada se refiere específicamente a **Sala**; `Cobertura mínima` autocompleta solo camareros (sin contratar ni programar cocineros artificialmente).
- Solo se puede editar con servicio `Closed`. Para comprobar los agentes físicos recién contratados, seleccionar sus filas hasta `EN TURNO`, cerrar el panel y comenzar un **nuevo servicio**. No se agregan personajes a mitad de un servicio ya abierto.

## Auditoría visual 01/10/2026 · vídeo 02:04 y referencia aprobada
El vídeo mostró la antigua presentación incompleta (espacios vacíos, sin ficha visible, Horarios negro, coste de cocina omitido). La rama ya dispone de los archivos de reconstrucción `BistroBuilderStaffPlayerScreen.ApprovedV1.cs`, `BistroBuilderStaffSchedulePlayerScreen.ApprovedV1.cs` y `BistroBuilderStaffVisuals.cs`: tablas SALA/COCINA, filas con retratos/rol/nivel/asignación/salario/estado, ficha lateral con retrato/XP/cuatro barras y pestañas/estilo marfil-latón compartidos. Su existencia en GitHub NO es una inspección visual PASS: debe verse en Unity después de actualizar la copia local.

Corrección adicional: `BistroBuilderStaffSchedulePlanner.TryBuildMinimumWaiterPlan` **solo agrega turnos de camareros que falten** mediante `TrySetShift` al plan clonado; conserva íntegros los turnos de cocina, los camareros extras y sus ventanas ya configuradas; si Sala ya tiene cobertura, devuelve el plan sin mutación. Gate estático: `Tools/Bistro Builder/Personal/V1 - Verificar conservación turnos`. Repetir prueba funcional y persistencia.

**Pendientes que impiden cierre definitivo:** inspección de pantalla comparada 1:1 a 1920×1080, 1280×720 y 800×600; confirmaciones, selector de turnos y retorno; dos/tres camareros físicos con WaiterId únicos y Save/Load; control de área de cocineros con prefab 3D definitivo; revisar panel contextual recortado observado aprox. 01:27–01:44 del vídeo. No declarar PASS por análisis del fuente o por 10/10 static.

## Verificación real posterior (01/10/2026)
- Verificación posterior (01/10/2026): Unity 6000.3.19f1 compiló correctamente. La prueba reversible `BistroBuilderStaffApprovedRuntimeProbe.RunBatch`, corrigiendo el probe para pulsar `ApprovedScheduleTab` en lugar de saltarse el cierre de 4F, terminó con **11 PASS / 0 FAIL** y proceso EXIT 0. Quedaron verificados la transición real Personal → Horarios, la estructura 56%/43%, los grupos Sala/Cocina, el retrato/XP/habilidades y los recuentos/coste conjunto de turnos.
- Límite de ese resultado: `Logs/PersonalApproved` quedó sin PNG materializados por el batch; no certifica fidelidad visual píxel a píxel ni 1920/1280/800. `BistroBuilderStaffPresenceV1SelfTest.Run` arrojó **STATIC 10 PASS / 0 FAIL**; prefab visual de cocinero aún **PENDING**. Save/Load operativo y presencia física multiagente continúan como gates independientes no declarados PASS.
