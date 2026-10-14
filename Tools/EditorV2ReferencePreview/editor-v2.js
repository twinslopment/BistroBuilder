/* The eight reference products are a visual-review fixture, never Unity game data. */
(() => {
  'use strict';
  const $=s=>document.querySelector(s), all=s=>[...document.querySelectorAll(s)];
  const paths={close:'<path d="m6 6 12 12M18 6 6 18"/>',search:'<circle cx="10" cy="10" r="6.5"/><path d="m15 15 6 6"/>',filter:'<path d="M3 5h18M5 10h14M8 15h8M11 20h2" stroke-width="2.8"/>',heart:'<path d="M12 21 3.8 12.5C-2 6.4 6.5-.5 12 6c5.5-6.5 14 .4 8.2 6.5Z"/>',star:'<path d="m12 2 3 6.2 6.8 1-4.9 4.8 1.2 6.8-6.1-3.2-6.1 3.2 1.2-6.8L2.2 9.2l6.8-1Z"/>','star-fill':'<path fill="currentColor" stroke="none" d="m12 2 3 6.2 6.8 1-4.9 4.8 1.2 6.8-6.1-3.2-6.1 3.2 1.2-6.8L2.2 9.2l6.8-1Z"/>','grid-fill':'<path fill="currentColor" stroke="none" d="M2 2h9v9H2zm11 0h9v9h-9zM2 13h9v9H2zm11 0h9v9h-9z"/>',clock:'<circle cx="12" cy="12" r="9"/><path d="M12 6v6l4 3"/>',left:'<path d="m14 5-7 7 7 7"/>',right:'<path d="m10 5 7 7-7 7"/>',arrow:'<path d="M3 12h18m-6-5 6 5-6 5"/>',bag:'<path fill="currentColor" d="M6 8h12l1 12H5Zm3 0V5h6v3"/>',undo:'<path d="m9 4-6 6 6 6M3 10h10c10 0 10 12 1 12"/>',redo:'<path d="m15 4 6 6-6 6m6-6H11c-10 0-10 12-1 12"/>',sun:'<circle fill="currentColor" cx="12" cy="12" r="5"/><path d="M12 1v2m0 18v2M1 12h2m18 0h2M4 4l2 2m12 12 2 2m0-16-2 2M6 18l-2 2"/>',camera:'<path fill="currentColor" stroke="none" d="M3 6h5l2-3h4l2 3h5v15H3Z"/><circle fill="#f7ecdc" stroke="none" cx="12" cy="13" r="5"/><circle fill="currentColor" cx="12" cy="13" r="3"/>',gear:'<path fill="currentColor" d="m10 2 4 0 1 3 3 1 2-1 2 4-2 2v3l2 2-2 4-3-1-2 1-1 3h-4l-1-3-3-1-2 1-2-4 2-2v-3L2 9l2-4 3 1 2-1Z"/><circle cx="12" cy="12" r="3" fill="#f7ecdc" stroke="none"/>',pencil:'<path fill="currentColor" d="m17 2 5 5-13 13-6 1 1-6Z"/><path d="m14 5 5 5" stroke="#f7ecdc"/>',cursor:'<path fill="currentColor" stroke="none" d="M4 1 21 14l-8 1 4 8-4 2-4-9-5 6Z"/>',chair:'<path d="M6 2h12v13H6Zm3 0v10m3-10v10m3-10v10M4 15h16M6 15 4 22m14-7 2 7M6 8h12" stroke-width="2"/>',hammer:'<path fill="currentColor" stroke="none" d="m15 2 7 6-3 3-3-1-4 4 1 2-8 8-4-4 8-8 2 1 4-4-3-3Z"/>',layers:'<path fill="currentColor" stroke="none" d="m12 1 11 6-11 6L1 7Zm-10 10 10 5 10-5 1 3-11 6L1 14Zm0 7 10 5 10-5v4l-10 5-10-5Z"/>',move:'<path fill="currentColor" stroke="none" d="m12 0 5 6h-3v5h5V8l6 5-6 5v-3h-5v5h3l-5 6-5-6h3v-5H5v3l-6-5 6-5v3h5V6H7Z"/>',rotate:'<path d="M20 10a8 8 0 1 0 0 8M20 2v8h-8" stroke-width="3.3"/>',duplicate:'<path d="M4 3h14v15H4ZM8 7h14v15H8Z" stroke-width="2.3"/>',trash:'<path d="M4 6h16M9 3h6M6 6l1 16h10l1-16M10 9v10m4-10v10"/>',magnet:'<path d="m3 9 4 4 4-4-4-4Zm10-4 4-4 4 4-4 4M5 12c-4 6 2 14 9 8l6-6-4-4-6 6c-2 2-4 0-2-2" fill="currentColor" stroke="none"/>',grid:'<path d="M3 3h18v18H3ZM9 1v22M15 1v22M1 9h22M1 15h22" stroke-width="2"/>',cube:'<path d="m12 2 10 6v10l-10 6-10-6V8Zm0 0v10m10-4-10 4L2 8m10 4v12" stroke-width="2"/>',link:'<path d="m10 15-1 1a4 4 0 0 1-6-6l4-4a4 4 0 0 1 6 0m1 3 1-1a4 4 0 0 1 6 6l-4 4a4 4 0 0 1-6 0m-4-4 7-7"/>',zoom:'<circle cx="10" cy="10" r="7"/><path d="m15 15 6 6M10 6v8m-4-4h8"/>',tools:'<path d="m3 3 18 18M6 1l-5 5m16 12-3 3M2 21 19 4m-3-3 6 6m-5-5 4 1 1 4" stroke-width="3"/>'};
  function icon(name) {
    const glyph = window.BistroEditorV2Glyphs?.[name];
    if (glyph) return `<svg class="icon reference-glyph" style="--reference-width:${glyph.viewBox.split(' ')[2]}px;--reference-height:${glyph.viewBox.split(' ')[3]}px" viewBox="${glyph.viewBox}" aria-hidden="true"><path d="${glyph.path}" fill-rule="evenodd"/></svg>`;
    return `<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">${paths[name] || paths.cube}</svg>`;
  }
  let serial=0;
  function atlas([x,y,w,h],{stretch=false,holes=[]}={}){const id=`art-${++serial}`;const path=`M${x},${y}h${w}v${h}h-${w}Z`+holes.map(([a,b,c,d])=>`M${x+a},${y+b}h${c}v${d}h-${c}Z`).join('');return `<svg class="atlas" viewBox="${x} ${y} ${w} ${h}" preserveAspectRatio="${stretch?'none':'xMidYMid meet'}" aria-hidden="true"><defs><clipPath id="${id}"><path d="${path}" clip-rule="evenodd"/></clipPath></defs><image href="assets/reference-full.png" width="1672" height="941" clip-path="url(#${id})"/></svg>`;}
  function frame(el){
    if (el.id === 'bottom-bar') {
      // Separate engraved ribbons and joins; no source text or source buttons.
      const shell = document.createElement('span');
      shell.className = 'frame-piece frame-footer-shell';
      const regions = [
        [8,830,1656,5], [8,921,1656,12], [8,830,25,103], [1636,830,28,103],
        [30,835,247,12], [359,835,259,12], [630,835,218,12],
        [939,835,248,12], [1201,835,173,12], [1505,835,131,12],
        [600,835,39,98], [1170,835,40,98]
      ];
      const id = `footer-frame-${++serial}`;
      shell.innerHTML = `<svg class="atlas" viewBox="8 830 1656 103" aria-hidden="true"><defs><clipPath id="${id}">${regions.map(([x,y,w,h])=>`<rect x="${x}" y="${y}" width="${w}" height="${h}"/>`).join('')}</clipPath></defs><image href="assets/reference-full.png" width="1672" height="941" clip-path="url(#${id})"/></svg>`;
      el.append(shell);
      return;
    }
    const boxes={catalog:[9,92,557,730,14],topbar:[8,8,1656,74,14],venue:[681,9,314,71,14]};const [x,y,w,h,b]=boxes[el.id]||[1344,92,318,730,14];const pieces={tl:[x,y,b,b],tr:[x+w-b,y,b,b],bl:[x,y+h-b,b,b],br:[x+w-b,y+h-b,b,b],top:[x+b,y,w-b*2,10],bottom:el.id==='catalog'?[23,812,113,10]:[x+b,y+h-10,w-b*2,10],left:[x,y+b,10,h-b*2],right:[x+w-10,y+b,10,h-b*2]};Object.entries(pieces).forEach(([side,box])=>{const p=document.createElement('span');p.className=`frame-piece frame-${side}`;p.innerHTML=atlas(box,{stretch:true});el.append(p);});}
  const products=[
    {id:'bistro',name:'Mesa bistró redonda',title:'Mesa bistró clásica',price:245,scope:'interior',pic:[1430,150,150,151],featured:true,description:'Mesa clásica de bistró con tablero de madera y base de hierro fundido. Un diseño versátil y atemporal que encaja en cualquier ambiente.',short:'Un clásico atemporal que encaja en cualquier ambiente.',width:80,depth:80,height:75,material:'Madera de roble'},
    {id:'walnut',name:'Mesa nogal cuadrada',related:'Mesa cuadrada',price:290,scope:'interior',pic:[270,628,69,53],relatedPic:[166,477,87,61],description:'Mesa cuadrada de nogal con cuatro patas de madera. Un acabado cálido para el comedor.',width:80,depth:80,height:75,material:'Nogal oscuro'},
    {id:'marble',name:'Mesa mármol claro',price:315,scope:'interior',pic:[368,628,68,55],relatedPic:[266,477,87,61],description:'Mesa de mármol claro con pedestal de hierro. Tablero redondo para dos comensales.',width:80,depth:80,height:75,material:'Mármol claro'},
    {id:'pedestal',name:'Mesa pedestal nogal',price:275,scope:'interior',pic:[467,628,70,57],relatedPic:[366,477,87,61],description:'Mesa redonda de nogal con pedestal oscuro y base compacta.',width:80,depth:80,height:75,material:'Nogal oscuro'},
    {id:'industrial',name:'Mesa industrial',price:420,scope:'interior',pic:[166,744,78,51],description:'Mesa de estilo industrial con estructura metálica y tablero gris.',width:90,depth:90,height:75,material:'Nogal oscuro'},
    {id:'rustic',name:'Mesa roble rústica',related:'Mesa extensible',price:420,scope:'interior',pic:[269,743,76,53],relatedPic:[466,477,87,61],description:'Mesa de roble con acabado rústico y cuatro patas robustas.',width:100,depth:80,height:75,material:'Madera de roble'},
    {id:'four',name:'Mesa cuatro plazas',price:380,scope:'interior',pic:[368,744,68,53],description:'Mesa de madera para cuatro plazas, con proporciones sencillas y acabado cálido.',width:100,depth:80,height:75,material:'Madera de roble'},
    {id:'terrace',name:'Mesa terraza exterior',price:310,scope:'exterior',pic:[474,744,65,54],description:'Mesa para terraza con tablero de cristal y estructura metálica.',width:80,depth:80,height:75,material:'Mármol claro'}
  ];
  function productArt(product, related = false) {
    const box = related && product.relatedPic ? product.relatedPic : product.pic;
    const holes = related && product.relatedPic ? [[box[2]-24, 0, 24, 30]] :
      product.id === 'bistro' ? [] : [[box[2]-15, 0, 15, 15]];
    return atlas(box, {holes});
  }
  const categories=[['all','Todos',[30,253,28,31]],['tables','Mesas',[29,299,29,32]],['seating','Asientos',[30,343,28,32]],['bar','Barra',[29,390,30,28]],['kitchen','Cocina',[28,435,30,31]],['storage','Almacenamiento',[29,483,30,31]],['lighting','Iluminación',[26,526,34,34]],['decoration','Decoración',[29,575,29,31]],['exterior','Exterior',[27,621,32,32]]];
  const key='bistro-editor-v2-reference-review';let stored={};try{stored=JSON.parse(localStorage.getItem(key)||'{}');}catch{}
  const valid=id=>products.some(p=>p.id===id);
  const state={tab:'all',category:'tables',query:'',scope:'all',min:null,max:null,order:'relevance',selected:'bistro',hero:0,related:0,properties:{},favorites:new Set((stored.favorites||[]).filter(valid)),recents:(stored.recents||[]).filter(valid)};
  function persist(){try{localStorage.setItem(key,JSON.stringify({favorites:[...state.favorites],recents:state.recents}));}catch{}}
  const normal=s=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase(),money=p=>`${p} €`;
  function visible(){const items=products.filter(p=>(state.category==='all'||state.category==='tables'||state.category==='exterior'&&p.scope==='exterior')&&(state.scope==='all'||p.scope===state.scope)&&(state.min===null||p.price>=state.min)&&(state.max===null||p.price<=state.max)&&normal(`${p.name} ${p.description}`).includes(normal(state.query))&&(state.tab==='all'||state.tab==='favorites'&&state.favorites.has(p.id)||state.tab==='recent'&&state.recents.includes(p.id)));const sorter={'price-asc':(a,b)=>a.price-b.price,'price-desc':(a,b)=>b.price-a.price,name:(a,b)=>a.name.localeCompare(b.name,'es')};if(sorter[state.order])items.sort(sorter[state.order]);else if(state.tab==='recent')items.sort((a,b)=>state.recents.indexOf(a.id)-state.recents.indexOf(b.id));return items;}
  function favorite(p){state.favorites.has(p.id)?state.favorites.delete(p.id):state.favorites.add(p.id);persist();render();}
  function select(p){state.selected=p.id;state.recents=[p.id,...state.recents.filter(id=>id!==p.id)].slice(0,8);persist();render();}
  function card(p,related=false){const c=document.createElement('div');c.className=`item-card${related?' related-card':p.id===state.selected?' selected':''}`;c.dataset.product=p.id;const action=document.createElement('button');action.type='button';action.className='card-select';action.setAttribute('aria-label',`Ver detalles: ${p.name}, ${money(p.price)}`);action.innerHTML=`<span class="product-art">${productArt(p, related)}</span><span class="name">${related?(p.related||p.name):p.name}</span><span class="price">${money(p.price)}</span>`;action.onclick=()=>select(p);c.append(action);const heart=document.createElement('button');heart.className=`heart${state.favorites.has(p.id)?' saved':''}`;heart.setAttribute('aria-label',`${state.favorites.has(p.id)?'Quitar de':'Guardar en'} favoritos: ${p.name}`);heart.setAttribute('aria-pressed',state.favorites.has(p.id));heart.innerHTML=icon('heart');heart.onclick=()=>favorite(p);c.append(heart);if(p.featured&&!related){const star=document.createElement('span');star.className='featured-star';star.innerHTML=icon('star-fill');c.append(star);}return c;}
  function renderInspector(){const p=products.find(p=>p.id===state.selected);$('#inspector-title').textContent=p.title||p.name;$('#inspector-description').textContent=p.description;$('#large-product').innerHTML=p.id==='bistro'?atlas([1370,150,270,156],{stretch:true,holes:[[233,124,36,32]]}):productArt(p);$('#zoom-product').innerHTML=productArt(p);const saved=state.favorites.has(p.id);$('#inspector-favorite').classList.toggle('saved',saved);$('#inspector-favorite').setAttribute('aria-pressed',saved);['width','depth','height'].forEach(k=>$('#'+k).innerHTML=`<option>${p[k]} cm</option>`);$('#wood-material').value=state.properties[p.id]?.wood||p.material;$('#base-material').value=state.properties[p.id]?.base||'Hierro negro';['x','y','z'].forEach(axis=>$('#rotation-'+axis).value=state.properties[p.id]?.[axis]||'0°');$('#variant-cards').replaceChildren(...['bistro','marble','pedestal','walnut'].map(id=>products.find(v=>v.id===id)).map(v=>{const b=document.createElement('button');b.className=v.id===p.id?'selected':'';b.setAttribute('aria-label',`Variante: ${v.name}`);b.setAttribute('aria-pressed',v.id===p.id);b.innerHTML=`<span class="product-art">${productArt(v)}</span>`;b.onclick=()=>select(v);return b;}));}
  function renderHero(items){$('#featured').hidden=!items.length;$('#related').hidden=!items.length;if(!items.length)return;const heroItems=items.slice(0,5);state.hero=((state.hero%heroItems.length)+heroItems.length)%heroItems.length;const p=heroItems[state.hero];$('#featured-name').textContent=p.name;$('#featured-description').textContent=p.short||p.description;$('#featured-price').textContent=money(p.price);$('#featured-favorite').classList.toggle('saved',state.favorites.has(p.id));$('#featured-favorite').setAttribute('aria-pressed',state.favorites.has(p.id));$('#featured-details').onclick=()=>select(p);$('#featured-favorite').onclick=()=>favorite(p);$('.hero-art').innerHTML = `<span class="hero-underlay">${atlas([336,247,20,149],{stretch:true})}</span>` +
      (p.id === 'bistro' ? atlas([155,247,399,193],{stretch:true,holes:[[0,0,195,193],[365,0,34,45]]}) : `<span class="hero-product">${productArt(p)}</span>`) + `<span class="hero-engraving">${atlas([155,247,399,193],{stretch:true,holes:[[8,8,383,177]]})}</span>`;$('#featured-dots').replaceChildren(...items.slice(0,5).map((_,i)=>{const b=document.createElement('button');b.className=i===state.hero?'active':'';b.setAttribute('aria-label',`Destacado ${i+1}`);b.onclick=()=>{state.hero=i;render();};return b;}));let related=items.filter(v=>v.id!==p.id);if(p.id==='bistro'){const order=['walnut','marble','pedestal','rustic','industrial','four','terrace'];related.sort((a,b)=>order.indexOf(a.id)-order.indexOf(b.id));}state.related=related.length?state.related%related.length:0;$('#related-cards').replaceChildren(...[...related.slice(state.related),...related.slice(0,state.related)].slice(0,4).map(v=>card(v,true)));}
  function render(){const items=visible();$('#items-grid').replaceChildren(...items.map(p=>card(p)));$('#empty').hidden=items.length>0;$('#all-heading').textContent=`Todos los artículos — ${categories.find(c=>c[0]===state.category)[1]}`;all('[data-tab]').forEach(b=>b.setAttribute('aria-selected',b.dataset.tab===state.tab));all('[data-category]').forEach(b=>b.classList.toggle('selected',b.dataset.category===state.category));$('#filter-toggle').classList.toggle('selected',state.scope!=='all'||state.min!==null||state.max!==null);renderHero(items);renderInspector();}
  function filters(open){$('#filters').hidden=!open;$('#filter-toggle').setAttribute('aria-expanded',open);if(open)$('#price-min').focus();}
  let timer;function toast(message){$('#toast').textContent=message;$('#toast').hidden=false;clearTimeout(timer);timer=setTimeout(()=>$('#toast').hidden=true,4200);}
  function command(name,value){window.dispatchEvent(new CustomEvent('editor-v2-command',{detail:{name,value,productId:state.selected,source:'panels-r4'}}));}
  function fit(){const s=Math.min(innerWidth/1672,innerHeight/941);document.documentElement.style.setProperty('--ui-scale',s);document.documentElement.style.setProperty('--ui-left',`${Math.round((innerWidth-1672*s)*devicePixelRatio/2)/devicePixelRatio}px`);document.documentElement.style.setProperty('--ui-top',`${Math.round((innerHeight-941*s)*devicePixelRatio/2)/devicePixelRatio}px`);}
  all('[data-icon]').forEach(el=>el.innerHTML=icon(el.dataset.icon));all('.ornate').forEach(frame);
  all('.property-section h2').forEach(h => {
    const rule = document.createElement('span');
    rule.className = 'heading-rule';
    rule.innerHTML = '<svg viewBox="0 0 180 24" preserveAspectRatio="none" aria-hidden="true"><path d="M0 12H180" stroke="#d9b681" stroke-width=".65"/><path d="m31 12 3-5 3 5-3 5Z M34 3v3m0 12v3" stroke="#bd8835" stroke-width=".8" fill="#f4d79a"/><path d="m174 12 3-4 3 4-3 4Z" fill="#c49343"/></svg>';
    h.append(rule);
  });
  $('#brand').innerHTML = atlas([19,3,202,75],{stretch:true});
  $('#fleur').innerHTML = icon('fleur');
  $('#restaurant-reference').innerHTML = atlas([568,83,776,747],{stretch:true});
  const preview = document.createElement('span');
  preview.className = 'preview-engraving';
  const previewRegions = [[1360,142,286,8],[1360,306,286,8],[1360,150,8,156],[1638,150,8,156]];
  const previewId = `preview-frame-${++serial}`;
  preview.innerHTML = `<svg class="atlas" viewBox="1360 142 286 172" aria-hidden="true"><defs><clipPath id="${previewId}">${previewRegions.map(([x,y,w,h])=>`<rect x="${x}" y="${y}" width="${w}" height="${h}"/>`).join('')}</clipPath></defs><image href="assets/reference-full.png" width="1672" height="941" clip-path="url(#${previewId})"/></svg>`;
  $('#inspector-preview').prepend(preview);
  $('.wood-swatch').innerHTML = atlas([1378,622,36,30],{stretch:true});
  $('.metal-swatch').innerHTML = atlas([1378,662,36,30],{stretch:true});
  $('#categories').replaceChildren(...categories.map(([id,label,art],index) => {
    const button = document.createElement('button');
    button.dataset.category = id;
    button.innerHTML = `<span class="category-art" style="left:${art[0]-22}px;top:${art[1]-247-index*46}px;width:${art[2]}px;height:${art[3]}px">${icon('category-'+id)}</span><span>${label}</span>`;
    button.onclick = () => { state.category=id; state.hero=0; state.related=0; render(); };
    return button;
  }));
  $('#search').oninput=e=>{state.query=e.target.value;state.hero=0;state.related=0;render();};all('[data-tab]').forEach(b=>b.onclick=()=>{state.tab=b.dataset.tab;state.hero=0;state.related=0;render();});$('#order').onchange=e=>{state.order=e.target.value;state.hero=0;render();};
  $('#filter-toggle').onclick=()=>filters($('#filters').hidden);$('#filter-close').onclick=()=>filters(false);$('#filters').onsubmit=e=>{e.preventDefault();const min=$('#price-min').value,max=$('#price-max').value;if(min!==''&&max!==''&&Number(min)>Number(max)){$('#filter-error').textContent='El mínimo no puede superar el máximo.';return;}state.min=min===''?null:Number(min);state.max=max===''?null:Number(max);state.scope=$('#filters input[name=scope]:checked').value;state.hero=0;state.related=0;$('#filter-error').textContent='';filters(false);render();};
  $('#filter-reset').onclick=()=>{$('#filters').reset();state.min=state.max=null;state.scope='all';state.hero=0;state.related=0;$('#filter-error').textContent='';render();};
  $('.hero-previous').onclick=()=>{state.hero--;render();};$('.hero-next').onclick=()=>{state.hero++;render();};$('.related-previous').onclick=()=>{const n=visible().length-1;state.related=n>0?(state.related+n-1)%n:0;render();};$('.related-next').onclick=()=>{state.related++;render();};$('#all-related').onclick=()=>$('#items-grid').scrollTo({top:0,behavior:'smooth'});
  $('#inspector-favorite').onclick=()=>favorite(products.find(p=>p.id===state.selected));$('#zoom').onclick=()=>$('#zoom-dialog').showModal();$('#zoom-close').onclick=()=>$('#zoom-dialog').close();$('#collapse-catalog').onclick=()=>{$('#catalog').hidden=true;$('#catalog-rail').hidden=false;};$('#catalog-rail').onclick=()=>{$('#catalog').hidden=false;$('#catalog-rail').hidden=true;};
  $('#day').onchange=e=>{const w=$('#restaurant-reference');w.classList.toggle('night',e.target.value==='Noche');w.classList.toggle('evening',e.target.value==='Atardecer');};$('#grid-toggle').onclick=e=>{const active=e.currentTarget.getAttribute('aria-pressed')!=='true';e.currentTarget.setAttribute('aria-pressed',active);e.currentTarget.classList.toggle('selected',active);$('#editor').dataset.grid=active;};
  function toggle(button) {
    const active = button.getAttribute('aria-pressed') !== 'true';
    button.setAttribute('aria-pressed', String(active));
    button.classList.toggle('selected', active);
    return active;
  }
  $('#snapping').onclick = e => command('snapping', toggle(e.currentTarget));
  $('#lock-ratio').onclick = e => {
    const active = e.currentTarget.getAttribute('aria-pressed') !== 'true';
    e.currentTarget.setAttribute('aria-pressed', String(active));
    command('lock-ratio', active);
  };
  all('[data-tool]').forEach(button => button.onclick = () => {
    all('[data-tool]').forEach(item => {item.classList.toggle('selected', item===button);item.setAttribute('aria-pressed', String(item===button));});
    command(button.dataset.tool);
  });
  all('[data-action]').forEach(button => button.onclick = () => command(button.dataset.action));
  ['undo','redo','photo','rename','views'].forEach(id => $('#'+id).onclick = () => command(id));
  all('#rotation select,#materials select').forEach(field => field.onchange = () => {
    const values = state.properties[state.selected] ||= {};
    const key = field.id === 'wood-material' ? 'wood' : field.id === 'base-material' ? 'base' : field.id.replace('rotation-','');
    values[key] = field.value;
    command(field.id, field.value);
  });
  $('#settings').onclick = $('#editor-badge').onclick = () => $('#review-dialog').showModal();
  $('#review-close').onclick = () => $('#review-dialog').close();
  async function fullscreen(){
    $('#review-dialog').close();$('#fullscreen-entry').hidden=true;
    try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}
    catch{toast('Pulsa F11 para usar la pantalla completa del navegador.');}
    fit();
  }
  $('#review-fullscreen').onclick=$('#fullscreen-enter').onclick=fullscreen;
  $('#fullscreen-skip').onclick=()=>$('#fullscreen-entry').hidden=true;
  if(new URLSearchParams(location.search).has('fullscreen')&&!document.fullscreenElement)$('#fullscreen-entry').hidden=false;
  document.addEventListener('fullscreenchange',()=>{fit();$('#editor').dataset.fullscreen=String(!!document.fullscreenElement);$('#review-fullscreen').textContent=document.fullscreenElement?'Volver a ventana':'Abrir en pantalla completa';});
  document.addEventListener('keydown',e=>{if(e.key==='Escape')filters(false);});document.addEventListener('pointerdown',e=>{if(!$('#filters').hidden&&!$('#filters').contains(e.target)&&!$('#filter-toggle').contains(e.target))filters(false);});window.addEventListener('resize',fit);fit();render();
})();
