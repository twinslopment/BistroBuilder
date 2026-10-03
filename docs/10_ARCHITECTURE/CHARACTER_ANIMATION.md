# BB Character & Interaction Animation System

**Estado canónico:** V1 INTEGRADO, VALIDADO Y SUBIDO. Cualquier ampliación futura se trata como V2/hardening.

## Arquitectura vinculante
`Gameplay decide → BBSIS valida → Navigation llega → Animation representa`.

## Autoridad
Animation controla únicamente representación visual del personaje: selección de Motion Recipe equivalente, blending, layers/masks, IK/constraints visuales, gaze, progreso visual, interruptibilidad, recovery y LOD.

## Modelo de datos
- `Motion Recipes` como recetas de representación;
- `Interaction Family Profile` para familias de interacción;
- `Asset Interaction Descriptor` para capacidades visuales del asset;
- `Runtime Resolved Plan` como plan visual resuelto en ejecución.

## Reglas
- No decide navegación, reservas espaciales, derechos lógicos ni ownership de props.
- No cambia el resultado de gameplay.
- No almacena una segunda verdad sobre seating, workstation o custody.
- Debe tolerar interrupciones, cancelaciones y rehidratación de estado sin romper las autoridades externas.
- El pipeline de authoring/validación debe ser no destructivo y disponer de validadores.

## Integraciones
Navigation proporciona movimiento lógico y llegada; BBSIS anchors/puertos válidos; Interaction puede exponer el derecho lógico; sistemas de objetos exponen su estado. Animation traduce esos hechos a una representación creíble sin apropiarse de ellos.

## Ampliación autorizada SAVIC: clientes en taburetes de barra

El prefab canónico de clientes puede referenciar un perfil Humanoid certificado. Cada miembro visual registra su propio actor en Animation V1; el bootstrap evita registrar también la raíz lógica sobre su Animator. El presentador de barra observa plaza/ocupación/asiento nativos, llegada real de Navigation y lease activo BBSIS, y representa sit/idle/stand mediante Motion Recipes existentes. Alinea solo el visual y la pelvis al SeatFrame con offset común autorado; el root lógico, reservas y resultados permanecen en sus autoridades. Cancelación, baja y rehidratación limpian la sesión visual propia.

Los tres taburetes reales pasan ocupación/llegada/lease, postura Humanoid sentada, root intacto, stand y cleanup; repetidos tras dos cargas SaveGame por asset. Capturas inspeccionadas sin materiales de error. No se certifica un nuevo ciclo walk, asiento de mesa ni jornada completa de IA. Pruebas aíslan otros flujos del cliente y usan ocupación del registro nativo; el checkpoint está desocupado. Evidencia y límites: SAVIC 77–78 y `bar-stool-main-catalog-runtime-acceptance.log` (03/10/2026 01:08 UTC, exit 0).

Reprueba estricta 01:14:31 UTC: MainCatalog/SaveDefinitionCatalog exactos, bindings activados sin reconfiguración diagnóstica y seis cargas reales en total, tres capturas inspeccionadas. `bar-stool-main-catalog-strict-native-acceptance.log`, exit 0. Animation V1 13/13 y clientes 10G PASS en la regresión final; SAVIC 79–80.
