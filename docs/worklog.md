# Worklog

Durable, append-only record of how `noteaerator` is being built. New sessions
go on top. See `AGENTS.md` for the workflow that produces this file.

> **Meta, not product.** This log captures the development process of the
> repository itself. It is not a feature or required convention of the
> noteaerator product.

## 2026-05-24 — v0.1.4 bug-fix release

- **meta**: cut as v0.1.3.1 first, but the Microsoft Store rejected the
  package — "Apps are not allowed to have a Version with a revision
  number other than zero specified in the app manifest." Renamed the
  entire release to v0.1.4 (manifest 0.1.4.0; UPDATE doc, whats-new,
  tag, and GitHub Release all renamed); the v0.1.3.1 tag and Release
  were deleted on the remote before re-cutting.
  _artifacts_: `packaging/store/Package.appxmanifest`,
  `packaging/store/UPDATE-v0.1.4.md`

- **code**: fixed issue [#6](https://github.com/rjduncan19/noteaerator/issues/6)
  — silent file drop when two filenames in the same folder shared
  their first three `-`-separated tokens (e.g.
  `71-giac-ab-post-draft.md` + `72-giac-ab-post-draft-v2-with-feedback.md`).
  `PrefixGrouping.InsertFile` now routes through a new `PlaceFile`
  helper that extends past `DefaultMaxDepth` on demand: when a
  collision is detected, the existing file gets pushed into a child
  node and the new file descends alongside it, recursing if further
  collisions occur down the chain. Added `PrefixNode.ClearFile`.
  Tightened the existing `Max_depth_applies_when_files_diverge_below_the_cap`
  test (renamed to `Max_depth_extends_on_demand_when_files_collide_at_the_cap`)
  to assert the new — correct — behavior, plus 4 fresh tests covering
  the real-world repro, 3-way collisions, file-at-leaf collisions,
  and idempotent re-insertion. Caught only after the v0.1.3.1 tag
  was first cut; fix was folded into the same release and the tag
  was moved.
  _artifacts_: `POC/Noteaerator.Core/PrefixGrouping.cs`,
  `POC/Noteaerator.Tests/PrefixGroupingTests.cs`
- **code**: fixed issue [#5](https://github.com/rjduncan19/noteaerator/issues/5)
  — AGENTS.md was no longer at the bottom of the file list after the
  v0.1.3 prefix-grouping rewrite. Added an "AGENTS.md always last"
  pre-stage to `SortedChildren` in `PrefixGrouping`, and the same
  rule to `Flat` (no-grouping mode). Other sort behavior preserved:
  numeric prefixes still come first, the rest stay alphabetical.
  _artifacts_: `POC/Noteaerator.Core/PrefixGrouping.cs`,
  `POC/Noteaerator.Tests/PrefixGroupingTests.cs`
- **code**: fixed issue [#4](https://github.com/rjduncan19/noteaerator/issues/4)
  — extracted `projects.json` parse/serialize logic into a testable
  `Noteaerator.Core.ProjectConfigStore`. The reader now (a) keeps
  backwards compat with the legacy string-array and current object
  forms; (b) captures unknown per-project JSON properties into a
  per-project `Extra` dict and round-trips them verbatim on save;
  (c) preserves unknown top-level array items in `UnknownRootItems`
  and re-emits them after the known projects; (d) never throws on
  unrecognized shapes. Writer uses stable property order to keep
  file diffs minimal across saves.
  _artifacts_: `POC/Noteaerator.Core/ProjectConfigStore.cs`,
  `POC/Noteaerator.Tests/ProjectConfigStoreTests.cs`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **doc**: made the Microsoft Store install path prominent in
  GitHub-facing docs (`README.md` + `INSTALL.md`).
- **code**: bumped Store package version `0.1.3.0` → `0.1.3.1` in
  `packaging/store/Package.appxmanifest`, rewrote
  `packaging/store/listing/whats-new.md` for the patch, and added
  `packaging/store/UPDATE-v0.1.3.1.md` with the manual Partner Center
  ritual. All 80 Core tests still pass; WPF app builds cleanly.

## 2026-05-23 — Prefix-group UX polish + naming guidance

- **code**: clicking the *label* of a synthetic folder row now jumps to
  content instead of toggling expansion. The chevron retains the
  expand/collapse behavior. When the folder has a file at its node
  (typically via the `-overview` alias), that file opens; otherwise
  the first descendant file in DFS / numeric-sort order opens, and
  the path to it is auto-expanded so the selection is visible. This
  removes the "dead row" feeling — every click on a row text now
  navigates to content. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: added two pure helpers in `PrefixGrouping` —
  `FirstFileIn(node)` (DFS / sort-key-aware) and
  `ExpandAncestorsOf(root, target)`. Refactored the existing
  EnsureVisible-by-path helper in MainWindow to share the
  expansion code rather than duplicating the recursion. _artifacts_:
  `POC/Noteaerator.Core/PrefixGrouping.cs`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **doc**: expanded the bundled `01-Welcome.md` with a new
  "How to name files for nice grouping" section pushing back on
  abbreviations (use `company-google-larry.md`, not
  `co-ggl-lar.md`) and recommending `<prefix>-overview.md` to
  anchor non-trivial groups. Added an "If you use an AI assistant"
  section with a copy-pasteable snippet for the user's folder-level
  `AGENTS.md`. Updated `02-Tips and Tricks.md`'s prefix-grouping
  section to describe the new chevron-vs-label click behavior and
  the expanded sample uses `company-google`, `company-anthropic`,
  etc. instead of the old `corp-orcl` shorthand. _artifacts_:
  `POC/Noteaerator/Assets/FirstRun/01-Welcome.md`,
  `POC/Noteaerator/Assets/FirstRun/02-Tips and Tricks.md`
- **verify**: 63/63 tests pass (4 new in `PrefixGroupingTests.cs`
  covering FirstFileIn at-node, FirstFileIn DFS into folder-only
  subtree, FirstFileIn respecting numeric sort keys, and
  ExpandAncestorsOf reaching a previously-hidden descendant).
  Launched the dev build and confirmed the click behavior.
- **code**: rebuilt the v0.1.3 MSIX bundle. New SHA256:
  `5F971D925CCB46661E0DE48F41D0FE5CECE58956566677A99D51816E0C6DEA84`
  (replaces the previous
  `C66235D605D2F4101755FC4DD824F69A504B0C4BA3C71D89D925882A725990C4`).
  `packaging/store/UPDATE-v0.1.3.md` updated with the new hash; the
  whats-new bullets are unchanged because this round is UX polish on
  features already in the announce list. _artifacts_:
  `packaging/store/dist/NoteAerator-0.1.3.0.msixbundle`,
  `packaging/store/UPDATE-v0.1.3.md`

## 2026-05-23 — file:// link rendering + safe handling (Option A)

- **decision**: per `POC/file-link-rendering-options.md`, chose Option
  A (Reveal-in-Explorer for all file:// links). file:// is now allowed
  through markdown-it's `validateLink`, but every click is routed
  through `explorer.exe` — folders open, files highlight in their
  parent folder. The file's own default-handler is NEVER invoked, so
  no markdown link can accidentally run an .exe / .bat / .docm. Other
  schemes (javascript:, vbscript:, non-image data:) remain blocked.
  _artifacts_: `POC/file-link-rendering-options.md`
- **code**: new pure `Noteaerator.Core.ExternalLink.Classify(uri)`
  returns an `ExternalLinkPlan { Kind, Target, Arguments, LocalPath }`.
  Kinds: `Default` (pass uri to ShellExecute), `FileFolder` (run
  `explorer.exe "<path>"`), `FileItem` (run
  `explorer.exe /select,"<path>"`), `FileRejected` (UNC, malformed,
  empty). Folder vs file detection is injectable for testing.
  _artifacts_: `POC/Noteaerator.Core/ExternalLink.cs`
- **code**: rewired `MainWindow.LaunchExternal` to dispatch via
  `ExternalLink.Classify` — non-file URLs keep the previous
  ShellExecute path so http(s) / mailto / ms-windows-store /
  vscode etc. work exactly as before. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: overrode `md.validateLink` in `viewer.html` to allow
  `file:` while still blocking javascript/vbscript/data — markdown-it
  was silently dropping file:// links because they're on its built-in
  unsafe-protocol list. _artifacts_:
  `POC/Noteaerator/Assets/viewer.html`
- **verify**: 59/59 tests pass (17 new in `ExternalLinkTests.cs`
  covering non-file passthrough, file→explorer arg construction,
  forward→backslash normalization, %20 decoding, UNC rejection,
  malformed-input rejection, and the key safety theorem: any
  file:// URL with a dangerous extension (.exe, .bat, .docm, .msi)
  still routes through `explorer.exe /select,` and never through
  ShellExecute on the file). Built clean, launched the dev build to
  spot-check rendering. _artifacts_:
  `POC/Noteaerator.Tests/ExternalLinkTests.cs`
- **code**: rebuilt the v0.1.3 MSIX bundle to include the fix.
  Bundle SHA256:
  `C66235D605D2F4101755FC4DD824F69A504B0C4BA3C71D89D925882A725990C4`
  (replaces the earlier
  `390775C4D46C01A61C8333E2BCDA7B9735EBC9EE9C167C3125C62DF0B44DEBBE`).
  Updated `packaging/store/UPDATE-v0.1.3.md` with the new hash + a
  fourth bullet in the release notes, and mirrored the bullet into
  `packaging/store/listing/whats-new.md`. The Step 1 file:// link
  in UPDATE-v0.1.3.md will now actually be clickable in the new
  build. _artifacts_:
  `packaging/store/dist/NoteAerator-0.1.3.0.msixbundle`,
  `packaging/store/UPDATE-v0.1.3.md`,
  `packaging/store/listing/whats-new.md`

## 2026-05-23 — First-run experience

- **decision**: when a brand-new install launches for the first time
  (detected by the absence of `%APPDATA%\noteaerator\viewer\projects.json`),
  drop a getting-started project at
  `%USERPROFILE%\Documents\Note Aerator\` and add it to the project
  list. Three bundled markdown files demonstrate Mermaid, math, task
  lists, `<!-- @ai: -->` markers, and prefix grouping. Only ever fires
  on a truly first run — if the user later removes the last project we
  respect the empty list and don't re-seed. _artifacts_:
  `POC/Noteaerator/Assets/FirstRun/*.md`,
  `POC/Noteaerator.Core/FirstRunSeeder.cs`
- **code**: added `Noteaerator.Core.FirstRunSeeder` with a testable
  `Seed(sourceDir, destDir)` core that copies every `*.md` from source
  to dest, never overwrites an existing file (so users can edit the
  seeded content safely), and creates the destination dir if missing.
  Wired into `MainWindow.LoadProjects` via the file-missing check.
  _artifacts_: `POC/Noteaerator.Core/FirstRunSeeder.cs`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: fixed a case-sensitivity bug in `ProjectConfig` parsing
  uncovered while end-to-end testing the seeder. The default
  `JsonSerializer.Serialize` writes PascalCase (`"Path"`,
  `"GroupByPrefix"`) but my loader only looked for camelCase. Now the
  loader accepts either and the writer is pinned to camelCase
  explicitly via `JsonNamingPolicy.CamelCase`. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **verify**: 42/42 tests pass (5 new in `FirstRunSeederTests.cs`
  covering copy, no-overwrite, dest-dir creation, missing source,
  and idempotency across repeated calls). End-to-end smoke test on
  this machine: deleted `projects.json` + the docs subfolder,
  launched → seeder created the docs folder with all 3 files and
  wrote a single-entry `projects.json` (camelCase keys); second
  launch loaded the existing config without re-seeding; original
  config restored after the test. Documents folder was OneDrive-
  redirected (`C:\Users\richardd\OneDrive - Microsoft\Documents`)
  and `Environment.SpecialFolder.MyDocuments` handled that
  transparently. _artifacts_:
  `POC/Noteaerator.Tests/FirstRunSeederTests.cs`

## 2026-05-23 — File-list prefix grouping

- **decision**: implemented Proposal A from
  `POC/file-list-grouping-proposals.md` with the user's
  modifications: 1-member groups are allowed (so adding a file never
  reshuffles existing entries), `-overview` files act as the parent
  anchor (`corp-crwd-overview.md` represents `corp-crwd`), and the
  on/off control is a right-click "Group by prefix" toggle on each
  project tab (default ON, persisted per project). _artifacts_:
  `POC/Noteaerator.Core/PrefixGrouping.cs`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: added `Noteaerator.Core.PrefixGrouping` — pure trie
  builder + flattener with chain-collapse so a meaningless parent
  (e.g. `corp` with only `orcl` under it) renders as `corp-orcl`
  instead of `corp > orcl`. Numeric leading tokens (`30-…`) are
  treated as sort keys, never as grouping tokens. Max depth = 3;
  tokens beyond the cap fold into the deepest node's tail.
  _artifacts_: `POC/Noteaerator.Core/PrefixGrouping.cs`
- **code**: refactored `ProjectTab` to render
  `ObservableCollection<FileListRow>` instead of the old
  `FileEntry` list. New data template shows a chevron hit-target +
  depth-indented label (via `DepthToIndentConverter`). Chevron click
  toggles `IsExpanded` on the underlying `PrefixNode` and
  re-flattens; row click opens the file (or, on synthetic
  folder-only rows, toggles expansion). _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Converters.cs`
- **code**: bumped `projects.json` schema from `string[]` to
  `[{path, groupByPrefix}, …]`. Loader still accepts the old shape
  (existing installs keep working); saver always writes the new
  shape. Search hits inside a collapsed group auto-expand their
  ancestors so the result is reachable. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **verify**: 37/37 tests pass (15 new in
  `PrefixGroupingTests.cs` covering tokenize, overview alias,
  chain-collapse, depth cap, numeric sort, in-session expand-state
  preservation, and the Security-folder example). Built the
  solution clean (0 warnings) and launched the app to confirm the
  existing 6 saved projects load and render with grouping enabled.
  _artifacts_:
  `POC/Noteaerator.Tests/PrefixGroupingTests.cs`

## 2026-05-21 — Note Aerator live on the Microsoft Store

- **decision**: app published successfully under product ID
  `9N5DTC0FZP7M`. Deep link
  `ms-windows-store://pdp/?productid=9N5DTC0FZP7M`; web listing
  `https://apps.microsoft.com/detail/9N5DTC0FZP7M`. Submission cleared
  on the first try (no certification feedback needed). _artifacts_:
  `README.md`
- **doc**: added a "Get it from Microsoft" badge + Store deep link to
  the README and updated the Status callout to mention the Store
  availability. _artifacts_: `README.md`
- **meta**: tagged the released build as `v0.1.2` so the published
  package and the source tree stay in sync. The exact bundle on the
  Store is the one built from commit `083f3c9` (see prior entry).

## 2026-05-20 — Microsoft Store submission package

- **decision**: target the Store with an MSIX bundle built from a
  self-contained .NET publish; no separate code-signing step because the
  Store signs the bundle during ingestion. Single architecture (x64) for
  the first submission; arm64 is opt-in via a build script flag.
  _artifacts_: `packaging/store/SUBMISSION.md`
- **decision**: chose `POC/icons/12-decanter.svg` as the canonical app icon
  for the Store (SHA1 of its 256×256 render matches the 256-frame already
  embedded in `app.ico`). Background color `#1e293b` is reused as the tile
  `BackgroundColor` and the fill for non-square Wide / Splash compositions.
  _artifacts_: `packaging/store/generate-assets.py`,
  `packaging/store/Assets/`
- **code**: generated the full MSIX visual-asset set from the source SVG —
  47 PNGs covering `Square44/71/150/310`, `Wide310x150`, `SplashScreen`,
  `StoreLogo`, and `LockScreenLogo` at the required scales, plus
  `Square44x44` target-size plated + unplated variants. Also produced the
  300×300 listing logo. _artifacts_: `packaging/store/Assets/*.png`,
  `packaging/store/listing/StoreLogo-300x300.png`
- **code**: authored `Package.appxmanifest` for a WPF/.NET 8 MSIX — uses
  `Windows.FullTrustApplication` entry point + `runFullTrust` capability,
  declares a `.md`/`.markdown` file-type association, and leaves three
  documented placeholders for the per-account identity values
  (`__PARTNER_CENTER_IDENTITY_NAME__`, `__PARTNER_CENTER_PUBLISHER_ID__`,
  `__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__`).
  _artifacts_: `packaging/store/Package.appxmanifest`
- **code**: authored `build-msix.ps1` — validates placeholders, restores
  `Microsoft.Windows.SDK.BuildTools` on demand into `.tools/` (no Windows
  SDK install required), publishes self-contained per architecture, stages
  payload + Assets + manifest, runs `makeappx pack` per arch, then
  `makeappx bundle` over the staging dir. Honors `-Version` and
  `-IncludeArm64` flags. _artifacts_: `packaging/store/build-msix.ps1`
- **verify**: end-to-end build succeeded — produced
  `dist/NoteAerator-0.1.2.0-x64.msix` and `NoteAerator-0.1.2.0.msixbundle`
  (~69 MB each). Bundle re-unpacked with `makeappx unbundle` and the
  inner `.msix` with `makeappx unpack`; manifest inside reflects the
  expected Identity, VisualElements, file-type association, and
  `runFullTrust` capability.
- **doc**: wrote the listing copy under `packaging/store/listing/`
  (`description.md`, `short-description.txt`, `features.txt`,
  `whats-new.md`, `system-requirements.txt`,
  `age-rating-answers.md`) and the end-to-end Partner Center walkthrough
  in `packaging/store/SUBMISSION.md`. The walkthrough covers identity
  values, build, screenshots, pricing, properties, age rating, packages,
  store listing, system requirements, and certification notes.
  _artifacts_: `packaging/store/listing/`, `packaging/store/SUBMISSION.md`

## 2026-05-20 — Watcher reliability on OneDrive folders

- **code**: new top-level `.md` files weren't appearing in projects rooted
  in OneDrive. Root cause: the per-project
  `FileSystemWatcher` was `IncludeSubdirectories = true`, so OneDrive sync
  activity under deep descendants generated enough events to overflow the
  default 8 KB internal buffer, silently dropping the `Created` event for the
  legitimately-relevant top-level file. Fixed by switching to two
  non-recursive watchers (project root + archive subdir, the archive watcher
  spun up lazily when `archive/` first appears), raising
  `InternalBufferSize` to 64 KB, and wiring the `Error` event to
  re-enumerate + re-enable the watcher. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **verify**: `dotnet build POC/Noteaerator.sln` → 0/0; `dotnet test` →
  22/22 pass; new build launched (`bin\Debug\net8.0-windows\Noteaerator.exe`,
  PID 7984).

## 2026-05-19 — Close out GitHub issues #1, #2, #3

- **code**: issue #1 (preserve scroll on file sync) — viewer.html now tracks
  the user's last intentional scroll position via a `scroll` listener
  (`_lastUserScrollY`), suppressed during programmatic scrolls. `renderMarkdown`
  restores from that value instead of the live `window.scrollY`, so the
  transient `scrollY=0` that follows an `innerHTML` mutation when two renders
  overlap can't clobber the restore. Also debounced `FileSystemWatcher` md
  events on the C# side (180 ms `DispatcherTimer` + per-tab pending set) so
  one save no longer fires multiple back-to-back renders. _artifacts_:
  `POC/Noteaerator/Assets/viewer.html`, `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: issue #2 (Ctrl+F not awesome) — removed the WPF Ctrl+F
  `KeyBinding` and `ApplicationCommands.Find` `CommandBinding` (and the
  `OnOpenSearchExecuted` handler). With `AreBrowserAcceleratorKeysEnabled`
  defaulting to true, Ctrl+F in the WebView2 now opens Edge's native find
  bar against the current rendered file. The magnifier button in the
  header still opens the cross-project search popup. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml`, `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: issue #3 (SVG images don't render) — `PushAsync` now also
  passes a `baseUri` (the parent directory of the current `.md`, as a
  `file://` URL) to `window.renderMarkdown`. New JS helper
  `rewriteRelativeImageUrls` walks `img[src]` and resolves non-scheme
  references against `baseUri`, so `![](automated-convergence.svg)` loads
  from the markdown file's folder instead of `Assets/`. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`, `POC/Noteaerator/Assets/viewer.html`
- **verify**: `dotnet build POC/Noteaerator.sln` → 0 warnings, 0 errors;
  `dotnet test POC/Noteaerator.Tests` → 22/22 pass.

## 2026-05-02 — Project bootstrap

- **decision**: chose Option 1 (C# WPF + WebView2 + markdown-it +
  mermaid.js) for the viewer POC. Recorded in
  `POC/implementation-choices.md`.
- **code**: scaffolded `POC/Viewer/` (.NET 8 WPF, WebView2 NuGet). UI is a
  horizontal `TabControl` for projects, each containing a vertical
  `TabControl` (file list) + `WebView2` (renderer) split by a
  `GridSplitter`. `FileSystemWatcher` per project triggers re-render on
  disk change. Project list persists at
  `%APPDATA%\noteaerator\viewer\projects.json`. _artifacts_:
  `POC/Viewer/Viewer.csproj`, `POC/Viewer/MainWindow.xaml`,
  `POC/Viewer/MainWindow.xaml.cs`
- **code**: bundled `Assets/viewer.html` using markdown-it + mermaid +
  highlight.js + github-markdown-css from CDN (vendor-locally is a
  follow-up). Custom rendering for `<!-- @ai: -->` and
  `<!-- @ai-done: -->` comment markers. _artifacts_:
  `POC/Viewer/Assets/viewer.html`
- **code**: added sample notes (`POC/sample-notes/welcome.md`,
  `notes.md`) covering headings, code, tasks, mermaid, an `@ai:` comment,
  and a table — for smoke testing the viewer.
- **doc**: wrote `POC/README.md` with build/run instructions, scope of
  the POC, engineering notes, and known gaps.
- **meta**: added `bin/` and `obj/` to `.gitignore`.
- **verify**: `dotnet build` clean (0 warnings, 0 errors); `dotnet run`
  launched and stayed running for 10s without crashing (full GUI
  interaction not verified headlessly).
- **audit**: confirmed POC is **strictly read-only** on `.md` files.
  Single open site uses `FileMode.Open` + `FileAccess.Read`; HTML view
  has no `contenteditable` / form / `fetch` write paths; only writes are
  to `%APPDATA%\noteaerator\viewer\projects.json`. Documented in
  `POC/README.md`. _artifacts_: `POC/README.md`
- **rename**: renamed POC project `Viewer` → `Noteaerator`. Folder
  `POC/Viewer/` → `POC/Noteaerator/`, `Viewer.csproj` →
  `Noteaerator.csproj`, namespaces and XAML class names updated, doc
  titles refreshed. Build remains clean. _artifacts_:
  `POC/Noteaerator/**`, `POC/README.md`,
  `POC/implementation-choices.md`, `POC/sample-notes/welcome.md`
- **code**: added `POC/launch.ps1` and `POC/launch.cmd` — verifies .NET 8
  SDK, builds (`-Rebuild` for clean), launches the built `Noteaerator.exe`
  (or falls back to `dotnet run`). Smoke-tested end-to-end. _artifacts_:
  `POC/launch.ps1`, `POC/launch.cmd`
- **doc/assets**: generated 10 SVG icon options under `POC/icons/`
  (bubble-note, whisk-page, sparkle-note, wind-lift, sprout-page,
  fizzy-note, balloon-page, breathing-N, robot-reader, thought-cloud) plus
  `preview.html` rendering them at 128/64/32/16 px and a `README.md`. No
  icon picked yet — per the decision-points ground rule, awaiting user
  selection before wiring `app.ico` into the WPF project. _artifacts_:
  `POC/icons/**`
- **assets**: added 6 more icon options (total now 16) at user request,
  including the wine-glass family they suggested: `11-wine-glass`,
  `12-decanter` (the literal aerator), `13-champagne-flute`,
  `14-paper-plane`, `15-quill-sparkle`, `16-steam-mug`. Updated
  `preview.html` and `README.md`. Still no pick. _artifacts_:
  `POC/icons/**`
- **decision**: user picked icon **12-decanter**.
- **code**: built `POC/icons/svg_to_ico.py` (resvg-py + Pillow) to
  rasterize an SVG into a multi-res `.ico` (16/24/32/48/64/128/256).
  Generated `POC/Noteaerator/app.ico` from the decanter SVG. Wired
  `<ApplicationIcon>app.ico</ApplicationIcon>` into the csproj and
  `Icon="app.ico"` on the main window. _artifacts_:
  `POC/icons/svg_to_ico.py`, `POC/Noteaerator/app.ico`,
  `POC/Noteaerator/Noteaerator.csproj`, `POC/Noteaerator/MainWindow.xaml`
- **code**: added modern Fluent-ish styling — `Theme.xaml` resource
  dictionary merged via `App.xaml`, restyled `Button`, `ToolBar`,
  horizontal project tabs (underline-on-select, decanter-wine accent),
  vertical file tabs (left-border + soft fill on select). New header
  bar in `MainWindow.xaml` shows the app icon, name, and an
  ellipsis-trimmed status line. `viewer.html` got matching typography,
  a card surface with subtle border, and a polished AI-comment
  callout. _artifacts_: `POC/Noteaerator/Theme.xaml`,
  `POC/Noteaerator/App.xaml`, `POC/Noteaerator/MainWindow.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`
- **doc**: wrote `POC/comments-design.md` proposing 5 UX options
  (right-click, selection pill, margin "+", comment-mode toggle,
  keyboard shortcut), explaining MD comment-persistence conventions
  honestly (HTML comments, CriticMarkup, Obsidian `%%`, Pandoc spans,
  sidecar files) and why our `<!-- @ai: -->` choice sits in the
  standard middle. No UX or persistence picked. _artifacts_:
  `POC/comments-design.md`
- **verify**: `dotnet build` clean; built `Noteaerator.exe` launched
  with new icon + theme and stayed up in headless smoke test.
- **decision**: comments persistence = **sidecar JSON**
  (`<basename>-comments.json`); UX = **A (right-click)** + **C (hover
  margin "+")**. Lifecycle: agent processes comments and deletes the
  sidecar (or empties the array → viewer auto-deletes).
- **code**: implemented sidecar comment system end-to-end. C# side:
  `CommentStore` (load/save/add/delete with atomic temp+replace,
  auto-deletes empty sidecar), `CommentFile`/`CommentEntry`/
  `CommentAnchor` DTOs with self-doc `_purpose` field, second
  `FileSystemWatcher` for `*-comments.json`, `WebMessageReceived`
  handler for `addComment`/`deleteComment`, self-write suppression
  window. JS side: block-anchor annotation
  (`data-anchor="<heading-slug>:<block-index>"`), 3-step anchor
  resolution (exact slug+index → text-quote fuzzy → same-heading →
  "stale" footer), hover "+" margin button, custom right-click context
  menu (only inside `.markdown-body`), inline floating comment input
  (Ctrl+Enter to save, Esc to cancel), comment cards with `×` delete,
  scroll position preservation on re-render. **`.md` files remain
  strictly read-only** — every `.md` open uses `FileMode.Open` +
  `FileAccess.Read`; all writes go to sidecar JSON or
  `%APPDATA%\noteaerator\viewer\projects.json`. _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`
- **doc**: rewrote AGENTS.md "Human-in-the-loop comments" section to
  cover BOTH layers — inline `<!-- @ai: -->` markers (humans write by
  hand) AND sidecar `<basename>-comments.json` (viewer writes). Spelled
  out the agent lifecycle: read sidecar, act on each entry in the
  `.md`, delete the sidecar. _artifacts_: `AGENTS.md`
- **doc/sample**: added `POC/sample-notes/welcome-comments.json` with
  two demo entries anchored to real headings in `welcome.md` so the
  rendering shows up immediately. Updated `POC/README.md` to document
  the new UX, the sidecar lifecycle, and the refined read-only
  guarantee. _artifacts_: `POC/sample-notes/welcome-comments.json`,
  `POC/README.md`
- **verify**: build clean (0 warn / 0 err); smoke launch passed;
  read-only audit of `MainWindow.xaml.cs` confirms `.md` untouched.
- **fix**: comments could only attach to whole blocks, so a multi-row
  table accepted only one anchor at the bottom — unacceptable given how
  often Claude generates tables.
  - Extended anchor schema with `subPath` (e.g. `tr:2`, `li:0`).
  - JS annotates each `<tr>` and top-level `<li>` with its own
    `data-anchor`; hover-+ and right-click now work per row / per
    item. Row hover gets a soft highlight so the target is obvious.
  - Per-row "+" lives inside the first `<th>/<td>` so it doesn't get
    clipped by the table border.
  - Sub-anchored comment cards render after the *parent block*
    (insertion target walks up to the top-level child of `#content`),
    so a row-anchored comment never breaks table layout. The card
    shows a small "row N" / "item N" badge.
  - C# `CommentAnchor` gained `SubPath`; `addComment` handler reads it.
  - Anchor resolution: exact full anchor → parent-block fallback →
    text-quote fuzzy → same-heading → "stale" footer.
  - Sample sidecar gained a row-anchored demo on the table in
    `welcome.md`. Updated `AGENTS.md` schema docs and `POC/README.md`.
  - Build clean; smoke launch passed.
  _artifacts_: `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`,
  `POC/sample-notes/welcome-comments.json`, `AGENTS.md`,
  `POC/README.md`
- **redesign**: cards rendered too far from their anchor (after the
  parent table/list), and "+" margin button was missing/inconsistent
  for rows and items. Moved both into a single positioning **overlay
  layer** inside the `.markdown-body`:
  - Comment cards float in the **right margin** at the anchor's exact
    top, rendered as semi-transparent yellow cards (`rgba` + subtle
    `backdrop-filter: blur`) with a small dashed leader line back to
    the anchor. Multiple cards on the same anchor stack vertically.
  - "+" button is placed at the **left margin at the vertical center
    of every anchored element** — uniform position regardless of
    element type (block, table row, list item).
  - Hover wiring uses a 150ms grace timer so the mouse can travel
    between the anchor and the button without losing it.
  - Inline comment input is also overlay-positioned (anchored to the
    target's top in the right margin), with a brief outline flash on
    the target so it's clear what's being commented on.
  - `ResizeObserver` on the markdown body re-runs layout on resize,
    font-load, mermaid render, comment add/delete.
  - Stale-anchor comments still fall through to a footer section in
    normal flow.
  - Build clean; smoke launch passed.
  _artifacts_: `POC/Noteaerator/Assets/viewer.html`
- **ui**: file list (vertical TabControl) wasn't scrollable and rows
  felt heavy with many files. Overrode `FileTabControlStyle` template
  to wrap the `TabPanel` in a `ScrollViewer` (vertical bar auto-shows
  when needed). Tightened `FileTabItemStyle` (padding 12,8 → 10,4;
  font 13 → 12; removed inter-item margin; `MinHeight=0`). Switched
  the file label `TextBlock` from `TextWrapping=Wrap` to
  `TextTrimming=CharacterEllipsis` so long names show as
  `welcome….md` instead of wrapping onto two lines. Tooltip still
  shows the full path. _artifacts_: `POC/Noteaerator/Theme.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **doc**: refreshed root `README.md` to reflect the actual POC state
  — repo layout includes `POC/` tree, viewing/editing section points
  to the bundled viewer with `launch.ps1`, comments section covers
  both inline markers and sidecar JSON, and the Status section
  enumerates what's working today vs. what's not yet built.
  _artifacts_: `README.md`
- **meta**: first push of the project to `origin/main` (everything up
  to and including the POC). Single commit so the early history stays
  legible.
- **fix**: WebView2 was creating its user-data folder right next to
  the exe (`Noteaerator.exe.WebView2/`). That works in `bin\Debug` but
  would fail when installed to Program Files (read-only for non-admin).
  Switched to `CoreWebView2Environment.CreateAsync(null,
  %LOCALAPPDATA%\noteaerator\WebView2)` so per-user cache lives in the
  right place. _artifacts_: `POC/Noteaerator/MainWindow.xaml.cs`
- **code**: added `POC/install.ps1` and `POC/uninstall.ps1`.
  Installer runs `dotnet publish -c Release -r win-x64
  --self-contained true` by default (framework-INDEPENDENT, ~160 MB,
  no .NET prerequisite on the target), with `-FrameworkDependent` for
  a ~3 MB build. Self-elevates for system-wide install; `-PerUser`
  skips elevation and installs under `%LOCALAPPDATA%\Programs\`.
  Creates a Start Menu shortcut pointing at the installed exe with the
  embedded decanter icon. Writes a `.install-manifest.json` so
  `uninstall.ps1` knows what to remove. End-to-end install + launch +
  uninstall verified. Files saved as UTF-8 + BOM (Windows PowerShell
  5 falls over on BOM-less non-ASCII). _artifacts_:
  `POC/install.ps1`, `POC/uninstall.ps1`, `POC/README.md`
- **ux**: round of polish requested by the user.
  - **Branding**: `noteaerator` reads awkwardly. Window title is now
    `Note Aerator (POC)`; the in-app header shows the icon plus
    "Aerate your Notes" (no "POC" tag in the client area). csproj
    `AssemblyTitle`/`Product` set to `Note Aerator`. Start Menu
    shortcut renamed to `Note Aerator.lnk` (uninstall cleans up both
    legacy and new names). `launch.ps1` and `install.ps1` user-facing
    strings rebranded. The `noteaerator` identifier still appears in
    code namespaces, file paths under `%APPDATA%\noteaerator\`, and
    the exe filename — none of which are part of the UX surface.
  - **Status bar timestamp**: status now shows
    `welcome.md  ·  modified 5 minutes ago (2026-05-03 10:45:23)`,
    plus comment count when present. Full path moved to the status
    text's tooltip.
  - **Remove project**: the toolbar button is gone; right-click any
    project tab → "Remove project from list" with a confirmation
    prompt. Files on disk are not touched.
  - **Archive feature**: the file `TabControl` was replaced with a
    `DockPanel` containing an active-files `ListBox` (fills) and an
    `Expander` titled "Archive  (N)" docked at the bottom (collapsed
    by default).
    - Right-click an active file → "Move to Archive…" — the `.md`
      and its sidecar `<basename>-comments.json` (if present) are
      moved together to a `archive/` subfolder of the project.
    - Right-click an archived file → "Restore from Archive" — moves
      both files back to the top-level project folder.
    - Both watchers (`*.md`, `*-comments.json`) are now recursive so
      changes inside `archive/` trigger a refresh; the file list
      filters to top-level + `archive/` only (anything deeper is
      ignored).
    - Selection is mutually exclusive between the two lists; a
      `_suppressSelChange` guard prevents recursion when restoring
      selection across a refresh.
  - Build clean; smoke launch with a temp project (containing both
    active and archived `.md` files) ran without crashes.
  _artifacts_: `POC/Noteaerator/MainWindow.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Theme.xaml`,
  `POC/Noteaerator/Noteaerator.csproj`,
  `POC/install.ps1`, `POC/uninstall.ps1`, `POC/launch.ps1`
- **doc**: wrote `POC/search-design.md` capturing the search-feature
  design space (current state = no search; dimensions = filename /
  find-in-page / cross-file lexical / cross-project / sidecar /
  semantic) and three implementation tiers, including a fair pass
  over semantic options (local ONNX embeddings with model
  candidates, cloud APIs, Copilot CLI hand-off, hybrid). Listed UX
  questions and a suggested sequencing. **No path picked** per the
  decision-points ground rule. _artifacts_: `POC/search-design.md`
- **decision**: search = naive lexical scan over `.md` files +
  sidecar comments; UI = magnifier button top-right that expands a
  search panel with scope radios (whole project default, this file);
  Ctrl+F also opens the panel. Indexing deferred.
- **feature**: implemented cross-file lexical search.
  - `SearchEngine.Search(query, folderPath?, singleFile?)` does a
    case-insensitive substring scan over every `.md` in the project
    (top-level + `archive/`) and over each sidecar's
    `comments[].body`. Hits capped at 200. Returns `SearchHit`
    records (file path, line number, snippet, optional comment id +
    term used).
  - `MainWindow` gained a magnifier button in the top-right corner of
    the header, a `KeyBinding Ctrl+F` and `Esc` (via
    `ApplicationCommands.Find` / `Close`), and a 440px overlay panel
    anchored top-right with input + scope radios + results list.
    Search is debounced 180 ms; `↓`/`Enter` from the input moves to
    the results list and activates; `Esc` closes from anywhere.
  - Click / `Enter` on a hit opens the file in the active project
    (auto-expanding the Archive section if needed) and asks the
    renderer to scroll to it: `window.scrollToCommentId(id)` for
    sidecar hits, `window.scrollToText(term)` for content hits.
    Both flash the target with the existing `anchor-flash` class.
  - `ProjectTab` exposes `CurrentFile` and a new
    `OpenFileForSearch(SearchHit)` entry point; PushAsync now
    triggers the scroll script when a hit is pending.
  - Sanity-check via reflection: scanning `POC/sample-notes` for
    "mermaid" returns 3 content lines + 1 comment hit; "Sample
    sidecar" returns the comment hit. Build clean; smoke launch
    passed. _artifacts_: `POC/Noteaerator/MainWindow.xaml`,
    `POC/Noteaerator/MainWindow.xaml.cs`,
    `POC/Noteaerator/Assets/viewer.html`,
    `POC/search-design.md`
- **fix**: archiving a file used to auto-expand the Archive expander
  on every move. Removed — the expander now stays in whatever state
  the user left it. (The auto-expand still kicks in on the
  search-result jump path, where it's needed so the user actually
  sees the selected archived item.) _artifacts_:
  `POC/Noteaerator/MainWindow.xaml.cs`
- **fix**: search overlay rendered BEHIND the WebView2 content panel
  and got clipped. Root cause is the well-known WPF airspace problem
  — WebView2 is a native HWND child whose paint always wins over WPF
  z-order, regardless of `Panel.ZIndex`. Switched the search panel
  from an in-Grid `Border` overlay to a `Popup` (which hosts its own
  HWND and can correctly paint over the WebView2). Popup is
  positioned via `PlacementTarget=ProjectsTabs` with
  `HorizontalOffset` recomputed each open so the panel anchors to the
  project area's top-right with a 16px right inset. Esc still closes;
  Ctrl+F still toggles. Build clean; smoke launch passed.
  _artifacts_: `POC/Noteaerator/MainWindow.xaml`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **release**: shipped pre-built binaries via GitHub Releases.
  - Local sanity: `dotnet publish -c Release -r win-x64
    --self-contained true` -> 161.8 MB; same for `win-arm64` -> 175.5
    MB. Both ship arch-specific `WebView2Loader.dll`.
  - Single-file commit of binaries was off the table (GitHub blocks
    files >100 MB without LFS). Added
    `.github/workflows/release.yml` instead: on push of a `v*` tag
    (or manual workflow_dispatch), it publishes both architectures
    on `windows-latest`, zips each output, uploads as artifacts,
    then a separate `release` job (ubuntu-latest) downloads them and
    creates a GitHub Release with both zips attached. Uses the
    auto-injected `GITHUB_TOKEN`; no extra secrets required.
  - Wrote `INSTALL.md` covering: download (with arch selection
    snippet), Unblock step (Windows mark-of-the-web), extract,
    optional Start Menu shortcut snippet, build-from-source path,
    expected first-launch behavior, and uninstall. README's Status
    section refreshed to mention pre-built binaries and link
    INSTALL.md.
  - Tagged `v0.1.0-poc` and pushed to trigger the workflow.
  - Could not verify the run via API from this environment (the MCP
    GitHub server returned 404 for this private repo); the user can
    confirm at
    <https://github.com/rjduncan19/noteaerator/actions> and the
    release will land at /releases/tag/v0.1.0-poc.
  _artifacts_: `.github/workflows/release.yml`, `INSTALL.md`,
  `README.md`
- **infra**: installed `gh` CLI (v2.92.0) into
  `%LOCALAPPDATA%\Programs\gh` from the official release zip and
  added it to the user PATH; authenticated via the device-code
  browser flow as `rjduncan19`. This unblocks workflow / release
  verification from the agent's session.
- **fix**: the first run of the release workflow failed for both
  architectures with "Publish output is missing Noteaerator.exe".
  Root cause: `dotnet publish` resolves `-p:PublishDir` **relative
  to the project directory**, not the working directory. Passing
  `POC/Noteaerator/bin/Release/publish-...` from the repo root made
  it land at `POC/Noteaerator/POC/Noteaerator/bin/...`. Local sanity
  worked only because I had `cd`'d into the project folder first.
  Switched the workflow to compute an absolute `PublishDir` via
  `Join-Path (Resolve-Path .) "publish-<rid>"` and exported it via
  `GITHUB_ENV` so the zip step uses the same path. Force-moved the
  `v0.1.0-poc` tag to the fix commit; re-run succeeded for both
  arches and the release was created with both zips attached.
  Verified via `gh release view v0.1.0-poc`. _artifacts_:
  `.github/workflows/release.yml`
- **fix**: links inside rendered Markdown were navigating *inside*
  the WebView2, replacing the rendered note. Strictly external-only
  now.
  - C#: subscribed to `CoreWebView2.NavigationStarting` and cancel
    any navigation away from the initial `viewer.html` (allowing
    same-document fragment / `#anchor` navigation). Cancelled URI
    is shell-launched via `Process.Start(UseShellExecute=true)` so
    it opens in the user's default browser. Same treatment for
    `NewWindowRequested` (catches `target="_blank"`, `window.open`,
    Ctrl+click).
  - JS: extended the right-click menu in `viewer.html` to detect
    `<a>` ancestors. Right-click on any link now shows
    "🔗 Open link in browser" and "📋 Copy link" entries (in
    addition to the existing "Add comment here" item when the
    target is also an anchored block). Copy uses
    `navigator.clipboard.writeText` with a hidden-textarea +
    `execCommand('copy')` fallback for restricted contexts.
  - Build clean; smoke launch passed.
  - Released as **v0.1.1-poc**.
  _artifacts_: `POC/Noteaerator/MainWindow.xaml.cs`,
  `POC/Noteaerator/Assets/viewer.html`
- **doc**: wrote `POC/pipeline-design.md` capturing the analysis of
  Microsoft Store publication (cost, MSIX packaging, asset suite,
  CDN-libs gotcha, ~2-3 days of effort) and a layered "more
  professional pipeline" roadmap (PR validation + tests, release
  quality gates, code signing, MSIX, winget, Store, observability).
  Highest-leverage starting point flagged: **we have zero tests
  today**. **No path picked** per the decision-points ground rule.
  _artifacts_: `POC/pipeline-design.md`
- **decision**: pursue the "free wins" tier from
  `POC/pipeline-design.md` — extract a Core library, write tests,
  add PR-validation workflow that runs on GitHub-hosted runners
  (so it works the same in a Codespace, not a local box), generate
  SBOMs in the release workflow, and ship a winget manifest.
  Microsoft Store deferred (FTE benefits for Partner Center are a
  separate question to chase via internal HR/dev-relations
  channels).
- **refactor**: extracted **`Noteaerator.Core`** (`net8.0`,
  cross-platform) containing `SearchEngine`, `SearchHit`,
  `CommentStore`, `CommentFile/Entry/Anchor`, and `TimeFormat`
  (relative-time helper). The WPF host project now references it.
  Created an `.sln`. The Core library builds and runs on any OS
  (it's pure .NET 8); only the WPF host is `net8.0-windows`.
  _artifacts_: `POC/Noteaerator.Core/**`, `POC/Noteaerator.sln`,
  `POC/Noteaerator/Noteaerator.csproj`,
  `POC/Noteaerator/MainWindow.xaml.cs`
- **tests**: created **`Noteaerator.Tests`** (xUnit, `net8.0`,
  cross-platform) with **22 tests** across:
  - `TimeFormatTests` — relative-time formatting at every threshold
    boundary (just-now, minutes, hours, yesterday, days, months,
    years).
  - `CommentStoreTests` — sidecar path naming, default load,
    add/load round-trip, last-comment-deletes-file, atomic save
    leaves no `.tmp`.
  - `SearchEngineTests` — empty query, project-scope across files +
    `archive/`, file-scope only scans one file, case-insensitivity,
    sidecar comment-body matching, snippet ellipsis truncation,
    200-hit cap, display string formatting.
  - All 22 pass locally. _artifacts_: `POC/Noteaerator.Tests/**`
- **infra**: added `.github/workflows/pr-ci.yml` triggered on
  `pull_request` and pushes to `main`:
  - **Linux job** restores + builds Core + Tests with
    `-warnaserror` and runs `dotnet test` (writes `.trx`).
  - **Windows job** matrix builds the WPF host for both `win-x64`
    and `win-arm64` to keep the host buildable.
  - Uploads test results as an artifact.
  Concurrency-grouped per branch so a new push cancels the previous.
  _artifacts_: `.github/workflows/pr-ci.yml`
- **infra**: added `.devcontainer/devcontainer.json` based on
  `mcr.microsoft.com/devcontainers/dotnet:1-8.0` so anyone can open
  a GitHub Codespace and immediately work on Core + Tests (the WPF
  host can't run in a Linux container, but Core + Tests do — this
  is exactly why the refactor was done first). Includes the C# Dev
  Kit, GitHub PR, and YAML extensions; sets the default solution.
  _artifacts_: `.devcontainer/devcontainer.json`
- **release**: added an SBOM step to `release.yml`. Per arch, after
  the publish step, install `Microsoft.Sbom.DotNetTool` and run
  `sbom-tool generate` over the publish output, producing an SPDX
  2.2 manifest. Zip the SBOM directory into a sibling release
  asset (`NoteAerator-<tag>-<rid>.sbom.zip`). _artifacts_:
  `.github/workflows/release.yml`
- **packaging**: added a `winget` manifest under
  `packaging/winget/rjduncan19.NoteAerator/0.1.1-poc/` (three YAML
  files per winget v1.6 schema: version, locale, installer).
  Treats the app as a `portable` installer wrapped in a `zip`,
  pointing at the GitHub Release zip URL with the SHA256 of each
  arch (computed via `gh release download`). Added `LICENSE` (MIT,
  referenced by the manifest) and a `packaging/winget/README.md`
  with local-test, submission, and update-procedure instructions.
  _artifacts_: `packaging/winget/**`, `LICENSE`
- **doc**: refreshed `POC/pipeline-design.md` to reflect what was
  actually shipped: Layer A (tests + PR CI + Codespaces) is ✅,
  Layer B is 🟡 (SBOM done, release-time test gate + coverage still
  ⬜), Layer E is 🟡 (winget manifest authored / locally installable;
  upstream submission still ⬜). Layers C/D/F/G remain ⬜. Status
  markers added throughout. Decision section closed with the
  free-wins tier and remaining open question about FTE Partner
  Center benefits. _artifacts_: `POC/pipeline-design.md`
- **doc**: addressed two of the Microsoft-Store gotchas listed in
  `pipeline-design.md`:
  - **Privacy URL.** Wrote `PRIVACY.md` at the repo root (clear
    "we collect nothing" policy with full disclosure of the only
    network calls -- the jsDelivr CDN fetches for markdown-it /
    mermaid / highlight.js / github-markdown-css). Linked from
    README and INSTALL. Store URL would be
    `https://github.com/rjduncan19/noteaerator/blob/main/PRIVACY.md`.
  - **WebView2 Evergreen runtime.** Added an explicit startup check
    in `App.xaml.cs.OnStartup`: calls
    `CoreWebView2Environment.GetAvailableBrowserVersionString()` and,
    if missing or unusable, shows a friendly `MessageBox` with a
    "Open download page now?" prompt linking to
    `developer.microsoft.com/microsoft-edge/webview2/` then
    `Shutdown(2)`. Documented the Evergreen choice (no fixed
    runtime bundled) in `INSTALL.md` and `POC/README.md`. Updated
    `pipeline-design.md` to mark gotchas 3 + 4 as ✅.
  _artifacts_: `PRIVACY.md`, `POC/Noteaerator/App.xaml.cs`,
  `INSTALL.md`, `POC/README.md`, `README.md`,
  `POC/pipeline-design.md`

- **doc**: extended `POC/implementation-choices.md` with (a) a "Future-fit"
  section showing how WYSIWYG editing (Milkdown / ToastUI / TipTap) and
  comment UX layer onto Option 1, and (b) a "should we just use Milkdown
  or Obsidian today?" reality-check section with three honest framings.
  No implementation choice made — left pending per the new
  decision-points ground rule. _artifacts_: `POC/implementation-choices.md`

- **doc**: created `POC/implementation-choices.md` capturing the five viewer
  POC options (C#+WebView2, C#+Markdig, C++/Qt, Electron/Tauri, static HTML),
  decision criteria, comparison table, and recommendation. Decision section
  left pending for the user. _artifacts_: `POC/implementation-choices.md`

- **ground-rule**: added "Decision points: stop and ask" section to
  `AGENTS.md`. When the user asks for options to decide, the agent must
  stop after presenting them and not autopilot into picking one and
  executing. Triggered by autopilot picking Option 1 for the viewer POC and
  starting to scaffold without waiting. Also rolled back the partial
  scaffold under `apps/viewer`. _artifacts_: `AGENTS.md`

- **clarification** (added later same day): the work-tracking workflow is
  meta — it documents how this repo is being built. It is **not** a feature
  the noteaerator product enforces or prescribes for end users. Updated
  `AGENTS.md`, `README.md`, and this file to make the meta vs. product
  distinction explicit. _artifacts_: `AGENTS.md`, `README.md`,
  `docs/worklog.md`

- **decision**: adopted Markdown as the canonical artifact format for all
  durable content — easiest for humans to read and for AI to manipulate.
  _artifacts_: `README.md`
- **decision**: established a two-tier storage model — durable artifacts
  (committed + synced via Google Drive / OneDrive) vs. transient artifacts
  (under `./.tmp/`, gitignored, not synced). _artifacts_: `README.md`,
  `AGENTS.md`, `.gitignore`
- **decision**: human → AI comments in Markdown use the convention
  `<!-- @ai: ... -->`; agents address and remove them on the next pass.
  _artifacts_: `AGENTS.md`, `README.md`
- **decision**: development of this project is tracked in two places — the
  per-session SQL store (`work_log` table + `todos`) for live tracking, and
  `docs/worklog.md` for the durable history that survives across sessions.
  _artifacts_: `AGENTS.md`, `docs/worklog.md`
- **doc**: expanded `README.md` from a one-line tagline to a full vision
  document covering AI-first input, storage/sync model, viewing/editing,
  human comment convention, and current status. _artifacts_: `README.md`
- **doc**: created `AGENTS.md` capturing the work-tracking workflow,
  storage/sync conventions, the human-comment marker, and house rules for
  agents. _artifacts_: `AGENTS.md`
- **meta**: created `.gitignore` to exclude `./.tmp/` and common transient
  artifacts from version control. _artifacts_: `.gitignore`
- **meta**: deferred creation of `notes/`, `projects/`, `tasks/` directories
  until real content exists, to avoid speculative scaffolding.
