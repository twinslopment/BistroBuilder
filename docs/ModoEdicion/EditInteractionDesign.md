# Bistro Builder — Edit Interaction Design

**Documento canónico:** decisiones aprobadas sobre interacción del modo edición.
**Estado:** ACTIVO
**Última actualización:** 2026-09-28

## 1. Propósito

Este documento recoge únicamente comportamientos de edición aprobados para Bistro Builder.
No es un listado de ideas ni una copia de referencias externas.
Cada decisión debe quedar definida desde la experiencia visible del jugador y desde su integración con los sistemas existentes.

## 2. Reglas permanentes

- La experiencia deseada manda sobre la implementación heredada: si una parte antigua impide alcanzar el comportamiento aprobado, se rediseña.
- No se aplicarán parches específicos por asset ni cadenas de excepciones frágiles.
- No se sustituirá un sistema existente que ya resuelva bien su responsabilidad.
- Toda interacción nueva debe integrarse con los sistemas transversales que le afecten.
- Una característica solo podrá figurar como **APROBADA** cuando se cumplan las dos condiciones: el usuario la quiere para Bistro Builder y su viabilidad real ha sido confirmada técnicamente para el proyecto, ya sea con los sistemas actuales o mediante un rediseño limpio y razonable.
- Si la viabilidad no está suficientemente comprobada, la característica no se registrará como aprobada.
- El modo edición debe ocultar la complejidad técnica al jugador y ofrecer feedback inmediato, claro y reversible.
- El estado definitivo del restaurante solo se modifica cuando una operación de edición ha sido validada y confirmada.
- Las referencias externas sirven como inspiración de comportamiento; no se copian arquitecturas sin comprobar su encaje con Bistro Builder.

## 3. Sistemas que deben tenerse en cuenta

- BBSIS — Spatial Interaction System.
- Interaction & Reservation System.
- Navigation & Crowd Flow.
- Character & Interaction Animation System.
- SAVIC.
- Edit Feedback System.
- Selección, snapping y validación del modo edición actual.
- Cámara y presentación visual del modo edición.
- Save/Load y persistencia.
- Gameplay posterior del objeto colocado: clientes, empleados, cocina, reservas, servicio y demás sistemas afectados.

## 4. Decisión aprobada 001 — Coger, mover y soltar mobiliario

**Estado:** APROBADO  
**Referencia conceptual:** comportamiento observado en Set the Mood, adaptado a Bistro Builder.

### Experiencia del jugador

1. El jugador selecciona un mueble u objeto editable.
2. Al iniciar el arrastre, el objeto se eleva ligeramente de forma suave.
3. El objeto sigue el cursor con movimiento fluido, sin sensación de arrastre plano o brusco.
4. Durante el transporte se mantiene claramente en estado de edición provisional.
5. El sistema propone posiciones mediante snapping contextual cuando corresponde.
6. El jugador recibe feedback continuo sobre si la posición propuesta es válida o no.
7. Mientras se mueve, el objeto no debe atravesar de forma aceptada otros objetos, límites o elementos incompatibles.
8. Al soltar en una posición válida, el objeto baja y se asienta suavemente.
9. Al soltar en una posición no válida, la operación no se confirma.
10. La interacción debe poder cancelarse y deshacerse de forma limpia.

### Sensación visual

- El levantamiento y asentamiento serán breves y sutiles.
- El movimiento debe transmitir que el jugador está transportando el objeto, no moviendo un icono.
- Se permitirá una diferencia ligera de sensación de peso por familia de objeto.
- Ejemplo orientativo: una silla responde casi de inmediato; una mesa o frigorífico puede sentirse algo más asentado.
- La diferencia de peso será visual y de respuesta, no una simulación física pesada.

### Regla de implementación

El objeto definitivo no debe tratarse como una pieza confirmada mientras el jugador lo está desplazando.
Durante el movimiento se trabajará con un estado provisional de colocación que permita previsualizar, hacer snapping, validar, cancelar y confirmar sin ensuciar el estado real del restaurante.

La confirmación de la nueva posición solo ocurre al completar correctamente la operación de soltar.

### Integración obligatoria

- **BBSIS:** validará requisitos espaciales, zonas de uso, envolventes y restricciones relevantes.
- **Navigation & Crowd Flow:** la colocación confirmada deberá actualizar o invalidar lo necesario para navegación y flujo.
- **Interaction & Reservation System:** deberá preservar la coherencia de asignaciones, permisos, claims o usos afectados.
- **Character & Interaction Animation System:** el objeto recolocado debe seguir siendo utilizable por personajes.
- **SAVIC:** los assets deben aportar los datos necesarios para que el sistema sepa qué son y cómo pueden colocarse.
- **Edit Feedback System:** gestionará la representación visual de preview, validez, snapping y confirmación.
- **Save/Load:** la transformación confirmada debe persistir y rehidratarse correctamente.
- **Gameplay:** una silla sigue siendo una silla funcional después de recolocarla; una mesa sigue conservando sus relaciones y reglas.

### Criterio de aceptación

La operación se considerará terminada únicamente cuando la experiencia visual sea fluida y, tras colocar el objeto, todos los sistemas afectados sigan reconociéndolo y funcionando correctamente.

## 5. Decisión aprobada 002 — Preview antes de confirmar

**Estado:** APROBADO  
**Viabilidad técnica:** CONFIRMADA sobre la arquitectura actual de colocación.

### Regla funcional

Toda colocación o movimiento debe mostrar un estado provisional claramente diferenciado antes de modificar el estado definitivo del restaurante.
La confirmación seguirá siendo responsabilidad del sistema transaccional existente.

### Base existente que se conserva

- `RestaurantPlacementTransactionService` ya soporta posiciones candidatas, validación, confirmación y cancelación.
- `RestaurantEditInteractionController` ya mantiene y publica una pose provisional.
- El snapping existente seguirá proponiendo posiciones; la validación seguirá teniendo la última palabra.
- BBSIS y Navigation seguirán actualizándose de forma controlada y no se reconstruirán innecesariamente cada frame.
- Undo/Redo y Save/Load continuarán trabajando sobre cambios confirmados.

### Nueva capa visual requerida

La preview actual de color válido/inválido se considera insuficiente como experiencia final.
Se sustituirá o ampliará por un sistema visual premium que pueda combinar, sin ensuciar el estado real:
- representación provisional del objeto;
- contorno y lectura de validez;
- huella o zona de apoyo en el suelo;
- guías de snapping contextuales;
- visualización localizada del conflicto cuando la posición sea inválida;
- transición visual de asentamiento cuando se confirma.

La dirección artística exacta de esta preview se decidirá por separado antes de implementarla.
## 6. Decisión aprobada 003 — UX de manipulación directa

**Estado:** APROBADO  
**Viabilidad técnica:** CONFIRMADA.

La UX del modo edición se rediseñará priorizando manipulación directa, feedback inmediato y mínimo número de pasos.

### Regla de compatibilidad

- Se conservarán los servicios de dominio y aplicación que ya resuelven correctamente selección autorizada, colocación, validación, snapping, transacciones, historial, BBSIS, navegación, persistencia y reglas de gameplay.
- La capa de presentación e interacción podrá modificarse o sustituirse cuando limite la experiencia objetivo.
- No se conservará una interacción antigua solo por existir si obliga al jugador a realizar pasos artificiales o poco naturales.
- No se duplicarán sistemas funcionales ya existentes.

### Objetivo de experiencia

El jugador debe pensar en la acción que quiere realizar —seleccionar, coger, mover, girar, colocar, inspeccionar— y no en qué subsistema debe manejar.
La concreción de gestos, paneles y accesos se aprobará por decisiones posteriores.

## 7. Decisión aprobada 004 — Sistema global de cámaras

**Estado:** APROBADO  
**Viabilidad técnica:** CONFIRMADA sobre el sistema de cámara existente 369A/369B/369C.

La cámara profesional actual pasa a considerarse el núcleo del sistema global de cámaras de Bistro Builder, no una solución exclusiva del modo edición.

### Base existente que se conserva

- `BistroBuilderProfessionalCameraController` como controlador principal de navegación.
- `BistroBuilderCameraViewService` para vistas y encuadres.
- `BistroBuilderCameraInspectionService` para contexto, memoria por modo e inspección de objetivos.
- Ajustes, límites, suavizado, zoom, giro, desplazamiento y validadores ya implementados.

### Evolución aprobada

- El juego tendrá contextos de cámara coordinados para servicio, edición, inspección y futuras vistas de presentación.
- Los cambios de contexto deben ser suaves y conservar memoria cuando corresponda.
- La cámara podrá encuadrar objetivos del juego sin crear controladores paralelos.
- Las vistas predefinidas y futuras cámaras de presentación se integrarán sobre el mismo núcleo.
- No se introducirá otro sistema de cámara independiente salvo necesidad técnica demostrada.

### Regla

La ampliación debe respetar el comportamiento estable ya conseguido en 369A/369B/369C. Se amplía el sistema; no se rehace gratuitamente.

## 8. Decisión aprobada 005 — BB Universal Preview System

**Estado:** APROBADO  
**Viabilidad técnica:** CONFIRMADA.  
**Implementación:** fundación V1 creada en `feature/bb-universal-preview-v1`.

### Objetivo

Bistro Builder tendrá un único sistema transversal de preview para edición y construcción. Los sistemas funcionales siguen siendo autoridad sobre sus reglas; el sistema universal es la autoridad sobre cómo se representa cualquier estado provisional al jugador.

### Alcance

El mismo núcleo visual debe servir para:

- mobiliario y equipamiento;
- paredes;
- habitaciones;
- puertas y ventanas;
- superficies;
- módulos estructurales;
- zonas y futuras herramientas de edición que necesiten previsualización.

No se crearán sistemas visuales independientes por familia salvo un adaptador especializado que publique sus datos en el núcleo universal.

### Principios visuales

- Aspecto elegante, sobrio y de juego comercial.
- Objeto real o representación visual fiel; no hologramas genéricos.
- Elevación visual suave al transportar mobiliario.
- Huella/contorno de colocación limpia y discreta.
- Ghost tenue de la posición anterior cuando aporte información.
- Snapping con guías breves y pulso suave.
- Posición válida con confirmación visual contenida.
- Posición inválida mostrando el conflicto de forma localizada siempre que los datos existentes lo permitan.
- Confirmación con asentamiento suave y retirada de ayudas.
- Sin tintar por defecto el objeto completo de verde/rojo.
- Sin cuadrículas, flechas o líneas técnicas permanentes cuando no sean necesarias.

### Arquitectura aprobada

- `BistroBuilderUniversalPreviewService` mantiene el estado visual provisional común.
- `BistroBuilderUniversalPreviewRenderer` representa guías, huellas, ghost, conflicto y snap.
- `BistroBuilderFurniturePreviewProxyRenderer` separa la presentación visual del mobiliario mientras se transporta, permitiendo elevación y asentamiento sin trasladar esa animación a las reglas espaciales.
- El controlador de mobiliario publica su validación y snapping existentes en el sistema universal.
- La herramienta de construcción publica paredes, habitaciones y huecos en el mismo núcleo.
- Los previews anteriores de `feature/18n-construction-authoring-v1` se conservan como referencia y fuente de comportamiento válido; no se descartan sin motivo.

### Sistemas existentes que conservan autoridad

- BBSIS decide viabilidad y requisitos espaciales cuando corresponda.
- Navigation & Crowd Flow conserva la autoridad sobre circulación.
- Placement Validation conserva la autoridad de colocación.
- Snapping propone posiciones, pero no confirma.
- Las transacciones de edición siguen separando provisional y confirmado.
- Undo/Redo, Save/Load y gameplay solo consideran definitivos los cambios confirmados.
- Construction Authoring conserva su modelo de borrador, geometría y materialización.
- SAVIC aporta identidad y datos de los assets; no decide la presentación de preview.

### Regla de extensibilidad

Una futura herramienta de superficies, módulos, BBPLFS u otra familia no debe crear otra estética de preview. Debe publicar su candidato, validez, snap, ghost y conflictos en BB Universal Preview System.

### Criterio de aceptación

El sistema se considerará definitivo cuando una operación equivalente tenga el mismo lenguaje visual en muebles y construcción, mantenga intactas las reglas existentes y permita cancelar, confirmar, deshacer, rehacer, guardar y recargar sin divergencias.

**Fundación técnica inicial:** commit `62bd2f2c`.

## 9. Registro de próximas decisiones

Las nuevas decisiones aprobadas se añadirán como secciones numeradas `006`, `007`, etc.
Cada sección deberá incluir comportamiento visible, reglas, integración con sistemas existentes y criterio de aceptación.
