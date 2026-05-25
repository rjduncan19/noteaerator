Release 0.1.4 — bug-fix patch:

- **No more silently missing files.** When two filenames in the same
  folder shared their first three `-`-separated tokens (for example
  `71-giac-ab-post-draft.md` and `72-giac-ab-post-draft-v2-with-feedback.md`),
  the second file used to vanish from the project file list. The
  prefix-grouping engine now extends past its depth cap just enough to
  keep every file reachable (issue #6).
- **AGENTS.md no longer clutters the top of the file list.** It is
  now always sorted to the bottom of the project (issue #5). Files
  with a numeric prefix like `30-foo.md` keep coming first, and the
  rest stay alphabetical in the middle.
- **projects.json is forward-compatible.** Unknown properties added
  by a future version are now read tolerantly and round-tripped on
  save instead of being silently dropped, so editing your project
  list in an older build can no longer corrupt newer-format data
  (issue #4).
