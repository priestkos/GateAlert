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
        'max-width:90%;text-align:center;line-height:1.35;pointer-events:none;';
      document.body.appendChild(cap);
    }
    cap.textContent = captionText;
  }, text);
}

async function removeCaption(page) {
  await page.evaluate(() => {
    document.getElementById('walkthrough-caption')?.remove();
  });
}

const scenes = [
  {
    html: path.join(screenshotSrc, '01-main-window-armed.html'),
    caption: 'Gate Alert window — /gatealert, ARMED, Every GATE, chat preview',
    holdMs: 5500,
    async interact(page) {
      await page.waitForTimeout(1800);
      await page.evaluate(() => {
        const pill = document.querySelector('.pill-armed');
        if (!pill) return;
        pill.textContent = 'PAUSED';
        pill.classList.replace('pill-armed', 'pill-paused');
      });
      await page.waitForTimeout(900);
      await page.evaluate(() => {
        const pill = document.querySelector('.pill-paused');
        if (!pill) return;
        pill.textContent = 'ARMED';
        pill.classList.replace('pill-paused', 'pill-armed');
      });
    },
  },
  {
    html: path.join(screenshotSrc, '02-gate-picker-custom.html'),
    caption: 'WHAT TO WATCH — selective GATEs, custom list, slash commands',
    holdMs: 5500,
    async interact(page) {
      await page.waitForTimeout(1200);
      const toggles = ['Air Force One', 'The Slice Is Right'];
      for (const name of toggles) {
        await page.locator(`button.gate-btn:has-text("${name}")`).click({ force: true });
        await page.waitForTimeout(450);
      }
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
      await page.waitForTimeout(800);
    },
  },
  {
    html: path.join(screenshotSrc, '03-output-reminders-active.html'),
    caption: 'OUTPUT & REMINDERS — toast, accent keys, stop nagging',
    holdMs: 5500,
    async interact(page) {
      await page.waitForTimeout(1000);
      await page.locator('button.btn:has-text("gold 60")').click({ force: true });
      await page.waitForTimeout(500);
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
      await page.waitForTimeout(600);
      await page.locator('button.btn:has-text("stop nagging")').click({ force: true });
      await page.waitForTimeout(700);
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
    },
  },
  {
    html: path.join(screenshotSrc, '04-ingame-chat-toast.html'),
    caption: 'Alerts in chat (and optional toast) — attendant line, reminders 2/3',
    holdMs: 5000,
    async interact(page) {
      await page.waitForTimeout(800);
      await page.evaluate(() => {
        const toast = document.querySelector('.toast');
        if (toast) {
          toast.style.transition = 'opacity 0.4s';
          toast.style.opacity = '0';
        }
      });
      await page.waitForTimeout(500);
      await page.evaluate(() => {
        const toast = document.querySelector('.toast');
        if (toast) toast.style.opacity = '1';
      });
      await page.waitForTimeout(1200);
      await page.evaluate(() => {
        const lines = document.querySelectorAll('.chat-line');
        lines.forEach((line, i) => {
          line.style.opacity = i <= 1 ? '1' : '0.35';
          line.style.transition = 'opacity 0.35s';
        });
      });
      await page.waitForTimeout(900);
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
    holdMs: 5500,
    async interact(page) {
      await page.waitForTimeout(1200);
      await page.locator('button.btn', { hasText: /^test$/ }).click({ force: true });
      await page.waitForTimeout(500);
      await page.locator('button.btn:has-text("Fire a test alert")').click({ force: true });
      await page.waitForTimeout(600);
      await page.evaluate(() => {
        const btn = [...document.querySelectorAll('button.btn')].find((b) => b.textContent.includes('Fire'));
        if (btn) {
          btn.style.outline = '2px solid rgba(245,199,74,0.7)';
          btn.style.outlineOffset = '2px';
        }
      });
      await page.waitForTimeout(900);
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

const page = await context.newPage();

const introUrl = fileUrl(path.join(__dirname, 'walkthrough-intro.html'));
await page.goto(introUrl, { waitUntil: 'networkidle' });
await page.waitForTimeout(4000);

for (const scene of scenes) {
  await page.goto(fileUrl(scene.html), { waitUntil: 'networkidle' });
  await page.waitForTimeout(350);
  await injectCaption(page, scene.caption);
  if (scene.interact) {
    await scene.interact(page);
  }
  const remaining = Math.max(800, scene.holdMs);
  await page.waitForTimeout(remaining);
  await removeCaption(page);
}

await page.goto(introUrl, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const h1 = document.querySelector('h1');
  if (h1) h1.textContent = 'End of mock-up tour';
  const p = document.querySelector('.intro-wrap p:not(.badge)');
  if (p) p.textContent = 'See README screenshots for still frames.';
});
await page.waitForTimeout(1800);

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
