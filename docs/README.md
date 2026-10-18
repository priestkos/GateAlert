# Docs assets

Simulated UI for Gate Alert — not in-game captures.

| Path | What |
| --- | --- |
| [`mockups/`](mockups/) | HTML sources for each state |
| [`screenshots/`](screenshots/) | PNGs rendered from those HTML files |

| Mock | Screenshot |
| --- | --- |
| `mockups/config-armed.html` | `screenshots/01-config-armed.png` |
| `mockups/config-paused.html` | `screenshots/02-config-paused.png` |
| `mockups/chat-alert.html` | `screenshots/03-chat-alert.png` |
| `mockups/alert-concept.html` | `screenshots/04-alert-concept.png` |

Root `mockup.html` / `concept.html` stay as the editable originals; the files under
`mockups/` are snapshot copies used for screenshots (notes/captions hidden so the PNGs stay clean).

### Re-render

```bash
SHOT() {
  out=$1; size=$2; url=$3
  udir=$(mktemp -d)
  timeout 25 google-chrome \
    --headless=new --no-sandbox --disable-gpu --disable-dev-shm-usage \
    --hide-scrollbars --user-data-dir="$udir" --window-size="$size" \
    --screenshot="$out" --virtual-time-budget=2000 "$url"
  rm -rf "$udir"
}
SHOT docs/screenshots/01-config-armed.png 500,720 file://$PWD/docs/mockups/config-armed.html
SHOT docs/screenshots/02-config-paused.png 500,720 file://$PWD/docs/mockups/config-paused.html
SHOT docs/screenshots/03-chat-alert.png 920,700 file://$PWD/docs/mockups/chat-alert.html
SHOT docs/screenshots/04-alert-concept.png 860,980 file://$PWD/docs/mockups/alert-concept.html
```
