# Bistro Builder — Iconografía 21B

Sistema canónico y data-driven para implementar la propuesta de iconografía de Bistro Builder en Unity.

## Estado visual

Cada icono usa una única fuente gráfica y cuatro estados de presentación:

- **Normal**: neutro claro.
- **Hover**: mayor luminosidad, borde reforzado, escala `1.055` y elevación visual de `2 px`.
- **Seleccionado**: dorado Bistro Builder, borde dorado y subrayado inferior.
- **Desactivado**: contraste reducido y sin interacción.

Tamaños canónicos: **16 / 24 / 32 / 48 px**.

El color semántico se reserva para estado o énfasis: positivo, atención, crítico, información y acento.

## Instalación en Unity

1. Cambiar a la rama `feature/21b-iconography-system`.
2. Abrir el proyecto y esperar a que Unity compile.
3. Ejecutar `Bistro Builder > UI > Iconografía > Instalar o actualizar`.
4. El instalador descarga las fuentes SVG fijadas a **Lucide 1.45.0**, las guarda en `Assets/BistroBuilder/UI/Iconography/Icons/` y reconstruye `Assets/Resources/BistroBuilder/UI/BBIconCatalog.asset`.
5. Ejecutar `Bistro Builder > UI > Iconografía > Validar catálogo`.
6. El resultado esperado es `VALIDATION PASS` y cero entradas sin sprite.

El instalador es idempotente: puede ejecutarse de nuevo sin duplicar assets ni entradas.

## Uso runtime

Añadir `BBIconButton` al control uGUI que deba representar un icono. Asignar:

- `Icon Image`: `Image` que muestra el sprite.
- `Background Image`: superficie del control, si existe.
- `Border Image`: borde independiente, si existe.
- `Selection Underline`: línea inferior de selección, si existe.
- `Canvas Group`: opcional.
- `Icon Id`: concepto canónico que representa el control.

`BBIconButton` resuelve el sprite desde `BBIconCatalog`; ningún panel debe hardcodear rutas de fichero.

Para iconos de estado, activar `Use Semantic Color`. Para navegación y objetos normales debe permanecer desactivado.

Código de ejemplo:

```csharp
iconButton.Configure(BBIconId.NavActivity);
iconButton.SetSelected(true);
iconButton.SetInteractable(true);
```

## Familias incluidas

- Navegación principal
- Áreas y objetos
- Acciones
- Estados
- Clientes y reservas
- Comida y bebida
- Economía
- Direccionales y generales
- Indicadores en escena

La misma forma se reutiliza cuando el significado es el mismo. Por ejemplo, `users`, `calendar-days`, `hourglass` o `sparkles` pueden tener varios usos semánticos sin duplicar el SVG.

## Fuente gráfica

Las fuentes iniciales se sincronizan desde **Lucide 1.45.0**. Lucide se utiliza como base coherente para la primera implementación porque mantiene una gramática lineal consistente y permite sustituir cualquier icono posteriormente sin cambiar los contratos públicos `BBIconId`.

El contrato del juego es `BBIconId`, no Lucide. Una futura familia dibujada específicamente para Bistro Builder puede reemplazar sprites dentro del catálogo sin modificar gameplay ni las vistas consumidoras.

Consulta `THIRD_PARTY_NOTICES.md` para atribución y licencia.
