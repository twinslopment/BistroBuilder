# Bistro Builder — aceptación y validación

## Definición de PASS
Un sistema no queda cerrado solo porque compile. El cierre exige evidencia proporcional al riesgo y al alcance real.

## Gates mínimos
- compilación Unity limpia;
- instalación idempotente o no destructiva;
- validador estructural sin errores;
- autotest/self-test sin fallos;
- prueba funcional sobre la escena o flujo real;
- Save/Load cuando el sistema persista estado;
- Console final sin Error, Exception ni Assert inesperados;
- auditoría de autoridad: ninguna segunda fuente de verdad;
- regresiones relevantes de sistemas dependientes.

## Sistemas visuales/espaciales
Además de métricas, requieren evidencia visual o prueba jugable. No se acepta PASS basado únicamente en contadores automáticos cuando el defecto puede ser perceptivo o de interacción.

## Rendimiento
Las operaciones frecuentes del jugador deben evitar bloqueos perceptibles. Edición, selección, mover/eliminar y confirmaciones deben probarse en condiciones representativas, no solo en escenas mínimas.

## Persistencia
Round-trip, carga cruzada cuando corresponda, repetición y estados incómodos. No duplicar IDs, agentes, grants, estructura ni dinero tras Load.

## Queen / pruebas destructivas
Los bloques críticos deben incluir escenarios integrados que intenten romper invariantes, con rollback seguro cuando la prueba muta datos/escena.

## Cierre documental
Al declarar un bloque COMPLETO/VALIDADO/CERRADO, actualizar `00_PRODUCT/ROADMAP.md`, la documentación del sistema y la evidencia de validación en el mismo cambio.