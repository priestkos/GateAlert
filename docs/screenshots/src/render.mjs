import { chromium } from 'playwright-core';
import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const srcDir = __dirname;
const outDir = path.join(__dirname, '..');

const pages = [
  { html: '01-main-window-armed.html', png: '01-main-window-armed.png', width: 1280, height: 720 },
  { html: '02-gate-picker-custom.html', png: '02-gate-picker-custom.png', width: 1280, height: 720 },
  { html: '03-output-reminders-active.html', png: '03-output-reminders-active.png', width: 1280, height: 720 },
  { html: '04-ingame-chat-toast.html', png: '04-ingame-chat-toast.png', width: 1280, height: 720 },
  { html: '05-recent-history.html', png: '05-recent-history.png', width: 1280, height: 720 },
];

const chromePaths = [
  process.env.CHROME_PATH,
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/usr/bin/chromium-browser',
].filter(Boolean);

let executablePath = chromePaths.find((p) => fs.existsSync(p));

const browser = await chromium.launch({
  headless: true,
  executablePath,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

for (const page of pages) {
  const p = await browser.newPage({ viewport: { width: page.width, height: page.height } });
  const fileUrl = 'file://' + path.join(srcDir, page.html);
  await p.goto(fileUrl, { waitUntil: 'networkidle' });
  await p.waitForTimeout(300);
  await p.screenshot({ path: path.join(outDir, page.png), type: 'png' });
  await p.close();
  const stat = fs.statSync(path.join(outDir, page.png));
  console.log(`${page.png}: ${(stat.size / 1024).toFixed(1)} KB`);
}

await browser.close();

let total = 0;
for (const page of pages) {
  total += fs.statSync(path.join(outDir, page.png)).size;
}
console.log(`Total: ${(total / 1024 / 1024).toFixed(2)} MB`);
