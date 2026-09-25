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
| D-013 | VIGENTE | UI de Servicio: navegación horizontal superior; Actividad izquierda; contexto derecha; barra inferior operativa. La ubicación de indicadores globales se rige por D-033. |
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
| D-033 | VIGENTE | En modo normal/servicio, la barra horizontal inferior integra **Velocidad**, `Caja` y **Climatología**, además de las acciones contextuales que correspondan. Estos tres elementos no se colocan en la barra superior. |
| D-034 | VIGENTE | Los tiempos y estados de espera del servicio se definen desde configuración canónica compartida; no se hardcodean por pantalla/acción. Cocina admite `tiempo específico -> perfil de preparación -> default global`, por lo que la carta puede completarse progresivamente sin inventar tiempos de platos aún no diseñados. |
| D-035 | VIGENTE | Las acciones contextuales aparecen solo cuando existe una condición semántica válida del objeto seleccionado; no forman un menú fijo. `Disculpa`, `Explicar demora` y `Agilizar cuenta` están ratificadas para Mesa/Cliente; nuevas acciones permanecen como propuestas hasta decisión explícita. |
| D-036 | VIGENTE | `Agilizar cuenta` no aparece desde que se solicita la cuenta: se ofrece a partir del estado **Atención**. En Demora se destaca, en Incidencia puede coexistir con `Disculpa`, no admite pulsaciones repetidas sobre la misma necesidad y desaparece cuando la cuenta ya está siendo atendida o resuelta. |
| D-037 | VIGENTE | `Explicar demora` se ofrece desde **Demora** en adelante y solo una vez por necesidad activa. Mitiga de forma configurable la penalización de satisfacción atribuible a la espera, pero no reduce el tiempo real, no cambia el estado semántico y no altera la prioridad de la tarea; el valor concreto de balance permanece provisional. |
| D-038 | VIGENTE | `Disculpa` se ofrece cuando una necesidad alcanza **Incidencia/Crítico** o existe un fallo explícito recuperable de la comanda canónica. No elimina la causa ni acelera el servicio: recupera solo parte del impacto de satisfacción. `CustomerChange` no cuenta como fallo del restaurante. La aplicación no es repetible sobre la misma incidencia ya cubierta, puede reaparecer ante nuevas incidencias explícitas y todo su tuning de recuperación permanece configurable/provisional. |
| D-039 | VIGENTE | El timing contextual de Mesa/Cliente se extiende a `TakeOrder` y `FoodDelivery` reutilizando los contadores canónicos existentes. `TakeOrder` usa perfil configurable; `FoodDelivery` deriva Atención/Demora/Incidencia/Crítico del tiempo esperado real de la comanda, resolviendo los tiempos de preparación efectivos de la carta de la partida y usando el catálogo canónico solo como fallback, sin cronómetro paralelo ni tiempo fijo universal. `Explicar demora` entra desde Demora y `Disculpa` desde Incidencia/Crítico. `Priorizar atención` permanece pendiente de ratificación y no se implementa. |