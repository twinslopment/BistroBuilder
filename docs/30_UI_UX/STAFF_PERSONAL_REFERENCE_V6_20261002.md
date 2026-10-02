# PERSONAL V6 — fidelidad de tablas y tipografía canónica (02/10/2026)

## Referencia visual vinculante
Imagen original `Captura de pantalla 2026-10-01 093447(1).png`: pantalla izquierda de la comparativa `image(20261002-093222).png`. NO utilizar la pantalla derecha ni las V3/V4 como guía final. La V5 conserva la composición general; la V6 ajusta fuentes y tablas.

## Fuentes oficiales (sin nuevas fuentes)
- Título `PERSONAL`, `SALA`, `COCINA`, HABILIDADES, nombre principal de la ficha y título de Horarios: Recoleta-SDF del recurso canónico `Assets/Resources/BistroBuilder/UI/Typography/Recoleta-SDF.asset`.
- Datos, columnas, estado y textos auxiliares: Inter-Regular-SDF, mediante `BistroBuilderTypography.Body`.
- Nombres tabulares, columnas destacadas, contadores y botones: Inter-SemiBold-SDF mediante `BistroBuilderTypography.Emphasis`.
- En Personal, la instancia runtime de Recoleta recibe Inter como fallback exclusivo de glifos no presentes; no usa OS Georgia, no toca ni redistribuye los archivos originales y no modifica otros módulos.
- La maqueta web V6 referencia los TTF/OTF *ya existentes* en el proyecto del usuario mediante URL local relativa a `C:/Users/mruperez/Downloads`. No incluye/copía archivos de fuentes.

## Tabla aprobada Sala / Cocina
- Dos tarjetas de tabla continua, sin coloreado alterno ni marcos individuales en cada empleado.
- Columnas: NOMBRE 30%, ROL 13.5%, NIVEL 10.5%, ASIGNACIÓN 17%, SALARIO 13%, ESTADO 16%, con separadores finos alineados en encabezados y filas.
- Solo el empleado seleccionado obtiene fondo miel suave y contorno fino; estados muestran punto de color independiente visualmente del texto oscuro.
- Cabecera de sección crema con icono y contador, filas compactas de 40 unidades, plaza disponible de 52, intersección entre Sala y Cocina separada 7 unidades; el color tint no multiplica dos veces la base de la fila.
- Apertura de Plantilla sin barra de pestañas permanente: selector contextual en el título PERSONAL. Horarios y Candidatos conservan sus servicios y botones reales.
- Márgenes según referencia: la tabla comienza directamente tras el marco interior (se elimina sangrado lateral añadido por V5); título/icono y tagline alineados con la captura.
- En Unity los importes siguen siendo los *reales por servicio*, no los salarios de ejemplo mensuales de la imagen.

## Assets gráficos y preview
Los cinco retratos y cuatro iconos importados en V5 se conservan sin duplicaciones. La V6 solo modifica fuentes, separación, jerarquía y rejilla. Preview HTML/CSS/JS `Personal_BistroBuilder_PREVIEW_V6.html` con las 3 vistas; abrir desde Windows Downloads para que lea Recoleta/Inter de `../ProyectoBB/BistroBuilder_UniversalPreview/Assets/Resources/BistroBuilder/UI/Typography/`. La captura local de prueba exacta está en `C:/Users/mruperez/Downloads/Personal_V6_OfficialFonts_FINAL.png`.

## Calidad y límites
- Compilación + `BistroBuilderStaffApprovedRuntimeProbe.RunBatch` sobre Unity 6000.3.19f1. El probe comprueba familias oficiales, titular / filas / Horarios, rejilla de columnas, selector, retratos, costes y navegación; resultado de esta ejecución en su log.
- La preview web muestra datos ficticios para comparar con la referencia; no modifica estado de partida.
- Pendiente aceptación visual de captura Game View en 1920/1280/800 (una compilación no certifica identidad píxel a píxel).
- No alterar en este bloque horarios operativos, spawn de camareros ni Save/Load.
