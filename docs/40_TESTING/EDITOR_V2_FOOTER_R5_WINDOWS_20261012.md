# Editor V2 — R5 Windows: barra inferior compacta — 12/10/2026

## Decisión del usuario y alcance

El usuario pide corregir exclusivamente la barra inferior del ejecutable: MODO, EDITAR y AYUDAS más pequeños y sobre el fondo de su celda, barra de menor altura y unida al borde inferior, sin restaurante ni otra pantalla visible por debajo. Evidencia aportada: `codex-clipboard-5bf5249a-39fb-4135-b55a-4d5d1eac2b7c.png`.

Esta petición precisa la composición R4 y sustituye las medidas anteriores del pie (101 de altura y margen inferior 8) por las siguientes. Conserva la historia y las demás decisiones de paneles/filtros. La implementación y la entrega continúan en Unity/TMP y Windows x64.

## Resultado implementado

- Altura 85 unidades de referencia: reducción del 15,8 % respecto a 101. En la pantalla física 1920 × 1080 ocupa aproximadamente 98 píxeles.
- Pie a posición inferior 0, ancho completo del viewport; no queda una franja del mundo debajo o en los laterales.
- Papel opaco continuo en toda la barra, incluidos huecos ornamentales de los rótulos y esquinas. El fondo antes tenía un inset de 10 que dejaba ver el mundo entre los segmentos del marco.
- Rótulos de 16 en lugar de 20, centrados en sus huecos ornamentales. AYUDAS también corrige su centro. Ningún rótulo invade botones o sale del fondo de papel.
- Once botones de altura 54 en lugar de 64; iconos SDF al 85 % y etiquetas de 13. Un solo marco ornamental conserva las uniones y se ajusta al ancho/alto real del pie.
- Acciones y filtros existentes siguen conectados a sus autoridades. No se cambia la escena ni el contenido del catálogo. La build R5 abre directamente V2 a pantalla completa, con F11 para alternar ventana.

## Verificación

- `Logs/editor_v2_r5_footer_qa_2.log`: **147 comprobaciones Play Mode PASS**, salida 0. Incluye búsqueda, filtros, favoritos, recientes, precio/ámbito, orden, reciclado, colocación, ficha, zoom, selección y arquitectura; añade borde inferior sin hueco, cobertura total de papel, tamaño compacto y títulos sin desbordamiento ni solape con botones.
- `Logs/editor_v2_r5_footer_build.log`: Unity 6000.3.19f1; **Build Succeeded, 0 errores, 13 advertencias**. Se restaura la configuración general del proyecto tras compilar la revisión específica.
- `Logs/editor_v2_r5_footer_startup.log`: Player GPU, salida 0; `CAPTURE_COMPLETE`, Seleccionar, Mesas, un inspector y FullScreenWindow **1920 × 1080**.
- `Logs/editor_v2_r5_footer_player.log`: Player GPU, salida 0; `CAPTURE_COMPLETE` en catálogo, asientos, búsqueda, vacío, filtros, ficha, construcción, superficies y selección. Tamaños 1280 × 720, 1672 × 941 y 1908 × 1042; pantalla completa física 1920 × 1080 y regreso a ventana verificados.
- Captura `ReviewStartup_Fullscreen.png` inspeccionada visualmente, con recorte de los 98 píxeles del pie: rótulos sobre papel, botones más pequeños, ningún resto de restaurante bajo el marco. `WorldSelection_4K.png` conserva supersampling 2× y no representa un monitor físico 4K.

La primera ejecución de QA (`editor_v2_r5_footer_qa.log`) falló al comparar exactamente dos Rect de pantalla con redondeos subpíxel. Se corrigió la prueba para admitir menos de 0,75 píxeles y mostrar los límites/alpha reales; se volvió a ejecutar la batería completa. Se conservan ambas evidencias. No se atribuye PASS a la ejecución fallida.

La regresión B9 de 10.000 artículos y 42/42 comprobaciones se conserva como evidencia de R4; no se repite ni se cuenta como resultado nuevo de R5, porque este ajuste solo modifica la geometría/presentación del pie.

## Entrega

`Builds/Windows/EditorV2_R5_20261012/BistroBuilder_EditorV2_R5.exe` contiene el ajuste. Mantener toda la carpeta de datos/runtime junto al ejecutable. Incluye capturas, registros, fuentes de la intervención e informe. El manifiesto SHA256 compara el código de `Assembly-CSharp.dll` con R4 para verificar que contiene la corrección.

R4 permanece intacta como revisión anterior. No se declara aceptación gráfica global ni igualdad por píxel de todos los paneles; esta revisión atiende el ajuste específico del pie. Rama de implementación sin integrar.
