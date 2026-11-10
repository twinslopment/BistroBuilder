from pathlib import Path
from bs4 import BeautifulSoup
import re
OUT=Path('/mnt/data/proveedores_bloque04')
src=BeautifulSoup(Path('/mnt/data/BistroBuilder_Inventario_Responsive_V7.html').read_text(encoding='utf8'),'html.parser')
source_top=BeautifulSoup(Path('/mnt/data/referencias_inventario/BistroBuilder_V21_V30_Mejorada_Preview.html').read_text(encoding='utf8'),'html.parser')
css_base=src.style.text
css_top=source_top.style.text
# Barra superior real aprobada V21 + V30, sin redefinir iconos.
match=re.search(r'(\.bistro-topbar\{.*?)(?=\.replay-control\{)',css_top,re.S)
assert match
nav_css=match.group(1)
# Especificación de movimientos V30 (copy original)
nav_motion=re.search(r'(@keyframes bb-v30-press\{.*?)(?=body\.motion-normal)',css_top,re.S).group(1)
css_extra=(OUT/'proveedores.css').read_text(encoding='utf8')
nav=source_top.select_one('.bistro-topbar')
nav['aria-label']='Barra superior V21/V30 — Proveedores'
for btn in nav.select('.nav-button'):
    if btn.get('data-section')=='Proveedores':btn['aria-current']='page'
    elif btn.has_attr('aria-current'):del btn['aria-current']
nav_markup=str(nav)
footer=str(src.select_one('.hud.bottom'))
footer=footer.replace('referencia de Personal','E5.5: referencia visual aprobada')
decor=str(src.select_one('.sidebar-decor'))
label_fit=src.select_one('#title-fit-controller').text
side_icons={
'suppliers':'<path d="M4 20V6l8-3 8 3v14H4Zm6 0v-7h4v7M4 9h16"/>',
'catalog':'<path d="M4 5h16v15H4zM4 10h16M8 5V3m8 2V3M8 14h8"/>',
'smart':'<path d="m3 18 6-8 5 4 7-10M14 4h7v7"/>',
'orders':'<path d="M5 3h14v18l-3-2-4 2-4-2-3 2V3ZM8 9h8m-8 4h8"/>'
}
labels=[('suppliers','Proveedores',''),('catalog','Catálogo','catalogCount'),('smart','Compra inteligente',''),('orders','Pedidos','ordersCount')]
links=[]
for i,(id,label,count) in enumerate(labels):
    add=f'<span class="count" id="{count}">0</span>' if count else ''
    links.append(f'<button type="button" data-tab="{id}" class="side-link {"active" if i==0 else ""}" aria-current="{ "page" if i==0 else "false" }" ><svg fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.6" viewBox="0 0 24 24">{side_icons[id]}</svg>{label}{add}</button>')
main='''<div class="app">
<aside class="sidebar"><h1 class="sidebar-title"><span class="sidebar-title-text">Proveedores</span></h1>
<nav id="menu" class="sidebar-links" aria-label="Secciones de Proveedores">'''+''.join(links)+'''</nav>'''+decor+'''</aside>
<main class="main" aria-label="Gestión de proveedores">
<div class="main-header"><div class="heading-text"><h1 id="screenTitle">Proveedores (6)</h1><p id="subtitle">Condiciones y fiabilidad de cada proveedor.</p></div>
<div class="action-bar"><button type="button" id="resetDemo" class="btn-icon" title="Restablecer datos ilustrativos" aria-label="Restablecer datos ilustrativos">↺</button><button type="button" id="primaryAction" class="btn-gold">Ver catálogo</button></div></div>
<div class="toolrow"><div class="pills" id="pills" aria-label="Filtros"></div><label class="searchbar"><svg viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5" fill="none"><circle cx="10" cy="10" r="6.5"/><path d="m15 15 6 6"/></svg><input type="search" id="searchInput" placeholder="Buscar proveedor..." aria-label="Buscar en la sección"/></label><select id="order" class="select-order" aria-label="Ordenar"></select></div>
<section class="table-frame" aria-label="Listado de Proveedores"><div id="cols" class="table-head"></div><div id="content" class="table-rows"></div><div class="table-foot"><span class="foot-note" id="tableFoot">Datos de demostración</span><span style="white-space:nowrap">DEMO · SIN UNITY</span></div></section>
<aside class="drawer" id="drawer" aria-label="Ficha contextual" aria-hidden="true" role="complementary"><div class="drawer-head" id="drawerHead"></div><button type="button" id="drawerClose" class="drawer-close" aria-label="Cerrar detalle">×</button><div class="drawer-body" id="drawerBody"></div><div class="drawer-actions" id="drawerActions"></div></aside>
</main></div>'''
modal='''<dialog id="orderDialog" aria-label="Revisión de pedido"><form method="dialog" id="orderForm"><div class="dialog-head"><h2 id="orderModalTitle">Revisar pedido</h2><small id="orderModalSub"></small></div><div class="dialog-body" id="orderSummary"></div><div class="dialog-foot"><button type="button" class="btn-minor" id="cancelOrderModal">Volver</button><button type="submit" id="submitOrder" class="btn-gold">Confirmar</button></div></form></dialog>'''
css=css_base+'\n'+nav_css+'\n'+nav_motion+'\n'+css_extra
js=(OUT/'proveedores.js').read_text(encoding='utf8')
html='''<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Bistro Builder · Bloque 04 Proveedores · Previsualización funcional</title><style>'''+css+'''</style></head><body>'''+nav_markup+main+footer+'''<div role="status" aria-live="polite" id="toast"></div>'''+modal+'''<script>'''+js+'''</script><script id="title-fit-controller">'''+label_fit+'''</script></body></html>'''
path=OUT/'BistroBuilder_Proveedores_Bloque04_Preview_V1.html'
path.write_text(html,encoding='utf-8')
print('Saved',path,'bytes',path.stat().st_size)