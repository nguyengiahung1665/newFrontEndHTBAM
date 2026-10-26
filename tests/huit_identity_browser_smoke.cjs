// Read-only browser acceptance: editor changes are discarded; no catalog record is created.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { chromium } = require(process.env.HTBAM_PLAYWRIGHT_MODULE || 'playwright');
const base = process.argv[2] || 'http://127.0.0.1:5173';
if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(base)) throw Error('Loopback only');
const out = path.resolve(__dirname, '../.runtime-fix-backup-huit-ui/browser');
fs.mkdirSync(out, { recursive: true });
let checks = 0, screenshots = 0;
const check = (value, message) => { assert.ok(value, message); checks++; };
async function api(url, token, body) {
  const response = await fetch(base + url, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  assert.equal(response.status, 200, `${url}: ${response.status}`); return response.json();
}
(async () => {
  const roles = ['admin','dean','head1','head2','head3','lecturer1','lecturer2','lecturer3','lecturer4','lecturer5','lecturer6','editor','viewer'];
  const tokens = {};
  const browser = await chromium.launch({ headless: true });
  const errors = []; let page;
  async function valueIs(locator, expected) { for(let attempt=0;attempt<100;attempt++){if(await locator.inputValue()===expected)return true;await page.waitForTimeout(20);}return false; }
  async function shot(name) { await page.screenshot({ path: path.join(out, `${name}.png`) }); screenshots++; }
  async function context(role, viewport) {
    if(!tokens[role]) tokens[role] = (await api('/api/auth/login', null, { userName: `demo.${role}`, password: process.env.SEED_DEFAULT_PASSWORD || 'HTBAM-Local-Demo!2026#' })).accessToken;
    const ctx = await browser.newContext({ viewport, isMobile: viewport.width < 600, hasTouch: viewport.width < 600, timezoneId: 'Asia/Ho_Chi_Minh' });
    await ctx.addInitScript(token => sessionStorage.setItem('htbam_token', token), tokens[role]);
    return ctx;
  }
  try {
    for (const [device, viewport] of [['desktop', { width:1440,height:1000 }], ['mobile',{width:390,height:844}]]) {
      for (const role of roles) {
        const ctx = await context(role, viewport); page = await ctx.newPage(); page.on('pageerror', error => errors.push(error.message));
        await page.goto(base + '/management'); await page.getByRole('tab').first().waitFor();
        await page.locator('app-notification-center .unread-count').waitFor();
        check(await page.locator('.content > .notice.warning').count() === 0, `${device}/${role}: no repeated DEMO banner`);
        if (device === 'mobile') await page.getByRole('button',{name:'Mở menu',exact:true}).click();
        const logo = page.locator('aside .huit-logo');
        const dimensions = await logo.evaluate(image => ({ width:image.clientWidth,height:image.clientHeight,naturalWidth:image.naturalWidth,naturalHeight:image.naturalHeight,alt:image.alt }));
        check(dimensions.naturalWidth > 0 && dimensions.naturalWidth === dimensions.naturalHeight && dimensions.width === dimensions.height, `${device}/${role}: original square logo loads without distortion`);
        check(dimensions.height === 52 && dimensions.alt === 'Logo Trường Đại học Công Thương Thành phố Hồ Chí Minh', `${device}/${role}: identity size and alternative text`);
        if(device === 'mobile') {
          await page.waitForFunction(()=>document.querySelector('aside').getBoundingClientRect().left>=0);
          if(role==='admin')await shot('mobile-sidebar');
          await page.getByRole('button',{name:'Đóng menu',exact:true}).click();
          await page.waitForFunction(()=>document.querySelector('aside').getBoundingClientRect().right<=1);
        }
        if(device === 'desktop' && role === 'admin') {
          await page.getByRole('button',{name:'Thu gọn thanh bên',exact:true}).click();await page.waitForFunction(()=>document.querySelector('.shell.collapsed aside .huit-logo')?.clientHeight===40); check(await logo.evaluate(image=>image.clientHeight) === 40,'collapsed logo is 40px'); await shot('desktop-collapsed');
          await page.getByRole('button',{name:'Mở rộng thanh bên',exact:true}).click();
        }
        await page.getByRole('button',{name:'Thông báo',exact:true}).click();
        const panel = page.getByRole('region',{name:'Trung tâm thông báo'}); await panel.waitFor();
        check(await panel.getByRole('heading',{name:'Dữ liệu mẫu phục vụ kiểm thử'}).count() === 1,`${device}/${role}: single stable DEMO notice`);
        const bounds=await panel.boundingBox();check(bounds.x>=0 && bounds.x+bounds.width<=viewport.width+1 && bounds.height<viewport.height*.8,`${device}/${role}: panel fits viewport`);
        if(role === 'dean')await shot(device+'-notifications');
        await page.keyboard.press('Escape');await panel.waitFor({state:'hidden'});check(await panel.count()===0,`${device}/${role}: Escape closes notifications`);
        if(role === 'dean') {
          await page.getByRole('tab',{name:'Môn học',exact:true}).click();
          await page.getByRole('columnheader',{name:'Năm học bắt đầu',exact:true}).waitFor();
          const headers=await page.locator('[role=tabpanel] thead th').allTextContents();
          check(!headers.includes('Mã') && headers.includes('Năm học bắt đầu') && headers.includes('Học kỳ bắt đầu'),device+': subject table columns');
          await page.getByText('Chưa thiết lập',{exact:true}).first().waitFor();check(await page.getByText('Chưa thiết lập',{exact:true}).count()>0,device+': legacy period remains unset');
          await shot(device+'-courses');
          await page.getByRole('button',{name:'+ Thêm Môn học',exact:true}).click();
          const dialog=page.getByRole('dialog',{name:'Thêm Môn học',exact:true});await dialog.waitFor();
          check(await dialog.locator('input[readonly],input[name*=code i]').count()===0,device+': editor has no code controls');
          check(!(await dialog.innerText()).includes('Mã dự kiến'),device+': no preview code');
          await dialog.locator('[name=courseName]').fill('Kiểm thử giao diện, không lưu');
          const year=dialog.locator('[name=startAcademicYear]'),semester=dialog.locator('[name=startSemester]');
          await year.fill('2026-2028');await year.press('Tab');await dialog.getByText('Năm học phải có dạng YYYY-YYYY và hai năm phải liên tiếp.',{exact:true}).waitFor();check(true,device+': invalid year explained');
          await year.fill(' 2026 – 2027 ');await semester.fill('1');await semester.press('Tab');check(await valueIs(year,'2026-2027') && await valueIs(semester,'HK1'),device+': typed start period normalizes');
          await semester.fill('HK3');await semester.press('Tab');await dialog.getByText('Học kỳ chỉ nhận HK1 hoặc HK2.',{exact:true}).waitFor();check(true,device+': invalid semester explained');
          await semester.fill('HK2');await semester.press('Tab');await shot(device+'-course-editor');
          await dialog.getByRole('button',{name:'Hủy',exact:true}).click();
        }
        if(role === 'admin') {
          await page.goto(base+'/academic-configuration');await page.getByRole('button',{name:'+ Cấu hình học kỳ',exact:true}).click();
          const dialog=page.getByRole('dialog',{name:'Cấu hình học kỳ và giai đoạn',exact:true});await dialog.waitFor();
          const start=dialog.locator('app-date-input[name=startsOn]'),end=dialog.locator('app-date-input[name=endsOn]');
          await start.locator('input').fill('1/2/2026');await start.locator('input').press('Tab');check(await valueIs(start.locator('input'),'01/02/2026'),device+': short date normalizes');
          await end.locator('input').fill('31/02/2026');await end.locator('input').press('Tab');await end.locator('.date-error').waitFor();check(await end.locator('.date-error').count()===1,device+': invalid calendar date rejected');
          await end.locator('input').fill('31/01/2026');await end.locator('input').press('Tab');await end.getByText('Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.',{exact:true}).waitFor();check((await end.innerText()).includes('Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.'),device+': reversed range explained inline');
          await end.locator('input').fill('25/10/2026');await end.locator('input').press('Tab');
          await start.locator('.date-toggle').click();const calendar=page.locator('.date-calendar');await calendar.waitFor();
          const calendarBounds=await calendar.boundingBox(),footerBounds=await dialog.locator('.modal-footer').boundingBox();
          check(calendarBounds.x>=0&&calendarBounds.x+calendarBounds.width<=viewport.width+1,device+': date calendar fits width');
          check(calendarBounds.y+calendarBounds.height<=footerBounds.y+1,device+': calendar avoids modal footer');
          await shot(device+'-calendar');await page.keyboard.press('Escape');await calendar.waitFor({state:'hidden'});check(await calendar.count()===0&&await dialog.count()===1,device+': Escape closes calendar while preserving editor');
          await start.locator('.date-toggle').focus();await page.keyboard.press('Space');await calendar.waitFor();await calendar.getByRole('button',{name:'02/02/2026',exact:true}).click();check(await valueIs(start.locator('input'),'02/02/2026'),device+': keyboard opens calendar and selection updates text');
          await dialog.getByRole('button',{name:'Hủy',exact:true}).click();
        }
        if(role === 'dean') {
          await page.getByRole('button',{name:'Thông báo',exact:true}).click();await page.getByRole('button',{name:'Đánh dấu tất cả đã đọc',exact:true}).click();
          await page.reload();await page.locator('app-notification-center').waitFor();await page.waitForLoadState('networkidle');
          check(await page.locator('app-notification-center .unread-count').count()===0,device+': read state survives reload');
          await page.getByRole('button',{name:'Thông báo',exact:true}).click();await panel.waitFor();check(await page.getByRole('heading',{name:'Dữ liệu mẫu phục vụ kiểm thử'}).count()===1,device+': reload does not duplicate notice');
          await page.mouse.click(viewport.width-20,viewport.height-20);await panel.waitFor({state:'hidden'});check(await panel.count()===0,device+': outside click closes notice');
          await page.route('**/api/system/capabilities',async route=>{const response=await route.fetch();const data=await response.json();await route.fulfill({json:{...data,developmentDataWarning:null}});});await page.reload();await page.waitForLoadState('networkidle');await page.getByRole('button',{name:'Thông báo',exact:true}).click();
          await page.getByText('Không có thông báo mới.',{exact:true}).waitFor();check(await page.getByText('Không có thông báo mới.',{exact:true}).count()===1,device+': no DEMO notice for real-data capability');
        }
        check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${device}/${role}: no page overflow`);
        await ctx.close();
      }
    }
    check(errors.length===0,'no browser runtime errors: '+errors.join('; '));
    fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({checks,screenshots,roles,devices:['1440x1000','390x844'],errors},null,2));
    console.log('HUIT_IDENTITY_BROWSER_OK '+JSON.stringify({checks,screenshots,roles:roles.length}));
  } catch(error) { if(page&&!page.isClosed())await shot('failure');throw error; }
  finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
