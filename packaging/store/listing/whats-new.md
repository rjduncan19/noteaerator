Release 0.1.3.1 — bug-fix patch:

- **AGENTS.md no longer clutters the top of the file list.** It is
  now always sorted to the bottom of the project (issue #5). Files
  with a numeric prefix like `30-foo.md` keep coming first, and the
  rest stay alphabetical in the middle.
- **projects.json is forward-compatible.** Unknown properties added
  by a future version are now read tolerantly and round-tripped on
  save instead of being silently dropped, so editing your project
  list in an older build can no longer corrupt newer-format data
  (issue #4).
