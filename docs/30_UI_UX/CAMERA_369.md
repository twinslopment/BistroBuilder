# Bistro Builder — cámara profesional 369

**Estado:** 369A validada; 369B implementada históricamente; 369C3 contextual/no destructiva aceptada. La UX final actual **no expone vistas predefinidas**.

## Controles reservados
- `WASD` / flechas: mover.
- `Q / E`: rotación horizontal.
- `R / F`: subir / bajar.
- `Shift`: desplazamiento rápido.
- Rueda: zoom.
- Botón central: pan con ratón.
- Botón derecho: rotación/pitch.
- Bordes de pantalla: desplazamiento cuando no estén protegidos por UI.

Estos controles quedan reservados a cámara: la UI no debe asignar atajos que entren en conflicto.

## Comportamiento contextual
Seleccionar elementos puede recentrar suavemente la vista sin aplicar zoom automático. Actividad y elementos operativos pueden centrar/seleccionar su objetivo. La transición de cámara debe ser suave y funcional, no cinematográfica en detrimento del control.

## 369B — decisión sustituida
General/Isométrica llegaron a validarse y Cenital fue retirada. Posteriormente se decidió **no ofrecer vistas predefinidas por ahora**. El código 369B puede mantenerse como infraestructura histórica, pero no deben reaparecer botones/atajos de General, Isométrica, Cenital u otras vistas sin una nueva decisión explícita.

## 369C — regla no destructiva
La cámara contextual/inspección nunca redistribuye mobiliario ni modifica el layout. Una versión temprana produjo una regresión destructiva sobre `Prototype_Restaurant.unity`; 369C3 fijó que la cámara solo observa/transiciona y que el layout pertenece al Modo Edición canónico.

## Protección de UI
El input de cámara solo debe actuar cuando procede del Game View y respetar UI bajo puntero/bordes. Scroll o movimiento sobre herramientas/paneles no debe disparar zoom o edge-pan accidental.