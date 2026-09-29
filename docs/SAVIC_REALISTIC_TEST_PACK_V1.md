# SAVIC — Realistic Test Pack V1

## Objetivo

Dar a las builds de prueba de Bistro Builder una base visual más creíble usando únicamente contenido ya presente en el proyecto y contratos existentes de SAVIC/Placeable Factory.

El pack no sustituye al contenido definitivo. Es una selección curada para pruebas funcionales y visuales.

## Contenido

### Colocables ya existentes y reutilizados

- `factory_test_plant`
- `pf_bb_chair_master_001_olive`
- `pf_bb_chair_master_001_red`
- `pf_bb_chair_master_001_white`
- `pf_bb_chair_master_001_yellow`
- `chair_bistro_01`
- `bb_chair_master_002`
- `table_basic`
- `table_basic_4`
- `bb_table_b90c47bde3e949918ec76a13dc17c61c`

### Variantes nuevas que instala el pack

- `chair_bistro_01_oak_warm` — Silla bistró, roble cálido
- `chair_bistro_01_painted_black` — Silla bistró, negro
- `chair_bistro_01_painted_white` — Silla bistró, blanco
- `chair_bistro_01_sage_green` — Silla bistró, verde salvia
- `chair_bistro_01_walnut_dark` — Silla bistró, nogal oscuro

Estas cinco variantes parten de prefabs visuales ya existentes. El instalador las pasa por `BistroBuilderPlaceableFactoryEngine` con preset `Chair`, por lo que obtiene prefab jugable, `RestaurantPlaceableObject`, `EditableObjectDefinition`, capacidades de seating, collider cuando procede, thumbnail y alta de catálogo.

### Construcción incluida en la validación

- `Pared_0.5m.prefab`
- `Pared_1.0m.prefab`
- `Pared_2.0m.prefab`
- `Pared_4.0m.prefab`
- `Puerta_roble_abierta.prefab`
- `Ventana_marco_grafito.prefab`

## Resultado esperado

- 15 artículos colocables disponibles en el catálogo.
- 6 elementos constructivos válidos.
- 21 piezas curadas utilizables en pruebas.
- Todos los placeables deben resolver prefab, icono/preview y `EditableObjectDefinition`.
- La instalación es idempotente: volver a ejecutarla no debe duplicar artículos.

## Uso

Instalar o reparar:

`Tools > Bistro Builder > SAVIC > Packs > Realistic Test Pack V1 > Install or Repair`

Validar:

`Tools > Bistro Builder > SAVIC > Packs > Realistic Test Pack V1 > Validate`

También dispone de entradas batch:

- `BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.InstallOrRepairFromCommandLine`
- `BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.ValidateFromCommandLine`

Runner de una sola orden:

`Tools/BistroBuilder/RunSavicRealisticTestPackV1.ps1`

El runner instala/repara el pack, ejecuta después la validación y devuelve código de salida distinto de cero si cualquiera de las dos fases falla.

## Alcance

Este V1 usa solo contenido ya almacenado en Bistro Builder. La siguiente expansión deberá centrarse en añadir variedad real de mesas, iluminación, decoración y equipamiento pasivo mediante SAVIC, evitando incorporar assets externos sin licencia/procedencia clara.
