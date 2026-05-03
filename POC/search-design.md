# Search -- design discussion

Captured 2026-05-03. The viewer POC has **no search functionality
today** -- not filename filter, not in-page find, not cross-file, not
semantic. This doc lays out the design space so we can pick a path
deliberately. **No path is committed yet.**

## Current state

- The active-file `ListBox` shows whatever `*.md` lives at the
  top-level of a project folder (and `archive/`). No filter input.
- The `WebView2` does not surface any built-in find UI; there is no
  Ctrl+F overlay.
- Sidecar comments (`<basename>-comments.json`) are not searchable
  from the app at all today.

## Search dimensions

| Scope                                        | Surface                                                   | Today | Effort  |
|---|---|---|---|
| Filter file list by filename                 | Textbox above the file list                               | none  | trivial |
| Find in current file                         | Ctrl+F overlay in the renderer                            | none  | small   |
| Content search across project                | Results panel (file -> matched line excerpts)             | none  | medium  |
| Content search across all projects           | Same panel, multi-project                                 | none  | medium  |
| Search inside sidecar comments               | Folded into content search                                | none  | small (additive) |
| Semantic / "meaning" search                  | "Files about X" rather than "files containing X"          | none  | large   |

These layer; they're not mutually exclusive.

## Implementation tiers

### Tier 1 -- low risk, high value

1. **Filename filter.** Textbox above the active list; substring
   match (case-insensitive). Optionally narrows the Archive section
   too. ~30 lines XAML + code-behind.
2. **Find-in-page.** Small overlay in `viewer.html` (Ctrl+F brings it
   up). Uses `window.find()` plus a custom highlight pass that wraps
   matches in `<mark>` spans. Current file only. The renderer already
   owns the DOM; we just decorate it.

### Tier 2 -- cross-file lexical

3. **Naive scan.** Walk all `*.md` under the project (and
   `archive/`), substring match in memory. Show results as
   `file -> line N: ...context...`. Fine for libraries up to a few
   thousand small files (~50-100ms for ~500 files).
4. **Indexed (SQLite FTS5).** SQLite ships with full-text search
   built in. Index every paragraph or chunk; query supports
   stemming, prefix, AND/OR, phrase. Sub-millisecond queries even at
   tens of thousands of files. Rebuild on `FileSystemWatcher` events
   (debounced). A bigger investment but proportionally faster and
   richer.

5. **Comment search**, folded into 3 or 4 -- read each sidecar
   JSON's `comments[].body`, treat each as another searchable doc
   tagged with the source `.md`.

### Tier 3 -- semantic search

This is where the choices actually matter.

#### A. Fully local embeddings (ONNX Runtime + small open model)

- Candidate models (all roughly equivalent for noteaerator's likely
  scale):
  - **bge-small-en-v1.5** -- ~33M params, ~130 MB, 384-dim;
    consistently strong for size.
  - **all-MiniLM-L6-v2** -- ~22M params, ~80 MB, 384-dim; the classic.
  - **E5-small-v2** -- Microsoft, similar size; multilingual variant
    available.
- Storage: SQLite with **sqlite-vec** extension, or a simple in-memory
  ANN such as USearch / hnswlib bindings.
- Pipeline: chunk each `.md` (paragraph or token-window), embed,
  store `(vector, file, char-range)`. Query -> embed query string ->
  cosine top-K -> render results with the matched chunk plus an
  "open at this location" link.
- Runtime cost: ~50 ms to embed a query; one-time ~10-30 s to embed
  a few hundred files; <5 ms retrieval.
- **Pros:** offline, private, no API key, deterministic, fits the
  "AI-first but local" feel of noteaerator.
- **Cons:** ships ~80-150 MB of weights; ONNX Runtime adds ~30 MB
  native deps; re-embedding on edits costs CPU (mitigate with
  per-file change detection + debounce).

#### B. Cloud embeddings (OpenAI / Azure / Cohere)

- ~5 MB code surface; just HTTP.
- Better quality at the top end (e.g. `text-embedding-3-large`
  beats most small local models).
- Costs money, requires network + API key, conflicts with the
  implicit "works on a laptop without internet" goal.

#### C. Hand off to Copilot CLI

- "Ask Copilot" button passes the query plus the project path to
  `copilot` (or composes a prompt).
- Cheapest to build (a few lines).
- Best fit for the AI-first DNA, but gives up the
  "viewer is self-contained" property and depends on the CLI being
  installed and configured.

#### D. Hybrid lexical + semantic

- BM25 (FTS5) for exact terminology + dense embeddings for
  paraphrases. Combine via reciprocal rank fusion (a few lines).
- ~1.5x the work of A alone but gives state-of-the-art results for
  small libraries.

## UX questions worth deciding before building

1. **Search box location.** Top of the file pane (acts as filter),
   header (global), or a `Ctrl+K` palette?
2. **Results presentation.** Replace the file list, slide-out panel,
   separate window?
3. **Scope toggle.** "This project" vs "all projects" vs "include
   archive" -- checkboxes with persisted preference?
4. **Click behavior.** Open the file and scroll/highlight to the
   match, or just open the file?
5. **In-rendered-view highlighting.** Keep the search term
   highlighted in the rendered MD after click?
6. **Comments in or out of search?** Default in (they're knowledge
   content too); some users may want a private-by-default mode.
7. **Index storage.** Per-project (alongside notes -- but then it
   syncs through OneDrive, bad for a binary index) vs per-machine
   (`%LOCALAPPDATA%\noteaerator\index\<hash>.db`, recommended).

## Suggested sequencing

1. **Tier 1**: filename filter + find-in-page. Half a day, instant
   value.
2. **Tier 2 naive scan**: covers 80 % of cross-file need without an
   index.
3. **Tier 2 FTS5 index**: when speed/quality of (2) becomes a
   problem.
4. **Tier 3A local ONNX semantic**: hybrid (D) is the natural
   endpoint.
5. **Tier 3C "Ask Copilot" button**: orthogonal, cheap, can be
   added at any tier.

## Two non-obvious considerations

- **Watcher integration.** Any index we build must update on
  `FileSystemWatcher` events. The infrastructure is already there;
  OneDrive sync events will fire and that is fine -- just debounce.
- **Anchor reuse.** If we add semantic chunks, the existing sidecar
  comment anchor scheme (`headingSlug + blockIndex + textQuote`)
  generalizes. Search results can deep-link to a specific block,
  and the renderer already knows how to scroll/highlight there.

## Decision

**2026-05-03 — Tier 1 partial + Tier 2 naive scan selected.** Build
cross-file lexical search over `.md` content **and** sidecar
comment bodies via a simple in-memory scan (no FTS5 index).
UX: magnifier button in the top-right of the header expands a small
overlay panel with a search box and scope radios (default "whole
project", optional "this file"). `Ctrl+F` also opens the panel; `Esc`
closes it. Hits cap at 200. Click → open the file and scroll/flash to
the match (text or comment).
Semantic search (Tier 3) is deferred.
