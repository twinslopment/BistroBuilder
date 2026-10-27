# Carta V3 — asociación determinista de fotografías

Fecha: 2026-10-27
Rama de trabajo: `feature/carta-v3-dish-photo-mapping-20261027`
Base: `codex/carta-v3-step15-fidelity-20261026`

## Hechos verificados

- Existen 29 fotografías distintas en `Assets/Art/UI/Dishes/Inbox/`.
- El catálogo `Assets/Resources/BistroBuilder/Menu/DishImageCatalog.asset` conserva sus 29 entradas y Sprites.
- Antes del ajuste, `dishes_manifest.csv` indicaba 3 `OK` y 26 `IMAGE_ONLY`. La segunda etiqueta significa que no existe una definición `BistroBuilderDishDefinition` con ese DishId, **no** que la imagen esté dañada o ausente.
- Hay ocho definiciones de platos en esta rama. Crear arbitrariamente 26 recetas, precios y categorías solo para eliminar `IMAGE_ONLY` rompería la autoridad de contenido y la jugabilidad.

## Resolución de imagen (sin alterar DishId)

1. Prioridad 1: buscar el `DishId` exacto del plato en el catálogo de Sprites.
2. Si no existe, probar una clave visual normalizada a partir del `DishId`: minúsculas, sin tildes, separadores unificados y prefijo convencional `dish_` eliminado solo para la comparación visual.
3. Si sigue sin existir, probar la misma clave sobre el **nombre visible** (`DisplayName`) y los alias ya existentes del importador (por ejemplo «Tortilla de patatas», «Pimientos de Padrón», «Pan tumaca»).
4. Si dos imágenes diferentes comparten una clave visual, no escoger una arbitrariamente: mostrar ninguna hasta resolver la colisión; una coincidencia exacta por `DishId` sigue funcionando.
5. Si ningún identificador coincide, conservar el mensaje veraz «Imagen no disponible».

El normalizador compartido `BistroBuilderDishImageKeyUtility` es utilizado tanto por el importador Editor como por Carta V3 runtime, evitando divergencias. La lista de platos y la lógica de cocina, recetas, precios, progreso y SAVIC no se modifican.

## Alcance

- Las **29 fotografías son accesibles por nombre normalizado** y podrán presentarse en la lista y en el panel de detalle cuando exista un plato compatible, incluidos platos creados posteriormente por el jugador.
- Las **26 imágenes `IMAGE_ONLY` no se convierten en platos jugables por este cambio**: requieren sus definiciones/recetas de autoría cuando se decida incorporarlos al catálogo.
- El manifiesto mantiene honestamente ese estado hasta que la fuente de verdad de platos incorpore las definiciones correspondientes.

## Comprobación

Ejecutar desde Unity 6000.3.19f1:

`Tools > Bistro Builder > Menu > Run Dish Photo Matching Self-Test`

O en batch mode: `-executeMethod BistroBuilderDishImageMatchingSelfTest.RunBatch`.

El test reconstruye el catálogo; verifica las 29 imágenes físicas, carga por ID y nombre, alias, tildes, prioridad de ID, ausencia de falsos positivos, detección de colisiones y ausencia de creación de nuevos platos. Informe: `Logs/DishPhotoMatching_20261027.txt`.

## Integración

No copiar ni sobrescribir archivos del worktree de Carta V3 en curso. Integrar el commit de esta rama sobre la rama de Carta V3 después de completar la validación.
