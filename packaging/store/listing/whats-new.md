Release 0.2.1 — bug-fix patch:

- **Fixed auto-refresh for files in sub-folders.** In "Show folders"
  view, a file in a sub-folder that changed on disk used to require a
  manual Refresh; it now updates automatically again. Changes inside
  hidden folders are ignored unless "Show hidden files" is on.

Includes everything from 0.2.0:

- **Show folders.** A new per-project view lists a folder's own files
  flat and shows its sub-directories as expandable chevrons, to any
  depth, with a file count on each folder. Right-click a project tab to
  switch between "Show folders" and "Group by prefix".
- **Hide files and folders.** Right-click any file (or a sub-folder in
  Show folders mode) and choose Hide to declutter the list. A "Show
  hidden files" toggle brings them back, shown dimmed, where you can
  unhide them. Your choices are remembered between sessions.
- **Help button.** A new info button in the toolbar opens the project's
  GitHub page for docs and issue reporting.
- **Now ships for both Intel (x64) and ARM (arm64) PCs.**
