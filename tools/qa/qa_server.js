// A tiny remote control for the Kalyeah web build, for interaction audits with Playwright.
//
//   node tools/qa/qa_server.js            (needs `npm i -g playwright` and a Chromium; see CHROME below)
//   curl 'localhost:9300/open?w=390&h=844&url=http://localhost:8765/index.html?server=ws://localhost:8080'
//   curl 'localhost:9300/tap?x=360&y=1100'        (x, y in the game's 720 x 1280 design pixels)
//   curl 'localhost:9300/shot?name=home'          (saves <OUT>/home.png)
//
// The game draws everything on one canvas, so there is no DOM to query: taps are given in design pixels and mapped to the
// page (the project stretches the 720 px width to the window, `canvas_items` + `expand`). Several named pages can be open
// at once (?p=a, ?p=b) to test rooms with two players. Touches go through the DevTools protocol so the game sees real
// InputEventScreenTouch / ScreenDrag, the way a phone sends them.
const http = require('http');
const path = require('path');
const fs = require('fs');
const { chromium } = require(process.env.PLAYWRIGHT || '/opt/node22/lib/node_modules/playwright');

const PORT = Number(process.env.QA_PORT || 9300);
const OUT = process.env.QA_OUT || '/tmp/qa';
const CHROME = process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
fs.mkdirSync(OUT, { recursive: true });

let browser;
const pages = {}; // name -> { ctx, page, cdp, w, h, logs }

async function open(q) {
  const name = q.p || 'a';
  if (pages[name]) await pages[name].ctx.close().catch(() => {});
  const w = Number(q.w || 390), h = Number(q.h || 844), touch = q.touch !== '0';
  const ctx = await browser.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: Number(q.dpr || 2), hasTouch: touch, isMobile: touch });
  const page = await ctx.newPage();
  const logs = [];
  page.on('console', m => { if (m.type() !== 'log' || /error|warn/i.test(m.text())) logs.push(m.type() + ': ' + m.text().slice(0, 300)); });
  page.on('pageerror', e => logs.push('pageerror: ' + e.message.slice(0, 300)));
  const cdp = await ctx.newCDPSession(page);
  pages[name] = { ctx, page, cdp, w, h, logs };
  await page.goto(q.url);
  return { opened: name, w, h };
}

const P = q => pages[q.p || 'a'];
// design pixels (720 wide) -> css pixels
const css = (p, v) => v * p.w / 720;

async function touches(p, type, pts) {
  await p.cdp.send('Input.dispatchTouchEvent', { type, touchPoints: pts.map(t => ({ x: css(p, t.x), y: css(p, t.y), id: t.id || 0 })) });
}

const routes = {
  async open(q) { return open(q); },
  async tap(q) { const p = P(q); await touches(p, 'touchStart', [{ x: +q.x, y: +q.y }]); await p.page.waitForTimeout(Number(q.hold || 60)); await touches(p, 'touchEnd', []); return 'ok'; },
  async click(q) { const p = P(q); await p.page.mouse.click(css(p, +q.x), css(p, +q.y)); return 'ok'; },
  async move(q) { const p = P(q); await p.page.mouse.move(css(p, +q.x), css(p, +q.y)); return 'ok'; },
  // ?path=x,y;x,y;...&hold=ms between points   one finger, press at the first point, drag through the rest, lift
  async drag(q) {
    const p = P(q); const pts = q.path.split(';').map(s => s.split(',').map(Number));
    await touches(p, 'touchStart', [{ x: pts[0][0], y: pts[0][1] }]);
    for (const [x, y] of pts.slice(1)) { await p.page.waitForTimeout(Number(q.hold || 40)); await touches(p, 'touchMove', [{ x, y }]); }
    await p.page.waitForTimeout(Number(q.end || 40)); await touches(p, 'touchEnd', []); return 'ok';
  },
  // ?hold=x,y&hold_ms=1500  presses and keeps a finger (id 0) down; ?release=1 lifts everything
  async down(q) { const p = P(q); await touches(p, 'touchStart', [{ x: +q.x, y: +q.y, id: Number(q.id || 0) }]); return 'ok'; },
  async movefinger(q) { const p = P(q); await touches(p, 'touchMove', [{ x: +q.x, y: +q.y, id: Number(q.id || 0) }]); return 'ok'; },
  async up(q) { const p = P(q); await touches(p, 'touchEnd', []); return 'ok'; },
  // two fingers: ?a=x,y&b=x,y  both down (ids 0 and 1), kept until /up
  async down2(q) { const p = P(q); const a = q.a.split(',').map(Number), b = q.b.split(',').map(Number); await touches(p, 'touchStart', [{ x: a[0], y: a[1], id: 0 }, { x: b[0], y: b[1], id: 1 }]); return 'ok'; },
  async key(q) { const p = P(q); await p.page.keyboard.press(q.k); return 'ok'; },
  async keydown(q) { const p = P(q); await p.page.keyboard.down(q.k); return 'ok'; },
  async keyup(q) { const p = P(q); await p.page.keyboard.up(q.k); return 'ok'; },
  async type(q) { const p = P(q); await p.page.keyboard.type(q.text); return 'ok'; },
  async wheel(q) { const p = P(q); await p.page.mouse.move(css(p, +q.x), css(p, +q.y)); await p.page.mouse.wheel(0, Number(q.dy || 200)); return 'ok'; },
  async wait(q) { await P(q).page.waitForTimeout(Number(q.ms || 500)); return 'ok'; },
  async resize(q) { const p = P(q); p.w = Number(q.w); p.h = Number(q.h); await p.page.setViewportSize({ width: p.w, height: p.h }); return 'ok'; },
  async shot(q) { const p = P(q); const f = path.join(OUT, (q.name || 'shot') + '.png'); await p.page.screenshot({ path: f }); return f; },
  async logs(q) { const p = P(q); const l = p.logs.splice(0); return l.join('\n') || '(no warnings or errors)'; },
  async eval(q) { const p = P(q); return String(await p.page.evaluate(q.js)); },
};

(async () => {
  browser = await chromium.launch({ executablePath: CHROME, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--enable-webgl'] });
  http.createServer(async (req, res) => {
    const u = new URL(req.url, 'http://x');
    const q = Object.fromEntries(u.searchParams);
    const fn = routes[u.pathname.slice(1)];
    try {
      if (!fn) { res.writeHead(404); return res.end('unknown ' + u.pathname); }
      const out = await fn(q);
      res.writeHead(200, { 'content-type': 'text/plain' });
      res.end(typeof out === 'string' ? out : JSON.stringify(out));
    } catch (e) { res.writeHead(500); res.end(String(e.message || e)); }
  }).listen(PORT, () => console.log('qa server on', PORT, 'screenshots in', OUT));
})();
