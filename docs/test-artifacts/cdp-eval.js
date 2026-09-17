const http = require('http');
const fs = require('fs');

const jsFile = process.argv[2];
const js = fs.readFileSync(jsFile, 'utf8');

function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => {
        try { resolve(JSON.parse(d)); } catch (e) { reject(e); }
      });
    }).on('error', reject);
  });
}

(async () => {
  const pages = await getJson('http://127.0.0.1:9222/json');
  const page = pages.find(p => p.type === 'page' && (p.description || '').includes('"visible":true'))
    || pages.find(p => p.type === 'page');
  if (!page) throw new Error('no page');
  console.log('page', page.id, page.title, page.url);

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();

  ws.onmessage = (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.method === 'Page.javascriptDialogOpening') {
      const id = nextId++;
      ws.send(JSON.stringify({ id, method: 'Page.handleJavaScriptDialog', params: { accept: true } }));
      console.log('auto-accepted dialog:', msg.params?.message);
      return;
    }
    if (msg.id && pending.has(msg.id)) {
      pending.get(msg.id)(msg);
      pending.delete(msg.id);
    }
  };

  await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });

  function send(method, params) {
    const id = nextId++;
    return new Promise((resolve, reject) => {
      const t = setTimeout(() => reject(new Error('timeout ' + method)), 30000);
      pending.set(id, (msg) => { clearTimeout(t); resolve(msg); });
      ws.send(JSON.stringify({ id, method, params }));
    });
  }

  try { await send('Runtime.enable', {}); } catch (e) { console.log('Runtime.enable soft-fail', e.message); }
  // Auto-confirm dialogs without Page domain (some WebViews ignore Page.enable)
  await send('Runtime.evaluate', {
    expression: 'window.confirm = () => true; window.alert = () => {}; true',
    returnByValue: true
  });

  const evalRes = await send('Runtime.evaluate', {
    expression: js,
    awaitPromise: true,
    returnByValue: true
  });
  const out = 'C:/Users/mdulche/code/Gaia-Life/docs/test-artifacts/cdp-last.json';
  fs.writeFileSync(out, JSON.stringify(evalRes, null, 2));
  if (evalRes.result?.exceptionDetails) {
    console.error('EXC', evalRes.result.exceptionDetails.exception?.description || evalRes.result.exceptionDetails.text);
    process.exitCode = 2;
  } else {
    console.log(JSON.stringify(evalRes.result?.result?.value ?? evalRes.result?.result, null, 2));
  }
  ws.close();
})().catch((e) => { console.error(e); process.exit(1); });
