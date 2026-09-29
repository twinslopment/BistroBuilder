# Bistro Builder — Presentation & Interaction Quality

**Estado:** IMPLEMENTACIÓN EN VALIDACIÓN  
**Rama activa:** `feature/bb-presentation-interaction-quality-v1`  
**Base funcional:** Universal Preview V2 validado e integrado.  
**Objetivo:** elevar la lectura visual del Modo Edición y del restaurante desde whitebox funcional hasta una presentación coherente, legible y comercial, sin duplicar autoridades de gameplay.

## 1. Principio rector

La mejora de calidad pertenece a **Presentation**.

No cambia:
- BBSIS ni sus contratos espaciales;
- Interaction & Reservation;
- Navigation & Crowd Flow;
- reglas de placement;
- footprints/colliders como autoridad;
- economía;
- Save/Load;
- cámara profesional ni sus controles.

La Presentation puede suavizar, ocultar o sustituir visualmente una geometría provisional, pero nunca convertir esa representación visual en autoridad funcional.

## 2. Jerarquía visual

Durante Modo Edición:
1. el restaurante/viewport es el protagonista;
2. el objeto manipulado debe leerse antes que los paneles;
3. catálogo e inspector explican, pero no sustituyen el feedback in-world;
4. la cuadrícula apoya y nunca domina;
5. controles transitorios o legacy no pueden competir con el chrome canónico.

Fuera de Modo Edición:
- desaparecen guías, cuadrícula y feedback provisional;
- el restaurante recupera el protagonismo;
- los actores operativos vuelven a mostrarse normalmente.

## 3. Manipulación de mobiliario

Secuencia canónica:

`intención → pickup → seguimiento continuo → snap semántico → validación → confirmación → settle`

### Pickup
- elevación corta y perceptible;
- altura adaptativa al tamaño del asset;
- easing de salida;
- sombra de contacto de Presentation, suave y sin collider, para hacer perceptible la elevación y el settle;
- el objeto original deja de dibujarse y Presentation usa un proxy visual.

### Movimiento libre
- la pose lógica puede continuar cuantizada para placement;
- la pose visual sigue la intención del puntero de forma continua;
- SmoothDamp tiene límite de retraso para evitar efecto gomoso;
- rotación visual interpola hacia la pose lógica.

### Snap
- snap funcional conserva autoridad en `RestaurantPlacementSnapService`;
- Presentation muestra:
  - ancla/diamante;
  - halo expansivo corto;
  - línea de intención hacia el objeto relacionado cuando existe `RelatedObject`;
- la entrada al snap reduce inercia visual para transmitir magnetismo.

### Conflicto
- no se tinta el objeto completo por defecto;
- se destaca la geometría conflictiva cuando la autoridad la expone;
- pulso breve localizado;
- el inspector explica el motivo, pero el mundo debe señalar primero dónde está el problema.

### Confirmación
- descenso corto con sensación de peso;
- la representación converge exactamente a la pose funcional antes de liberar el proxy.

## 4. Universal Preview

Una única gramática visual para:
- mobiliario/equipamiento;
- paredes;
- habitaciones;
- openings;
- superficies;
- módulos;
- zonas futuras.

Reglas:
- sin hologramas opacos;
- sin verde/rojo de debug sobre todo el asset;
- footprint y contact fill suaves;
- conflicto más visible que validez normal;
- snap más perceptible que movimiento libre;
- cuando el snap expone `RelatedObject`, una línea de intención breve hace legible la relación semántica (por ejemplo silla → mesa);
- grosor adaptativo a distancia de cámara;
- construcción y mobiliario deben parecer partes del mismo sistema.

## 5. Escenario y whitebox

Mientras existan primitivas funcionales:
- sus colliders y componentes permanecen;
- Presentation puede sustituir solo su renderer.

### Política runtime
`BistroBuilderPrototypePresentationService`:
- aplica materiales canónicos a primitivas de mobiliario/equipamiento;
- aplica acabado cálido/arquitectónico a obstáculos whitebox;
- instala proxies visuales de mesa cuando una mesa funcional sigue siendo un cubo;
- durante Modo Edición oculta primitivas visuales de clientes/camareros para que un banco técnico no contamine la evaluación del editor;
- añade un plinto visual oscuro bajo `Floor_Test` para que el local no se lea como una lámina flotante sobre el vacío;
- al salir de edición restaura exactamente sus renderers.

### Mesa provisional
Proxy interno de mesa del `BistroBuilderPrototypePresentationService`:
- conserva la mesa funcional original;
- no crea colliders;
- dibuja tablero, cuatro patas y faldón con la malla cúbica funcional como fuente, sin crear primitivas/colliders nuevos;
- usa material canónico de madera;
- proyecta/recibe sombras;
- no altera plazas, TableId, seat bays, footprint ni persistencia;
- replica el `MaterialPropertyBlock` de la mesa funcional para conservar acentos de estado.

Esta política es temporal: cuando un asset real sustituya al whitebox, el proxy deja de ser necesario.

## 6. Materiales e imagen PC

Base actual:
- suelo: `Suelo_caliza`;
- madera provisional: `Roble_marcos`;
- arquitectura provisional: `Enlucido_calido`;
- equipamiento provisional: `Metal_grafito`.

Render PC:
- SMAA High por cámara;
- post-processing URP activo;
- ACES;
- AO moderado para contacto;
- contraste/saturación leves;
- bloom y viñeta mínimos;
- sombras de calidad alta;
- no usar TAA mientras pueda introducir ghosting en manipulación rápida y no exista una necesidad visual demostrada.

## 7. Cuadrícula

- líneas menores finas y de baja opacidad;
- líneas mayores más legibles;
- atenuación con distancia/zoom;
- visible únicamente cuando aporta información;
- nunca debe hacer que el viewport parezca Scene View.

## 8. UI de Modo Edición

### Chrome
- una sola barra superior canónica;
- una sola barra inferior de herramientas/acciones;
- el antiguo selector flotante Normal/Edición queda retirado.

### Diseño inicial
- acciones de guardado/validación se muestran en ribbon claro y compacto;
- no se usa panel negro permanente sobre el viewport.

### Inspector de artículo
- ancho contenido;
- altura calculada por contenido;
- preview se oculta si el artículo no dispone de imagen;
- durante una colocación activa el inspector entra en modo compacto (título + estado) para devolver espacio al viewport;
- no reservar grandes zonas vacías;
- reglas y estado de placement permanecen visibles;
- catálogo e inspector no deben reducir innecesariamente el viewport.

## 9. Persistencia de assets

Invariante:
> Todo `RestaurantPlaceableItemDefinition` que el catálogo jugable considera colocable debe ser resoluble por `BistroBuilderSaveDefinitionCatalog` con el mismo ItemId.

No se permiten listas paralelas divergentes por asset.

El catálogo Save adopta las definiciones jugables canónicas en runtime y conserva las referencias legacy necesarias para partidas antiguas.

## 10. Audio de interacción

Contrato previsto:
- pickup;
- snap;
- rotate;
- confirm;
- reject.

No se generan beeps/procedural placeholders para una evaluación de calidad comercial. La implementación sonora se activará con assets de audio autorados/licenciados adecuados. Actualmente el repositorio no contiene archivos de audio utilizables para esta capa.

## 11. Gate de aceptación visual

No se considera cerrado por compilar.

Debe verificarse en Game View:
- mover una silla se percibe continuo;
- pickup y settle se distinguen;
- un Seat Bay/snap se reconoce sin leer el inspector;
- un conflicto se localiza visualmente;
- mesa provisional ya no parece un cubo de Unity;
- el entorno de edición no muestra cápsulas/esferas de actores whitebox;
- cuadrícula ayuda sin dominar;
- inspector no contiene grandes vacíos;
- no reaparecen selector flotante ni panel negro de diseño inicial;
- salir de edición restaura presentación operativa;
- colocar una definición jugable y guardar no falla por ItemId no resoluble.

Solo después de estas comprobaciones y aprobación visual del usuario se integrará esta pasada en `integration/master-current-20260918`.
