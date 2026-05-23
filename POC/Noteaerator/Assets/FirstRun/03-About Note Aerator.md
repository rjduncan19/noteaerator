# About Note Aerator

## What it is

A polished, fast, distraction-free Markdown viewer for personal
knowledge work. The notes, plans, and project state you already keep
in `.md` files — rendered beautifully, navigable, instantly
searchable.

## What it isn't

- It is **not an editor** (yet). It is a viewer with light
  annotation. Use your favorite text editor — or an AI assistant
  prompted from a terminal — to author content.
- It is **not a cloud notes service**. There is no account, no
  sync, no upload. Your files stay where you put them.
- It is **not a replacement** for Obsidian or Notion if you depend
  on their graph view, plugin ecosystems, or block databases.

## Privacy

- **No telemetry.** The app does not phone home.
- **No account.** There is nothing to sign in to.
- **No upload.** The renderer is local; the only network requests
  are to load the rendering libraries (markdown-it, highlight.js,
  Mermaid, KaTeX) from public CDNs into the embedded WebView2
  control.

See the full privacy statement at
<https://github.com/rjduncan19/noteaerator/blob/main/PRIVACY.md>.

## Where settings live

| What                                                         | Where                                              |
| ------------------------------------------------------------ | -------------------------------------------------- |
| The list of folders you've added as projects                 | `%APPDATA%\noteaerator\viewer\projects.json`       |
| WebView2 user-data (cache, cookies for the embedded browser) | `%LOCALAPPDATA%\noteaerator\WebView2\`             |
| This getting-started project's notes                         | `%USERPROFILE%\Documents\Note Aerator\`            |

You can safely delete any of these at any time. Note Aerator never
modifies the `.md` files in folders you add as projects.

## Support

- Source code, issues, feature requests:
  <https://github.com/rjduncan19/noteaerator>
- Microsoft Store listing:
  <https://apps.microsoft.com/detail/9N5DTC0FZP7M>

## License

MIT. Free, forever. See
<https://github.com/rjduncan19/noteaerator/blob/main/LICENSE>.
