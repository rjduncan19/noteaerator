# Worklog

Durable, append-only record of how `noteaerator` is being built. New sessions
go on top. See `AGENTS.md` for the workflow that produces this file.

> **Meta, not product.** This log captures the development process of the
> repository itself. It is not a feature or required convention of the
> noteaerator product.

## 2026-05-02 — Project bootstrap

- **decision**: chose Option 1 (C# WPF + WebView2 + markdown-it +
  mermaid.js) for the viewer POC. Recorded in
  `POC/implementation-choices.md`.
- **code**: scaffolded `POC/Viewer/` (.NET 8 WPF, WebView2 NuGet). UI is a
  horizontal `TabControl` for projects, each containing a vertical
  `TabControl` (file list) + `WebView2` (renderer) split by a
  `GridSplitter`. `FileSystemWatcher` per project triggers re-render on
  disk change. Project list persists at
  `%APPDATA%\noteaerator\viewer\projects.json`. _artifacts_:
  `POC/Viewer/Viewer.csproj`, `POC/Viewer/MainWindow.xaml`,
  `POC/Viewer/MainWindow.xaml.cs`
- **code**: bundled `Assets/viewer.html` using markdown-it + mermaid +
  highlight.js + github-markdown-css from CDN (vendor-locally is a
  follow-up). Custom rendering for `<!-- @ai: -->` and
  `<!-- @ai-done: -->` comment markers. _artifacts_:
  `POC/Viewer/Assets/viewer.html`
- **code**: added sample notes (`POC/sample-notes/welcome.md`,
  `notes.md`) covering headings, code, tasks, mermaid, an `@ai:` comment,
  and a table — for smoke testing the viewer.
- **doc**: wrote `POC/README.md` with build/run instructions, scope of
  the POC, engineering notes, and known gaps.
- **meta**: added `bin/` and `obj/` to `.gitignore`.
- **verify**: `dotnet build` clean (0 warnings, 0 errors); `dotnet run`
  launched and stayed running for 10s without crashing (full GUI
  interaction not verified headlessly).
- **audit**: confirmed POC is **strictly read-only** on `.md` files.
  Single open site uses `FileMode.Open` + `FileAccess.Read`; HTML view
  has no `contenteditable` / form / `fetch` write paths; only writes are
  to `%APPDATA%\noteaerator\viewer\projects.json`. Documented in
  `POC/README.md`. _artifacts_: `POC/README.md`
- **rename**: renamed POC project `Viewer` → `Noteaerator`. Folder
  `POC/Viewer/` → `POC/Noteaerator/`, `Viewer.csproj` →
  `Noteaerator.csproj`, namespaces and XAML class names updated, doc
  titles refreshed. Build remains clean. _artifacts_:
  `POC/Noteaerator/**`, `POC/README.md`,
  `POC/implementation-choices.md`, `POC/sample-notes/welcome.md`
- **code**: added `POC/launch.ps1` and `POC/launch.cmd` — verifies .NET 8
  SDK, builds (`-Rebuild` for clean), launches the built `Noteaerator.exe`
  (or falls back to `dotnet run`). Smoke-tested end-to-end. _artifacts_:
  `POC/launch.ps1`, `POC/launch.cmd`
- **doc/assets**: generated 10 SVG icon options under `POC/icons/`
  (bubble-note, whisk-page, sparkle-note, wind-lift, sprout-page,
  fizzy-note, balloon-page, breathing-N, robot-reader, thought-cloud) plus
  `preview.html` rendering them at 128/64/32/16 px and a `README.md`. No
  icon picked yet — per the decision-points ground rule, awaiting user
  selection before wiring `app.ico` into the WPF project. _artifacts_:
  `POC/icons/**`
- **assets**: added 6 more icon options (total now 16) at user request,
  including the wine-glass family they suggested: `11-wine-glass`,
  `12-decanter` (the literal aerator), `13-champagne-flute`,
  `14-paper-plane`, `15-quill-sparkle`, `16-steam-mug`. Updated
  `preview.html` and `README.md`. Still no pick. _artifacts_:
  `POC/icons/**`
- **decision**: user picked icon **12-decanter**.
- **code**: built `POC/icons/svg_to_ico.py` (resvg-py + Pillow) to
  rasterize an SVG into a multi-res `.ico` (16/24/32/48/64/128/256).
  Generated `POC/Noteaerator/app.ico` from the decanter SVG. Wired
  `<ApplicationIcon>app.ico</ApplicationIcon>` into the csproj and
  `Icon="app.ico"` on the main window. _artifacts_:
  `POC/icons/svg_to_ico.py`, `POC/Noteaerator/app.ico`,
  `POC/Noteaerator/Noteaerator.csproj`, `POC/Noteaerator/MainWindow.xaml`
- **code**: added modern Fluent-ish styling — `Theme.xaml` resource
  dictionary merged via `App.xaml`, restyled `Button`, `ToolBar`,
  horizontal project tabs (underline-on-select, decanter-wine accent),
  vertical file tabs (left-border + soft fill on select). New header
  bar in `MainWindow.xaml` shows the app icon, name, and an
  ellipsis-trimmed status line. `viewer.html` got matching typography,
  a card surface with subtle border, and a polished AI-comment
  callout. _artifacts_: `POC/Noteaerator/Theme.xaml`,
  `POC/Noteaerator/App.xaml`, `POC/Noteaerator/MainWindow.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`
- **doc**: wrote `POC/comments-design.md` proposing 5 UX options
  (right-click, selection pill, margin "+", comment-mode toggle,
  keyboard shortcut), explaining MD comment-persistence conventions
  honestly (HTML comments, CriticMarkup, Obsidian `%%`, Pandoc spans,
  sidecar files) and why our `<!-- @ai: -->` choice sits in the
  standard middle. No UX or persistence picked. _artifacts_:
  `POC/comments-design.md`
- **verify**: `dotnet build` clean; built `Noteaerator.exe` launched
  with new icon + theme and stayed up in headless smoke test.
- **decision**: comments persistence = **sidecar JSON**
  (`<basename>-comments.json`); UX = **A (right-click)** + **C (hover
  margin "+")**. Lifecycle: agent processes comments and deletes the
  sidecar (or empties the array → viewer auto-deletes).
- **code**: implemented sidecar comment system end-to-end. C# side:
  `CommentStore` (load/save/add/delete with atomic temp+replace,
  auto-deletes empty sidecar), `CommentFile`/`CommentEntry`/
  `CommentAnchor` DTOs with self-doc `_purpose` field, second
  `FileSystemWatcher` for `*-comments.json`, `WebMessageReceived`
  handler for `addComment`/`deleteComment`, self-write suppression
  window. JS side: block-anchor annotation
  (`data-anchor="<heading-slug>:<block-index>"`), 3-step anchor
  resolution (exact slug+index → text-quote fuzzy → same-heading →
  "stale" footer), hover "+" margin button, custom right-click context
  menu (only inside `.markdown-body`), inline floating comment input
  (Ctrl+Enter to save, Esc to cancel), comment cards with `×` delete,
  scroll position preservation on re-render. **`.md` files remain
  strictly read-only** — every `.md` open uses `FileMode.Open` +
  `FileAccess.Read`; all writes go to sidecar JSON or
  `%APPDATA%\noteaerator\viewer\projects.json`. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`
- **doc**: rewrote AGENTS.md "Human-in-the-loop comments" section to
  cover BOTH layers — inline `<!-- @ai: -->` markers (humans write by
  hand) AND sidecar `<basename>-comments.json` (viewer writes). Spelled
  out the agent lifecycle: read sidecar, act on each entry in the
  `.md`, delete the sidecar. _artifacts_: `AGENTS.md`
- **doc/sample**: added `POC/sample-notes/welcome-comments.json` with
  two demo entries anchored to real headings in `welcome.md` so the
  rendering shows up immediately. Updated `POC/README.md` to document
  the new UX, the sidecar lifecycle, and the refined read-only
  guarantee. _artifacts_: `POC/sample-notes/welcome-comments.json`,
  `POC/README.md`
- **verify**: build clean (0 warn / 0 err); smoke launch passed;
  read-only audit of `MainWindow.xaml.cs` confirms `.md` untouched.
- **fix**: comments could only attach to whole blocks, so a multi-row
  table accepted only one anchor at the bottom — unacceptable given how
  often Claude generates tables.
  - Extended anchor schema with `subPath` (e.g. `tr:2`, `li:0`).
  - JS annotates each `<tr>` and top-level `<li>` with its own
    `data-anchor`; hover-+ and right-click now work per row / per
    item. Row hover gets a soft highlight so the target is obvious.
  - Per-row "+" lives inside the first `<th>/<td>` so it doesn't get
    clipped by the table border.
  - Sub-anchored comment cards render after the *parent block*
    (insertion target walks up to the top-level child of `#content`),
    so a row-anchored comment never breaks table layout. The card
    shows a small "row N" / "item N" badge.
  - C# `CommentAnchor` gained `SubPath`; `addComment` handler reads it.
  - Anchor resolution: exact full anchor → parent-block fallback →
    text-quote fuzzy → same-heading → "stale" footer.
  - Sample sidecar gained a row-anchored demo on the table in
    `welcome.md`. Updated `AGENTS.md` schema docs and `POC/README.md`.
  - Build clean; smoke launch passed.
  _artifacts_: `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`,
  `POC/sample-notes/welcome-comments.json`, `AGENTS.md`,
  `POC/README.md`
- **redesign**: cards rendered too far from their anchor (after the
  parent table/list), and "+" margin button was missing/inconsistent
  for rows and items. Moved both into a single positioning **overlay
  layer** inside the `.markdown-body`:
  - Comment cards float in the **right margin** at the anchor's exact
    top, rendered as semi-transparent yellow cards (`rgba` + subtle
    `backdrop-filter: blur`) with a small dashed leader line back to
    the anchor. Multiple cards on the same anchor stack vertically.
  - "+" button is placed at the **left margin at the vertical center
    of every anchored element** — uniform position regardless of
    element type (block, table row, list item).
  - Hover wiring uses a 150ms grace timer so the mouse can travel
    between the anchor and the button without losing it.
  - Inline comment input is also overlay-positioned (anchored to the
    target's top in the right margin), with a brief outline flash on
    the target so it's clear what's being commented on.
  - `ResizeObserver` on the markdown body re-runs layout on resize,
    font-load, mermaid render, comment add/delete.
  - Stale-anchor comments still fall through to a footer section in
    normal flow.
  - Build clean; smoke launch passed.
  _artifacts_: `POC/Noteaerator/Assets/viewer.html`
- **ui**: file list (vertical TabControl) wasn't scrollable and rows
  felt heavy with many files. Overrode `FileTabControlStyle` template
  to wrap the `TabPanel` in a `ScrollViewer` (vertical bar auto-shows
  when needed). Tightened `FileTabItemStyle` (padding 12,8 → 10,4;
  font 13 → 12; removed inter-item margin; `MinHeight=0`). Switched
  the file label `TextBlock` from `TextWrapping=Wrap` to
  `TextTrimming=CharacterEllipsis` so long names show as
  `welcome….md` instead of wrapping onto two lines. Tooltip still
  shows the full path. _artifacts_: `POC/Noteaerator/Theme.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **doc**: refreshed root `README.md` to reflect the actual POC state
  — repo layout includes `POC/` tree, viewing/editing section points
  to the bundled viewer with `launch.ps1`, comments section covers
  both inline markers and sidecar JSON, and the Status section
  enumerates what's working today vs. what's not yet built.
  _artifacts_: `README.md`
- **meta**: first push of the project to `origin/main` (everything up
  to and including the POC). Single commit so the early history stays
  legible.
- **fix**: WebView2 was creating its user-data folder right next to
  the exe (`Noteaerator.exe.WebView2/`). That works in `bin\Debug` but
  would fail when installed to Program Files (read-only for non-admin).
  Switched to `CoreWebView2Environment.CreateAsync(null,
  %LOCALAPPDATA%\noteaerator\WebView2)` so per-user cache lives in the
  right place. _artifacts_: `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: added `POC/install.ps1` and `POC/uninstall.ps1`.
  Installer runs `dotnet publish -c Release -r win-x64
  --self-contained true` by default (framework-INDEPENDENT, ~160 MB,
  no .NET prerequisite on the target), with `-FrameworkDependent` for
  a ~3 MB build. Self-elevates for system-wide install; `-PerUser`
  skips elevation and installs under `%LOCALAPPDATA%\Programs\`.
  Creates a Start Menu shortcut pointing at the installed exe with the
  embedded decanter icon. Writes a `.install-manifest.json` so
  `uninstall.ps1` knows what to remove. End-to-end install + launch +
  uninstall verified. Files saved as UTF-8 + BOM (Windows PowerShell
  5 falls over on BOM-less non-ASCII). _artifacts_:
  `POC/install.ps1`, `POC/uninstall.ps1`, `POC/README.md`

- **doc**: extended `POC/implementation-choices.md` with (a) a "Future-fit"
  section showing how WYSIWYG editing (Milkdown / ToastUI / TipTap) and
  comment UX layer onto Option 1, and (b) a "should we just use Milkdown
  or Obsidian today?" reality-check section with three honest framings.
  No implementation choice made — left pending per the new
  decision-points ground rule. _artifacts_: `POC/implementation-choices.md`

- **doc**: created `POC/implementation-choices.md` capturing the five viewer
  POC options (C#+WebView2, C#+Markdig, C++/Qt, Electron/Tauri, static HTML),
  decision criteria, comparison table, and recommendation. Decision section
  left pending for the user. _artifacts_: `POC/implementation-choices.md`

- **ground-rule**: added "Decision points: stop and ask" section to
  `AGENTS.md`. When the user asks for options to decide, the agent must
  stop after presenting them and not autopilot into picking one and
  executing. Triggered by autopilot picking Option 1 for the viewer POC and
  starting to scaffold without waiting. Also rolled back the partial
  scaffold under `apps/viewer`. _artifacts_: `AGENTS.md`

- **clarification** (added later same day): the work-tracking workflow is
  meta — it documents how this repo is being built. It is **not** a feature
  the noteaerator product enforces or prescribes for end users. Updated
  `AGENTS.md`, `README.md`, and this file to make the meta vs. product
  distinction explicit. _artifacts_: `AGENTS.md`, `README.md`,
  `docs/worklog.md`

- **decision**: adopted Markdown as the canonical artifact format for all
  durable content — easiest for humans to read and for AI to manipulate.
  _artifacts_: `README.md`
- **decision**: established a two-tier storage model — durable artifacts
  (committed + synced via Google Drive / OneDrive) vs. transient artifacts
  (under `./.tmp/`, gitignored, not synced). _artifacts_: `README.md`,
  `AGENTS.md`, `.gitignore`
- **decision**: human → AI comments in Markdown use the convention
  `<!-- @ai: ... -->`; agents address and remove them on the next pass.
  _artifacts_: `AGENTS.md`, `README.md`
- **decision**: development of this project is tracked in two places — the
  per-session SQL store (`work_log` table + `todos`) for live tracking, and
  `docs/worklog.md` for the durable history that survives across sessions.
  _artifacts_: `AGENTS.md`, `docs/worklog.md`
- **doc**: expanded `README.md` from a one-line tagline to a full vision
  document covering AI-first input, storage/sync model, viewing/editing,
  human comment convention, and current status. _artifacts_: `README.md`
- **doc**: created `AGENTS.md` capturing the work-tracking workflow,
  storage/sync conventions, the human-comment marker, and house rules for
  agents. _artifacts_: `AGENTS.md`
- **meta**: created `.gitignore` to exclude `./.tmp/` and common transient
  artifacts from version control. _artifacts_: `.gitignore`
- **meta**: deferred creation of `notes/`, `projects/`, `tasks/` directories
  until real content exists, to avoid speculative scaffolding.
