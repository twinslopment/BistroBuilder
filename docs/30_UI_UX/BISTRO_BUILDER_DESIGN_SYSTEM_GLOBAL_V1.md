# BISTRO BUILDER — SISTEMA DE DISEÑO VISUAL GLOBAL V1

**Fecha de consolidación:** 09/11/2026  
**Estado:** **IDENTIDAD ARTÍSTICA GENERAL APROBADA**; prototipo visual de Actividad V1.2 aprobado. **No supone implementación Unity ni aprobación de cada nueva pantalla.**

> **Este archivo es el punto de entrada de diseño para TODAS las interfaces del juego.** Las pantallas nuevas deben reutilizar su identidad, no crear estilos paralelos. Cuando un componente todavía no tenga medidas, tokens o comportamiento verificados, se indica como propuesta; no se inventa una aprobación.

## 1. Lenguaje visual vinculante

- Simulador de gestión de restaurante **PC premium**, elegante, cálido, profesional, cinematográfico y sobrio. El restaurante 3D predomina en Modo Juego; evitar aspecto de CRM, móvil o dashboard empresarial.
- **Barras en negro/grafito profundo**; **paneles grafito cálido**, de la misma familia cromática y ligeramente más claros, nunca marrón excesivo.
- **Panel izquierdo:** cuerpo grafito cálido y listas/tarjetas marfil.
- **Panel contextual derecho:** cabecera grafito oscuro, interior marfil/crema; jerarquía definida por contraste y capas, sin acumulación de ornamentos.
- **Marco de luz fino alrededor de los paneles:** línea bronce-dorada de brillo suave, sin neón. **Brillo aprobado: 50 %** en la previsualización V1.2; aplicar como patrón de referencia a nuevos paneles mientras no haya excepción justificada.
- Bronce cálido para selección y botón principal; verde apagado para correcto/favorable; rojo para incidencias críticas y acciones destructivas. No utilizar rojo como color de decoración ni dorado saturado en grandes superficies.
- Textos: **Recoleta** para títulos, **Inter** para controles, tablas, datos y cuerpo. No usar otras familias por capricho. Algunas previews utilizan serif de sustitución y no reemplazan la decisión tipográfica.
- Iconos coherentes, reconocibles, adultos, con trazos y proporciones estables; sin ilustraciones infantiles.
- Bordes, divisores, sombras suaves, relieves y espaciados armonizados en todos los paneles.

## 2. Componentes reutilizables obligatorios

Todas las interfaces —Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación, Opciones, contexto y las pantallas de Modo Edición donde se aplique la identidad— compartirán:

| Familia | Estado/estética |
| --- | --- |
| Botón principal | Bronce cálido, relieve muy contenido; hover luminoso suave; pressed con sensación de presión |
| Botón secundario | Marfil/crema, borde cálido, bajo contraste visual frente al principal |
| Botón destructivo | Rojo reservado para eliminar, despedir o confirmaciones destructivas |
| Botón deshabilitado | Reducir contraste, sin interacción ni brillo engañoso |
| Pestañas/filtros | Activo en bronce; inactivo grafito; hover discreto; un estado activo claro |
| Tarjetas/filas/tablas | Fondo marfil, sombras mínimas; seleccionado con borde/luz bronce; hover sin desplazar contenido |
| Estado crítico | Señal roja pequeña y clara, diferenciada de selección |
| Estados positivos | Verde mate y etiqueta/icono, nunca solo color |
| Panel oscuro | Grafito cálido con borde fino iluminado, radios consistentes |
| Panel contextual claro | Cabecera oscura y área marfil con módulos de información y acciones |
| Foco teclado | Perceptible y compatible con accesibilidad; no sustituir por solo hover |
| Microinteracciones | Sutiles, sin destellos agresivos ni desplazamientos amplios |

**Movimiento reducido: ACTIVADO por defecto** en la preview Actividad V1.2, elegido expresamente. Usarlo como referencia de accesibilidad de los componentes comunes sin alterar las animaciones previamente aprobadas de las barras V21/V30 y E5.5. En la implementación habrá que respetar también la preferencia del usuario y los mecanismos de accesibilidad.

## 3. Barras globales bloqueadas — fuera del rediseño

### Superior
- **V21 aprobada**, negro satinado e iconos definitivos; animación de clic **V30**.
- Orden obligatorio: **Actividad, Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación, Opciones**.
- No reinterpretar el logo ni los iconos desde los renders. Usar los archivos/recursos de las versiones originales.
- Referencia de prototipo: `BistroBuilder_V21_V30_Mejorada_Preview.html` y `ESPECIFICACION_V21_V30.md`.

### Inferior
- **E5.5 negra** y revisiones posteriores de iconos de **Caja, Satisfacción, Velocidad y Climatología**.
- Conservar símbolos, tamaños, interacciones y animaciones aprobadas. Referencia de prototipo: `BistroBuilder_BarraInferior_Animada.html`.
- No copiar las barras dibujadas en imágenes generadas. La imagen artística elegida es vinculante **para los paneles y sus acabados**, no para sustituir las barras.

## 4. Prototipo validado y aprobado: Actividad

- Base compacta aprobada: panel izquierdo estrecho + panel contextual derecho estrecho, restaurante 3D central visible.
- Actividad retorna al restaurante 3D desde gestión, muestra panel izquierdo y lo oculta al pulsar de nuevo. Filtros **Hoy, Incidencias, Oportunidades, Reservas**.
- La selección de un evento relevante localiza la entidad en el restaurante y muestra contexto cuando proceda.
- Incidencia crítica: distintivo rojo discreto en icono Actividad y en el feed; no crear pantalla separada de alertas.
- Paneles de Actividad y Contexto son propios de **Modo Juego**, nunca de Modo Edición.
- Previsualización final aprobada de Actividad: `BistroBuilder_Actividad_Preview_V1_2_Aprobada.html`.
- Ajustes expresamente elegidos: **Reducir movimiento = Sí**; **brillo del marco = 50 %**.
- La escena del restaurante en HTML es una imagen estática de referencia; la preview prueba estilo e interacción simulada, no gameplay real ni Unity.

## 5. Reglas de aplicación en pantallas futuras

1. Reutilizar la misma biblioteca de botones, estados, tabs, tablas, tarjetas, bordes y hover; variar solo contenido, densidad, disposición y jerarquía funcional.
2. No duplicar navegación global ni panel contextual ni autoridades de gameplay.
3. Mantener un centro útil y minimizar ocupación lateral en Modo Juego. Adaptación a **1920×1080** y **1280×720** obligatoria; cambiar composición y scroll si hace falta, no reducirlo todo ciegamente.
4. Para Personal, Carta, Inventario, Proveedores, Reservas, Economía, Marketing, Reputación y Opciones: crear diseños específicos con **idénticos tokens y componentes**, pero someter cada pantalla a aprobación antes de implementación.
5. Modo Edición tiene sus barras y reglas propias; recibe la **identidad artística compartida** sin heredar las barras ni el feed de Modo Juego.
6. Las acciones contextuales solo aparecen cuando su estado y autoridad técnica las permiten. Canon: `CONTEXTUAL_ACTION_CATALOG.md`.

## 6. Qué está aprobado y qué sigue pendiente

### Aprobado
- Identidad visual global descrita en este documento y aplicabilidad transversal.
- Contraste grafito cálido/marfil, bronce controlado, verde positivo, rojo crítico.
- Marco luminoso fino y **50 %** de brillo en la previsualización de Actividad; **movimiento reducido** como configuración seleccionada.
- Composición compacta y estética final de Actividad.
- Respeto absoluto de V21/V30 superior y E5.5 inferior.

### Pendiente
- Valores HEX exactos, radios, paddings, medidas y elevaciones finales del componente en Unity (la tabla de tokens previa es propuesta).
- Duraciones específicas de hover, pressed y apertura/cierre como contrato técnico global (150/90/220 ms eran propuestas iniciales).
- Validación visual y jugable de cada pantalla nueva y de sus variantes 1280×720.
- Componentes reales compartidos implementados en Unity y pruebas funcionales/accesibilidad; **aún no implementado**.

## 7. Referencias y trazabilidad

- `docs/30_UI_UX/ACTIVIDAD_VISUAL_FASE1_20261109.md` — aprobación de paneles compactos.
- `docs/30_UI_UX/DESIGN_SYSTEM_V1_ESTADOS_PROPUESTA_20261109.md` — biblioteca propuesta y aprobación artística posterior.
- `docs/30_UI_UX/ACTIVIDAD_PREVIEW_INTERACTIVA_V11_20261109.md` — evidencia de pruebas y aprobación V1.2.
- `docs/30_UI_UX/UI_UX_DEFINITIVE.md` — composición y dirección canónica de HUD.
- `docs/30_UI_UX/CONTEXTUAL_ACTION_CATALOG.md` — acciones canónicas.
- `docs/90_DECISIONS/DECISION_REGISTER.md` — verificar numeración antes de registrar nueva decisión (en la rama consultada D-036 se refiere a Agilizar cuenta, no a Actividad).
- Referencia visual elegida por el usuario el 09/11/2026: imagen de Actividad con lateral izquierdo grafito, contexto marfil y marco luminoso bronce. Referencia de previsualización `BistroBuilder_Actividad_Preview_V1_2_Aprobada.html` entregada en conversación.

**Rama documental de diseño:** `design/actividad-visual-compacta-20261109`, aislada de la integración/runtime. Al fusionar tras revisión, reconciliar documentos canónicos y regenerar `ALL_MARKDOWN_INDEX.md` y `PROJECT_KNOWLEDGE_BUNDLE.md` según `AGENTS.md`. **No se han cambiado archivos de Unity ni las barras aprobadas.**
