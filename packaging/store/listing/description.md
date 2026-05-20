# Note Aerator

**An AI-first Markdown viewer for personal knowledge work.**

Note Aerator is a fast, distraction-free Markdown reader for the notes,
projects, and task lists you keep on your PC. It is designed for people who
use an AI coding/writing assistant (like GitHub Copilot CLI) to *author* their
notes, and want a clean, native Windows app to *read*, *browse*, and *lightly
annotate* them.

It does not store your notes in the cloud. It does not have an account. It
does not send your files anywhere. Your `.md` files stay where you put them -
in a folder on your PC or in your synced drive (OneDrive, Dropbox, etc.).

## Why Note Aerator

Most Markdown apps fall into two camps:

- **Heavy editors** (Obsidian, Notion, Typora) that want to own your
  workflow and lock you into their UI.
- **Plain text editors** (VS Code, Notepad++) that show you the source, not
  the rendered document.

Note Aerator is the missing third option: a *viewer-first* app that renders
your Markdown beautifully, lets you find anything instantly, and gets out of
the way when you want to edit. Editing itself is delegated to whichever tool
you already love - your text editor, or an AI assistant prompted from a
terminal.

## What you get

- **A polished Markdown viewer** with GitHub-flavored Markdown, syntax
  highlighting, Mermaid diagrams, math, tables, task lists, and footnotes.
- **Projects sidebar** - point Note Aerator at a folder and every `.md`
  file in it (and any `archive/` subfolder) shows up as a navigable list.
  Switch between projects with one click.
- **Live reload** - edit a file in any other app and the viewer updates
  the moment you save, without losing your scroll position.
- **Native fast search** - Ctrl+F opens the WebView2 find bar; jump
  between matches with Enter / Shift+Enter.
- **Inline AI comments** - drop `<!-- @ai: please rewrite this paragraph -->`
  anywhere in a note and it renders as a callout. The next time you run your
  AI assistant on that folder, it can pick the comments up and act on them.
- **Sidecar human comments** - right-click any block in the viewer to attach
  a comment. Comments are saved to a small JSON sidecar file next to your
  Markdown, so the source `.md` stays clean and merges cleanly across
  devices.
- **Built for sync drives** - tuned to be reliable on top of OneDrive,
  Dropbox, and Google Drive folders, where file change notifications are
  notoriously flaky.

## Privacy

Note Aerator is a local-only application. It reads files from folders you
choose on your PC. It does not upload your notes, collect telemetry, or
require sign-in. See the privacy policy linked from this listing for the
full statement.

## What it is not

- It is not a cloud notes service.
- It is not an editor (yet) - it is a viewer with light annotation. Use
  your favorite text editor or AI assistant to author.
- It is not a replacement for Obsidian or Notion if you depend on their
  graph view, plugin ecosystem, or block databases.

## Who it is for

- People who already write notes in Markdown and want a great way to *read*
  them.
- Developers and PMs who run GitHub Copilot CLI (or similar) and want their
  AI-generated artifacts to render beautifully.
- Anyone who values *local files, plain text, no lock-in* but is tired of
  staring at the source.

## System requirements

- Windows 10 (version 1809 / build 17763) or later, or Windows 11.
- 64-bit (x64) processor.
- Microsoft Edge WebView2 runtime. This is installed by default on Windows
  11 and on most up-to-date Windows 10 systems. If it is missing, Note
  Aerator will prompt you with a one-click download.

## Support

Issues and feature requests:
[github.com/rjduncan19/noteaerator/issues](https://github.com/rjduncan19/noteaerator/issues)
