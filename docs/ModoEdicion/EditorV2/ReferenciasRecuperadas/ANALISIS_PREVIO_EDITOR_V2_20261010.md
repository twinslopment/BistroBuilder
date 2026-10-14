# Editor V2: análisis previo de implementación

Fecha: 10/10/2026. Alcance de esta revisión: documentación, referencias gráficas recuperadas, código existente y evidencias de pruebas archivadas. No se ha modificado el código del editor ni se han ejecutado nuevas builds o pruebas de Unity durante esta revisión.

## Parecer

Sí: el diseño validado puede convertirse en un Editor V2 funcional dentro de Bistro Builder. La base de Unity ya tiene los servicios necesarios para desarrollar buena parte de la experiencia. El trabajo pendiente concentra dos exigencias: reproducir fielmente la presentación aprobada y completar la conexión de sus interacciones con las autoridades existentes.

La maqueta completa y los siete HTML recuperados permiten avanzar sin esperar a las capturas históricas restantes. Esas capturas siguen pendientes de recuperación y podrían aportar detalles adicionales; no son motivo para pedir que el usuario vuelva a aprobar el diseño.

El resultado debe verificarse con una build gráfica real. La coincidencia visual completa y la calidad de las interacciones todavía no están demostradas por la implementación existente.

## Evidencia revisada y límites

- Inventario y búsqueda transversal sobre **91 Markdown fuente**, 568.950 bytes, de `BistroBuilder_EditorV2`, excluyendo los dos documentos generados. Se conservan rutas, títulos, hashes, encabezados y referencias a los sistemas del editor en `Inventario_Documental.json`.
- Lectura de los documentos centrales de diseño UI, plan maestro, interacción, autoridades, modo edición, construcción, aceptación, tipografía y evidencias recientes B9–B13; contraste con el incremento visual descrito en el worktree de QA. El inventario global no se presenta como una lectura literal completa de todos los documentos históricos o del bundle.
- Referencias recuperadas: siete HTML originales, 18 imágenes incrustadas distintas y un PNG independiente. Los esquemas SVG versionados se mantienen identificados como esquemas.
- Código de las barras, marcos, Galería Viva, disposición responsive, inspector, Halo de Huella y render de superficies.
- Capturas gráficas existentes de **R5**, a 1920×1080 y 1280×720. No se atribuyen a R8.
- Informe **R8**: build Windows x64 compilada con cero errores y doce advertencias. No se ha localizado una captura gráfica R8 en las carpetas revisadas; su informe de compilación no demuestra fidelidad visual.
- El lector de los chats solo devuelve los turnos recientes y marcadores de contenido de algunas respuestas. No se afirma disponer del historial gráfico íntegro de ambas conversaciones.

## Dirección que se conserva

La confirmación actual del usuario de que el diseño gráfico exacto ya está validado prevalece sobre las notas antiguas de aprobación pendiente.

La referencia de trabajo conserva cinco zonas: barra superior específica de Editor V2, Galería Viva a la izquierda, restaurante central, inspector contextual a la derecha y barra inferior MODO / EDITAR / AYUDAS. Se mantienen el logotipo original, marfil y latón, selección miel, títulos Recoleta, textos Inter, rojo para acciones destructivas, Principal/Conjunto y selección con esquinas, Halo de Huella y microanimación.

La adaptación a 1280×720 debe conservar las acciones y permitir plegar el inspector; a 1920×1080 catálogo e inspector pueden convivir. Seleccionar, realizar una operación provisional, confirmar el gesto y aplicar la reforma siguen siendo estados distintos.

Los importes, nombres, propiedades y capacidades de los prototipos se sustituyen por los datos reales publicados por el juego. La geometría del restaurante y su iluminación necesitan recursos 3D y materiales adecuados para alcanzar la dirección artística de la referencia.

## Qué se puede aprovechar

| Área | Base existente | Trabajo de presentación pendiente |
|---|---|---|
| Sesión y reforma | Coordinador, baseline/draft, Apply/Discard y protección de salida | Revisiones y confirmaciones con el acabado y jerarquía aprobados |
| Selección y edición | Selección B8, principal, capacidades comunes, operaciones de grupo e historial global | Representación precisa por objeto y acceso coherente desde barras e inspector |
| Catálogo | B9: búsqueda, filtros, favoritos, recientes, virtualización y miniaturas | Composición Galería Viva, fotografía destacada, relacionados, tarjetas y estados |
| Sustitución | B10 y conexión al catálogo, coste y rollback | Conservar el flujo al terminar la ficha visual definitiva |
| Diagnósticos y plantillas | B11/B12 con evidencias técnicas y persistencia | Presentación de jugador y pruebas de interacción gráfica |
| Cámara | Núcleo 369 y trabajo B13 | Visibilidad de paredes y controles de edición verificados en build |

Estas bases tienen evidencias técnicas archivadas. Esta revisión no convierte esos informes en un PASS nuevo del editor completo.

## Hallazgos concretos

### 1. La presentación existente aún se aparta de la referencia

En las capturas R5 se observan tarjetas recortadas, textos del inspector claros sobre crema, controles de aspecto diferente al aprobado y una escena de prototipo con materiales y geometría provisionales. La composición general ya es reconocible, pero el acabado no alcanza el de la maqueta.

`BistroBuilderEditorV2BrassArt` genera marcos y botones procedurales de 128×128. Esta técnica produce una aproximación uniforme; no reproduce por sí sola la ornamentación, perfiles, sombras y relieve específicos del original. La implementación debe utilizar los recursos originales disponibles y piezas gráficas con fidelidad comprobable, con texto y controles nativos independientes.

La biblioteca existente de iconos tampoco equivale al inventario artístico final. Los conceptos actuales necesitan el aspecto aprobado, incluidos los iconos propios de selección, snapping, superficies y vistas.

### 2. La disposición todavía compite con componentes heredados

`BistroBuilderEditorV2PremiumLayout` aplica geometría en `LateUpdate` después de las pieles antiguas porque estas siguen modificando los mismos rectángulos. El propio comentario del código documenta esa competencia. Conviene establecer un propietario único de la disposición V2 y conservar los servicios de catálogo bajo él.

La versión actual calcula el ancho con `Screen.width` y lo escribe directamente en offsets de `RectTransform`. Hay que comprobar y corregir la conversión entre píxeles y unidades del Canvas con el escalado real, especialmente al cambiar resolución y abrir el inspector. Es un riesgo observable en el código; no se ha ejecutado una reproducción nueva de R8.

El defecto anterior de orden de Canvas sí está documentado: la UI nueva podía quedar por debajo del Canvas heredado. Existe un hotfix y un test específico. Debe conservarse esa protección al reconstruir la presentación.

### 3. El inspector está desarrollado parcialmente

La ficha de selección del mundo presenta Principal/Conjunto, miniatura, descripción, dimensiones y precio, y permite cambiar el principal. Todavía necesita reproducir las secciones y controles de Materiales/Variantes y las acciones respaldadas por capacidades reales.

La ficha de catálogo y la ficha de selección usan rutas de presentación distintas. Conviene compartir el tratamiento visual y los componentes de propiedades para evitar divergencias entre seleccionar un artículo y seleccionar su instancia colocada.

### 4. El Halo de Huella necesita completar geometría y animación

Existe `BistroBuilderEditorV2FootprintHalo`, conectado al catálogo. Utiliza la huella canónica para líneas de suelo y una envolvente de grupo.

Su implementación inicial tiene límites concretos:

- La decisión circular/rectangular consulta palabras del nombre visible (`redond`, `round`, `circular`). Debe depender de metadatos geométricos legítimos, con un comportamiento coherente cuando no estén disponibles.
- La opacidad pertenece al componente completo. La incorporación y retirada de miembros requiere estado visual por ID estable para animar solo el miembro afectado y conservar los demás.
- Este componente dibuja la huella de suelo; no implementa las esquinas ajustadas al volumen visible proyectado de cada objeto.
- La envolvente del grupo crea un array de esquinas durante su actualización. Conviene reutilizar buffers y limitar recálculos por cambios de selección, pose o cámara.

El renderer final debe contemplar mesas redondas y rectangulares, sillas con respaldo alto, rotaciones, cámaras y zoom, con entrada aproximada de 240 ms y salida de 120 ms. Su presentación lee el dominio sin modificar pose, física ni selección al pulsar.

### 5. Superficies requiere desarrollo funcional y gráfico adicional

`BistroBuilderSurfaceFinishVisuals.Build` solo materializa parches `floor` con `finish.floor.default`, usando un material de suelo del kit. La existencia de pestañas de Suelos/Paredes/Techos no demuestra soporte de otros acabados.

El Proyector Inteligente necesita selección de destino y acabado reales, orientación/escala reproducibles, reemplazo visible del material sobre la geometría pertinente, iluminación y oclusión correctas. Su confirmación utiliza las transacciones y persistencia canónicas. Las simulaciones CSS sobre fotografía son referencia de interacción y acabado; no constituyen el render de superficies Unity.

### 6. Hay documentación desactualizada

El índice generado enumera 81 fuentes frente a las 91 actualmente localizadas. `EDIT_MODE.md` afirma que V2 no ha comenzado; el plan maestro mantiene B13 como no iniciado, mientras su documento reciente y el código muestran trabajo técnico en desarrollo. La documentación visual conserva notas antiguas de aprobación pendiente y referencias a HTML que ahora están recuperados localmente.

La implementación debe reconciliar esas fuentes y regenerar índice/bundle en su rama. La confirmación gráfica actual del usuario se conserva como decisión vigente. Esto no implica que las pruebas de build pendientes se hayan realizado.

También existe una limitación tipográfica documentada: el recurso Recoleta procede de una versión DEMO y los acentos utilizan fallback. Hay que verificar el resultado real y el recurso de fuente disponible para alcanzar la fidelidad tipográfica esperada.

## Estrategia de implementación propuesta

**Tecnología:** Unity C#, uGUI y TextMeshPro, aprovechando la presentación y los contratos ya presentes. Los HTML se conservan como referencias comprobables de disposición, comportamiento y tiempos. El coste, validación, selección e historial proceden de los servicios del juego.

1. Fijar el punto de partida de Editor V2 y preservar el trabajo local existente. La carpeta de este chat está en `feature/dish-images-catalog`; la fuente V2 está en `BistroBuilder_EditorV2`. La copia de QA contiene una iteración anterior y se utiliza como evidencia comparativa.
2. Leer el corpus completo exigido por `AGENTS.md` antes de editar código y actualizar sus derivados si procede. Este análisis previo no ha iniciado esa edición.
3. Materializar el conjunto de barras, Galería Viva e inspector con las proporciones, arte, fuentes y estados de la referencia. Establecer una sola disposición responsable de V2 y conservar B9/B10.
4. Completar la representación de selección individual/grupal, microanimaciones y conexión de herramientas con B8.
5. Completar Taller de construcción y Superficies, incluido el prototipo 3D del Proyector Inteligente, sobre sus autoridades existentes.
6. Verificar en build gráfica 1920×1080 y 1280×720: aspecto, hit targets, scroll, foco, protección de input del mundo, movimiento de cámara, multiselección y transacciones.
7. Ejecutar regresiones proporcionales, Save/Load, Apply/Discard, Undo/Redo mixto y rendimiento representativo. El cierre requiere pruebas gráficas y funcionales además de compilación.

Las cifras de los benchmarks existentes no se convierten en garantías universales de FPS. La apariencia del escenario se evalúa con assets, materiales e iluminación reales; una fotografía de referencia no aporta automáticamente esos recursos.

## Conclusión operativa

Hay suficiente referencia recuperada y base técnica para desarrollar el Editor V2 aprobado. La parte más directa es la interfaz de paneles y barras. La selección proyectada y Superficies requieren mayor trabajo 3D y pruebas. El siguiente desarrollo debe producir una build verificable contra la referencia y conservar las autoridades, datos y comportamiento ya validados.

## Fuentes principales

- [Maqueta completa recuperada](/C:/Users/mruperez/ProyectoBB/BistroBuilder/docs/ModoEdicion/EditorV2/ReferenciasRecuperadas/Imagenes_Originales/EditorV2_MaquetaCompleta_Original.jpg)
- [Diseño UI V2](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md)
- [Incremento visual en QA](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2_UI_VisualQA_20261010/docs/ModoEdicion/EditorV2/UI/EDITOR_V2_UI_DESIGN.md:550)
- [Plan maestro](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/docs/20_GAME_SYSTEMS/EDITOR_V2_MASTER_PLAN.md)
- [Interacción aprobada](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/docs/ModoEdicion/EditInteractionDesign.md)
- [Disposición V2 actual](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderEditorV2PremiumLayout.cs:56)
- [Arte procedural actual](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Assets/Scripts/Presentation/UI/BistroBuilderEditorV2BrassArt.cs:38)
- [Halo actual](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderEditorV2FootprintHalo.cs:78)
- [Render actual de superficies](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Assets/Scripts/Presentation/Restaurant/EditMode/Block18/BistroBuilderSurfaceFinishVisuals.cs)
- [Captura gráfica R5 · 1920×1080](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Builds/Windows/EditorV2_PremiumReview_20261010_R5/EditorV2_Visual_Capture/EditorV2_1920x1080.png)
- [Captura gráfica R5 · 1280×720](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/Builds/Windows/EditorV2_PremiumReview_20261010_R5/EditorV2_Visual_Capture/EditorV2_1280x720.png)
- [Informe de compilación R8](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/EditorV2_PremiumReview_R8_Build_Report.txt)
- [Criterios de aceptación](/C:/Users/mruperez/ProyectoBB/BistroBuilder_EditorV2/docs/40_TESTING/ACCEPTANCE_AND_VALIDATION.md)
