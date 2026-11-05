(() => {
  'use strict';
  const buttons=[...document.querySelectorAll('.nav-button')];
  const panelTitle=document.querySelector('#sectionTitle');
  const description=document.querySelector('#sectionDescription');
  const status=document.querySelector('#sectionStatus');
  const clickCounter=document.querySelector('#clickCounter');
  const animationState=document.querySelector('#animationState');
  const frame=document.querySelector('#demoScreen');
  const descriptions=Object.freeze({
    'Actividad':'Feed de acontecimientos, alertas y seguimiento de lo que sucede en el restaurante.',
    'Personal':'Gestión de plantilla, candidatos, turnos y estado del equipo.',
    'Carta':'Edición de las cartas, sus reglas y los platos disponibles.',
    'Inventario':'Existencias, alertas de stock, almacén y planificación de compras.',
    'Proveedores':'Ofertas, proveedores, pedidos y aprovisionamiento del restaurante.',
    'Reservas':'Agenda, ocupación prevista y gestión de las reservas de mesas.',
    'Economía':'Ingresos, gastos, caja, márgenes e informes del negocio.',
    'Marketing':'Campañas, posicionamiento y captación de clientes.',
    'Reputación':'Satisfacción, valoraciones y evolución de la reputación.',
    'Opciones':'Configuración del juego, pantalla, sonido, interfaz y partida.'
  });
  const timers=new WeakMap();
  let clicks=0;
  function animate(button){
    const old=timers.get(button);
    if(old!==undefined)clearTimeout(old);
    button.classList.remove('is-animating');
    void button.offsetWidth; // Fuerza reinicio de keyframes para clics repetidos y rápidos.
    button.classList.add('is-animating');
    const duration=document.body.classList.contains('motion-slow')?840:280;
    timers.set(button,setTimeout(()=>button.classList.remove('is-animating'),duration+40));
  }
  function activate(button){
    for(const other of buttons){
      if(other===button)other.setAttribute('aria-current','page');
      else other.removeAttribute('aria-current');
    }
    const selected=button.dataset.section;
    panelTitle.textContent=selected;
    description.textContent=descriptions[selected]||'';
    status.textContent='Seleccionado: '+selected;
    clicks++;
    clickCounter.textContent=clicks+' '+(clicks===1?'clic de prueba':'clics de prueba');
    animationState.textContent='V30 mejorada · '+selected;
    animate(button);
  }
  for(const button of buttons)button.addEventListener('click',()=>activate(button));
  // Control que permite repetir la misma animación sin cambiar de sección.
  document.querySelector('#replayActive').addEventListener('click',()=>{
    const current=buttons.find(x=>x.getAttribute('aria-current')==='page')||buttons[0];
    activate(current);
  });
  // Teclado: flechas, Inicio y Fin para recorrer los diez accesos; Enter/Espacio activan el botón.
  document.querySelector('.navigation').addEventListener('keydown',event=>{
    if(!['ArrowRight','ArrowLeft','Home','End'].includes(event.key))return;
    const i=buttons.indexOf(document.activeElement);
    if(i<0)return;
    event.preventDefault();
    const next=event.key==='Home'?0:event.key==='End'?buttons.length-1:(i+(event.key==='ArrowRight'?1:buttons.length-1))%buttons.length;
    buttons[next].focus();
  });
  for(const control of document.querySelectorAll('[data-size]'))control.addEventListener('click',()=>{
    for(const other of document.querySelectorAll('[data-size]'))other.setAttribute('aria-pressed',String(other===control));
    frame.classList.remove('mode-1280','mode-1920');
    if(control.dataset.size!=='fluid')frame.classList.add('mode-'+control.dataset.size);
  });
  for(const control of document.querySelectorAll('.preview-settings [data-motion]'))control.addEventListener('click',()=>{
    for(const other of document.querySelectorAll('.preview-settings [data-motion]'))other.setAttribute('aria-pressed',String(other===control));
    document.body.classList.remove('motion-normal','motion-slow','motion-off');
    document.body.classList.add('motion-'+control.dataset.motion);
    animationState.textContent=control.dataset.motion==='slow'?'V30 a cámara lenta':control.dataset.motion==='off'?'Movimiento desactivado':'V30 mejorada';
  });
  // DEMOSTRADOR: selección visual y movimiento; no ejecuta navegación real en Unity.
})();