# BB Living Architecture — LA9 Herramienta jugable V1

Estado: **IMPLEMENTADO / AUDITADO ESTÁTICAMENTE / PENDIENTE UNITY REAL**.

## Alcance implementado

- `ArchitectureEditSession` concentra el estado de edición sin depender de `GameObject`, cámara ni input concreto.
- La sesión mantiene snapshot canónico propio, selección estable por `WallId`/`VertexId`, modo de herramienta, preview, informe de impacto, candidatos de snap y pilas Undo/Redo.
- La preview usa siempre LA3 (`ArchitectureTransactionEngine`) sobre un clon. Mientras el jugador previsualiza, el snapshot confirmado permanece intacto.
- `CaptureVisible()` devuelve la propuesta B cuando existe y A cuando no existe, siempre mediante `DeepClone`, evitando que Presentation pueda mutar autoridad.
- Crear pared reutiliza un `VertexId` existente cuando LA5 resuelve un snap de vértice; si no existe vértice compatible crea identidad nueva.
- Mover pared y mover vértice reutilizan las primitivas transaccionales LA3.
- Edición numérica V1 permite escribir una longitud objetivo de pared preservando uno de sus extremos y su dirección actual.
- Eliminar pared requiere selección estable y sigue el mismo flujo preview → impacto → confirmación.
- Cada propuesta lista para commit se analiza automáticamente con LA6. Un impacto `Blocking` o `SystemError` impide la confirmación, pero warnings/info permanecen informativos.
- Confirmar crea una única entrada semántica de Undo; cancelar descarta B sin alterar A.
- Un nuevo commit después de Undo invalida la rama Redo, evitando historial ambiguo.
- `ArchitectureEditToolController` es una fachada runtime para UI/input. Solo después de confirmación/Undo/Redo copia el snapshot de sesión a `ArchitectureStateService`.
- `ArchitectureRuntimePresenter` puede proyectar temporalmente `CaptureVisible()` para enseñar preview; la escena nunca se lee de vuelta para reconstruir arquitectura.
- `ReloadFromCanonicalState()` permite re-sincronizar LA9 después de Load de LA8 o ante un fallo de aplicación, con fail-safe hacia la autoridad canónica.

## Límites conscientes de LA9

- LA9 no define todavía el lenguaje visual definitivo de válido/inválido, ghost, pulsos, guías o materialización; corresponde a LA10.
- LA9 no acopla teclas/ratón concretos para evitar competir con el sistema de edición existente. Expone comandos estables para que la capa de input/UI los invoque.
- LA9 no crea un segundo Undo/Redo global del juego: mantiene el historial semántico de la sesión arquitectónica V1. La integración final con el coordinador universal de edición se valida en Unity.
- Las paredes siguen siendo rectas en V1; curvas, niveles complejos y escaleras permanecen fuera del alcance actual.

## Self-test puro LA9

`ArchitectureEditSessionSelfTest` cubre 12 casos:
1. preview de creación no muta A;
2. confirmar creación modifica el estado confirmado;
3. cancelar restaura la vista de A;
4. preview de mover pared es puro;
5. edición numérica aplica la longitud exacta;
6. eliminación de pared seleccionada;
7. Undo/Redo restaura fingerprints A/B;
8. un nuevo commit limpia Redo;
9. snap a vértice reutiliza identidad en vez de duplicarla;
10. longitud inválida se rechaza sin mutación;
11. selección conserva `WallId` tras editar;
12. un consumidor no puede mutar la sesión mediante `CaptureVisible()`.

Runner Unity Editor: `Bistro Builder/Living Architecture/LA9/Run Self Test`.

## Gates pendientes que requieren Unity real

- compilación C# completa del proyecto;
- ejecutar acumulativo LA1–LA9 y confirmar **12/0** en LA9;
- instalar `ArchitectureStateService`, `ArchitectureRuntimePresenter` y `ArchitectureEditToolController` en la composición real del modo Edición;
- dibujar una pared mediante la capa de input real y comprobar preview sin mutar estado confirmado;
- confirmar/cancelar y verificar que solo Confirm modifica `ArchitectureStateService`;
- seleccionar por identidad y mover una pared/vértice conectado;
- editar una longitud numérica y verificar medida física resultante;
- Undo/Redo real de una reforma completa;
- Save → Load con LA8 y posterior `ReloadFromCanonicalState()` sin duplicar proyección;
- verificar interacción con cámara 369C y con el coordinador universal de edición existente;
- Console 0 errores/excepciones/asserts.

No se declara LA9 validado/cerrado hasta superar esos gates.
