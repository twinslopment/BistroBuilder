# Bistro Builder — registro de decisiones vinculantes

**Regla:** una decisión explícita posterior prevalece sobre propuestas anteriores. `VIGENTE` = obligatoria; `SUPERADA` = conservar solo como historia.

| ID | Estado | Decisión |
|---|---|---|
| D-001 | VIGENTE | Arquitectura modular, data-driven, validable; evitar hardcode por asset. |
| D-002 | VIGENTE | Modo Edición solo fuera de servicio y sin obreros simulados. |
| D-003 | VIGENTE | No simular agua, extracción, gas ni ventilación como gameplay. |
| D-004 | VIGENTE | Un único almacén jugable; FEFO; lotes no gestionados manualmente. |
| D-005 | VIGENTE | Espera general virtual; espera física solo en barra cuando corresponda. |
| D-006 | VIGENTE | Cocina usa estados Fluida/Cargada/Saturada/Bloqueada y máximo 3 comandas priorizadas. |
| D-007 | VIGENTE | Camareros por zonas con asignación automática por cercanía/carga/prioridad; jefe de sala opcional. |
| D-008 | VIGENTE | BBSIS es única autoridad espacial; Render no equivale a Spatial. |
| D-009 | VIGENTE | Interaction & Reservation solo gobierna derechos lógicos; Wait Ticket únicamente para colas semánticas reales. |
| D-010 | VIGENTE | Navigation gobierna rutas/circulación; Animation solo representa. |
| D-011 | VIGENTE | Character Animation V1 está integrado/cerrado; futuras ampliaciones son V2/hardening. |
| D-012 | VIGENTE | BBSIS V1 está cerrado; no reabrir salvo regresión real. |
| D-013 | VIGENTE | UI de Servicio: navegación horizontal superior; Actividad izquierda; contexto derecha; acciones abajo. |
| D-014 | VIGENTE | `Caja` = dinero del servicio; `Satisfacción` = satisfacción del servicio. |
| D-015 | VIGENTE | No mostrar/reservar vistas predefinidas de cámara en UI final. |
| D-016 | SUPERADA | 369B exponía presets General/Isométrica; ya no forman parte de la experiencia final. |
| D-017 | VIGENTE | WASD queda reservado al control de cámara. |
| D-018 | VIGENTE | Clima sin dirección ni subtipos de lluvia/nieve/viento; condición exterior uniforme. |
| D-019 | VIGENTE | Interior confortable; sin simulación térmica interior extrema. |
| D-020 | VIGENTE | Todos los cambios de sistemas deben integrarse/subirse a su rama correspondiente. |
| D-021 | VIGENTE | Cada commit solicitado al usuario debe llevar Summary/Description exactos proporcionados por el asistente. |
| D-022 | VIGENTE | BBPLFS es sistema transversal activo; no debe operar como herramienta aislada del proyecto. |
| D-023 | VIGENTE | Feedback universal de edición: preview/snapping/materialización/validez/Undo-Redo, abstracto y no destructivo. |
| D-024 | VIGENTE | La presentación visual base es común a los locales salvo excepción explícita. |
| D-025 | VIGENTE | Los PASS visuales/espaciales requieren evidencia visual/funcional, no solo métricas. |
| D-026 | VIGENTE | La persistencia es universal/versionada; no crear guardados paralelos para un mismo dominio. |
| D-027 | VIGENTE | Modo Edición adopta como dirección gráfica la Propuesta C revisada: catálogo claro vertical a la izquierda, viewport central, inspector claro a la derecha con Reglas de colocación y franja inferior de herramientas/acciones. |
| D-028 | VIGENTE | BBFFVAS es una herramienta interna de autoría; no es una mecánica ni interfaz destinada al jugador. |
| D-029 | VIGENTE | Una variante puramente visual comparte geometría, footprint, collider y contratos espaciales; los acabados no justifican duplicar prefabs. |
| D-030 | VIGENTE | Acabado Automático solo completa zonas/canales missing o incompletos; nunca sobrescribe trabajo válido salvo orden explícita. |
| D-031 | VIGENTE | Toda propuesta automática requiere preview y aplicación explícita; reutilizar acabados canónicos tiene prioridad sobre generar nuevos. |
| D-032 | VIGENTE | Si la semántica de una superficie es incierta, el sistema solicita clasificación y no asigna materiales a ciegas. |
| D-033 | VIGENTE | V30 rápida (280 ms, 94 % → 103 % → 100 %) es la microinteracción estándar para todos los botones actuales y futuros fuera de Modo Edición; Editor V2 queda pendiente de decisión. Véase `docs/30_UI_UX/BUTTON_MOTION_V21_V30.md`. |
| D-034 | VIGENTE | Barra superior definitiva del **Modo Juego = V21 aprobada**: logotipo original solo como marca y, exactamente en este orden, **Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación y Opciones**. No incorporar más botones o controles operativos (Caja, fecha, climatología, velocidad, etc.) en esa barra. Iconos y estética V21, animación V30 rápida. El Modo Edición posee una interfaz propia y la rueda de Servicio es independiente. Referencia: `docs/30_UI_UX/BARRA_SUPERIOR_V21_V30_APROBADA.md` y `docs/30_UI_UX/UI_UX_DEFINITIVE.md`. |
| D-035 | SUPERADA | **Actividad (Modo Juego):** propuesta inicial de subopciones «Vista del restaurante» y «Actividad reciente»; su contenido se integra en el comportamiento directo del botón según D-036. Se mantiene la prohibición de una pantalla independiente de «Alertas e incidencias». |
| D-036 | VIGENTE | **Actividad NO tiene submenú.** El botón superior alterna el panel izquierdo: desde otra pantalla de gestión vuelve al restaurante 3D y abre Actividad; un segundo clic lo oculta y el siguiente lo muestra. El filtro interno «Incidencias» sustituye una subopción independiente de alertas. Si el panel está oculto y hay avisos críticos pendientes, el icono Actividad presenta un indicador discreto. El panel derecho conserva su función contextual. Es diseño aprobado, pendiente de integración y pruebas en Unity. Referencia: `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_ACTIVIDAD.md`. |
| D-037 | VIGENTE | **Rediseño visual global obligatorio:** TODOS los paneles, pantallas, menús y subopciones del juego deben compartir una única identidad visual premium, incluyendo iconos, colores, tipografías, botones, pestañas y estados. Reutilizar código/sistemas anteriores **no** equivale a mantener su aspecto antiguo. Las composiciones se adaptan a cada función sin romper diseños concretos ya aprobados (barra V21, barra inferior, Editor). Documento vinculante: `docs/30_UI_UX/SISTEMA_VISUAL_GLOBAL_OBLIGATORIO.md`. |
| D-038 | VIGENTE | **Personal → Plantilla: definición funcional aprobada.** Mostrar camareros y cocineros, filtrar por puesto, consultar estado, contrato, salario por servicio, antigüedad, habilidades, XP/nivel, rendimiento y asignación; permitir formación, disponibilidad y despido con confirmación. No contratar en Plantilla (usar Candidatos), no planificar turnos aquí (usar Horarios), no mostrar ascensos en primera fase ni dirigir manualmente operaciones de cocina y sala. Es aprobación **funcional**, no aprobación visual ni validación Unity. Referencia: `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_PERSONAL.md`. |
| D-039 | VIGENTE | **Personal → Candidatos: funcionalidades aprobadas.** Mostrar y filtrar aspirantes de sala y cocina; consultar perfil, habilidades, experiencia y salario por servicio; ordenar/comparar, contratar con confirmación y pasar el nuevo empleado a Plantilla sin asignarlo automáticamente a turno; permitir renovar el mercado una sola vez por día de juego. Excluir por ahora entrevistas, negociación salarial y tasas de contratación. Es una aprobación funcional, no visual ni de validación en Unity. Referencia: `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_PERSONAL.md`. |
| D-040 | VIGENTE | **Personal → Horarios: funcionalidades aprobadas.** Planificar por día (horizonte inicial 7 días) y por comida/cena, asignar **camareros y cocineros** según disponibilidad, consultar cobertura suficiente/insuficiente y coste salarial previsto, rellenar automáticamente, copiar planificación del día anterior y guardar/cargar sin duplicar estado; editar exclusivamente con restaurante cerrado, sin controlar tareas de servicio ni modelar horas extras/vacaciones en esta fase. **En la rama revisada el planificador UI y la cobertura solo contemplan camareros: incluir cocineros exige ampliación y pruebas posteriores.** Aprobación funcional, sin diseño visual ni implementación verificada. Referencia: `docs/30_UI_UX/SUBOPCIONES_MODO_JUEGO_PERSONAL.md`. |
