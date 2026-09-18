# Bistro Builder — Sistema de Acabados y Variantes de Mobiliario

**Nombre técnico:** BB Furniture Finishes & Variants Authoring System (BBFFVAS)

**Estado:** IMPLEMENTACIÓN TÉCNICA V1 — núcleo validado en Unity 6000.3.19f1; integración en producción pendiente

**Ámbito:** herramienta interna de autoría para el creador; no es una mecánica ni una interfaz destinada al jugador.

## 1. Objetivo

BBFFVAS permite crear, completar, previsualizar, validar, organizar y publicar acabados y variantes visuales de mobiliario ya integrado en Bistro Builder sin duplicar prefabs manualmente, sin tocar código y sin editar materiales uno a uno en el Inspector estándar de Unity.

El caso base es un único mueble maestro —por ejemplo `BB_Chair_Master_001`— del que pueden publicarse múltiples acabados comerciales manteniendo geometría, pivote, footprint, colliders, contratos BBSIS y comportamiento.

## 2. Principios vinculantes

1. **Editor-only.** La herramienta existe para producción de contenido. El jugador no accede a ella.
2. **Geometría única.** Un cambio exclusivamente visual no crea otro modelo, collider ni contrato espacial.
3. **Variantes data-driven.** Una variante es una combinación estable de acabados asignados a zonas semánticas.
4. **Biblioteca reutilizable.** Maderas, telas, metales, piedra, vidrio, pintura, cuero y otros acabados se registran una vez y pueden reutilizarse en muchos muebles.
5. **No sobrescritura automática.** Ninguna automatización modifica trabajo visual válido salvo orden explícita del creador.
6. **Preview antes de aplicar.** Toda propuesta automática se previsualiza y requiere aprobación.
7. **IDs estables.** Renombrar un acabado o una variante no rompe referencias ni contenido publicado.
8. **Undo/Redo real.** Las operaciones de autoría deben ser reversibles mediante el sistema de edición del Editor.

## 3. Qué es y qué no es una variante

Una **variante de acabado** cambia apariencia: madera, color, tapizado, metal, piedra, vidrio, mapas PBR o parámetros compatibles.

No es una variante de acabado si cambia geometría, dimensiones, patas, brazos, respaldo, número de plazas, footprint, collider, puertos de interacción, animaciones o comportamiento. Ese caso pertenece a otro asset o a una variante estructural fuera de este sistema.

## 4. Modelo conceptual

```text
FurnitureDefinition
├─ furnitureId
├─ prefab/model reference
├─ finishProfile
│  ├─ semantic zones
│  └─ compatible finish families
├─ defaultVariantId
└─ variants[]
   └─ FurnitureFinishVariant
      ├─ variantId
      ├─ displayName
      ├─ swatch/thumbnail
      └─ bindings[]
         ├─ zoneId
         └─ finishId
```

Cada instancia de `FurnitureFinishVariant` referencia acabados canónicos; no almacena copias innecesarias de texturas o materiales.

## 5. Zonas semánticas de acabado

Cada asset define una sola vez sus zonas de acabado con nombres comprensibles. Ejemplos:

- Silla: `Structure`, `Seat`, `Back`, `Metal`.
- Mesa: `Top`, `Structure`, `Metal`.
- Sofá: `Frame`, `Fabric`, `Legs`.
- Lámpara: `Shade`, `Structure`, `Cable`, `BulbHousing`.

Una zona puede mapear uno o varios Material Slots y, cuando proceda, máscaras internas de un material.

La UI de autoría nunca debe obligar a trabajar con nombres opacos como `Element 0` o `Material.003` una vez exista clasificación semántica.

## 6. Familias de acabado

La biblioteca central clasifica los acabados al menos en:

- Wood
- Fabric
- Leather
- Metal
- Stone
- Glass
- Paint
- Plastic
- Ceramic
- Other

Cada zona declara las familias compatibles. La herramienta filtra automáticamente opciones incompatibles para evitar asignaciones absurdas.

## 7. Definición de un acabado

Un acabado no es únicamente un color. Puede contener:

```text
FinishDefinition
├─ finishId
├─ displayName
├─ family
├─ material/shader profile
├─ baseColor / albedo
├─ normal
├─ roughness or smoothness
├─ metallic
├─ height (optional)
├─ ambient occlusion (optional)
├─ emission (optional)
├─ texture scale
├─ physical scale metadata
└─ tags
```

Los campos concretos dependen del pipeline de render de Bistro Builder; el contrato semántico permanece estable.

## 8. Ventana de autoría

Ruta propuesta: **Bistro Builder → Mobiliario → Acabados y Variantes**.

La ventana se divide en tres áreas:

1. **Variantes / biblioteca**, a la izquierda.
2. **Preview 3D**, en el centro.
3. **Propiedades y zonas**, a la derecha.

Debe permitir seleccionar un FurnitureDefinition, inspeccionar sus zonas, crear variantes, duplicarlas, asignar acabados, validar y publicar.

## 9. Flujo de creación manual

1. Seleccionar el mueble maestro.
2. Ver las zonas semánticas detectadas.
3. Pulsar **+ Nueva variante** o **Duplicar variante**.
4. Elegir acabados compatibles por zona.
5. Previsualizar el resultado en tiempo real.
6. Guardar la variante como borrador.
7. Validar.
8. Publicar cuando esté aprobada.

Duplicar una variante debe copiar únicamente sus bindings; después puede modificarse una o varias zonas sin alterar la variante original.

## 10. Vinculación de zonas

La herramienta permite vincular temporalmente varias zonas para aplicarles el mismo acabado en una sola acción.

Ejemplo: `Seat + Back` → `Fabric_Olive`.

Esta vinculación es una ayuda de autoría y no obliga a que ambas zonas permanezcan ligadas para siempre.

## 11. Preview 3D

El visor debe ofrecer como mínimo:

- orbit;
- zoom;
- pan;
- reset de cámara;
- iluminación neutra reproducible;
- fondo neutro;
- vista del asset completo;
- resaltado de la zona seleccionada;
- comparación **Original / Propuesta / Aplicado**.

La preview no modifica el asset publicado hasta ejecutar una acción de aplicación o publicación.

## 12. Miniaturas

El sistema puede generar miniaturas automáticamente con cámara, encuadre, iluminación y fondo estandarizados.

Las miniaturas son derivados regenerables; nunca son la fuente de verdad del acabado.

## 13. Acabado Automático

Cuando el sistema detecta una o más zonas sin acabado válido, muestra:

**Acabado Automático · N zonas**

Regla principal:

> **Acabado Automático solo completa zonas o canales que falten. Nunca sustituye una zona ya resuelta salvo orden explícita del creador.**

El flujo es:

`Analizar → Proponer → Previsualizar → Aplicar/Descartar`.

No existe guardado ni publicación automática tras generar una propuesta.

## 14. Qué se considera missing

El análisis distingue al menos:

- Material inexistente / `None`.
- Referencia de material perdida.
- Shader roto o no soportado.
- Material placeholder marcado como temporal.
- Slot obligatorio sin asignar.
- Base Color faltante.
- Normal faltante.
- Roughness/Smoothness faltante.
- Metallic faltante cuando sea necesario.
- Otros canales obligatorios según el perfil del material.

Una ausencia completa se presenta como **Sin acabado**. Una ausencia de mapas/canales se presenta como **Acabado incompleto**.

## 15. Completar frente a sustituir

Si una zona no tiene material, **Acabado Automático** propone un acabado completo.

Si ya existe un material válido pero faltan canales, **Completar acabado** conserva lo válido y completa únicamente los canales ausentes.

La sustitución total de un acabado existente requiere una acción explícita distinta.

## 16. Orden de resolución automática

El sistema intenta resolver un missing en este orden:

1. Identificar la semántica de la zona.
2. Consultar el resto del mismo asset.
3. Consultar la familia del mueble y sus reglas.
4. Buscar un acabado compatible en la biblioteca canónica.
5. Buscar una coincidencia visual/semántica suficientemente fiable.
6. Solo si no existe una solución reutilizable adecuada, ofrecer generar un acabado nuevo.

**Reutilizar antes que generar** es una regla vinculante.

## 17. Señales para la propuesta automática

El motor puede considerar:

- nombre semántico de la pieza;
- clasificación procedente de Assets4All;
- Material Slots;
- materiales ya presentes en zonas relacionadas;
- nombres y metadatos de materiales importados;
- textura/base color existente;
- familia de mobiliario;
- tags estilísticos;
- acabados usados por variantes hermanas;
- referencia visual disponible;
- compatibilidad técnica con el shader de Bistro Builder.

Estas señales producen una propuesta; no convierten a la automatización en autoridad.

## 18. Incertidumbre semántica

Si una zona se llama, por ejemplo, `Part_017` y no existe evidencia suficiente para saber si es madera, metal, tela u otra superficie, el sistema no asigna un acabado a ciegas.

Debe mostrar **Tipo de superficie incierto** y solicitar una clasificación breve:

`Madera | Metal | Tela | Cuero | Piedra | Vidrio | Plástico | Otro`.

La clasificación confirmada se guarda como metadato del asset para futuras operaciones.

## 19. Generación de acabado nuevo

Si no existe un acabado suficientemente apropiado en la biblioteca, la herramienta puede ofrecer **Generar acabado nuevo**.

El generador debe producir, según el perfil requerido, mapas PBR y parámetros necesarios, preferentemente tileables y técnicamente compatibles con el pipeline del juego.

La generación puede usar texto, referencia visual u otros datos de entrada, pero siempre produce un borrador revisable.

El proveedor o tecnología concreta de IA no forma parte del contrato V1 y puede cambiar sin afectar a los datos canónicos.

## 20. Protección del original

Antes de aplicar una propuesta automática se conservan:

- referencia al estado original;
- propuesta temporal;
- diff de zonas/canales afectados.

Las acciones mínimas son **Original**, **Propuesta automática**, **Aplicar** y **Descartar**.

Después de aplicar, Undo debe poder recuperar el estado anterior.

## 21. Resolver todo automáticamente

Cuando existen varias incidencias puede mostrarse **Resolver todo automáticamente**.

Esta acción sigue exactamente las mismas restricciones: solo toca missing/incompletos, prioriza reutilización, no publica y genera una única propuesta revisable con el conjunto de cambios.

## 22. Validación previa a publicación

Una variante no puede publicarse si incumple requisitos obligatorios. La validación incluye al menos:

- zonas obligatorias resueltas;
- referencias existentes;
- shader compatible;
- ausencia de materiales Missing;
- IDs únicos y estables;
- finish families compatibles;
- bindings válidos;
- preview renderizable;
- miniatura disponible o regenerable;
- ausencia de cambios espaciales no declarados.

Los errores bloquean publicación. Los avisos no bloqueantes deben quedar diferenciados.

## 23. Guardar y publicar

**Guardar** conserva un borrador de autoría.

**Publicar** registra la variante como contenido canónico consumible por el resto de Bistro Builder.

La publicación no decide cómo la variante se presenta al jugador. Agrupar variantes en una sola tarjeta, mostrarlas como artículos separados o determinar su precio pertenece a los sistemas con autoridad sobre catálogo/UI/economía.

## 24. Integración con Assets4All

Assets4All puede proporcionar:

- zonas/piezas semánticas;
- Material Slots;
- materiales detectados;
- confianza de clasificación;
- metadatos de pieza;
- referencias visuales;
- VariantSet existente.

BBFFVAS consume esos datos, pero puede corregir o completar la clasificación de acabado sin redefinir la segmentación geométrica que pertenezca a Assets4All.

## 25. Integración con BBSIS y Modo Edición

Cambiar un acabado no altera:

- Spatial Contracts;
- footprint;
- collider;
- Work Edges;
- Ports;
- Seat Bays;
- claims;
- navegación;
- reglas de colocación.

Si una modificación exige cambiar alguno de esos contratos, deja de ser una simple variante de acabado.

## 26. Integración con BBPLFS

BBPLFS puede consumir IDs de FurnitureDefinition y Variant/Finish ya publicados para producir propuestas estilísticamente coherentes.

BBFFVAS no decide layouts, distribución ni lógica procedural del local.

## 27. Integración con catálogo, economía y persistencia

BBFFVAS publica identidad y apariencia; no decide experiencia de compra.

- Catálogo/UI decide presentación al jugador.
- Economía decide precios, cobros y devoluciones.
- Persistence conserva IDs estables según los contratos canónicos.
- Un cambio de nombre visible nunca sustituye al ID persistente.

## 28. Estrategia Unity

Para simples diferencias visuales se prioriza compartir geometría y datos de mueble.

Los **Material Variants**, materiales maestros/derivados y parámetros compatibles pueden utilizarse como implementación técnica cuando aporten herencia útil.

Los **Prefab Variants** no son la solución primaria para simples cambios de acabado; se reservan para variaciones que realmente necesiten overrides estructurales de prefab.

Nunca se modifica accidentalmente un material compartido de forma que cambien assets no seleccionados.

## 29. Patrones de industria adoptados

El diseño sigue patrones consolidados:

- **Cities: Skylines II:** mesh único con zonas/máscaras y combinaciones de color para multiplicar variedad sin duplicar geometría.
- **Unity:** herencia de Prefab Variants y Material Variants para evitar copias desconectadas.
- **Unreal Engine:** Material Instances parametrizadas derivadas de materiales maestros.
- **House Flipper 2:** objetos con múltiples materiales/acabados reutilizables.
- **Substance / Unity AI Material Generator:** generación o reconstrucción asistida de materiales PBR.

BBFFVAS añade una capa propia: detección de missing + reutilización prioritaria + generación opcional + preview + aprobación humana.

## 30. Criterios de aceptación V1

V1 se considera funcional cuando:

1. Puede abrir un mueble existente y leer sus zonas/materiales.
2. Puede crear y duplicar variantes sin duplicar geometría.
3. Puede reutilizar acabados desde una biblioteca central.
4. Filtra acabados incompatibles por tipo de superficie.
5. Detecta zonas y canales missing/incompletos.
6. **Acabado Automático** modifica exclusivamente lo que falta.
7. Puede completar canales sin destruir mapas válidos existentes.
8. Ante semántica incierta solicita clasificación en lugar de adivinar.
9. Prioriza acabados existentes antes de generar nuevos.
10. Permite preview Original/Propuesta/Aplicado.
11. Aplicar/Descartar y Undo/Redo funcionan.
12. Valida y bloquea publicación ante errores críticos.
13. Publica IDs estables consumibles por los sistemas canónicos.
14. No altera BBSIS, navegación, colliders ni reglas espaciales.
15. No expone esta herramienta al jugador.

## 31. Frontera de autoridad

Este sistema posee la **autoría y definición visual de acabados/variantes de mobiliario**.

No posee geometría base, segmentación 3D primaria, gameplay, BBSIS, navegación, catálogo de jugador, economía, layouts BBPLFS ni persistencia global.

Si durante la implementación aparece una decisión perteneciente a otro sistema, se documenta la dependencia y se consume su contrato público; no se redefine desde BBFFVAS.
