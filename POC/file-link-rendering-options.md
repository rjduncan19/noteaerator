# File-link rendering — security options

> Status: **awaiting decision**. This document captures the options
> for how Note Aerator should handle `[label](file:///c:/...)` links
> in rendered Markdown.

## The bug we're solving

`markdown-it` ships with a `validateLink` allowlist that explicitly
blocks four protocols by default:

```js
const BAD_PROTO_RE = /^(vbscript|javascript|file|data):/;
```

So a Markdown link like

```markdown
[Open dist folder](file:///C:/Users/richardd/.../packaging/store/dist)
```

silently degrades to plain text (renders as `[Open dist folder](file:///C:/Users/richardd/.../packaging/store/dist)`,
not a clickable hyperlink). That's not a hard block at the OS layer —
markdown-it just refused to emit an `<a href>`.

## Why the default is the way it is

Note Aerator already shell-launches external links via
`Process.Start(uri, UseShellExecute=true)`. That means a clicked
`file:///` URL goes through the OS file-association handler — folders
open in Explorer (good), but `.exe` files would launch, `.docx` would
open Word, `.bat` would run, etc.

A Markdown file you opened from somewhere — synced from a shared
OneDrive folder, dropped in your Documents by another app, generated
by an AI — could embed `[Click here](file:///c:/users/you/script.exe)`
and one click executes it.

So we genuinely have a design decision to make, not just "flip a config
flag."

## Options

### Option A — "Reveal in Explorer" only

Render `file://` as a link, but on click always do
`explorer /select,<path>` (for files) or `explorer <path>` (for
folders). Never auto-launch the file with its default handler.

- **Security:** excellent — zero accidental execution, ever. Worst
  case is "Explorer shows you a file."
- **UX:** slight surprise that clicking a `.docx` link reveals it
  in Explorer instead of opening Word. Predictable once explained.
- **Effort:** ~30 min — one `LaunchExternal` branch + override
  `validateLink`.

### Option B — Project-folder allowlist

`file://` links render only when the path is inside one of the folders
the user has explicitly added in `projects.json` (or a subfolder
thereof). Paths outside render as plain text. Inside-project files
launch normally with their default handler.

- **Security:** strong — mirrors NA's existing trust model ("if you
  added the folder, you trust it"). Doesn't protect against a
  malicious file *inside* a trusted folder, but you already opened
  that folder, so it's no worse than running the file directly.
- **UX:** great when it works (invisible). Slightly confusing when it
  silently doesn't (link in untrusted location → no visual cue why).
- **Effort:** ~1–2 hr — path-canonicalization + prefix check + maybe
  a hover tooltip explaining "Untrusted location — not clickable."

### Option C — A + B combined ⭐

`file://` links inside trusted (project) folders launch normally with
their default handler; everything else does Reveal-in-Explorer.

- **Security:** excellent.
- **UX:** good — the common case (linking to your own notes) Just
  Works; external links degrade gracefully to Reveal.
- **Effort:** ~1.5–2 hr.

### Option D — Always confirm

Every `file://` click shows a "About to open `<path>` — Open / Reveal
in Explorer / Cancel" dialog.

- **Security:** strong (user must consent per-click).
- **UX:** click fatigue. After 3 dialogs you stop reading them, and
  the protection erodes.
- **Effort:** ~45 min.

### Option E — Trusted-locations allowlist in settings

A separate user-managed list of trusted path prefixes, like Office's
Trusted Locations.

- **Security:** strong.
- **UX:** heavy — new settings UI, new mental model, new failure mode.
- **Effort:** 1–2 days. Overkill for a single-user POC.

## Recommendation

**Option C.** It matches Note Aerator's existing implicit trust model
(folders you've added = trusted), keeps the common case ergonomic, and
has zero footguns for untrusted content because the fallback is always
"Reveal in Explorer" — itself a useful action, never destructive.
Option E would also be fine in a corporate-grade product, but it's a
lot of plumbing for a single-user POC.

<!-- @ai: pick one of the options (or tweak the parameters of one,
     e.g. should subfolders of MyDocuments\Note Aerator\ be
     auto-trusted alongside project folders?) and I'll implement it. -->
