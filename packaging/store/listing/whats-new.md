Release 0.1.3 — major feature update:

- **Prefix grouping in the file list.** Files that share dash-separated
  prefixes (e.g. `corp-orcl.md` and `corp-orcl-thomas.md`) now collapse
  into an expandable tree in the sidebar. Right-click any project tab
  to toggle **Group by prefix** on or off; the setting is saved per
  project. Smart touches: leading numeric tokens like `30-` are sort
  keys (not group keys), `<prefix>-overview.md` acts as the anchor for
  `<prefix>`, and meaningless single-child wrappers are auto-collapsed.

- **Getting-started experience.** First-time users now land on a
  welcoming three-document project at `Documents\Note Aerator\`
  instead of an empty window. Walks through the 30-second tour,
  search, archive, sidecar comments, AI directives, prefix grouping,
  Mermaid, math, and task lists. The seeded project is yours — edit,
  rename, or delete the files however you like.

- **Reliability fix.** The projects.json reader is now case-insensitive
  so existing installs are not affected by the schema bump.
- **file:// links now clickable.** Markdown links to local files and
  folders (e.g. [Open folder](file:///C:/path/to/folder)) are no longer
  silently dropped by the renderer. Clicking one opens Explorer's
  Reveal in Folder view — files are never auto-launched through their
  default handler, so a .exe or .bat link can't run by accident.
