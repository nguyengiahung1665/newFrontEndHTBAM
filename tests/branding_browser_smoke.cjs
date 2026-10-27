// Branding/layout acceptance against the production bundle. All API traffic is mocked.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.HTBAM_PLAYWRIGHT_MODULE || 'playwright');
const ts = require('../apps/web/node_modules/typescript');
const base = process.argv[2] || 'http://127.0.0.1:5174';
if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(base)) throw Error('Loopback only');
const out = path.resolve(__dirname, '../.runtime-fix-backup-branding-audit/browser');
fs.mkdirSync(out, { recursive: true });
const tooltip = 'HTBAM - Khoa Công nghệ Thông tin HUIT';
let checks = 0, screenshots = 0;
function check(value, message) { assert.ok(value, message); checks++; }

(async () => {
  const browser = await chromium.launch({ headless: true });
  const errors = []; let page;
  const shot = async name => { await page.screenshot({ path: path.join(out, `${name}.png`) }); screenshots++; };
  async function logo(locator, size, label) {
    await locator.waitFor();
    await locator.evaluate(img => img.decode());
    const result = await locator.evaluate(img => ({ src:new URL(img.src).pathname, width:img.clientWidth, height:img.clientHeight, naturalWidth:img.naturalWidth, naturalHeight:img.naturalHeight, fit:getComputedStyle(img).objectFit }));
    check(result.src === '/assets/huit-logo.png',label+': shared asset');
    check(result.width === size && result.height === size && result.naturalWidth === result.naturalHeight,label+': square, expected dimensions');
    check(result.fit === 'contain',label+': no distortion');
  }
  try {
    // Read the actual served production files, including every embedded ICO frame.
    const iconContext = await browser.newContext();
    const iconPage = await iconContext.newPage();
    await iconPage.goto(base + '/favicon.ico?v=huit-20261026');
    const iconResponse = await iconContext.request.get(base + '/favicon.ico?v=huit-20261026');
    check(iconResponse.status() === 200 && /image\/(x-icon|vnd.microsoft.icon)/.test(iconResponse.headers()['content-type']), 'ICO URL: HTTP 200 and icon MIME type');
    const ico = await iconResponse.body();
    check(ico.equals(fs.readFileSync(path.resolve(__dirname, '../apps/web/public/favicon.ico'))), 'production ICO matches generated asset');
    check(ico.readUInt16LE(0) === 0 && ico.readUInt16LE(2) === 1 && ico.readUInt16LE(4) === 3, 'ICO contains exactly three icon frames');
    const imageFrames = [];
    for (const [index, size] of [16, 32, 48].entries()) {
      const entry = 6 + index * 16;
      check(ico[entry] === size && ico[entry + 1] === size && ico.readUInt16LE(entry + 6) === 32, `ICO ${size}: square, 32-bit alpha`);
      const length = ico.readUInt32LE(entry + 8), offset = ico.readUInt32LE(entry + 12);
      check(offset >= 54 && offset + length <= ico.length, `ICO ${size}: valid frame bounds`);
      imageFrames.push({label:`ICO ${size}`,size,data:ico.subarray(offset,offset+length).toString('base64')});
    }
    for (const [name,size] of [['favicon-16x16.png',16],['favicon-32x32.png',32],['apple-touch-icon.png',180]]) {
      const response = await iconContext.request.get(base + '/assets/branding/' + name + '?v=huit-20261026');
      check(response.status() === 200 && response.headers()['content-type'].startsWith('image/png'), name + ': HTTP 200 and PNG MIME type');
      const data = await response.body();
      check(data.equals(fs.readFileSync(path.resolve(__dirname, '../apps/web/public/assets/branding',name))), name + ': production matches generated asset');
      imageFrames.push({label:name,size,data:data.toString('base64')});
    }
    for (const frame of imageFrames) {
      const decoded = await iconPage.evaluate(async frame => {
        const img = new Image(); img.src = 'data:image/png;base64,' + frame.data; await img.decode();
        const canvas = document.createElement('canvas'); canvas.width=img.naturalWidth;canvas.height=img.naturalHeight;
        const context=canvas.getContext('2d');context.drawImage(img,0,0);
        const alpha=(x,y)=>context.getImageData(x,y,1,1).data[3];
        return {width:img.naturalWidth,height:img.naturalHeight,corners:[alpha(0,0),alpha(canvas.width-1,0),alpha(0,canvas.height-1),alpha(canvas.width-1,canvas.height-1)],center:alpha(Math.floor(canvas.width/2),Math.floor(canvas.height/2))};
      },frame);
      check(decoded.width===frame.size && decoded.height===frame.size,frame.label+': browser decodes expected dimensions');
      check(decoded.corners.every(alpha=>alpha===0) && decoded.center>0,frame.label+': transparent corners, visible emblem');
    }
    await iconContext.close();
    for (const [device,viewport] of [['desktop',{width:1440,height:1000}],['mobile',{width:390,height:844}],['narrow',{width:320,height:740}]]) {
      const context = await browser.newContext({viewport});
      page = await context.newPage();page.on('pageerror',error=>errors.push(error.message));
      await page.route('**/api/**',route=>route.fulfill({json:[]}));
      await page.goto(base+'/login');
      await page.getByRole('heading',{name:'Đăng nhập hệ thống'}).waitFor();
      check(await page.title()===tooltip,device+': browser title');
      const iconLinks=await page.locator('link[rel=icon],link[rel=apple-touch-icon]').evaluateAll(links=>links.map(link=>({rel:link.rel,type:link.type,sizes:link.sizes.value,href:link.getAttribute('href')})));
      check(JSON.stringify(iconLinks)===JSON.stringify([
        {rel:'icon',type:'image/x-icon',sizes:'',href:'favicon.ico?v=huit-20261026'},
        {rel:'icon',type:'image/png',sizes:'32x32',href:'assets/branding/favicon-32x32.png?v=huit-20261026'},
        {rel:'icon',type:'image/png',sizes:'16x16',href:'assets/branding/favicon-16x16.png?v=huit-20261026'},
        {rel:'apple-touch-icon',type:'',sizes:'180x180',href:'assets/branding/apple-touch-icon.png?v=huit-20261026'}
      ]),device+': favicon declarations, sizes and cache version in a fresh browser context');
      await page.reload({waitUntil:'networkidle'});
      check(await page.title()===tooltip,device+': browser title after reload');
      if(device==='desktop')await logo(page.locator('.login-visual .huit-logo'),52,'desktop login');
      else {
        const brand=page.locator('.login-mobile-brand');
        await logo(brand.locator('img'),26,device+' compact brand');
        check(await brand.getAttribute('title')===tooltip,device+': brand tooltip');
        check(await brand.locator('img').getAttribute('alt')==='Logo HUIT',device+': compact alternative text');
        const layout=await brand.evaluate(el=>({gap:getComputedStyle(el).gap,align:getComputedStyle(el).alignItems,height:el.getBoundingClientRect().height}));
        check(layout.gap==='8px'&&layout.align==='center'&&layout.height===52,device+': gap/alignment/preserved strip height');
        check(await brand.getAttribute('href')==='/',device+': home navigation');
        if(device==='narrow')check(!await brand.locator('.compact-brand__copy').isVisible(),'narrow: hides the complete name, no partial clipping');
      }
      check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),device+': no horizontal overflow');
      await shot(device+'-login');
      await context.close();
    }
    for (const [role,viewport] of [['ADMIN',{width:1440,height:1000}],['LECTURER',{width:390,height:844}]]) {
      const context=await browser.newContext({viewport});
      const token='test.'+Buffer.from(JSON.stringify({exp:Math.floor(Date.now()/1000)+3600})).toString('base64url')+'.test';
      await context.addInitScript(token=>sessionStorage.setItem('htbam_token',token),token);
      page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
      let releaseProfile;const profileReady=new Promise(resolve=>releaseProfile=resolve);
      await page.route('**/api/**',async route=>{
        if(route.request().url().endsWith('/api/auth/me')){await profileReady;return route.fulfill({json:{id:1,userName:'branding-test',fullName:'Kiểm thử giao diện',roles:[role],permissions:['CATALOG_VIEW','DEPARTMENT_DATA_MANAGE','CLASS_SECTION_VIEW']}});}
        if(route.request().url().endsWith('/api/system/capabilities'))return route.fulfill({json:{backend:true,database:true,developmentDataWarning:null}});
        return route.fulfill({json:[]});
      });
      await page.goto(base+'/management',{waitUntil:'domcontentloaded'});
      await logo(page.locator('.app-loading .compact-brand img'),26,role+' loading');
      await shot(role.toLowerCase()+'-loading');releaseProfile();
      await page.getByRole('heading',{name:'Danh mục dữ liệu',exact:true}).waitFor();
      if(role==='LECTURER'){
        await page.getByRole('button',{name:'Mở menu',exact:true}).click();
        await page.waitForFunction(()=>document.querySelector('aside').getBoundingClientRect().left>=0);
      }
      const sidebar=page.locator('aside .huit-logo');await logo(sidebar,52,role+' sidebar');
      check(await sidebar.getAttribute('title')===tooltip,role+': sidebar tooltip');
      check((await page.locator('.brand-copy').innerText()).includes('Hệ thống theo dõi và phân tích hành vi sinh viên'),role+': preserves existing sidebar description');
      await shot(role.toLowerCase()+'-sidebar');
      if(role==='ADMIN'){
        await page.getByRole('button',{name:'Thu gọn thanh bên'}).click();
        await page.waitForFunction(()=>document.querySelector('aside .huit-logo').clientHeight===40);
        await page.waitForFunction(()=>document.querySelector('aside').getBoundingClientRect().width<=69);
        await logo(sidebar,40,'collapsed sidebar');check(!await page.locator('.brand-copy').isVisible(),'collapsed: hides the whole brand copy');await shot('collapsed-sidebar');
      }
      await context.close();
    }
    // Pause application scripts to inspect the real pre-bootstrap splash markup.
    const splashContext=await browser.newContext({viewport:{width:390,height:844}});
    page=await splashContext.newPage();await page.route('**/*.js',route=>route.abort());
    await page.goto(base+'/login',{waitUntil:'domcontentloaded'});
    await logo(page.locator('app-root .compact-brand img'),26,'pre-bootstrap splash');await shot('splash');await splashContext.close();

    // Execute the actual main.ts rejection handler with a rejected bootstrap promise.
    const fallbackContext=await browser.newContext({viewport:{width:390,height:844}});page=await fallbackContext.newPage();
    await page.goto(base+'/login');await page.getByRole('heading',{name:'Đăng nhập hệ thống'}).waitFor();
    const main=ts.transpileModule(fs.readFileSync(path.resolve(__dirname,'../apps/web/src/main.ts'),'utf8'),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS}}).outputText;
    await page.evaluate(code=>new Function('require','exports',code)(name=>name==='@angular/platform-browser'?{bootstrapApplication:()=>Promise.reject(new Error('Branding fallback test'))}:{},{}),main);
    await page.getByRole('heading',{name:'Không thể khởi động giao diện HTBAM'}).waitFor();
    await logo(page.locator('main .compact-brand img'),26,'bootstrap failure');check(await page.locator('main .compact-brand').getAttribute('href')==='/', 'failure: home link');await shot('bootstrap-error');await fallbackContext.close();
    check(errors.length===0,'no runtime page errors: '+errors.join('; '));
    fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({checks,screenshots,errors,apiMode:'mocked',sizes:['1440x1000','390x844','320x740']},null,2));
    console.log('BRANDING_BROWSER_OK '+JSON.stringify({checks,screenshots}));
  } catch(error) { if(page&&!page.isClosed())await shot('failure');throw error; }
  finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
