import { chromium } from 'playwright-core';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const screenshotSrc = path.resolve(__dirname, '../../screenshots/src');
const outDir = path.resolve(__dirname, '..');
const tmpVideoDir = path.join(outDir, '.tmp-video');
const mp4Path = path.join(outDir, 'mockup-walkthrough.mp4');
const gifPath = path.join(outDir, 'mockup-walkthrough.gif');
const overlayScript = fs.readFileSync(path.join(__dirname, 'walkthrough-overlay.js'), 'utf8');

const VIEWPORT = { width: 1280, height: 720 };

const chromePaths = [
  process.env.CHROME_PATH,
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/usr/bin/chromium-browser',
].filter(Boolean);

const executablePath = chromePaths.find((p) => fs.existsSync(p));

function fileUrl(relativeToScreenshotsOrVideos) {
  return `file://${path.join(relativeToScreenshotsOrVideos)}`;
}

async function injectCaption(page, text) {
  await page.evaluate((captionText) => {
    let cap = document.getElementById('walkthrough-caption');
    if (!cap) {
      cap = document.createElement('div');
      cap.id = 'walkthrough-caption';
      cap.style.cssText =
        'position:fixed;bottom:28px;left:50%;transform:translateX(-50%);' +
        'background:rgba(8,8,12,0.92);color:#f5c74a;padding:10px 22px;border-radius:6px;' +
        'font:600 17px "Segoe UI",system-ui,sans-serif;z-index:99999;' +
        'border:1px solid rgba(245,199,74,0.35);box-shadow:0 8px 32px rgba(0,0,0,0.5);' +
        'max-width:90%;text-align:center;line-height:1.35;pointer-events:none;' +
        'opacity:0;transition:opacity 0.4s ease, transform 0.4s ease;' +
        'transform:translateX(-50%) translateY(8px);';
      document.body.appendChild(cap);
    }
    cap.textContent = captionText;
    requestAnimationFrame(() => {
      cap.style.opacity = '1';
      cap.style.transform = 'translateX(-50%) translateY(0)';
    });
  }, text);
}

async function removeCaption(page) {
  await page.evaluate(() => {
    const cap = document.getElementById('walkthrough-caption');
    if (!cap) return;
    cap.style.opacity = '0';
    cap.style.transform = 'translateX(-50%) translateY(6px)';
    window.setTimeout(() => cap.remove(), 420);
  });
  await page.waitForTimeout(450);
}

async function centerOfLocator(page, locator) {
  const box = await locator.first().boundingBox();
  if (!box) return null;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

async function cursorClickLocator(page, locator, moveMs = 560) {
  const pt = await centerOfLocator(page, locator);
  if (!pt) return;
  await page.evaluate(
    async ({ x, y, ms }) => {
      await window.__wt.clickAt(x, y, ms);
    },
    { ...pt, ms: moveMs },
  );
}

async function transitionTo(page, url, { fadeOutMs = 420, fadeInMs = 480 } = {}) {
  await page.evaluate(async (ms) => {
    await window.__wt.fadeVeil(true, ms);
  }, fadeOutMs);
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.evaluate(async (ms) => {
    window.__wt.ensureDom();
    window.__wt.animatePanelEntrance();
    await window.__wt.fadeVeil(false, ms);
  }, fadeInMs);
  await page.waitForTimeout(120);
}

const scenes = [
  {
    html: path.join(screenshotSrc, '01-main-window-armed.html'),
    caption: 'Gate Alert window — /gatealert, ARMED, Every GATE, chat preview',
    holdMs: 5200,
    async interact(page) {
      await page.waitForTimeout(900);
      const pill = page.locator('button.pill-armed');
      await cursorClickLocator(page, pill);
      await page.evaluate(() => {
        const el = document.querySelector('.pill-armed');
        if (!el) return;
        el.textContent = 'PAUSED';
        el.classList.replace('pill-armed', 'pill-paused');
      });
      await page.waitForTimeout(700);
      await cursorClickLocator(page, page.locator('button.pill-paused'));
      await page.evaluate(() => {
        const el = document.querySelector('.pill-paused');
        if (!el) return;
        el.textContent = 'ARMED';
        el.classList.replace('pill-paused', 'pill-armed');
      });
      await page.waitForTimeout(600);
    },
  },
  {
    html: path.join(screenshotSrc, '02-gate-picker-custom.html'),
    caption: 'WHAT TO WATCH — selective GATEs, custom list, slash commands',
    holdMs: 5200,
    async interact(page) {
      await page.waitForTimeout(700);
      for (const name of ['Air Force One', 'The Slice Is Right']) {
        await cursorClickLocator(page, page.locator(`button.gate-btn:has-text("${name}")`));
        await page.evaluate((gateName) => {
          const btn = [...document.querySelectorAll('button.gate-btn')].find((b) =>
            b.textContent.includes(gateName),
          );
          if (btn) btn.classList.toggle('on');
        }, name);
        await page.waitForTimeout(380);
      }
      await cursorClickLocator(page, page.locator('label.cb'));
      await page.evaluate(() => {
        const box = document.querySelector('.cb-box.off');
        const label = box?.closest('label');
        if (box && label) {
          box.classList.remove('off');
          box.textContent = '✓';
          const muted = label.parentElement?.querySelector('.muted');
          if (muted) muted.textContent = '5 selected';
        }
      });
      await page.waitForTimeout(700);
    },
  },
  {
    html: path.join(screenshotSrc, '03-output-reminders-active.html'),
    caption: 'OUTPUT & REMINDERS — toast, accent keys, stop nagging',
    holdMs: 5200,
    async interact(page) {
      await page.waitForTimeout(650);
      await cursorClickLocator(page, page.locator('button.btn:has-text("gold 60")'));
      await page.evaluate(() => {
        const gold = [...document.querySelectorAll('button.btn')].find((b) => b.textContent.includes('gold'));
        const link = [...document.querySelectorAll('button.btn')].find((b) => b.textContent.includes('link'));
        if (gold && link) {
          gold.classList.add('btn-accent');
          link.classList.remove('btn-accent');
        }
        const input = document.querySelector('.input-sm');
        if (input) input.value = '60';
      });
      await page.waitForTimeout(450);
      await cursorClickLocator(page, page.locator('button.btn:has-text("stop nagging")'));
      await page.waitForTimeout(500);
      const track = page.locator('.slider-track').first();
      const trackBox = await track.boundingBox();
      const knob = page.locator('.slider-knob').first();
      const knobBox = await knob.boundingBox();
      if (trackBox && knobBox) {
        const y = trackBox.y + trackBox.height / 2;
        const xStart = knobBox.x + knobBox.width / 2;
        const xEnd = trackBox.x + trackBox.width * 0.22;
        await page.evaluate(
          async ({ x0, y0, x1, y1 }) => {
            await window.__wt.dragCursor(x0, y0, x1, y1, 620);
          },
          { x0: xStart, y0: y, x1: xEnd, y1: y },
        );
      }
      await page.evaluate(() => {
        const timesKnob = document.querySelectorAll('.slider-knob')[0];
        const timesFill = document.querySelectorAll('.slider-fill')[0];
        const timesLabel = document.querySelectorAll('.slider-row .muted')[0];
        if (timesKnob && timesFill) {
          timesKnob.style.left = '22%';
          timesFill.style.width = '22%';
          if (timesLabel) timesLabel.textContent = '3';
        }
      });
      await page.waitForTimeout(500);
    },
  },
  {
    html: path.join(screenshotSrc, '04-ingame-chat-toast.html'),
    caption: 'Alerts in chat (and optional toast) — attendant line, reminders 2/3',
    holdMs: 4800,
    async interact(page) {
      await page.waitForTimeout(550);
      const toast = page.locator('.toast');
      const toastPt = await centerOfLocator(page, toast);
      if (toastPt) {
        await page.evaluate(
          async ({ x, y }) => {
            await window.__wt.clickAt(x, y, 520);
          },
          toastPt,
        );
      }
      await page.evaluate(() => {
        const el = document.querySelector('.toast');
        if (el) {
          el.style.transition = 'opacity 0.45s ease, transform 0.45s ease';
          el.style.opacity = '0';
          el.style.transform = 'translateY(-6px)';
        }
      });
      await page.waitForTimeout(480);
      await page.evaluate(() => {
        const el = document.querySelector('.toast');
        if (el) {
          el.style.opacity = '1';
          el.style.transform = 'translateY(0)';
        }
      });
      await page.waitForTimeout(900);
      await page.evaluate(async () => {
        const lines = document.querySelectorAll('.chat-line');
        for (let i = 2; i < lines.length; i += 1) {
          await window.__wt.wait(120);
          lines[i].style.transition = 'opacity 0.35s ease';
          lines[i].style.opacity = '0.35';
        }
      });
      await page.waitForTimeout(750);
      await page.evaluate(() => {
        document.querySelectorAll('.chat-line').forEach((line) => {
          line.style.opacity = '1';
        });
      });
    },
  },
  {
    html: path.join(screenshotSrc, '05-recent-history.html'),
    caption: 'RECENT history, preview test, Fire a test alert, Dalamud config entry',
    holdMs: 5200,
    async interact(page) {
      await page.waitForTimeout(800);
      await cursorClickLocator(page, page.locator('button.btn', { hasText: /^test$/ }));
      await page.waitForTimeout(420);
      await cursorClickLocator(page, page.locator('button.btn:has-text("Fire a test alert")'));
      await page.evaluate(() => {
        const btn = [...document.querySelectorAll('button.btn')].find((b) => b.textContent.includes('Fire'));
        if (btn) {
          btn.style.outline = '2px solid rgba(245,199,74,0.7)';
          btn.style.outlineOffset = '2px';
          btn.style.transition = 'outline-color 0.35s ease';
        }
      });
      await page.waitForTimeout(850);
    },
  },
];

function runFfmpeg(args) {
  const result = spawnSync('ffmpeg', args, { encoding: 'utf8' });
  if (result.status !== 0) {
    console.error(result.stderr || result.stdout);
    throw new Error(`ffmpeg failed: ${args.join(' ')}`);
  }
}

fs.mkdirSync(tmpVideoDir, { recursive: true });
for (const f of fs.readdirSync(tmpVideoDir)) {
  fs.unlinkSync(path.join(tmpVideoDir, f));
}

const browser = await chromium.launch({
  headless: true,
  executablePath,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const context = await browser.newContext({
  viewport: VIEWPORT,
  recordVideo: { dir: tmpVideoDir, size: VIEWPORT },
});

await context.addInitScript({ content: overlayScript });

const page = await context.newPage();

const introUrl = fileUrl(path.join(__dirname, 'walkthrough-intro.html'));
await page.goto(introUrl, { waitUntil: 'networkidle' });
await page.evaluate(async () => {
  window.__wt.ensureDom();
  await window.__wt.fadeVeil(true, 0);
  await window.__wt.fadeVeil(false, 550);
});
await page.waitForTimeout(3200);

for (const scene of scenes) {
  await transitionTo(page, fileUrl(scene.html));
  await injectCaption(page, scene.caption);
  if (scene.interact) {
    await scene.interact(page);
  }
  const remaining = Math.max(700, scene.holdMs);
  await page.waitForTimeout(remaining);
  await removeCaption(page);
}

await transitionTo(page, introUrl, { fadeOutMs: 400, fadeInMs: 420 });
await page.evaluate(() => {
  const h1 = document.querySelector('h1');
  if (h1) h1.textContent = 'End of mock-up tour';
  const p = document.querySelector('.intro-wrap p:not(.badge)');
  if (p) p.textContent = 'See README screenshots for still frames.';
  document.querySelector('.intro-wrap')?.classList.add('wt-panel-enter');
});
await page.waitForTimeout(2200);
await page.evaluate(async () => {
  await window.__wt.fadeVeil(true, 500);
});
await page.waitForTimeout(520);

const video = page.video();
await context.close();
await browser.close();

const webmFiles = fs.readdirSync(tmpVideoDir).filter((f) => f.endsWith('.webm'));
if (webmFiles.length !== 1) {
  throw new Error(`Expected one webm in ${tmpVideoDir}, got: ${webmFiles.join(', ')}`);
}
const webmPath = path.join(tmpVideoDir, webmFiles[0]);

runFfmpeg([
  '-y',
  '-i',
  webmPath,
  '-c:v',
  'libx264',
  '-preset',
  'medium',
  '-crf',
  '26',
  '-pix_fmt',
  'yuv420p',
  '-r',
  '30',
  '-movflags',
  '+faststart',
  '-an',
  mp4Path,
]);

const palettePath = path.join(tmpVideoDir, 'palette.png');
runFfmpeg([
  '-y',
  '-i',
  mp4Path,
  '-vf',
  'fps=8,scale=800:-1:flags=lanczos,palettegen=max_colors=128',
  palettePath,
]);
runFfmpeg([
  '-y',
  '-i',
  mp4Path,
  '-i',
  palettePath,
  '-lavfi',
  'fps=8,scale=800:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer',
  '-loop',
  '0',
  gifPath,
]);

fs.rmSync(tmpVideoDir, { recursive: true, force: true });

const mp4Stat = fs.statSync(mp4Path);
const gifStat = fs.statSync(gifPath);
console.log(`Wrote ${mp4Path} (${(mp4Stat.size / 1024 / 1024).toFixed(2)} MB)`);
console.log(`Wrote ${gifPath} (${(gifStat.size / 1024 / 1024).toFixed(2)} MB)`);

if (mp4Stat.size > 10 * 1024 * 1024) {
  console.warn('Warning: MP4 exceeds 10 MB target');
}
if (gifStat.size > 5 * 1024 * 1024) {
  console.warn('Warning: GIF exceeds 5 MB target');
}
