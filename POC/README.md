# Noteaerator POC

Read-mostly Markdown viewer for the **noteaerator** project. Built per
Option 1 of [`POC/implementation-choices.md`](./implementation-choices.md):
**C# WPF + WebView2 + markdown-it + mermaid.js**.

## What works in this POC

> **Read-only guarantee for `.md` files.** The viewer never writes to your
> Markdown files. `.md` is opened with `FileMode.Open` + `FileAccess.Read`.
> Comments added via the UI are persisted to a **sidecar**
> `<basename>-comments.json` next to the `.md` (atomic temp+replace
> writes), and `%APPDATA%\noteaerator\viewer\projects.json` holds the
> project-list state. Source notes themselves are untouched.

- **Horizontal tabs = projects (directories).** Add a folder via the
  "Add project folder…" toolbar button. The tab strip across the top
  switches between projects.
- **Vertical tabs = `.md` files in the active project.** Top-level only
  (no recursion in this POC).
- **Auto-refresh on file change.** Two `FileSystemWatcher`s per project
  watch `*.md` and `*-comments.json`; when the active file or its
  sidecar changes on disk (e.g. OneDrive sync writes a new version),
  the view re-renders without losing scroll position.
- **Markdown features:** headings, lists, tables, GFM task lists,
  fenced code blocks with `highlight.js` syntax highlighting,
  **Mermaid diagrams** (official `mermaid` library), and special
  rendering for the `<!-- @ai: ... -->` and `<!-- @ai-done: ... -->`
  human-comment markers defined in
  [`AGENTS.md`](../AGENTS.md#human-in-the-loop-comments-in-markdown).
- **Sidecar comments (new):** add a comment to any block via:
  - **Right-click** on the block → *💬 Add comment here*, **or**
  - **Hover** over a block → click the small **+** that appears in the
    left margin.
  - **Granular anchoring:** comments can target the whole block *or*
    a single **table row** *or* a single **list item** — hover any row
    or item and the **+** appears on it; the comment card shows a small
    "row N" / "item N" badge so it's obvious what it's anchored to.
  - Comments persist to `<basename>-comments.json` next to the `.md`.
    Each comment renders as a yellow card right after the *parent*
    block (so a row-anchored comment never breaks the table layout),
    with a `×` to delete. When the array is empty, the sidecar file
    auto-deletes. The expected lifecycle is: human leaves comments →
    agent reads `.md` + sidecar → agent applies changes to `.md` →
    agent deletes the sidecar.
- **Project list persists** across runs in
  `%APPDATA%\noteaerator\viewer\projects.json`.

## Build and run

Requires the .NET 8 SDK and a modern Edge / WebView2 runtime
(present by default on Windows 10/11).

**Easiest path** -- from the `POC/` folder:

```pwsh
.\launch.ps1            # build + run (development; uses .\bin\Debug)
.\launch.ps1 -Rebuild   # force a clean rebuild
```

(or double-click `launch.cmd` if you prefer a cmd wrapper).

**Manual path:**

```pwsh
cd POC\Noteaerator
dotnet run
```

Then click **Add project folder...** and pick `POC\sample-notes` to see
the bundled sample, including a Mermaid diagram and an `@ai:` comment.

## Install (retail)

To produce a real installed build with a Start Menu entry:

```pwsh
# Standard install: framework-INDEPENDENT (~160 MB; embeds .NET 8 runtime;
# no .NET prerequisite on the target machine), Program Files + All-Users
# Start Menu (UAC prompt for elevation).
.\install.ps1

# Smaller install (~3 MB) when the machine already has the .NET 8 Desktop
# runtime.
.\install.ps1 -FrameworkDependent

# Per-user install: no admin needed, lands in
# %LOCALAPPDATA%\Programs\Noteaerator with a per-user Start Menu entry.
.\install.ps1 -PerUser
```

After install, hit the Win key and type **noteaerator**.

WebView2 runtime is required at runtime; it ships with Edge on Windows
10/11 so no separate install is needed there. Note Aerator uses the
Evergreen WebView2 runtime (whichever version Edge updated to most
recently) — no fixed runtime is bundled. If the runtime is somehow
missing on your machine, the app will show a friendly dialog with a
download link instead of crashing.

To remove:

```pwsh
.\uninstall.ps1
```

This removes the install directory and the Start Menu shortcut. Your
per-user state (the project list at `%APPDATA%\noteaerator\viewer\`) is
left in place.

## App icon

`POC\icons\` contains 10 SVG icon options plus a `preview.html` page that
renders them at 128 / 64 / 32 / 16 px. Pick one and the chosen SVG will
be converted to a multi-resolution `app.ico` and wired into the project.

## Engineering notes

- **Rendering libs are loaded from CDN** in `Assets\viewer.html` for POC
  simplicity. Vendoring them locally is a follow-up so the viewer works
  fully offline.
- **File listing is top-level `*.md` only.** A tree / recursive view is a
  follow-up.
- **`FileSystemWatcher`** is wired with `FileShare.ReadWrite | Delete`
  reads so it cooperates with OneDrive / Drive sync writes.
- **Self-write suppression is not yet needed** because the viewer is
  read-only. When edit mode lands, we'll need a short suppression
  window so the watcher doesn't re-trigger from the viewer's own save.

## Known gaps / next steps

- Vendor the JS libs locally for offline use.
- Recursive file tree per project.
- Lightweight WYSIWYG edit mode (likely Milkdown in the same WebView2;
  see `implementation-choices.md`).
- Multi-monitor / window-state persistence.
- More resilient comment anchoring (CRDT-style or deeper fuzzy match)
  if/when the simple `headingSlug + blockIndex + textQuote` proves too
  fragile.
