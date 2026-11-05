# Gate Alert mock-up walkthrough video

`mockup-walkthrough.mp4` and `mockup-walkthrough.gif` use stylised HTML mock-ups aligned with the README screenshots. They are **not** captures from the live game.

Sources:

- `src/walkthrough.html` — timed scenes, beat labels, bottom captions, gold **Mock-up** banner
- `src/render-video.mjs` — frame capture (`DURATION_SEC = 15`) and ffmpeg encode
- Scene styling shared with `../screenshots/src/dalamud-mock.css`
