import asyncio, json
from pathlib import Path
from playwright.async_api import async_playwright
ROOT=Path('/mnt/data/inventario_responsive_v7')
HTML=Path('/mnt/data/BistroBuilder_Inventario_Responsive_V7.html').read_text(encoding='utf-8')
SIZES=[(1024,768),(1280,720),(1366,768),(1440,900),(1600,900),(1904,910),(1920,1080),(2560,1440),(3440,1440)]
REPORT={'sizes':[],'functional':{},'js_errors':[]}

def check(v, msg):
 if not v: raise AssertionError(msg)

async def measure(pg):
 return await pg.evaluate('''() => {
  const box=s=>{const e=document.querySelector(s),b=e.getBoundingClientRect(),cs=getComputedStyle(e);return {x:Math.round(b.x),y:Math.round(b.y),right:Math.round(b.right),bottom:Math.round(b.bottom),width:Math.round(b.width),height:Math.round(b.height),scrollWidth:e.scrollWidth,clientWidth:e.clientWidth,overflow:cs.overflow,display:cs.display,fontSize:cs.fontSize}};
  return {scrollWidth:document.documentElement.scrollWidth,viewportWidth:innerWidth,scrollHeight:document.documentElement.scrollHeight,viewportHeight:innerHeight,top:box('.hud.top'),bottom:box('.hud.bottom'),app:box('.app'),sidebar:box('.sidebar'),title:box('.sidebar-title-text'),menu:box('.sidebar-links'),main:box('.main'),header:box('.main-header'),tools:box('.toolrow'),pills:box('.pills'),search:box('.searchbar'),table:box('.table-frame'),tableRows:box('.table-rows'),drawer:box('.drawer')};
}''')

async def main():
 async with async_playwright() as p:
  br=await p.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox'])
  for w,h in SIZES:
   page=await br.new_page(viewport={'width':w,'height':h},device_scale_factor=1)
   errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
   await page.set_content(HTML,wait_until='load')
   await page.wait_for_timeout(175)
   initial=await measure(page)
   check(initial['scrollWidth']<=w+1,f'Horizontal overflow {w}x{h}: {initial["scrollWidth"]}')
   check(initial['scrollHeight']<=h+1,f'Vertical overflow {w}x{h}: {initial["scrollHeight"]}')
   check(abs(initial['app']['y']-initial['top']['bottom'])<=1,f'App top vs HUD {w}x{h}')
   check(abs(initial['app']['bottom']-initial['bottom']['y'])<=1,f'App bottom vs HUD {w}x{h}')
   check(initial['title']['x']>=initial['sidebar']['x']-1 and initial['title']['right'] <= initial['sidebar']['right']+1, f'Title clipped {w}x{h}: {initial["title"]} {initial["sidebar"]}')
   check(initial['table']['height']>=135,f'Table too short {w}x{h} {initial["table"]}')
   check(initial['tools']['right']<=initial['main']['right']+1,f'Tools overflow {w}x{h}')
   check(await page.locator('.table-row').count()==17, f'Stock items {w}x{h}')
   shots=[]
   if (w,h) in [(1280,720),(1366,768),(1600,900),(1920,1080),(2560,1440),(3440,1440),(1024,768)]:
    shot=ROOT/f'Inventario_Existencias_{w}x{h}.png';await page.screenshot(path=str(shot));shots.append(str(shot))
   bytab={}
   for tab in ['alerts','moves','receipts','stock']:
    await page.locator(f'[data-tab="{tab}"]').click()
    await page.wait_for_timeout(35)
    n=await page.locator('.table-row').count()
    bytab[tab]=n
    check(n>0,f'Empty tab {tab} {w}x{h}')
    t=await measure(page)
    check(t['scrollWidth']<=w+1 and t['scrollHeight']<=h+1,f'overflow tab {tab} {w}x{h}')
    if (w,h) in [(1280,720),(1920,1080)]:
     shot=ROOT/f'Inventario_{tab}_{w}x{h}.png';await page.screenshot(path=str(shot));shots.append(str(shot))
   # Detail overlay and dialog are tested at each breakpoint
   await page.locator('[data-item="merluza"]').click()
   await page.wait_for_timeout(200)
   info=await measure(page)
   check((await page.locator('#drawer').get_attribute('class')).find('open')!=-1,f'No drawer {w}x{h}')
   check(info['drawer']['x']>=info['main']['x']-1 and info['drawer']['right']<=w+1, f'Drawer offscreen {w}x{h}')
   check(info['drawer']['bottom']<=info['app']['bottom']+1, f'Drawer bottom clipped {w}x{h}')
   await page.locator('#drawerClose').click()
   await page.locator('#primaryAction').click()
   check(await page.locator('#adjustDialog').evaluate('e=>e.open'),f'Dialog did not open {w}x{h}')
   dialog=await page.locator('#adjustDialog').bounding_box()
   check(dialog['y']>=0 and dialog['y']+dialog['height']<=h+1, f'Dialog offscreen {w}x{h}')
   await page.locator('#cancelAdjust').click()
   check(not errors,f'JS errors {w}x{h}: {errors}')
   REPORT['sizes'].append({'viewport':f'{w}x{h}','rows_per_tab':bytab,'horiz_page_overflow':False,'vert_page_overflow':False,'title_clipped':False,'drawer_inside':True,'dialog_inside':True,'table_height':initial['table']['height'],'screenshots':shots})
   print(f'PASS {w}x{h} · table {initial["table"]["height"]}px · {bytab} · JS 0')
   await page.close()
  # Deep functional regression + reduced motion
  page=await br.new_page(viewport={'width':1280,'height':720},reduced_motion='reduce')
  errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  await page.set_content(HTML,wait_until='load')
  await page.locator('#searchInput').fill('merluza')
  check(await page.locator('.table-row').count()==1,'Search not filtering')
  await page.locator('#searchInput').fill('')
  await page.locator('.pill[data-filter="urgent"]').click()
  check(await page.locator('.table-row').count()==3,'Urgent filter not working')
  await page.locator('.pill[data-filter="all"]').click()
  await page.locator('[data-item="merluza"]').click()
  await page.locator('#drawerActions [data-action="adjust"]').click()
  await page.locator('#btnMinus').click()
  await page.locator('#adjustAmount').fill('99')
  await page.locator('#adjustForm button[type="submit"]').click()
  check('No puedes reducir' in await page.locator('#formError').inner_text(),'Overdraw allowed')
  await page.locator('#btnPlus').click()
  await page.locator('#adjustAmount').fill('5')
  await page.locator('#adjustForm button[type="submit"]').click()
  state=await page.evaluate('window.BistroInventoryDemo.getState()')
  check(next(i for i in state['items'] if i['id']=='merluza')['stock']==9,'Stock adjustment did not update')
  check(state['movements'][0]['kind']=='ajuste','Movement missing')
  await page.locator('[data-tab="moves"]').click()
  check(await page.locator('.table-row').count()==10,'History not updated')
  await page.locator('[data-tab="receipts"]').click()
  await page.locator('.table-row[data-receipt]').first.click()
  check('open' in (await page.locator('#drawer').get_attribute('class')),'Receipt detail not open')
  await page.locator('#drawerActions [data-action="receipt-moves"]').click()
  check('Movimientos' in await page.locator('#screenTitle').inner_text(),'Receipt movement link broken')
  await page.locator('#resetDemo').click()
  state=await page.evaluate('window.BistroInventoryDemo.getState()')
  check(next(i for i in state['items'] if i['id']=='merluza')['stock']==4,'Reset failed')
  check(await page.locator('.table-row').count()==17,'Reset view failed')
  check(not errors,f'JS deep errors {errors}')
  REPORT['functional']={'search':True,'filters':True,'open_detail':True,'prevent_overdraw':True,'adjust_and_record':True,'receipt_to_moves':True,'reset':True,'reduced_motion':True,'js_errors':errors}
  await page.close();await br.close()
  (ROOT/'test_report.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2),encoding='utf-8')
  print('ALL TESTS PASS')
asyncio.run(main())