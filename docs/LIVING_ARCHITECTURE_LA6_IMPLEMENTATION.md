# BB Living Architecture — LA6 Impacto Bistro Builder

Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

## Alcance implementado

- `ArchitectureImpactService` analiza una propuesta A→B antes del commit y devuelve un reporte estructurado.
- Formato canónico de incidencia: `Severity + SourceSystem + EntityId + ReasonCode + HumanMessage + OptionalSuggestedDelta`.
- Severidades V1: `Info`, `Warning`, `Blocking` y `SystemError`.
- Fuentes tipadas: `Architecture`, `Placeables`, `Seating`, `Circulation` y `External`.
- Contratos read-only específicos para colocables, seating y circulación sin transferir autoridad al Domain arquitectónico.
- Puentes reutilizables `ArchitecturePlaceablesImpactAdapter`, `ArchitectureSeatingImpactAdapter` y `ArchitectureCirculationImpactAdapter`; la composición runtime puede conectar las autoridades canónicas existentes mediante consultas, sin dependencias Domain→MonoBehaviour.
- Cada adaptador recibe clones aislados de A/B. Si intenta mutarlos, LA6 detecta el fingerprint cambiado, descarta sus resultados y emite `LA6_ADAPTER_MUTATED_READONLY_SNAPSHOT`; el snapshot real y la propuesta original permanecen intactos.
- Una excepción de un sistema externo no rompe ni modifica la reforma: se convierte en `LA6_ADAPTER_EXCEPTION` con severidad de sistema.
- Orden y deduplicación deterministas para que el mismo A+B+estado externo produzca el mismo reporte.
- `HasBlockingIssues` distingue consecuencias informativas de incidencias que deben impedir una confirmación segura.
- Correcciones locales opcionales mediante `ArchitectureSuggestedDelta`, incluyendo desplazamiento mínimo y explicación para Presentation.
- Impactos arquitectónicos internos detectados sin persistir habitaciones: región creada/eliminada y señal de división/fusión a nivel de planta.

## Autoridad

LA6 **no mueve colocables, no reasigna seating, no recalcula rutas como autoridad y no modifica Arquitectura Viva**. Solo consulta y describe consecuencias antes del paso de commit. Las autoridades de colocación, seating y circulación siguen siendo las existentes en Bistro Builder.

Los adaptadores concretos de escena se componen en las capas Application/Runtime cuando LA7–LA9 necesiten presentarlos. El Domain no referencia `GameObject`, `Transform`, `MonoBehaviour`, NavMesh ni clases concretas de los sistemas previos.

## Self-test puro LA6

`ArchitectureImpactSelfTest` incluye 12 casos:
1. propuesta sin impactos externos;
2. impacto de colocables;
3. impacto de seating;
4. impacto de circulación;
5. conservación de corrección mínima sugerida;
6. orden determinista aunque cambie el orden de adaptadores;
7. deduplicación estable;
8. aislamiento de excepción externa;
9. detección y aislamiento de mutación read-only;
10. detección de región creada;
11. gate `Warning` frente a `Blocking`;
12. preservación de fingerprints A/B.

## Gates pendientes que requieren Unity real

- compilación C# del proyecto completo;
- ejecución acumulativa LA1+LA2+LA3+LA4+LA5+LA6;
- confirmar **12/0** en `ArchitectureImpactSelfTest`;
- enlazar los adaptadores runtime con las autoridades reales al entrar en LA7/LA9 y comprobar sus consultas en Play Mode;
- Console 0 errores/excepciones/asserts.

No se declara LA6 validado/cerrado hasta superar esos gates.
