const fs = require("fs");
const {JSDOM} = require("jsdom");
const path = 'hOps.web/Views/Home/Index.cshtml';
const text = fs.readFileSync(path, 'utf8');
const match = text.match(/<script>([\s\S]*?)<\/script>/);
if(!match){throw new Error('script not found');}
const script = match[1];
const html = `<!DOCTYPE html><body>
<div class="home-layout-controls">
  <button type="button" data-layout-action="start">Customize Layout</button>
  <div data-layout-editor class="btn-group d-none"></div>
  <div data-layout-hint class="d-none"></div>
</div>
<div class="home-dashboard">
  <div id="homeWidgetGrid" data-initial-layout='[]' data-size-options='{"third":4}' data-height-options='{}' data-persona="default" data-row-unit="30" data-row-gap="12" data-save-url="/Home/SaveWidgetLayout">
    <div class="home-widget" data-widget-id="test" data-widget-size="third" data-grid-span="4" data-grid-height="300" data-default-height="300">
      <div class="home-widget__editor"></div>
      <div class="home-widget__content"></div>
      <div class="home-widget__height-slider"><input data-height-slider type="range" value="300" /></div>
    </div>
  </div>
</div>
<form id="homeLayoutAntiforgery"><input name="__RequestVerificationToken" value="token"/></form>
</body>`;
const dom = new JSDOM(html, {url:'https://example.com/'});
const {window} = dom;
global.window = window;
global.document = window.document;
window.fetch = async () => ({ok:true});
window.alert = console.log;
try {
  eval(script);
  console.log('script executed');
  const button = document.querySelector('[data-layout-action="start"]');
  button.click();
  console.log('dashboard classes:', document.querySelector('.home-dashboard').className);
  console.log('editor group classes:', document.querySelector('[data-layout-editor]').className);
} catch (err) {
  console.error('script error', err);
}
