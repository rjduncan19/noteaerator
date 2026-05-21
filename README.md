# noteaerator

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9N5DTC0FZP7M)

A workspace for letting ideas, tasks, and project state breathe through
human-guided AI refinement.

`noteaerator` is an **AI-first rich editing experience** for personal
knowledge work — notes, ideas, todo lists, project tracking, and durable
status. The Copilot CLI is the primary input surface: you talk to it, and it
produces and maintains the underlying artifacts. Humans view and lightly edit
those artifacts in tools optimized for reading.

---

> **Status: Pre-MVP — early POC, planning & architecture phase.**
> A working Markdown-viewer POC ships under `POC/` with prebuilt
> Windows binaries and is also live on the
> [Microsoft Store](ms-windows-store://pdp/?productid=9N5DTC0FZP7M)
> ([web link](https://apps.microsoft.com/detail/9N5DTC0FZP7M)), but the
> broader vision (AI-first authoring loop, WYSIWYG editing, sync
> conventions, semantic search) is not yet built. Code, docs, and
> decisions are evolving in the open. Feedback welcome; production use
> is not. See the full [Status](#status) section below.

---

## Vision

- **AI-first input.** The expected way to create and modify content is through
  Copilot CLI prompts, not by hand-typing into an editor. The editor is for
  reading and small touch-ups.
- **Human- and AI-friendly storage.** Artifacts are plain **Markdown** so both
  humans and models can read, diff, and edit them without special tooling.
- **Great viewing baseline.** Microsoft Edge with a Markdown viewer extension
  is the default reader. Anything more is optional.
- **Lightweight WYSIWYG editing.** A simple WYSIWYG editor for minor manual
  edits, without taking over as the primary authoring tool.
- **Inline human → AI comments.** Humans can drop a comment into any Markdown
  file that the AI will pick up on the next pass (see "Human comments" below).
- **Sync across machines.** Durable artifacts live in a folder backed by a
  cloud sync provider (Google Drive or OneDrive) so the same workspace shows
  up on every machine.
- **More than notes.** Scope explicitly includes ideas capture, todo lists,
  personal project tracking, and living status documents — not just prose.

## Repository layout

```
.
├── README.md                 # this file -- vision and orientation
├── INSTALL.md                # download / install / run for end users
├── LICENSE
├── AGENTS.md                 # operating instructions for AI agents
├── docs/
│   └── worklog.md            # durable, append-only log of the build process
├── .devcontainer/            # GitHub Codespaces config (Linux dev env)
├── .github/workflows/
│   ├── pr-ci.yml             # tests + cross-arch build on every PR
│   └── release.yml           # tag-driven self-contained zips + SBOMs
├── packaging/winget/         # winget package manifest (portable installer)
├── POC/
│   ├── Noteaerator/          # WPF + WebView2 host (net8.0-windows)
│   ├── Noteaerator.Core/     # cross-platform library: SearchEngine, CommentStore, TimeFormat
│   ├── Noteaerator.Tests/    # xUnit tests for Core (run on Linux + Windows)
│   ├── Noteaerator.sln
│   ├── icons/                # 16 SVG icon options + svg→ico converter
│   ├── sample-notes/         # smoke-test notes (and a sample sidecar)
│   ├── implementation-choices.md
│   ├── comments-design.md
│   ├── search-design.md
│   ├── pipeline-design.md
│   ├── launch.ps1            # dev launcher (build + run from bin\Debug)
│   ├── install.ps1           # real install with Start Menu entry
│   ├── uninstall.ps1
│   └── README.md
├── notes/                    # (planned) free-form notes
├── projects/                 # (planned) per-project folders with status + tasks
├── tasks/                    # (planned) cross-cutting todo lists
└── .tmp/                     # gitignored scratch space for transient agent output
```

Folders marked _(planned)_ will be created as real content shows up — we are
deliberately not pre-scaffolding empty directories.

## Storage and sync model

- **Durable artifacts** (`README.md`, `AGENTS.md`, `docs/**`, `notes/**`,
  `projects/**`, `tasks/**`, ground rules, status files) live in the synced
  workspace and are intended to roam between machines via Google Drive or
  OneDrive. They are also the things that get committed to git.
- **Transient artifacts** (one-shot helper scripts, downloaded dependencies
  like `node_modules`, build outputs, scratch data) go to `./.tmp/` (or the OS
  temp dir) and are excluded from sync and from git.
- Agents are instructed in `AGENTS.md` to enforce this split.

## Viewing and editing

- **Read:** the bundled **Noteaerator viewer** (`POC/`) is the primary
  reader — a small WPF + WebView2 app that renders Markdown with
  Mermaid diagrams, syntax highlighting, GFM tables/task-lists, and
  the inline `<!-- @ai: -->` markers. Auto-refreshes on disk change so
  it composes cleanly with OneDrive / Google Drive sync. Microsoft
  Edge + a Markdown viewer extension still works as a fallback.
- **Edit (small changes):** a lightweight WYSIWYG Markdown editor.
  The specific tool is TBD — see `POC/implementation-choices.md`.
- **Edit (substantive changes):** prompt the Copilot CLI.

## Human comments for the AI

Two ways to leave a comment that the agent will pick up:

1. **Inline marker** — drop an HTML comment anywhere in the file:
   ```markdown
   <!-- @ai: rewrite this paragraph to focus on the sync story -->
   ```
   Renderable in any Markdown tool; the noteaerator viewer styles it as
   a yellow callout.

2. **Sidecar comment** *(viewer-driven)* — right-click any block, row,
   or list item in the viewer (or hover and click the **+** in the
   left margin) and write a comment. It's stored in
   `<basename>-comments.json` next to the `.md`, so the source file is
   never touched and sync conflicts on the note itself are avoided.
   Agents are expected to read these, act on them, and **delete the
   sidecar file** when done. See `AGENTS.md` for the schema and
   lifecycle.

## Try the viewer (POC)

```pwsh
cd POC
.\launch.ps1
```

Click **Add project folder…** and pick `POC\sample-notes` to see a
rendered file with a Mermaid diagram, an inline `@ai:` comment, and
three demo sidecar comments (including one anchored to a single table
row). See `POC/README.md` for the full feature list and the read-only
guarantee on `.md` files.

## How this project is built

> This section is about the **meta-process** of developing `noteaerator`,
> not a feature of the product itself. End users of noteaerator are not
> expected to keep a worklog unless they want to.

Every meaningful step taken by an AI agent while building this repo is
recorded in `docs/worklog.md` (durable) and in the session SQL store
(live). This gives us a clear history of the project's own evolution. The
full workflow is in `AGENTS.md`.

## Privacy

Note Aerator runs entirely on your machine and **does not collect any
data**. See [`PRIVACY.md`](./PRIVACY.md) for the full policy.

## Status

Early POC. What exists today:

- Conventions: `README.md`, `AGENTS.md`, `docs/worklog.md`, the
  meta-process for tracking project development.
- A working **Markdown viewer POC** under `POC/Noteaerator/` (C# WPF +
  WebView2 + markdown-it + mermaid.js):
  - Horizontal tabs for projects (folders), vertical scrollable list
    of `.md` files per project, with an "Archive" expander at the
    bottom (right-click any file to move it in/out of archive).
  - Auto-refresh on disk change via `FileSystemWatcher` (OneDrive /
    Drive friendly).
  - Mermaid + syntax highlighting + GFM + inline `@ai:` markers.
  - **Sidecar comments** (`<basename>-comments.json`) with overlay UI:
    hover-`+` in the left margin or right-click any block / table row /
    list item to comment; cards float in the right margin as
    semi-transparent overlays anchored exactly to their target.
  - **Cross-file search** (Ctrl+F or the magnifier in the top-right):
    naive scan over `.md` content + sidecar comment bodies, scoped to
    the whole project (default) or the current file.
  - **Strictly read-only on `.md` files** (audited).
  - Custom decanter app icon + Fluent-ish theme.
  - One-stop `POC\launch.ps1` launcher and `POC\install.ps1` /
    `POC\uninstall.ps1` for a real installed build with a Start Menu
    entry.
- **Pre-built binaries** for Windows x64 and arm64 are published as
  GitHub Release assets — see [`INSTALL.md`](./INSTALL.md).
- Decisions captured: see `POC/implementation-choices.md`,
  `POC/comments-design.md`, and `POC/search-design.md`.

Not yet: WYSIWYG editing, recursive file tree, offline JS bundling,
window-state persistence, semantic search, multi-machine sync
conventions beyond "point it at your synced folder".
