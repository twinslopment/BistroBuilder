# Bistro Builder — persistencia y Save/Load

## Estado
La base 366/366B y los hitos funcionales 367 asociados están validados dentro de su alcance. La persistencia continúa siendo una preocupación transversal: cada nuevo sistema debe integrarse en el SaveGame universal, no crear guardados paralelos.

## Principios vinculantes
- Cada dominio conserva su snapshot versionado y una identidad estable.
- Los proveedores definen fases coherentes de Prepare / Apply / Finalize cuando existen dependencias.
- La prevalidación debe poder rechazar datos inválidos antes de mutar estado vivo.
- Guardar durante servicio activo debe permitir reconstruir bindings y estado operativo sin duplicados.
- Las referencias de escena no sustituyen IDs persistentes.
- Un Load debe ser determinista respecto al snapshot cargado y no depender de búsquedas tardías o temporizadores arbitrarios.

## Secciones conocidas
- `game.general`: base general 366B.
- estructura del restaurante y seating.
- `service.runtime`: servicio activo y agentes/órdenes operativos.
- `staff.state`: empleados persistentes.
- `staff.schedule`: planificación de turnos.
- `finance.runtime`: autoridad financiera.
- estados de inventario, proveedores, reservas, progresión y demás dominios integrados.
- `climate.runtime`: estado climático V1 cuando su rama se integre/cierre.

## Regla de integración
Si un sistema necesita restaurar una relación entre dos autoridades, debe persistir identificadores/estado mínimo y reconstruir el binding en el orden correcto. No serializar GameObjects como sustituto del dominio.

## Gate
Todo sistema nuevo o ampliado debe demostrar round-trip, carga cruzada cuando aplique, Save/Load en estados no triviales y ausencia de duplicación o corrupción tras repetición.

## Asientos dinámicos de barra — SAVIC

`restaurant.structure` v2 añade enlaces mínimos de taburete: InstanceId del asiento, InstanceId de la barra e índice estable de plaza nativa. No duplica ocupación, capacidad ni reservas. Su proveedor migra v1 de forma pura, conservando registros anteriores y una lista vacía cuando no existía el campo. La prevalidación comprueba IDs/roles/índices/duplicados y los frames de prefab en las poses guardadas antes de retirar instancias vivas; rechaza enlaces ausentes o incompatibles. Carga barras antes de taburetes y verifica la asociación resultante; prepara la retirada en orden inverso de dependencia.

Al limpiar estado transitorio, BBSIS sigue siendo autoridad del lease: un ID cacheado en el adaptador no prueba una reserva activa. La consulta y reconciliación de barra comprueban el lease real, incluyendo expiración, antes de declarar readiness o conceder uno nuevo.

Verificado 02/10/2026: migración/negativos y dos reconstrucciones JSON nativas de barra+taburete con cada uno de los tres GLB reales, IDs estables/nuevas instancias/ocupación y leases/cleanup. La barra publicada sí pasó SaveGame real en Play Mode con v2 y Console limpia (`bar-counter-native-seat-foundation-runtime-final.log`, 23:44 UTC). El SaveGame real de taburetes y su representación sentada permanecen pendientes; las pruebas aisladas no autorizan publicación. Detalle y evidencia en SAVIC 73–76.

Actualización 03/10/2026: los tres taburetes ya pasaron dos cargas SaveGame reales por asset, primero como candidatos y luego publicados en catálogo principal. Se reconstruyen ambos muebles con mismos ItemId/InstanceId y plaza/enlace nativos, nuevas instancias Unity y sin residuos; un cliente nuevo ocupa y se sienta mediante Animation antes y después de cada carga. El slot diagnóstico se elimina y Console permanece limpia hasta Editor. Las instantáneas guardadas no tienen ocupante: no se afirma restauración de sesión de servicio activa. Evidencia `bar-stool-main-catalog-runtime-acceptance.log`, exit 0, 01:08 UTC; SAVIC 77–78.

La reprueba estricta posterior resuelve cada ItemDefinition exacto desde MainCatalog y SaveDefinitionCatalog, sin catálogo candidato ni reconfiguración del binding en el fixture. Seis cargas, mismo resultado y Console limpia: `bar-stool-main-catalog-strict-native-acceptance.log`, exit 0, 03/10/2026 01:14:31 UTC; SAVIC 79–80.
