# Bistro Builder — Ramas activas

**Fecha:** 2026-10-05
**Objetivo:** que el usuario no tenga que memorizar decenas de ramas técnicas.

## Regla simple

Para el trabajo normal de Bistro Builder solo existe **una MASTER del proyecto**:

**MASTER = `integration/master-current-20260918`**

Las demás ramas son ramas de trabajo. Cuando un trabajo se valida, se integra deliberadamente en MASTER. No se crea otra rama que compita conceptualmente con MASTER.

## Lo único que hay que recordar

| Nombre humano | Rama / repositorio | Para qué sirve | Quién la toca |
|---|---|---|---|
| **MASTER** | `integration/master-current-20260918` | Base estable acumulativa de Bistro Builder y origen de builds globales | Solo integraciones validadas |
| **EDITOR V2** | `feature/editor-v2` | Desarrollo controlado de Editor V2 | Hilo/agente de Editor V2 |
| **SAVIC** | `feature/savic-v1` | Desarrollo y hardening de SAVIC | Agente responsable de SAVIC |
| **ASSETS4ALL** | repositorio Assets4All; rama activa observada `feature/v0.1.19-pof2a-universal-scale` | Desarrollo de Assets4All / Boundary AI | Agente responsable de Assets4All |

## Worktrees principales

- MASTER:
  `C:\Users\mruperez\ProyectoBB\BistroBuilder_MasterIntegration_20260918`

- EDITOR V2:
  `C:\Users\mruperez\ProyectoBB\BistroBuilder_EditorV2`

- SAVIC:
  `C:\Users\mruperez\ProyectoBB\BistroBuilder_SAVICV1`

- Assets4All:
  `C:\Users\mruperez\Assets4All_LocalGate\candidate`

## Ramas de agentes

Puede haber ramas temporales como `codex/*`, `wip/*`, `validation/*`, `fix/*` o ramas de integración de prueba.

**El usuario no tiene que recordar sus nombres.**

Reglas:

1. el agente que crea una rama temporal es responsable de saber para qué sirve;
2. antes de integrar, se compara contra MASTER;
3. no se hace merge masivo de una rama vieja solo para rescatar una mejora;
4. una rama temporal no se convierte en una segunda MASTER;
5. cuando el trabajo queda absorbido y no contiene WIP exclusivo, puede archivarse/eliminarse en una limpieza Git separada y auditada;
6. nunca se borra una rama o worktree con cambios no publicados sin auditarlo primero.

## Assets4All → SAVIC

La conexión Assets4All → SAVIC está siendo desarrollada en paralelo.

A fecha de este documento no se designa una rama puente como nueva autoridad canónica. El resultado deberá integrarse de forma controlada:

**Assets4All → contrato estable → adaptador SAVIC → SAVIC → Bistro Builder**

Cuando los agentes terminen:

1. estabilizar primero SAVIC;
2. identificar exactamente qué commits pertenecen al puente Assets4All → SAVIC;
3. integrar el puente sobre la versión estable de SAVIC;
4. ejecutar el gate completo de cinco familias;
5. solo después incorporar el resultado validado a MASTER.

## Regla para futuros chats y agentes

Si existe duda sobre qué rama usar:

**no preguntar al usuario que recuerde el nombre.**

El agente debe:

1. leer este documento;
2. comprobar el estado real de Git;
3. comprobar worktrees y cambios locales;
4. trabajar sobre la rama humana correspondiente;
5. actualizar este documento cuando cambie una rama activa.

## Builds

La build global que representa el estado acumulativo del proyecto se genera desde **MASTER**.

Una build de una feature se etiqueta como build de prueba de esa feature y nunca sustituye conceptualmente a MASTER.

## Estado actual

- MASTER: estable y limpia al iniciar B0 Editor V2.
- EDITOR V2: creada desde MASTER + documentación canónica; B0 cerrado sin cambios funcionales.
- SAVIC: trabajo activo en paralelo; existen cambios locales/agentes y no se deben tocar desde Editor V2.
- Assets4All: trabajo activo en repositorio separado.

Con esta política, el usuario solo necesita decir **MASTER**, **EDITOR V2**, **SAVIC** o **ASSETS4ALL**. Los nombres técnicos secundarios quedan bajo responsabilidad de los agentes.
