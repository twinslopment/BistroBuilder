import asyncio,json
from pathlib import Path
from playwright.async_api import async_playwright
root=Path('/mnt/data/proveedores_bloque04');html=(root/'BistroBuilder_Proveedores_Bloque04_Preview_V1.html').read_text(encoding='utf8')
async def run():
 records=[]
 async with async_playwright() as p:
  browser=await p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
  for w,h in [(1024,768),(1280,720),(1366,768),(1600,900),(1920,1080),(2560,1440),(3440,1440)]:
   page=await browser.new_page(viewport={'width':w,'height':h});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
   await page.set_content(html)
   await page.locator('button[data-tab="catalog"]').click()
   await page.locator('#searchInput').fill('merluza')
   await page.locator('.table-row').first.click()
   await page.wait_for_timeout(190)
   layout=await page.evaluate('''() => {
    const r=e=>e.getBoundingClientRect();const row=document.querySelector('.table-row'),side=document.querySelector('.sidebar'),top=document.querySelector('.bistro-topbar'),bottom=document.querySelector('.hud.bottom'),drawer=document.querySelector('.drawer'),title=document.querySelector('.sidebar-title-text');
    return {pageOverflow:document.documentElement.scrollWidth>innerWidth+1||document.documentElement.scrollHeight>innerHeight+1,
     topBottomOverlap:r(top).bottom>r(side).top+1||r(bottom).top<r(side).bottom-1,
     titleClipped:r(title).left<r(side).left-1||r(title).right>r(side).right+1,
     drawerInViewport:r(drawer).left>=0&&r(drawer).right<=innerWidth&&r(drawer).bottom<=r(bottom).top+1,
     selectedRowVisible:r(row).left>=r(side).right&&r(row).right<=innerWidth+1,
     layoutCols:getComputedStyle(row).gridTemplateColumns,
     navRows:document.querySelectorAll('.side-link').length,
     activeTitle:document.querySelector('#screenTitle').textContent,
     actualCells:[...row.children].map(el=>({left:r(el).left,right:r(el).right})),
     rowBounds:{left:r(row).left,right:r(row).right}}}''')
   layout['resolution']=f'{w}x{h}';layout['errors']=errors
   assert not layout['pageOverflow'] and not layout['topBottomOverlap'] and not layout['titleClipped'] and layout['drawerInViewport'] and layout['selectedRowVisible'],layout
   await page.set_viewport_size({'width':max(1280,w-80),'height':max(720,h-50)})
   layout['resizePreservesTabAndSearch']= 'Catálogo' in await page.locator('#screenTitle').inner_text() and await page.locator('#searchInput').input_value()=='merluza'
   assert layout['resizePreservesTabAndSearch']
   if w==1920:
    await page.set_viewport_size({'width':1920,'height':1080})
    await page.screenshot(path=str(root/'Proveedores_Detalle_Catalogo_1920x1080.png'))
   if w==1280:
    await page.set_viewport_size({'width':1280,'height':720})
    await page.screenshot(path=str(root/'Proveedores_Detalle_Catalogo_1280x720.png'))
   records.append(layout);await page.close()
  await browser.close()
 (root/'INFORME_RESPONSIVE.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8')
 print(json.dumps([{k:v for k,v in rec.items() if k in ['resolution','pageOverflow','topBottomOverlap','titleClipped','drawerInViewport','selectedRowVisible','resizePreservesTabAndSearch','errors']} for rec in records],ensure_ascii=False,indent=2))
asyncio.run(run())