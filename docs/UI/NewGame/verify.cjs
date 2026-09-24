const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const {pathToFileURL}=require('url');
const path=require('path');
(async()=>{
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.route(/^https?:/,r=>r.abort());
await page.goto(pathToFileURL(path.resolve(process.argv[2]||__dirname+'/index.html')).href,{waitUntil:'domcontentloaded'});
const frame=page.frameLocator('iframe');
await frame.locator('.eq-option').first().waitFor();
for(const width of [1920,1280,1024,736,320]){
await page.setViewportSize({width,height:1100});await page.waitForTimeout(180);
const sizes=await frame.locator('.eq-option').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect();return [r.width,r.height]}));
if(sizes.some(s=>Math.abs(s[0]-sizes[0][0])>.1||s[1]!==sizes[0][1]))throw Error('Unequal cards');
if(await frame.locator('#bb-equal-screen').evaluate(el=>document.documentElement.scrollWidth>innerWidth))throw Error('Overflow');
console.log(width,JSON.stringify(sizes));

}
await frame.locator('.eq-option').last().click();
if(await frame.locator('.eq-option[aria-pressed="true"]').count()!==1)throw Error('Selection');
const input=frame.locator('input');await input.fill('');await frame.locator('.eq-create').click();
if(await input.evaluate(el=>el.checkValidity()))throw Error('Name validation');
await input.fill('La Esquina');await frame.locator('.eq-create').click();
if(!(await frame.locator('.eq-feedback').textContent()).includes('La Esquina · Últimos retoques'))throw Error('Create feedback');
await frame.locator('.eq-back').focus();await page.keyboard.press('Enter');
if(!(await frame.locator('.eq-feedback').textContent()).includes('volver'))throw Error('Keyboard back');
await page.emulateMedia({reducedMotion:'reduce'});
if(await frame.locator('.eq-dome').evaluate(el=>getComputedStyle(el).animationName)!=='none')throw Error('Reduced motion');
if(errors.length)throw Error(errors.join('\n'));
console.log('PASS: equal cards, offline assets, selection, input validation, button feedback, keyboard, reduced motion');
}finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
