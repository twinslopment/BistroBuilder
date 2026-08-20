# 4G — Queen Test real de Personal

Estado: **runner implementado / no validado en Unity**.

## Objetivo

Cerrar el Bloque 4 únicamente después de demostrar, en una partida real y con rollback completo, que Personal funciona de extremo a extremo sin sustituir ninguna autoridad existente.

## Herramientas 4G

- `Tools > Bistro Builder > Personal > 4G - Queen Test preflight`: comprueba autoridades, wiring, Save y agentes antes de mutar la partida.
- `Tools > Bistro Builder > Personal > 4G - Autotest estático`: exige que el runner use las autoridades canónicas y prohíbe fabricar `Waiter`, `WaiterTask`, otra cola o aplicar XP directamente.
- `Tools > Bistro Builder > Personal > 4G - QUEEN TEST reversible`: ejecuta el flujo final con rollback integral y dos slots temporales libres del rango 980–989.

## Precondición obligatoria

Ejecutar primero el preflight en Play Mode. Debe devolver 0 errores y comprobar:

- autoridad única de SaveGame, Staff, Recruitment, Development, Session, Facade y Screen;
- registro real de `staff.state`, `staff.recruitment` y `staff.session.runtime` en SaveGame;
- mercado de candidatos disponible;
- snapshots válidos de plantilla y binding;
- UI 4F correctamente cableada;
- agentes `Waiter` reales disponibles para el binding;
- servicio `Closed` y sin sesión 4D activa para iniciar el Queen flow.

## Queen flow reversible implementado

1. Localiza dos slots diagnósticos libres entre 980 y 989 y guarda rollback integral con el SaveGame universal.
2. Abre/cierra la pantalla 4F real y comprueba que Presentation puede mostrarse sin convertirse en autoridad.
3. Captura `staff.state`, mercado, `staff.session.runtime`, estado de servicio y número real de `Waiter`.
4. Contrata un candidato mediante `BistroBuilderStaffPlayerFacade` y exige:
   - desaparición del `CandidateId`;
   - `EmployeeId` nuevo y válido;
   - incremento exacto de plantilla;
   - número de `Waiter` inalterado.
5. Cambia la disponibilidad del empleado a `Unavailable` y de vuelta a `Available` por la autoridad canónica.
6. Ejecuta una formación V1 gratuita real mediante 4C/fachada; no se crea ninguna integración financiera alternativa.
7. Deja temporalmente no disponibles los demás empleados con rol operativo de camarero. El rollback inicial cubre esta mutación y garantiza que el empleado recién contratado sea el único elegible para la sesión diagnóstica.
8. Pasa `Closed → Preparing`, inicia 4D y exige binding real `EmployeeId ↔ WaiterId`; después abre `Open` mediante `RestaurantServiceStateService`.
9. Espera hasta 180 s una tarea **real** completada observada por 4D. El runner no crea clientes, tareas, colas, XP ni métricas.
10. Guarda checkpoint con servicio `Open`, deja evolucionar el runtime unos segundos de forma natural y carga el checkpoint.
11. Tras Load exige igualdad exacta de `staff.state`, mercado y `staff.session.runtime`, mismo estado `Open`, mismo número de `Waiter`, mismo `WaiterId` ligado y mismas métricas guardadas.
12. Inicia `Closing` y espera hasta que `WaiterTaskCoordinator.ActiveTaskCount == 0` y todos los agentes ligados estén `Idle`; solo entonces completa `Closed`.
13. Exige que 4D haya aplicado XP y rendimiento exactamente una vez al empleado objetivo y vuelve a invocar la finalización para comprobar idempotencia sin mutación adicional.
14. Guarda/carga un segundo checkpoint `Closed` y exige persistencia exacta de plantilla, mercado, XP/skills/rendimiento y sesión inactiva.
15. Carga el rollback inicial, comprueba igualdad exacta del estado previo y elimina ambos slots diagnósticos.

## Gates de cierre

El Bloque 4 no puede declararse cerrado hasta obtener simultáneamente:

- Unity compila con 0 errores;
- instaladores 4A–4F sin error;
- validadores estructurales sin error;
- autotests estáticos/puros sin fallos;
- Queen Test 4G real completo con rollback confirmado;
- prueba visual 4F sin referencias vacías, duplicados de filas ni bloqueo de input;
- Save/Load durante servicio activo sin duplicar `Employee`, `Waiter`, tareas ni secciones;
- logs Unity sin errores.

## Principio de seguridad

El Queen Test nunca fabrica una segunda fuente de verdad. Toda mutación pasa por las autoridades ya implementadas y el rollback utiliza el SaveGame universal. Si el servicio real no produce trabajo observable dentro del timeout, la prueba falla y restaura el rollback; nunca sustituye esa ausencia por métricas sintéticas.
