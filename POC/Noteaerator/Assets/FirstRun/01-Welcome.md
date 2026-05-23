# Welcome to Note Aerator 👋

Note Aerator is an AI-first Markdown viewer for the notes, projects, and
todo lists you keep on your PC. This window is showing one of those
notes right now.

## 30-second tour

- **The tabs across the top** are your *projects* — folders of `.md`
  files. You start with one (this one) and add more from the
  **Add project folder…** button in the toolbar.
- **The sidebar on the left** lists every `.md` file in the active
  project, with a built-in `Archive` drawer for files you want out of
  the way without deleting.
- **The main pane** (where you're reading right now) renders the
  selected file as nicely-formatted Markdown — code blocks,
  [Mermaid](https://mermaid.js.org) diagrams, math, tables, task
  lists, the works.

## Try this right now

1. Click **`02-Tips and Tricks.md`** in the sidebar to read about
   search, archive, sidecar comments, and the AI-directive markers.
   Your scroll position here is preserved when you come back.
2. Press **Ctrl+F** to search inside the current document. The 🔍
   icon in the top right opens a project-wide search.
3. **Right-click on this `Note Aerator` tab** at the top of the
   window. You'll see per-project options, including the
   **Group by prefix** toggle (which nests files that share
   dash-separated prefixes like `corp-orcl.md` /
   `corp-orcl-thomas.md`).

## Add your own notes

Click **Add project folder…** in the toolbar and pick any folder
that contains `.md` files. It could be your existing
`Documents\Notes` folder, a synced OneDrive folder, a git
checkout — anything. Note Aerator **never** modifies your `.md`
files; it only reads them.

Your files stay on your PC. Note Aerator has no cloud, no account,
and no telemetry. See `03-About Note Aerator.md` for the full
privacy story.

## How to name files for nice grouping

When **Group by prefix** is on (default — right-click a project tab
to toggle), Note Aerator nests files that share dash-separated
prefixes into a collapsible tree. Two small habits make this a lot
nicer to read:

1. **Use real words, not abbreviations.** Prefer
   `company-google-larry.md` over `comp-ggl-lar.md`. The prefix
   shows up as a folder label in the sidebar, but it doesn't appear
   in the rendered page, so length costs you nothing in the reading
   view.
2. **Drop a `<prefix>-overview.md` to anchor each group.** If you have
   `company-google-larry.md` and `company-google-sundar.md`, add a
   `company-google-overview.md` and write a short summary of the
   group at the top of it. Note Aerator promotes the overview file to
   *be* the `company-google` row in the sidebar — clicking the row
   opens the overview, and clicking the chevron expands the children.
   Without an overview file, clicking the row text jumps to the first
   descendant file (so you still land somewhere with content), but
   you have less control over what that "somewhere" is.

A small example layout:

```
resume-overview.md              ← anchors the "resume" group
resume-skills.md
resume-experience.md
company-google-overview.md      ← anchors "company-google"
company-google-larry.md
company-google-sundar.md
company-anthropic-overview.md   ← anchors "company-anthropic"
company-anthropic-dario.md
```

> 💡 **Tip:** the files for this getting-started project live at
> `%USERPROFILE%\Documents\Note Aerator\`. Feel free to edit, rename,
> or delete them — they're just plain Markdown. You can also remove
> the whole project from this list (right-click the tab →
> **Remove project from list**) without touching the files on disk.

## If you use an AI assistant on this folder

If you author notes with GitHub Copilot CLI (or similar), drop an
`AGENTS.md` in the folder with rules you want the assistant to
follow. Note Aerator doesn't read this file — your AI does — but
following the conventions below means the assistant will name new
files in a way that groups well in Note Aerator's sidebar.

Suggested snippet to paste into your folder's `AGENTS.md`:

```markdown
## File naming for Note Aerator

This folder is viewed in Note Aerator
(https://apps.microsoft.com/detail/9N5DTC0FZP7M), which groups
files in the sidebar by dash-separated leading prefixes. Follow
these rules when creating or renaming `.md` files:

1. Use full words separated by dashes for prefixes: `company-google`,
   `resume`, `project-acme`. Avoid abbreviations like `co-ggl`
   or `proj`.
2. When a topic has multiple files, also create
   `<prefix>-overview.md` and put a short summary at the top of it.
   Note Aerator promotes this file to *be* the parent row in the
   sidebar.
3. An optional numeric sort key may prefix the name, e.g.
   `30-anthropic-deep-dive.md`. Numeric tokens are sort keys only;
   they are not used for grouping.
4. Lowercase, kebab-case throughout. Spaces in filenames are
   tolerated but not encouraged — they break shell glob workflows.
```

Happy reading. 📖
