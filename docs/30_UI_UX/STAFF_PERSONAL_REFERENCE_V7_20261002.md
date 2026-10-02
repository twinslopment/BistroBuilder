# PERSONAL V7 — cierre integral de tipografía · 02/10/2026

> **HISTÓRICO:** sustituido por `STAFF_PERSONAL_REFERENCE_V8_20261002.md`. La captura marcada posteriormente exige Recoleta en todas las celdas, nombres, habilidades, ficha y botones. V7 con Inter en esas áreas ya no es la autoridad visual.

## Fuente de verdad visual
Captura original aportada por el usuario, **lado izquierdo** de la comparación. V5/V6 quedan históricas: V7 conserva su composición compacta y elimina toda excepción a la tipografía oficial de Bistro Builder.

## Sistema tipográfico único
| Elemento visible | Recurso del juego | Tamaño nominal Unity |
|---|---|---:|
| PERSONAL, SALA, COCINA, título de ficha, HABILIDADES y títulos de Horarios / diálogos | Recoleta-SDF | por jerarquía visual |
| Frases aclaratorias y pie informativo | Inter-Regular-SDF | 14 |
| Encabezados de columnas | Inter-SemiBold-SDF | 12,5 |
| Nombres de empleados de las tablas | Inter-SemiBold-SDF | 15 |
| Rol, nivel, asignación, salario, estados | Inter-Regular-SDF | 14 |
| Etiquetas y valores de la ficha | Inter-Regular-SDF | 14 |
| Barras de habilidades y sus valores | Inter-Regular-SDF | 14 |
| Contratar, Despedir, Formación, Horarios y demás controles | Inter-SemiBold-SDF | 14 |

- `BistroBuilderStaffVisuals` concentra los tokens tipográficos y la resolución de las fuentes canónicas desde `BistroBuilderTypography`; nunca crea Georgia ni copia fuentes del sistema operativo.
- `NormalizeHierarchy(panelRoot)` se ejecuta tras cada reconstrucción de Plantilla/Candidatos y de Horarios. Incluye *todos* los textos, también los prefabs y los modales inactivos.
- El estilo no se hereda de un `Button` contenedor de fila: solamente el nombre utiliza SemiBold; el resto de las celdas conserva Regular.
- Autoajuste limitado a un máximo de 1 unidad de reducción; no se aceptan textos mezclados artificialmente de tamaño 10 por el ancho del componente.
- Inter actúa como fallback de los caracteres ausentes en la copia runtime de Recoleta sin alterar el asset original.

## Preview programada
`docs/30_UI_UX/previews/Personal_BistroBuilder_PREVIEW_V7.html`: HTML/CSS/JavaScript navegable, con datos de demostración; importa las tres fuentes **mediante referencia relativa a `Assets/Resources/BistroBuilder/UI/Typography` del propio proyecto**. No añade ni redistribuye fuentes. La misma preview se deja disponible en `C:/Users/mruperez/Downloads/Personal_BistroBuilder_PREVIEW_V7.html`, con rutas adaptadas a Downloads.
`docs/30_UI_UX/previews/Personal_V7_OfficialTypography_1313.png`: captura real obtenida con Chrome en Windows leyendo los recursos tipográficos locales (no es una imagen generada).

## Gate real Unity
- Proyecto: Unity 6000.3.19f1, escena `Prototype_Restaurant`.
- Método reversible: `BistroBuilderStaffApprovedRuntimeProbe.RunBatch`.
- **20 PASS / 0 FAIL, EXIT 0**, sin errores CS.
- Auditoría integral: **137 textos** de Personal/Candidatos/modales y **45 textos** de Horarios, todos con Recoleta o Inter.
- Comprobaciones específicas: frase aclaratoria de SALA (Inter Regular), encabezado NOMBRE (Inter SemiBold), SALARIO en la fila (Inter Regular), ficha (Inter Regular), Skill_0 (Inter Regular), Contratar (Inter SemiBold), además de retratos/iconos, navegación, límites de panel y costes de turnos.
- Preview responsive evaluada a 1600, 1313, 1280, 900, 800 y 600 px: sin errores de JavaScript ni desbordamiento horizontal (transformación uniforme de la maqueta en anchuras pequeñas).

## Límites
La preview no es la pantalla Unity ni contiene datos de partida. La equivalencia visual píxel a píxel con la captura de Game View todavía necesita revisión humana; el test garantiza las fuentes/jerarquía/estructura y que el flujo navega, no automatiza ese juicio. La lógica de spawn de agentes, horarios operativos y Save/Load queda fuera del alcance de esta intervención.
