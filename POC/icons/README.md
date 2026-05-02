# Icon options

Sixteen directions to choose from for the noteaerator app icon. Open
[`preview.html`](./preview.html) in any browser to see them all rendered
side-by-side at 128 / 64 / 32 / 16 px.

| # | File | Concept |
|---|---|---|
| 01 | `01-bubble-note.svg`     | **Bubble Note** — page with bubbles aerating up out of it |
| 02 | `02-whisk-page.svg`      | **Whisk Page** — kitchen whisk over a page (most literal) |
| 03 | `03-sparkle-note.svg`    | **Sparkle Note** — page with AI sparkles |
| 04 | `04-wind-lift.svg`       | **Wind Lift** — page lifted by a gust of wind |
| 05 | `05-sprout-page.svg`     | **Sprout Page** — leaf growing out of a page |
| 06 | `06-fizzy-note.svg`      | **Fizzy Note** — carbonation rising in a glass-shaped page |
| 07 | `07-balloon-page.svg`    | **Balloon Page** — hot-air balloon lifting a note |
| 08 | `08-breathing-n.svg`     | **Breathing N** — bold typographic N with breath rings |
| 09 | `09-robot-reader.svg`    | **Robot Reader** — friendly bot holding a note |
| 10 | `10-thought-cloud.svg`   | **Thought Cloud** — checked todo with a thinking cloud |
| 11 | `11-wine-glass.svg`      | **Wine Glass** — wine glass with bubbles rising |
| 12 | `12-decanter.svg`        | **Decanter** — a wine decanter, the literal aerator |
| 13 | `13-champagne-flute.svg` | **Champagne Flute** — slim flute with bubble column |
| 14 | `14-paper-plane.svg`     | **Paper Plane** — folded note taking flight |
| 15 | `15-quill-sparkle.svg`   | **Quill Sparkle** — quill pen + AI sparkles |
| 16 | `16-steam-mug.svg`       | **Steam Mug** — page-shaped mug with curling steam |

Pick the number you like and I'll:

1. Convert the chosen SVG to a multi-resolution `app.ico` (16/32/48/64/128/256).
2. Drop it into the WPF project (`POC/Noteaerator/app.ico`).
3. Wire it into `Noteaerator.csproj` (`<ApplicationIcon>`) and into the
   main window (`Icon="app.ico"`) so it shows up in the title bar,
   taskbar, alt-tab, and on the built `.exe`.
