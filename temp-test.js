const fs = require('fs');
const {JSDOM} = require('jsdom');
const html = `<!DOCTYPE html><body>
<div id="logsLayout">
  <div id="spreadsheetContainer">
    <div id="spreadsheetPlaceholder">No log selected</div>
    <div id="logsGrid" class="d-none"></div>
  </div>
  <div id="logList"></div>
  <div id="logEmptyState" class="d-none"></div>
  <div id="logTitle"></div>
  <div id="logSubtitle"></div>
  <button id="toggleSidebarBtn" data-open-label="Show Logs" data-close-label="Hide Logs"><span data-sidebar-toggle-label></span></button>
  <button id="closeLogsSidebarBtn"></button>
  <div id="logsSidebarOverlay"></div>
  <div id="logTabs"></div>
  <div id="logTabsBar"></div>
  <div id="logTabsEmptyState"></div>
  <div id="logTabsScroll"></div>
  <button id="addTabBtn"></button>
  <form id="createLogForm"></form>
  <input id="logNameInput" />
  <button id="importLogBtn"></button>
  <input id="importLogInput" type="file" />
  <button id="duplicateLogBtn"></button>
  <button id="deleteLogBtn"></button>
  <button id="renameLogBtn"></button>
  <button id="viewAuditLogBtn"></button>
  <button id="addRowBtn"></button>
  <button id="insertRowBtn"></button>
  <button id="deleteRowBtn"></button>
  <button id="addColumnBtn"></button>
  <button id="deleteColumnBtn"></button>
  <button id="clearLogBtn"></button>
  <button id="exportExcelBtn"></button>
  <button id="undoBtn"></button>
  <button id="zoomInBtn"></button>
  <button id="zoomOutBtn"></button>
</div>
</body>`;
const dom = new JSDOM(html, {url:'https://example.com/'});
const {window} = dom;
global.window = window;
global.document = window.document;
window.bootstrap = {};
const storage = new Map();
window.localStorage = {
  getItem(key){return storage.has(key)?storage.get(key):null;},
  setItem(key, val){storage.set(key,String(val));},
  removeItem(key){storage.delete(key);}
};
window.requestAnimationFrame = (cb) => setTimeout(cb,0);
const script = fs.readFileSync('hOps.web/wwwroot/js/logs.js','utf8');
window.eval(script);
window.document.dispatchEvent(new window.Event('DOMContentLoaded'));
console.log('placeholder classes', document.getElementById('spreadsheetPlaceholder').className);
console.log('grid classes', document.getElementById('logsGrid').className);
console.log('log list children', document.getElementById('logList').children.length);
console.log('logsGrid html', document.getElementById('logsGrid').innerHTML.slice(0,200));
