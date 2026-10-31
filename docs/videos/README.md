# Gate Alert mock-up walkthrough video

`mockup-walkthrough.mp4` and `mockup-walkthrough.gif` are generated from the same HTML mock-ups as the README screenshots. They are **not** captures from the live game.

## Regenerate

```bash
cd docs/videos
npm install
node src/render.mjs
```

Requires Chromium or Google Chrome on `PATH` (or set `CHROME_PATH`), system `ffmpeg`, and Playwright's recorder helper:

```bash
npx playwright install ffmpeg
```

Sources:

- `src/walkthrough-intro.html` — opening disclaimer card
- `src/render.mjs` — Playwright screen recording + ffmpeg MP4/GIF export
- Scene HTML lives in `../screenshots/src/` (shared with PNG render)
