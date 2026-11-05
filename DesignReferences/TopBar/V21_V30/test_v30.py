from playwright.sync_api import sync_playwright
from pathlib import Path
p=Path('/mnt/data/BistroBuilder_V21_V30_Mejorada_Preview.html')
out=Path('/mnt/data/BistroBuilder_V21_V30_Mejorada')
with sync_playwright() as pw:
    browser=pw.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox','--disable-gpu'])
    for w,h in ((1280,720),(1920,1080)):
        page=browser.new_page(viewport={'width':w,'height':h},device_scale_factor=1)
        errors=[]
        page.on('pageerror',lambda e:errors.append(str(e)))
        page.set_content(p.read_text(encoding="utf-8"),wait_until="load")
        page.wait_for_timeout(150)
        btns=page.locator('.nav-button')
        assert btns.count()==10,btns.count()
        assert all(btns.nth(i).is_visible() for i in range(10))
        assert page.locator('.brand-logo').evaluate('(i)=>i.naturalWidth')==1752
        assert page.locator('.brand-logo').evaluate('(i)=>i.naturalHeight')==897
        assert page.evaluate('getComputedStyle(document.body).getPropertyValue("--v30-duration").trim()')=='280ms'
        bar=page.locator('.bistro-topbar').bounding_box()
        bb=[btns.nth(i).bounding_box() for i in range(10)]
        assert all(x['x']>=bar['x']-1 and x['x']+x['width']<=bar['x']+bar['width']+1 for x in bb), (w,bar,bb)
        assert all(bb[i]['x']+bb[i]['width']<=bb[i+1]['x']+.5 for i in range(9))
        for i in range(10):
            btn=btns.nth(i)
            name=btn.get_attribute('data-section')
            btn.click()
            assert btn.get_attribute('aria-current')=='page',name
            assert btn.evaluate('(e)=>e.classList.contains("is-animating")')
            assert btn.evaluate('(e)=>getComputedStyle(e.querySelector(".icon-stage")).animationName')=='bb-v30-press'
            assert btn.evaluate('(e)=>getComputedStyle(e.querySelector(".nav-icon")).animationName')=='bb-v30-flash'
            assert page.locator('#sectionTitle').inner_text()==name
        before=int(page.locator('#clickCounter').inner_text().split()[0]);btns.nth(4).click();btns.nth(4).click();assert int(page.locator('#clickCounter').inner_text().split()[0])==before+2
        page.locator('[data-motion="slow"]').click()
        assert page.evaluate('getComputedStyle(document.body).getPropertyValue("--v30-duration").trim()')=='840ms'
        page.locator('#replayActive').click()
        assert page.locator('.nav-button[aria-current="page"]').evaluate('(e)=>getComputedStyle(e.querySelector(".icon-stage")).animationDuration')=='0.84s'
        page.locator('[data-motion="normal"]').click()
        btns.nth(0).click();page.wait_for_timeout(380)
        assert not btns.nth(0).evaluate('(e)=>e.classList.contains("is-animating")')
        page.locator('[data-motion="off"]').click()
        btns.nth(2).click()
        assert btns.nth(2).evaluate('(e)=>getComputedStyle(e.querySelector(".icon-stage")).animationName')=='none'
        page.locator('[data-motion="normal"]').click()
        btns.nth(0).focus();btns.nth(0).press('ArrowRight')
        assert btns.nth(1).evaluate('(e)=>document.activeElement===e')
        btns.nth(1).press('Enter');assert btns.nth(1).get_attribute('aria-current')=='page'
        page.locator('[data-size="1280"]').click(); assert page.locator('#demoScreen').evaluate('(e)=>e.classList.contains("mode-1280")')
        page.locator('[data-size="1920"]').click(); assert page.locator('#demoScreen').evaluate('(e)=>e.classList.contains("mode-1920")')
        page.locator('[data-size="fluid"]').click()
        btns.nth(0).click()
        page.wait_for_timeout(350)
        shot=out/f'V21_V30_Mejorada_{w}.png'
        page.screenshot(path=str(shot),full_page=True)
        # top bar crop isolated for quick viewing
        bar=page.locator('.bistro-topbar')
        bar.screenshot(path=str(out/f'Barra_V21_V30_{w}.png'))
        assert not errors,(w,errors)
        print(w,h,'PASS - 10 nav buttons, click/replay/repeated, keyboard, CSS keyframes, durations, size, logo, no errors',shot)
        page.close()
    rpage=browser.new_page(viewport={'width':1280,'height':720},reduced_motion='reduce')
    rpage.set_content(p.read_text(encoding="utf-8"),wait_until="load");rpage.locator('.nav-button').nth(1).click()
    assert rpage.locator('.nav-button').nth(1).evaluate('(e)=>getComputedStyle(e.querySelector(".icon-stage")).animationName')=='none'
    assert rpage.locator('.nav-button').nth(1).evaluate('(e)=>getComputedStyle(e.querySelector(".nav-icon")).animationName')=='none'
    print('Reduced motion PASS')
    browser.close()
