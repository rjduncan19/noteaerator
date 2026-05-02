# Noteaerator POC — implementation choices

Captured from the discussion on 2026-05-02. The goal of the POC is a simple
**read-mostly Markdown viewer** that auto-refreshes when the underlying file
changes on disk (e.g. as OneDrive or Google Drive sync writes a new version).

## Decision criteria

In rough priority order:

1. **Mermaid diagram quality.** Modern, faithful rendering — ideally the
   official `mermaid` library, which is the de-facto standard.
2. **Efficiency.** Small footprint, fast startup, low memory.
3. **OSS leverage.** Reuse mature libraries instead of writing a Markdown
   parser or renderer from scratch.
4. **Language familiarity.** Preference: C# or C++ over JavaScript. JS is
   acceptable if it's clearly the best path for Markdown + Mermaid support.
5. **Auto-refresh on file change.** Native file watching (e.g.
   `FileSystemWatcher`) preferred over polling.
6. **Future path to lightweight WYSIWYG.** Whatever we pick should leave a
   clean upgrade route to a small in-place editor later.

## Options considered

### Option 1 — C# (WPF or WinUI 3) + WebView2 + markdown-it + mermaid.js  *(recommended)*
- **Shell:** C#. Native window, menus, file picker, `FileSystemWatcher`,
  hotkeys.
- **Rendering:** A small static HTML page bundled with `markdown-it`,
  `mermaid`, and `highlight.js`. Loaded into WebView2. On file change, read
  the `.md` and push it into the page via `ExecuteScriptAsync`; the page
  re-renders.
- **Pros:**
  - WebView2 reuses the system Edge runtime — no Chromium ships with the app.
  - Mermaid is first-class (it's the reference implementation).
  - Same rendering stack VS Code / GitHub effectively use.
  - Almost no rendering code to write; ~30 lines of JS glue, isolated to one
    HTML file.
  - Clear upgrade path to a WYSIWYG editor later (e.g. swap in Milkdown or
    ToastUI Editor inside the same WebView2).
- **Cons:**
  - Windows-only (matches the stated baseline of Edge on Windows).
  - Requires .NET 8 SDK + WebView2 SDK.

### Option 2 — C# + Markdig (native render) + WebView2 only for Mermaid
- **Shell + parsing:** All C#, using
  [Markdig](https://github.com/xoofx/markdig) (fast, GFM-compatible,
  extensible).
- **Mermaid:** Either embed a WebView2 just for diagram blocks, or shell out
  to `mmdc` (mermaid-cli, requires Node) to produce SVG per code block and
  inline it.
- **Pros:** Most of the pipeline is C#; could even render prose to a
  `RichTextBox` for a more native feel.
- **Cons:** Two rendering paths (native for prose, web/SVG for diagrams)
  with inevitable styling drift. More glue code, fewer wins than Option 1.
  The `mmdc` route reintroduces the Node dependency we wanted to avoid.

### Option 3 — C++ with Qt 6 + QtWebEngine + markdown-it + mermaid
- Same architecture as Option 1, but in C++.
- **Pros:** Cross-platform; single-binary feel.
- **Cons:** QtWebEngine ships a full Chromium (~150 MB). Heavier build
  setup. More boilerplate for what WebView2 + C# gives for free. Only worth
  it if cross-platform is a real, near-term requirement.

### Option 4 — Electron / Tauri (JS/TS or Rust shell)
- **Electron:** Easiest path *if you're already a JS shop*. Heavy runtime
  (~100 MB), fully JS.
- **Tauri:** Tiny binary, Rust shell, webview UI — but Rust isn't on the
  preferred-language list either.
- Given the language preferences, neither beats Option 1.

### Option 5 — "Just a browser tab" (current baseline, no build)
- A single static `viewer.html` opened in Edge that loads a `.md` via
  `fetch`, polls for changes, renders with markdown-it + mermaid.
- **Pros:** Zero install, ~50 lines of code total.
- **Cons:** No real file watching from the browser (polling only, and
  `file://` `fetch` is restricted — needs a flag or a tiny local HTTP
  server). No native window chrome. No clean hook for the future WYSIWYG
  edit mode.

## Comparison

| Criterion | 1. C#+WebView2 | 2. C#+Markdig | 3. C++/Qt | 4. Electron | 5. Static HTML |
|---|---|---|---|---|---|
| Efficiency | ✅ reuses Edge | ✅ | ⚠️ ships Chromium | ❌ heavy | ✅ trivial |
| OSS leverage | ✅✅ | ⚠️ split stack | ✅ | ✅ | ✅ |
| Mermaid quality | ✅ official | ⚠️ via mmdc/web | ✅ | ✅ | ✅ |
| Preferred language | ✅ C# | ✅✅ mostly C# | ✅ C++ | ❌ JS | ❌ JS |
| File-watch + auto-refresh | ✅ FileSystemWatcher | ✅ | ✅ QFileSystemWatcher | ✅ chokidar | ⚠️ polling only |
| Path to WYSIWYG later | ✅ swap viewer for editor in same WebView2 | ❌ rebuild | ✅ | ✅ | ⚠️ |

## Recommendation

**Option 1 — C# (WPF) + WebView2 + markdown-it + mermaid.js.** It hits every
stated criterion, the JS surface area is tiny and isolated to a single
static HTML file, and it leaves the cleanest upgrade path to the planned
WYSIWYG mode.

## Future-fit: WYSIWYG editing and comments under Option 1

Both fit cleanly. This is one of the strongest reasons to prefer Option 1.

### WYSIWYG editing

WebView2 is a Chromium surface, so any web-based Markdown editor drops in.
Mature OSS candidates:

- **Milkdown** (MIT) — plugin-based, ProseMirror under the hood, good
  Markdown round-trip, supports custom nodes (Mermaid blocks can stay as
  editable code with live preview).
- **ToastUI Editor** (MIT) — split or true WYSIWYG mode, GFM, built-in code
  blocks; widely used.
- **TipTap** (MIT core) — ProseMirror-based, very flexible, large extension
  ecosystem.
- **CodeMirror 6 + side preview** — alternative if "great editor + live
  preview" is preferred over true WYSIWYG.

Architecture impact under Option 1:

- Same WPF shell, same WebView2.
- Add a View ↔ Edit mode toggle. View mode loads `viewer.html`
  (markdown-it + mermaid). Edit mode loads `editor.html` (e.g. Milkdown).
- C# gains a save path: editor posts dirty Markdown back via
  `WebMessageReceived`, C# writes it to disk. The existing
  `FileSystemWatcher` needs a short suppression window so it doesn't
  re-trigger from its own write.
- Sync-conflict handling (file changed on disk while editing) is the same
  problem any editor has with Drive/OneDrive — solvable with a prompt or
  a 3-way merge later.

Why this is *clean* specifically under Option 1: the C# shell already owns
file IO and watching; only the in-WebView page swaps. Option 2 (native
render) and Option 5 (browser tab) would need a much bigger rework.

### Comments

Two layers, depending on ambition:

**1. The `<!-- @ai: ... -->` markers we already standardized on.**
These are just Markdown text, so:

- In view mode, a small `markdown-it` plugin detects them and renders a
  styled callout (e.g. a sticky-note block).
- In edit mode, Milkdown/TipTap can register a custom node so they render
  as a real comment chip with a delete button, while still serializing
  back to `<!-- @ai: ... -->` on disk. Humans don't have to memorize the
  syntax.

**2. Richer "margin comments / track changes" (later, optional).**

- Best long-term anchor strategy is a CRDT (Yjs or Automerge) so comments
  survive edits to surrounding text. Both have ProseMirror bindings, which
  pair naturally with Milkdown/TipTap.
- Storage choice (deferred): keep comments inline in the `.md` (HTML
  comments — simple, syncs for free, human-readable) vs. a sidecar
  `<file>.comments.json` (richer metadata: author, timestamp, resolved
  state, robust anchor) vs. a hybrid. Sidecar is more flexible but doubles
  the sync surface and can drift.

### Bottom line

Option 1 doesn't just allow a future WYSIWYG + comments — it makes both the
easiest path, because the same web ecosystem provides the best Markdown
editors and the best Mermaid renderer. Mode-switching is "swap the HTML
page loaded in WebView2" plus a small save channel.

## Honest reality check: should we just use Milkdown (or Obsidian) today?

Asked 2026-05-02. Worth keeping in the doc so the question isn't re-asked
later from scratch.

### What Milkdown is

Milkdown is a **WYSIWYG Markdown editor library** (JS/TS component), not an
application. To "use Milkdown" you have to host it in something — a web
page, an Electron app, a VS Code-style shell. The official reference
editor is **Crepe**, which is a demo, not a notes app.

### Mapped against the stated workflow

| Need | Milkdown alone |
|---|---|
| WYSIWYG editing of `.md` | ✅ core strength |
| Read-mostly viewer | ⚠️ overkill — viewer is markdown-it/marked territory |
| **Auto-refresh on OneDrive sync** | ❌ browsers can't reliably watch the FS (polling only, `file://` `fetch` restrictions) |
| Mermaid | ✅ via official mermaid plugin |
| `<!-- @ai: ... -->` comments as first-class UI | ⚠️ possible via custom node (you write the plugin) |
| "Open this folder of synced notes" experience | ❌ Milkdown has no folder/file UI |
| AI-first input via Copilot CLI | n/a — CLI writes files independently |

The two gaps that matter most: **file watching** and **a real folder/notes
shell**. A native shell (WPF + WebView2) gives both cheaply; a pure
browser/Milkdown setup gives them painfully.

### Three honest framings

1. **"I want to stop building and start writing notes today."** Then skip
   this project for now and use **Obsidian** / Logseq / Mark Text / Zettlr
   pointed at the OneDrive folder. They already have Mermaid, comments,
   plugins, etc., and will be better than anything a POC produces in a
   week.
2. **"I want a small custom thing tuned to AI-first input + the
   `<!-- @ai: -->` convention + clean auto-refresh."** Then Milkdown is a
   *component* of the future POC, not a replacement. Option 1 is still
   the right shape; Milkdown plugs into WebView2 in edit mode later.
3. **"Validate the workflow first."** Use Obsidian or VS Code preview on a
   real OneDrive folder for a week or two. If the AI-first workflow feels
   good, build the custom thing with confidence; if not, you saved the
   build effort.

### The blunt take

- If the real goal is **"great Markdown experience right now"** → install
  **Obsidian** and don't build anything yet.
- If the real goal is **"a custom AI-first workspace where the CLI is the
  primary actor"** → Milkdown alone doesn't get you there; Option 1 is
  still the right path with Milkdown as a future building block.

## Decision

**2026-05-02 — Option 1 selected.** Build the POC as C# WPF + WebView2 +
markdown-it + mermaid.js. Vertical tab strip lists `.md` files within the
active project; horizontal tab strip switches between projects (folders).
Implementation lives under `POC/Noteaerator/`.
