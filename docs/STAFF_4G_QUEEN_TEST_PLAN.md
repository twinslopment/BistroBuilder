# 4G — Queen Test real de Personal

Estado: **preparado / no validado en Unity**.

## Objetivo

Cerrar el Bloque 4 únicamente después de demostrar, en una partida real y con rollback completo, que Personal funciona de extremo a extremo sin sustituir ninguna autoridad existente.

## Precondición obligatoria

Ejecutar primero `Tools > Bistro Builder > Personal > 4G - Queen Test preflight` en Play Mode. El preflight debe devolver 0 errores y comprobar:

- autoridad única de SaveGame, Staff, Recruitment, Development, Session, Facade y Screen;
- registro real de `staff.state`, `staff.recruitment` y `staff.session.runtime` en SaveGame;
- mercado de candidatos disponible;
- snapshots válidos de plantilla y binding;
- UI 4F correctamente cableada;
- agentes `Waiter` reales disponibles para el binding.

## Queen flow reversible previsto

1. Localizar dos slots diagnósticos libres y guardar rollback integral de la partida.
2. Abrir la UI 4F real desde el botón `Personal`.
3. Registrar identidad y revisiones iniciales de plantilla, mercado y sesión.
4. Contratar un candidato real desde 4B y verificar que:
   - desaparece `CandidateId` de la oferta;
   - aparece un `EmployeeId` nuevo y estable;
   - no se crea ningún `Waiter` adicional.
5. Cambiar disponibilidad del empleado y restaurarla, comprobando revisionado y guardas de sesión.
6. Ejecutar una formación V1 de coste 0 si existe una formación elegible; confirmar skill/revisión y que no aparece ningún movimiento financiero paralelo.
7. Preparar/abrir un servicio real y comprobar binding `EmployeeId ↔ WaiterId` sin sustituir `WaiterTaskCoordinator`.
8. Ejecutar trabajo real de sala suficiente para producir métricas observables.
9. Guardar checkpoint durante el servicio activo.
10. Alterar estado runtime de manera segura y cargar el checkpoint.
11. Verificar después de Load:
    - mismo `EmployeeId`;
    - mismo mercado de candidatos;
    - mismo binding `EmployeeId ↔ WaiterId`;
    - mismo estado de servicio;
    - ausencia de agentes duplicados;
    - ausencia de referencias huérfanas.
12. Cerrar servicio con todos los agentes libres y exigir que 4D consolide rendimiento/XP una sola vez mediante operationId idempotente.
13. Guardar/cargar de nuevo con el servicio cerrado y verificar persistencia de XP, skills, rendimiento y estado laboral.
14. Restaurar el rollback inicial y comprobar que el estado previo queda exactamente recuperado.
15. Eliminar slots diagnósticos.

## Gates de cierre

El Bloque 4 no puede declararse cerrado hasta obtener simultáneamente:

- Unity compila con 0 errores;
- instaladores 4A–4F sin error;
- validadores estructurales sin error;
- autotests estáticos/puros sin fallos;
- Queen Test real completo con rollback confirmado;
- prueba visual 4F sin referencias vacías, duplicados de filas ni bloqueo de input;
- Save/Load durante servicio activo sin duplicar `Employee`, `Waiter`, tareas ni secciones;
- logs Unity sin errores.

## Principio de seguridad

El Queen Test nunca fabrica una segunda fuente de verdad. Toda mutación debe pasar por las autoridades ya implementadas y el rollback debe usar el SaveGame universal.
