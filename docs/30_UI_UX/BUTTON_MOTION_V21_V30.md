# Bistro Builder — Microinteracción global V30 rápida

**Decisión aprobada:** 04/11/2026.

**Referencia visual:** barra superior V21 aprobada.

**Estado técnico:** implementación base en rama aislada; pendiente validación visual en Unity y fusión a la rama integradora.

## Regla vinculante

Todos los botones interactivos actuales y futuros del juego deben utilizar el feedback táctil **V30 rápida**, independientemente de la pantalla en la que se creen. No duplicar animaciones por cada módulo ni inventar variantes para nuevas pantallas.

**Excepción temporal:** el Modo Edición V2 queda fuera de esta adopción hasta una decisión específica. No alterar la animación del editor por efecto de esta decisión.

## V30 rápida: secuencia

Duración total: **280 ms**, no depende del reloj del juego.

1. Compresión 100 % → **94 %** durante los primeros 70 ms.
2. Retorno elástico hasta **103 %** entre 70 y 162 ms.
3. Recuperación al **100 %** entre 162 y 280 ms.
4. Destello blanco breve (intensidad baja) únicamente cuando existe un icono gráfico adecuado.
5. La selección, foco de teclado, estado hover, disponibilidad y acción del botón no se modifican.

En botones de solo texto aplicar la misma microinteracción táctil sin forzar un brillo que perjudique el contraste. Los iconos con fondo negro conservarán el efecto blanco suave, no azul ni dorado.

## Arquitectura

- `BistroBuilderV30ButtonMotion`: componente de presentación sobre `UnityEngine.UI.Button`, sin lógica de juego ni callbacks propios de gestión.
- `BistroBuilderV30ButtonInstaller`: instalador global mediante un único barrido periódico (0,6 s) de botones de Canvas; incluye los creados posteriormente.
- Evitar dos efectos de presión simultáneos: deshabilitar solo el antiguo `BistroBuilderUiSelectableMotion` sobre el mismo botón, sin borrar sus datos.
- Convivir con `BBIconButton` (estados y hover) animando la raíz del botón, no el RectTransform del icono, que ya gestiona `BBIconButton`.
- Respetar `BistroBuilderOptionsScreen.ReducedMotion`; cancelar y restaurar al deshabilitar, cambiar a edición o dejar de ser interactuable.
- No crear una nueva autoridad sobre navegación, menús, selección ni estado de simulación.

## Validación de aceptación

- [ ] Compilación limpia en Unity 6000.3.19f1.
- [ ] Clic de ratón; clic repetido; mismo botón; distintas secciones.
- [ ] Activación con teclado; estado seleccionado estable.
- [ ] Iconos blancos sobre negro y botones claros de pantallas de gestión.
- [ ] Escalado 1920×1080 / 1280×720 y sin cambios de layout.
- [ ] Botones añadidos en runtime y ventanas que se crean bajo un Canvas nuevo.
- [ ] `ReducedMotion`, Time.timeScale=0, botón deshabilitado durante el efecto.
- [ ] Al entrar en Modo Edición no aplicar V30; al volver al servicio sí.
- [ ] Confirmar que no se disparan acciones duplicadas.

## Integración

La rama aislada NO cambia prefabs ni escenas ni sobrescribe trabajo paralelo. La adopción final exige fusionarla en la rama integradora y ejecutar regresiones de UI, Carta, Personal y opciones antes de build.
