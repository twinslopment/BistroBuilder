import asyncio,json
from pathlib import Path
from playwright.async_api import async_playwright
HTML=Path('/mnt/data/BistroBuilder_Inventario_Responsive_V7.html').read_text()
async def main():
 async with async_playwright() as p:
  br=await p.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox'])
  pg=await br.new_page(viewport={'width':1920,'height':1080},reduced_motion='reduce')
  errors=[];pg.on('pageerror',lambda e:errors.append(str(e)))
  await pg.set_content(HTML,wait_until='load')
  await pg.locator('[data-tab="alerts"]').click()
  await pg.locator('.pill[data-filter="expiry"]').click()
  selected_before=await pg.locator('.pill.selected').inner_text()
  for w,h in [(1280,720),(3440,1440),(1024,768),(1920,1080)]:
   await pg.set_viewport_size({'width':w,'height':h})
   await pg.wait_for_timeout(155)
   title=await pg.locator('.sidebar-title-text').bounding_box()
   side=await pg.locator('.sidebar').bounding_box()
   visible=await pg.locator('.table-row').count()
   assert title['x']>=side['x']-1 and title['x']+title['width'] <= side['x']+side['width']+1,(w,h,title,side)
   assert visible==4,(w,h,visible) # cuatro alertas de caducidad
   assert await pg.locator('.pill.selected').inner_text()==selected_before
   assert await pg.locator('.side-link.active').get_attribute('data-tab')=='alerts'
   print('resize PASS',w,h,'alertas',visible)
  assert errors==[],errors
  await pg.close();await br.close()
asyncio.run(main())