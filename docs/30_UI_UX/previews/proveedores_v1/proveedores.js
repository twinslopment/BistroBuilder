/* Bistro Builder · Bloque 04 · previsualización funcional (sin Unity).
   Datos y operaciones son DEMOSTRACIÓN. La integración futura debe usar las
   autoridades canónicas 2.3A/2.3C/2.3E/2.3F/2.3I; esta demo no las reproduce. */
(()=>{'use strict';
const $=sel=>document.querySelector(sel), $$=sel=>Array.from(document.querySelectorAll(sel));
const clone=x=>JSON.parse(JSON.stringify(x));
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const euros=n=>new Intl.NumberFormat('es-ES',{style:'currency',currency:'EUR',minimumFractionDigits:2,maximumFractionDigits:2}).format(n);
const suppliersSeed=[
 {id:'s1',name:'Mercado Central',category:'General',quality:'Buena',reliability:91,lead:'1 día',minimum:65,unlocked:true,notes:'Surtido amplio y entregas regulares.'},
 {id:'s2',name:'Huerta del Norte',category:'Verduras',quality:'Alta',reliability:96,lead:'1 día',minimum:45,unlocked:true,notes:'Verduras de temporada y compra de proximidad.'},
 {id:'s3',name:'Pescados del Cantábrico',category:'Pescados',quality:'Alta',reliability:88,lead:'2 días',minimum:90,unlocked:true,notes:'Especialista en pescado fresco.'},
 {id:'s4',name:'Cárnicas de Asturias',category:'Carnes',quality:'Alta',reliability:93,lead:'1 día',minimum:70,unlocked:true,notes:'Cortes frescos y formatos profesionales.'},
 {id:'s5',name:'Lácteos del Valle',category:'Lácteos',quality:'Buena',reliability:94,lead:'2 días',minimum:42,unlocked:true,notes:'Quesos, leche y mantequilla.'},
 {id:'s6',name:'Selección Gourmet',category:'Especialidades',quality:'Muy alta',reliability:98,lead:'3 días',minimum:125,unlocked:false,notes:'Proveedor sujeto a progresión de la partida.'}
];
const offersSeed=[
 {id:'o1',supplier:'s1',name:'Aceite de oliva',family:'Aceites',pack:'Garrafa 5 l',price:36.4,available:true,promo:false,quality:'Buena'},
 {id:'o2',supplier:'s1',name:'Arroz redondo',family:'Secos',pack:'Saco 10 kg',price:21.8,available:true,promo:true,quality:'Buena'},
 {id:'o3',supplier:'s1',name:'Harina de trigo',family:'Secos',pack:'Saco 10 kg',price:14.9,available:true,promo:false,quality:'Buena'},
 {id:'o4',supplier:'s2',name:'Lechuga romana',family:'Verduras',pack:'Caja 12 ud',price:19.6,available:true,promo:false,quality:'Alta'},
 {id:'o5',supplier:'s2',name:'Tomate rama',family:'Verduras',pack:'Caja 6 kg',price:16.8,available:true,promo:true,quality:'Alta'},
 {id:'o6',supplier:'s2',name:'Patatas',family:'Verduras',pack:'Saco 10 kg',price:18.3,available:true,promo:false,quality:'Alta'},
 {id:'o7',supplier:'s3',name:'Merluza fresca',family:'Pescados',pack:'Caja 5 kg',price:89.5,available:true,promo:false,quality:'Alta'},
 {id:'o8',supplier:'s3',name:'Gambas',family:'Pescados',pack:'Caja 3 kg',price:62.7,available:true,promo:false,quality:'Alta'},
 {id:'o9',supplier:'s3',name:'Bacalao',family:'Pescados',pack:'Caja 5 kg',price:74.2,available:false,promo:false,quality:'Alta'},
 {id:'o10',supplier:'s4',name:'Pechuga de pollo',family:'Carnes',pack:'Caja 6 kg',price:47.4,available:true,promo:true,quality:'Alta'},
 {id:'o11',supplier:'s4',name:'Jamón ibérico',family:'Carnes',pack:'Pieza 3 kg',price:84.5,available:true,promo:false,quality:'Alta'},
 {id:'o12',supplier:'s5',name:'Queso cabrales',family:'Lácteos',pack:'Caja 3 kg',price:42.8,available:true,promo:false,quality:'Buena'},
 {id:'o13',supplier:'s5',name:'Leche entera',family:'Lácteos',pack:'Caja 12 l',price:17.5,available:true,promo:false,quality:'Buena'},
 {id:'o14',supplier:'s6',name:'Trufa negra',family:'Especialidades',pack:'Estuche 100 g',price:138.5,available:true,promo:false,quality:'Muy alta'}
];
const ordersSeed=[
 {id:'P-2041',supplier:'s2',created:'Día 09',status:'Draft',day:'Sin programar',lines:[{offer:'o4',qty:2},{offer:'o5',qty:1}],source:'manual'},
 {id:'P-2039',supplier:'s3',created:'Día 08',status:'Confirmed',day:'Día 11',lines:[{offer:'o7',qty:2}],source:'manual'},
 {id:'P-2038',supplier:'s4',created:'Día 08',status:'PendingDelivery',day:'Día 10',lines:[{offer:'o10',qty:2}],source:'manual'},
 {id:'P-2034',supplier:'s1',created:'Día 07',status:'InDelivery',day:'Día 10',lines:[{offer:'o1',qty:2}],source:'manual'},
 {id:'P-2028',supplier:'s5',created:'Día 04',status:'Delivered',day:'Día 06',lines:[{offer:'o12',qty:1},{offer:'o13',qty:2}],source:'manual'},
 {id:'P-2022',supplier:'s2',created:'Día 02',status:'Cancelled',day:'—',lines:[{offer:'o6',qty:3}],source:'manual'}
];
const plans=[
 {id:'ahorrar',name:'Ahorrar',tag:'Menor coste',lead:'2–3 días',risk:'Medio',reason:'Favorece envases económicos, sin priorizar la entrega más rápida.',lines:[{offer:'o8',qty:2},{offer:'o5',qty:3},{offer:'o10',qty:2}]},
 {id:'equilibrado',name:'Equilibrado',tag:'Recomendado',lead:'1–2 días',risk:'Bajo',reason:'Equilibrio ilustrativo entre disponibilidad, coste y fiabilidad.',lines:[{offer:'o7',qty:2},{offer:'o4',qty:3},{offer:'o10',qty:2}]},
 {id:'urgente',name:'Urgente',tag:'Priorizar entrega',lead:'1 día',risk:'Bajo',reason:'Da prioridad a la reposición rápida aunque el coste pueda ser mayor.',lines:[{offer:'o7',qty:2},{offer:'o4',qty:4},{offer:'o10',qty:3}]}
];
let suppliers=clone(suppliersSeed),offers=clone(offersSeed),orders=clone(ordersSeed),planCreated=new Set();
let current='suppliers',filter='Todos',search='',sorting='default',selected=null,drawerOpen=false,packageCount=1,selectedPlan='equilibrado',seq=2042,dialogAction=null;
const names={suppliers:'Proveedores',catalog:'Catálogo de productos',smart:'Compra inteligente',orders:'Pedidos del restaurante'};
const hints={suppliers:'Condiciones y fiabilidad de cada proveedor.',catalog:'Ofertas, formatos y disponibilidad de compra.',smart:'Compara estrategias sin realizar compras automáticas.',orders:'Borradores, confirmaciones y seguimiento de entregas.'};
const supplier=id=>suppliers.find(s=>s.id===id);
const offer=id=>offers.find(o=>o.id===id);
const total=o=>o.lines.reduce((sum,l)=>sum+((offer(l.offer)?.price||0)*l.qty),0);
const planTotal=p=>p.lines.reduce((sum,l)=>sum+offer(l.offer).price*l.qty,0);
const draftCount=()=>orders.filter(o=>o.status==='Draft').length;
const statusNames={Draft:'Borrador',Confirmed:'Confirmado',PendingDelivery:'Pendiente entrega',InDelivery:'En reparto',Delivered:'Entregado',Cancelled:'Cancelado'};
const statusCl={Draft:'draft',Confirmed:'confirmed',PendingDelivery:'low',InDelivery:'intransit',Delivered:'delivered',Cancelled:'cancelled'};
const tag=(label,cl='')=>`<span class="status ${cl}">${esc(label)}</span>`;
const shape={supplier:'<path d="M4 20V6l8-3 8 3v14H4Zm6 0v-7h4v7M4 9h16M7 6v2m5-3v3m5-2v2"/>',catalog:'<path d="M4 5h16v15H4zM4 10h16M8 5V3m8 2V3M8 14h8m-8 3h5"/>',smart:'<path d="M3 18h18M6 15l4-5 4 3 4-8M18 5h-5m5 0v5"/>',orders:'<path d="M5 3h14v18l-3-2-4 2-4-2-3 2V3Zm3 5h8M8 12h8"/>',box:'<path d="M4 7 12 3l8 4v11l-8 4-8-4V7Zm0 0 8 5 8-5M12 12v10"/>',fish:'<path d="M2 12c5-9 14-9 19 0-5 9-14 9-19 0Zm0 0 4-5v10l-4-5ZM13 11h.01"/>',leaf:'<path d="M20 4C11 4 4 8 4 16c0 5 12 5 16-12ZM4 20c2-6 7-10 12-12"/>',truck:'<path d="M2 6h13v11H2zM15 10h4l3 4v3h-7M5 19h3m9 0h3"/><circle cx="6.5" cy="18" r="2"/><circle cx="18" cy="18" r="2"/>',lock:'<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',meat:'<path d="M6 5c4-3 11 0 12 6s-3 10-9 9S1 13 6 5Zm5 5 3 3"/>',cart:'<path d="M2 4h3l3 13h11l3-9H6M10 21h.01M18 21h.01"/>'};
const ico=(kind,sz=23)=>`<svg aria-hidden="true" viewBox="0 0 24 24" width="${sz}" height="${sz}" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${shape[kind]||shape.box}</svg>`;
const iconType=(category)=>category==='Pescados'?'fish':category==='Verduras'?'leaf':category==='Carnes'?'meat':category==='Especialidades'?'lock':'box';
function portrait(icon){return `<span class="mini-portrait">${ico(icon)}</span>`}
function cName(name,sub,icon){return `<span class="c-name">${portrait(icon)}<span><strong>${esc(name)}</strong><small>${esc(sub)}</small></span></span>`}
function row(id,cl,content,extra=''){return `<button type="button" data-row="${esc(id)}" class="table-row ${cl} ${selected===id?'selected':''}" ${extra}>${content}</button>`}
function setHead(titles,cls){$('#cols').className='table-head '+cls;$('#cols').innerHTML=titles.map(t=>`<span>${esc(t)}</span>`).join('')}
function setRows(rows,foot){$('#content').innerHTML=rows||'<div class="empty">No hay resultados con estos filtros.</div>';$('#tableFoot').textContent=foot}
function switchTo(section,params={}){current=section;filter='Todos';search=params.search||'';sorting='default';selected=params.selected||null;drawerOpen=Boolean(params.selected);packageCount=1;$('#searchInput').value=search;$('#order').value='default';render()}
function openDrawer(id){selected=id;drawerOpen=true;packageCount=1;render()}
function closeDrawer(){drawerOpen=false;selected=null;renderDrawer();$$('.table-row').forEach(el=>el.classList.remove('selected'))}
function notify(message){let el=$('#toast');el.textContent=message;el.classList.add('show');clearTimeout(notify.timer);notify.timer=setTimeout(()=>el.classList.remove('show'),3100)}
const formatQty=x=>String(x).replace('.',',');
function pillsFor(){let names=[];
 switch(current){case 'suppliers':names=['Todos','Disponibles','Bloqueados','Especialistas'];break;
 case 'catalog':names=['Todos','Disponibles','Promociones','Verduras','Pescados','Carnes'];break;
 case 'smart':names=['Todos','Ahorrar','Equilibrado','Urgente'];break;
 case 'orders':names=['Todos','Borradores','En curso','Entregados','Cancelados'];break;}
 $('#pills').innerHTML=names.map(n=>`<button type="button" class="pill ${filter===n?'selected':''}" data-filter="${esc(n)}" aria-pressed="${filter===n}">${esc(n)}</button>`).join('');
}
function renderHead(){
 $('#screenTitle').textContent=`${names[current]}${current==='suppliers'?` (${suppliers.length})`:current==='orders'?` (${orders.length})`:''}`;
 $('#subtitle').textContent=hints[current];
 $$('.side-link').forEach(b=>{b.classList.toggle('active',b.dataset.tab===current);b.setAttribute('aria-current',b.dataset.tab===current?'page':'false')});
 $('#catalogCount').textContent=offers.filter(o=>o.available&&supplier(o.supplier).unlocked).length;
 $('#ordersCount').textContent=draftCount();
 $('#searchInput').placeholder=current==='suppliers'?'Buscar proveedor...':current==='catalog'?'Buscar producto o proveedor...':current==='smart'?'Buscar estrategia...':'Buscar pedido o proveedor...';
 const btn=$('#primaryAction');btn.textContent= current==='suppliers'?'Ver catálogo':current==='catalog'?'Ver pedidos':current==='smart'?'Crear borradores del plan':'Buscar productos';
 $('#order').innerHTML=current==='suppliers'?'<option value="default">Por nombre</option><option value="reliability">Fiabilidad</option><option value="lead">Plazo</option>':current==='catalog'?'<option value="default">Por producto</option><option value="price">Menor precio</option><option value="supplier">Proveedor</option>':current==='smart'?'<option value="default">Estrategia</option><option value="price">Menor coste</option>':'<option value="default">Más recientes</option><option value="price">Mayor importe</option><option value="supplier">Proveedor</option>';
 $('#order').value=sorting;
}
function matches(str){return str.toLocaleLowerCase('es').includes(search.toLocaleLowerCase('es').trim())}
function renderSuppliers(){let data=suppliers.filter(s=>matches(`${s.name} ${s.category} ${s.notes}`));
 if(filter==='Disponibles')data=data.filter(s=>s.unlocked);if(filter==='Bloqueados')data=data.filter(s=>!s.unlocked);if(filter==='Especialistas')data=data.filter(s=>s.category!=='General');
 if(sorting==='reliability')data.sort((a,b)=>b.reliability-a.reliability);else if(sorting==='lead')data.sort((a,b)=>parseInt(a.lead)-parseInt(b.lead));else data.sort((a,b)=>a.name.localeCompare(b.name,'es'));
 setHead(['Proveedor','Especialidad','Calidad','Fiabilidad','Entrega','Estado',''],'grid-supplier');
 const html=data.map(s=>row(s.id,'grid-supplier',`${cName(s.name,s.category,iconType(s.category))}<span class="cell-muted">${esc(s.category)}</span><span class="cell-quality">${esc(s.quality)}<span class="quality-meter"><i style="width:${s.quality==='Muy alta'?95:s.quality==='Alta'?82:65}%"></i></span></span><span class="cell-numeric">${s.reliability}%</span><span class="cell-muted">${esc(s.lead)}</span>${tag(s.unlocked?'Disponible':'Bloqueado',s.unlocked?'':'locked')}<span class="cell-end">•••</span>`)).join('');
 setRows(html,`${data.length} de ${suppliers.length} proveedores · El desbloqueo depende de la progresión del juego`);
}
function renderCatalog(){let data=offers.filter(o=>matches(`${o.name} ${o.family} ${supplier(o.supplier).name}`));
 if(filter==='Disponibles')data=data.filter(o=>o.available&&supplier(o.supplier).unlocked);
 if(filter==='Promociones')data=data.filter(o=>o.promo);if(['Verduras','Pescados','Carnes'].includes(filter))data=data.filter(o=>o.family===filter);
 if(sorting==='price')data.sort((a,b)=>a.price-b.price);else if(sorting==='supplier')data.sort((a,b)=>supplier(a.supplier).name.localeCompare(supplier(b.supplier).name,'es'));else data.sort((a,b)=>a.name.localeCompare(b.name,'es'));
 setHead(['Producto','Proveedor','Formato','Precio','Estado',''],'grid-catalog');
 setRows(data.map(o=>row(o.id,'grid-catalog',`${cName(o.name,o.family,iconType(o.family))}<span class="cell-muted">${esc(supplier(o.supplier).name)}</span><span class="cell-muted">${esc(o.pack)}</span><span class="cell-numeric">${euros(o.price)}</span>${tag(!supplier(o.supplier).unlocked?'Bloqueado':!o.available?'Sin oferta':o.promo?'Promoción':'Disponible',!supplier(o.supplier).unlocked?'locked':!o.available?'cancelled':o.promo?'promo':'')}<span class="cell-end">•••</span>`)).join(''),`${data.length} de ${offers.length} ofertas ilustrativas · Compras por envase, sin micromanejo de unidades`);
}
function renderSmart(){let data=plans.filter(p=>matches(`${p.name} ${p.tag} ${p.reason}`));if(filter!=='Todos')data=data.filter(p=>p.name===filter);
 if(sorting==='price')data.sort((a,b)=>planTotal(a)-planTotal(b));
 setHead(['Estrategia','Importe estimado','Plazo ilustrativo','Riesgo','Preferencia',''],'grid-smart');
 setRows(data.map(p=>row(p.id,'grid-smart',`${cName(p.name,p.reason,'smart')}<span class="cell-numeric">${euros(planTotal(p))}</span><span class="cell-muted">${esc(p.lead)}</span><span class="cell-muted">${esc(p.risk)}</span>${tag(p.id==='equilibrado'?'Recomendada':'Alternativa',p.id==='equilibrado'?'':'neutral')}<span class="cell-end">•••</span>`,p.id==='equilibrado'?'data-recommended="true"':'' )).join(''),`Comparación no vinculante · Ninguna estrategia compra automáticamente`);
}
function renderOrders(){let data=orders.filter(o=>matches(`${o.id} ${supplier(o.supplier).name} ${statusNames[o.status]}`));
 if(filter==='Borradores')data=data.filter(o=>o.status==='Draft');
 if(filter==='En curso')data=data.filter(o=>['Confirmed','PendingDelivery','InDelivery'].includes(o.status));
 if(filter==='Entregados')data=data.filter(o=>o.status==='Delivered');
 if(filter==='Cancelados')data=data.filter(o=>o.status==='Cancelled');
 if(sorting==='price')data.sort((a,b)=>total(b)-total(a));else if(sorting==='supplier')data.sort((a,b)=>supplier(a.supplier).name.localeCompare(supplier(b.supplier).name,'es'));else data.sort((a,b)=>Number(b.id.match(/\d+/)[0])-Number(a.id.match(/\d+/)[0]));
 setHead(['Pedido','Proveedor','Importe','Entrega estimada','Estado',''],'grid-orders');
 setRows(data.map(o=>row(o.id,'grid-orders',`${cName(o.id,`${o.lines.length} línea(s) · ${o.created}`,'orders')}<span class="cell-muted">${esc(supplier(o.supplier).name)}</span><span class="cell-numeric">${euros(total(o))}</span><span class="cell-muted">${esc(o.day)}</span>${tag(statusNames[o.status],statusCl[o.status])}<span class="cell-end">•••</span>`)).join(''),`${data.length} de ${orders.length} pedidos · Los estados reflejan un ciclo simulado de pedido`);
}
function makeStat(label,value){return `<span class="stat"><small>${esc(label)}</small><strong>${esc(value)}</strong></span>`}
function makeLine(k,v){return `<div class="data-line"><span>${esc(k)}</span><strong>${esc(v)}</strong></div>`}
function action(label,code,cl='btn-gold',disabled=false){return `<button class="${cl}" data-action="${code}" ${disabled?'disabled':''}>${esc(label)}</button>`}
function renderDrawer(){const box=$('#drawer');box.classList.toggle('open',drawerOpen);box.setAttribute('aria-hidden',drawerOpen?'false':'true');if(!drawerOpen||!selected){return}
 let header='',body='',actions='';
 if(current==='suppliers'){
  const s=supplier(selected);if(!s){closeDrawer();return}
  header=`<span class="kicker">FICHA DEL PROVEEDOR · ${esc(s.category)}</span><h2>${esc(s.name)}</h2>${tag(s.unlocked?'Disponible':'Bloqueado',s.unlocked?'':'locked')}`;
  body=`<div class="stat-strip">${makeStat('Fiabilidad',s.reliability+'%')}${makeStat('Calidad',s.quality)}${makeStat('Entrega',s.lead)}</div><h3>Condiciones de suministro</h3>${makeLine('Pedido mínimo',euros(s.minimum))}${makeLine('Oferta de catálogo',offers.filter(o=>o.supplier===s.id).length+' productos')}${makeLine('Estado',s.unlocked?'Desbloqueado':'Pendiente de progresión')}<div class="notice">${esc(s.notes)} ${s.unlocked?'':'Esta versión solo muestra el bloqueo; no inventa condiciones para desbloquearlo.'}</div>`;
  actions=action(s.unlocked?'Ver su catálogo':'Ver requisitos de progreso',s.unlocked?'supplierCatalog':'lockedInfo','btn-gold')+action('Compra inteligente','smart','btn-minor');
 }else if(current==='catalog'){
  const o=offer(selected);if(!o){closeDrawer();return}const s=supplier(o.supplier);const available=o.available&&s.unlocked;
  header=`<span class="kicker">CATÁLOGO · ${esc(o.family)}</span><h2>${esc(o.name)}</h2>${tag(!available?'No disponible':o.promo?'Promoción':'Disponible',!available?'locked':o.promo?'promo':'')}`;
  body=`<div class="stat-strip">${makeStat('Envase',o.pack)}${makeStat('Precio',euros(o.price))}${makeStat('Calidad',o.quality)}</div><h3>Oferta seleccionada</h3>${makeLine('Proveedor',s.name)}${makeLine('Pedido mínimo del proveedor',euros(s.minimum))}${makeLine('Entrega estimada',s.lead)}<div class="qty-control"><label for="packQty">Envases</label><button type="button" data-action="decQty" aria-label="Restar envase">−</button><input id="packQty" type="number" min="1" max="99" step="1" value="${packageCount}" aria-label="Número de envases"><button type="button" data-action="incQty" aria-label="Sumar envase">+</button></div><div class="compare-box"><small>Importe de esta línea</small><strong id="packTotal">${euros(o.price*packageCount)}</strong><small>El pedido se conserva como borrador hasta confirmar.</small></div>${!available?'<div class="notice">Oferta bloqueada o temporalmente no disponible; no se pueden crear borradores con ella.</div>':''}`;
  actions=action('Añadir a borrador','addDraft','btn-gold',!available)+action('Ver pedidos','orders','btn-minor');
 }else if(current==='smart'){
  let p=plans.find(p=>p.id===selected)||plans.find(p=>p.id===selectedPlan);selected=p.id;selectedPlan=p.id;
  header=`<span class="kicker">MOTOR DE COMPRA INTELIGENTE · DEMO</span><h2>${esc(p.name)}</h2>${tag(p.tag,p.id==='equilibrado'?'':'neutral')}`;
  body=`<div class="stat-strip">${makeStat('Coste',euros(planTotal(p)))}${makeStat('Plazo',p.lead)}${makeStat('Riesgo',p.risk)}</div><div class="notice">${esc(p.reason)}</div><h3>Reposición propuesta</h3>${p.lines.map(l=>makeLine(`${offer(l.offer).name} · ${l.qty} envase(s)`,euros(offer(l.offer).price*l.qty))).join('')}<p class="demo-explanation">La maqueta utiliza ejemplos fijos. En Unity, este cálculo corresponde al motor 2.3F y a la disponibilidad del inventario.</p>`;
  actions=action(planCreated.has(p.id)?'Plan ya convertido en borradores':'Crear borradores de este plan','createPlan','btn-gold',planCreated.has(p.id))+action('Ver pedidos','orders','btn-minor');
 }else if(current==='orders'){
  let o=orders.find(o=>o.id===selected);if(!o){closeDrawer();return}const s=supplier(o.supplier);let sum=total(o);const canConfirm=o.status==='Draft'&&sum>=s.minimum&&o.lines.length>0;
  header=`<span class="kicker">HISTORIAL DE PEDIDOS · ${esc(o.id)}</span><h2>${esc(s.name)}</h2>${tag(statusNames[o.status],statusCl[o.status])}`;
  body=`<div class="stat-strip">${makeStat('Importe',euros(sum))}${makeStat('Líneas',o.lines.length)}${makeStat('Entrega',o.day)}</div><h3>Productos del pedido</h3>${o.lines.map(l=>makeLine(`${offer(l.offer)?.name||'Producto'} · ${l.qty} env.`,euros((offer(l.offer)?.price||0)*l.qty))).join('')}<h3>Estado administrativo</h3>${makeLine('Estado',statusNames[o.status])}${makeLine('Proveedor',s.name)}${makeLine('Pedido mínimo',euros(s.minimum))}${makeLine('Creado',o.created)}${o.status==='Draft'&&sum<s.minimum?`<div class="notice">Faltan ${euros(s.minimum-sum)} para alcanzar el pedido mínimo. Añade más productos del proveedor desde Catálogo antes de confirmar.</div>`:''}`;
  if(o.status==='Draft')actions=action('Revisar y confirmar','confirmOrder','btn-gold',!canConfirm)+action('Cancelar borrador','cancelOrder','btn-minor');
  else if(['Confirmed','PendingDelivery'].includes(o.status))actions=action('Solicitar cancelación','cancelOrder','btn-gold')+action('Ver proveedor','orderSupplier','btn-minor');
  else actions=action('Ver proveedor','orderSupplier','btn-minor')+action('Ver catálogo','catalog','btn-minor');
 }
 $('#drawerHead').innerHTML=header;$('#drawerBody').innerHTML=body;$('#drawerActions').innerHTML=actions;
}
function render(){renderHead();pillsFor();if(current==='suppliers')renderSuppliers();else if(current==='catalog')renderCatalog();else if(current==='smart')renderSmart();else renderOrders();renderDrawer()}
function addDraft(){let o=offer(selected),s=supplier(o?.supplier);if(!o||!s||!o.available||!s.unlocked)return notify('La oferta no está disponible para pedido.');
 let count=Number($('#packQty')?.value||packageCount);if(!Number.isInteger(count)||count<1||count>99)return notify('Indica entre 1 y 99 envases.');
 let draft=orders.find(d=>d.status==='Draft'&&d.supplier===s.id);if(!draft){draft={id:`P-${seq++}`,supplier:s.id,created:'Día 10',status:'Draft',day:'Sin programar',lines:[],source:'catalog'};orders.unshift(draft)}
 let line=draft.lines.find(l=>l.offer===o.id);if(line)line.qty+=count;else draft.lines.push({offer:o.id,qty:count});notify(`${count} envase(s) añadidos a ${draft.id}. No se ha confirmado ninguna compra.`);switchTo('orders',{selected:draft.id})}
function createPlan(){let p=plans.find(x=>x.id===selectedPlan);if(!p||planCreated.has(p.id))return;
 let group={};for(const ln of p.lines){const sid=offer(ln.offer).supplier;(group[sid]??=[]).push(ln)}
 let ids=[];for(const [sid,lines] of Object.entries(group)){let draft=orders.find(o=>o.status==='Draft'&&o.supplier===sid);if(!draft){draft={id:`P-${seq++}`,supplier:sid,created:'Día 10',status:'Draft',day:'Sin programar',lines:[],source:'smart'};orders.unshift(draft)}for(const line of lines){let x=draft.lines.find(l=>l.offer===line.offer);if(x)x.qty+=line.qty;else draft.lines.push({...line})}ids.push(draft.id)}
 planCreated.add(p.id);notify(`Plan ${p.name}: ${ids.length} borrador(es) actualizado(s). Ningún pedido confirmado.`);switchTo('orders',{selected:ids[0]});}
function openOrderDialog(action){const o=orders.find(x=>x.id===selected);if(!o)return;dialogAction={action,id:o.id};const sum=total(o),s=supplier(o.supplier);$('#orderModalTitle').textContent=action==='confirm'?'Confirmar pedido simulado':'Cancelar pedido simulado';$('#orderModalSub').textContent=`${o.id} · ${s.name}`;
 $('#orderSummary').innerHTML=`<div class="review-line"><span>Proveedor</span><strong>${esc(s.name)}</strong></div><div class="review-line"><span>Importe total</span><strong>${euros(sum)}</strong></div><div class="review-line"><span>Pedido mínimo</span><strong>${euros(s.minimum)}</strong></div><div class="review-line"><span>Estado actual</span><strong>${esc(statusNames[o.status])}</strong></div><div class="small-review">${action==='confirm'?'En esta maqueta el pedido cambia a Confirmado; no se cobra dinero ni se modifica inventario.':'La cancelación solo afecta a estos datos de ejemplo.'}</div>`;
 $('#submitOrder').textContent=action==='confirm'?'Confirmar pedido':'Confirmar cancelación';$('#orderDialog').showModal();}
function applyOrderDialog(ev){ev.preventDefault();const action=dialogAction;if(!action)return;let o=orders.find(x=>x.id===action.id);if(!o)return;const sup=supplier(o.supplier);
 if(action.action==='confirm'){if(o.status!=='Draft'||total(o)<sup.minimum){notify('No es posible confirmar: revisa el pedido mínimo y el estado.');return}o.status='Confirmed';o.day='Día 11';notify(`Pedido ${o.id} confirmado SOLO en la simulación.`)}
 else {if(!['Draft','Confirmed','PendingDelivery'].includes(o.status)){notify('Este estado no permite cancelación en la maqueta.');return}o.status='Cancelled';o.day='—';notify(`Pedido ${o.id} cancelado SOLO en la simulación.`)}
 $('#orderDialog').close();dialogAction=null;render();}
function reset(){suppliers=clone(suppliersSeed);offers=clone(offersSeed);orders=clone(ordersSeed);seq=2042;planCreated=new Set();filter='Todos';search='';sorting='default';selected=null;drawerOpen=false;packageCount=1;selectedPlan='equilibrado';current='suppliers';$('#searchInput').value='';render();notify('Datos ilustrativos restaurados.');}
$('#menu').addEventListener('click',ev=>{const b=ev.target.closest('[data-tab]');if(b)switchTo(b.dataset.tab)});
$('#pills').addEventListener('click',ev=>{const b=ev.target.closest('[data-filter]');if(!b)return;filter=b.dataset.filter;selected=null;drawerOpen=false;if(current==='smart'&&filter!=='Todos'){selectedPlan=filter.toLocaleLowerCase('es');}render()});
$('#searchInput').addEventListener('input',ev=>{search=ev.target.value;render()});
$('#order').addEventListener('change',ev=>{sorting=ev.target.value;render()});
$('#content').addEventListener('click',ev=>{const row=ev.target.closest('[data-row]');if(row)openDrawer(row.dataset.row)});
$('#drawerClose').addEventListener('click',closeDrawer);
$('#drawer').addEventListener('click',ev=>{const b=ev.target.closest('[data-action]');if(!b)return;const id=b.dataset.action;
 if(id==='supplierCatalog'){let s=supplier(selected);switchTo('catalog',{search:s.name})}
 else if(id==='lockedInfo')notify('Este proveedor permanece bloqueado; la progresión no se simula.');
 else if(id==='smart')switchTo('smart',{selected:'equilibrado'});
 else if(id==='catalog')switchTo('catalog');
 else if(id==='orders')switchTo('orders');
 else if(id==='orderSupplier'){let o=orders.find(o=>o.id===selected);if(o)switchTo('suppliers',{selected:o.supplier})}
 else if(id==='addDraft')addDraft();
 else if(id==='createPlan')createPlan();
 else if(id==='confirmOrder')openOrderDialog('confirm');
 else if(id==='cancelOrder')openOrderDialog('cancel');
 else if(id==='incQty'||id==='decQty'){packageCount=Math.max(1,Math.min(99,packageCount+(id==='incQty'?1:-1)));const i=$('#packQty');if(i)i.value=packageCount;const o=offer(selected);if($('#packTotal')&&o)$('#packTotal').textContent=euros(o.price*packageCount)}
 });
$('#drawer').addEventListener('input',ev=>{if(ev.target.id==='packQty'){const val=Number(ev.target.value);if(Number.isInteger(val)&&val>0&&val<=99){packageCount=val;$('#packTotal').textContent=euros(offer(selected).price*packageCount)}}});
$('#primaryAction').addEventListener('click',()=>{if(current==='suppliers')switchTo('catalog');else if(current==='catalog')switchTo('orders');else if(current==='smart')createPlan();else switchTo('catalog')});
$('#resetDemo').addEventListener('click',reset);
$('#orderForm').addEventListener('submit',applyOrderDialog);
$('#cancelOrderModal').addEventListener('click',()=>{$('#orderDialog').close();dialogAction=null});
window.addEventListener('keydown',ev=>{if(ev.key==='Escape'&&drawerOpen&&!$('#orderDialog').open)closeDrawer()});
// V21 + V30: barra oficial de prototipo, solo el área Proveedores navega en esta maqueta.
$$('.bistro-topbar .nav-button').forEach(b=>b.addEventListener('click',()=>{
 b.classList.remove('is-animating');void b.offsetWidth;b.classList.add('is-animating');
 if(b.dataset.section==='Proveedores')switchTo('suppliers');else notify('La maqueta del bloque 04 solo contiene la pantalla Proveedores.');
 setTimeout(()=>b.classList.remove('is-animating'),300);
}));
render();
})();