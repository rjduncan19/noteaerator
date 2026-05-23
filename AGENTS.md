# AGENTS.md

Operating instructions for AI agents (Copilot CLI and others) working in this repository.

## Project context

`noteaerator` is an AI-first rich editing workspace for notes, ideas, tasks, and
project state. The primary input channel is the Copilot CLI; agents generate and
maintain durable artifacts (mostly Markdown) that humans view in a Markdown
renderer (baseline: Microsoft Edge with a Markdown viewer extension) or edit in
a lightweight WYSIWYG editor. See `README.md` and `docs/` for the full vision.

## Work tracking workflow (REQUIRED — meta / build-time only)

> **Scope note.** This workflow governs how *this repository is developed*.
> It is **not** a feature of the `noteaerator` product and should not leak
> into product design, end-user docs, or any conventions the product
> prescribes for its own users. We track our build process so future humans
> and agents can see how `noteaerator` came to be; we are not declaring that
> every noteaerator workspace must do the same.

Every meaningful unit of work performed by an agent **on building this
repo** MUST be recorded so we have a clear, queryable history of how the
project was developed. This applies to design decisions, scaffolding, doc
changes, code changes, experiments, and explicit "did nothing because X"
determinations.

### Where work is tracked

Work is tracked in **two complementary places**:

1. **Session SQL store** — the per-session SQLite database that the Copilot CLI
   exposes via the `sql` tool. Use it for live, structured tracking during a
   session (todos, dependencies, in-progress status, and a `work_log` table for
   the running activity log).
2. **`docs/worklog.md`** — the durable, human-readable changelog that is
   committed to the repo. It is the source of truth across sessions, since the
   session DB is ephemeral.

### Required actions per session

At the **start** of a session that does any non-trivial work:

1. Ensure the `todos`, `todo_deps`, and `work_log` tables exist (the `sql`
   tool auto-creates `todos` and `todo_deps`; create `work_log` if missing —
   schema below).
2. Read the tail of `docs/worklog.md` to pick up prior context.
3. Insert/refresh todos for the work you plan to do, and set `status` to
   `in_progress` on the one you are actively working.

During the session:

4. Append a row to `work_log` for each meaningful step (decision, artifact
   created/changed, experiment run, blocker hit). Keep entries terse; link to
   files or commits in `artifacts` when relevant.
5. Update todo `status` as work moves (`pending` → `in_progress` → `done` /
   `blocked`).

At the **end** of the session (before `task_complete`):

6. Flush the new `work_log` rows for this session into `docs/worklog.md` as a
   new dated section so the durable log stays in sync.
7. Make sure all touched todos have a final status.

### `work_log` schema

```sql
CREATE TABLE IF NOT EXISTS work_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  timestamp TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  category TEXT NOT NULL,            -- e.g. decision | doc | code | experiment | blocker | meta
  summary TEXT NOT NULL,             -- one-line description
  details TEXT,                      -- optional longer notes / rationale
  artifacts TEXT,                    -- comma-separated paths, URLs, or commit SHAs
  related_todo TEXT                  -- optional todos.id this entry belongs to
);
```

### `docs/worklog.md` format

Append-only. One H2 section per session, newest at the top of the "Sessions"
list. Each entry is a bullet with category, summary, and (optionally) artifacts.

```markdown
## YYYY-MM-DD — short session title

- **decision**: chose Markdown as the canonical artifact format — simplest for
  both humans and AI to read/write. _artifacts_: `README.md`
- **doc**: expanded README with vision, principles, and storage model.
  _artifacts_: `README.md`
```

## Storage and sync conventions

These conventions exist so durable artifacts sync cleanly across machines and
transient junk does not.

- **Durable artifacts** (committed to the repo and/or kept in the synced drive
  root): `README.md`, `AGENTS.md`, `docs/**`, `notes/**`, `projects/**`,
  `tasks/**`, status files, ground rules, and any other long-lived Markdown.
- **Transient artifacts** (must NOT sync): one-off scripts written to satisfy
  a single prompt, downloaded dependencies (`node_modules`, virtualenvs),
  build outputs, scratch data. Place these under a temp directory — by
  default `./.tmp/` inside the repo (gitignored), or the OS temp dir if they
  do not need to be near the repo at all.
- When in doubt, ask: "Will a human or future agent want to read this in a
  week?" If yes → durable. If no → temp.

## Human-in-the-loop comments in Markdown

Two layers, both supported by the noteaerator viewer:

### 1. Inline `<!-- @ai: ... -->` markers (free-form, written by hand)

Drop an HTML comment beginning with `@ai:` anywhere in a Markdown file:

```markdown
<!-- @ai: rewrite this paragraph to focus on the sync story -->
```

- Marker: HTML comment beginning with `@ai:` (case-insensitive).
- Survives any Markdown renderer; noteaerator renders it as a yellow
  callout.
- Agents should scan for `<!-- @ai:` when reading a file, address each
  comment, and remove the comment once the request is handled (or
  convert it to a `<!-- @ai-done: ... -->` note if the history is
  useful).

### 2. Sidecar `<basename>-comments.json` (viewer-driven, anchored)

When a human adds a comment via the noteaerator viewer (right-click on a
block, or the hover "+" margin button), it is **not** written into the
`.md` file. Instead it is stored in a sidecar JSON file alongside it:

```
project/
  welcome.md
  welcome-comments.json    ← sidecar, only present when comments exist
```

This protects the `.md` file from sync conflicts and keeps git diffs of
the source clean. Schema:

```json
{
  "_purpose": "Human comments... agents should DELETE this file when done.",
  "version": 1,
  "comments": [
    {
      "id": "...",
      "createdAt": "2026-05-02T19:30:00Z",
      "anchor": {
        "headingSlug": "introduction",
        "blockIndex": 2,
        "subPath": "",
        "textQuote": "First 80 chars of the anchored block..."
      },
      "body": "the human's comment text"
    }
  ]
}
```

**Anchor fields:**

- `headingSlug` — slug of the most recent heading above this block
  (resets the block counter).
- `blockIndex` — 0-based index of the top-level block under that
  heading.
- `subPath` *(optional)* — for granular anchors *within* a block.
  Currently supported:
  - `"tr:N"` — the Nth `<tr>` of a table (header row is `tr:0`).
  - `"li:N"` — the Nth top-level `<li>` of a list.
  - Empty string or omitted = anchored to the whole block.
- `textQuote` — first ~80 chars of the anchored block's plain text;
  used as a fuzzy fallback if the structural anchor no longer
  matches.

#### Lifecycle (REQUIRED for agents)

When an agent processes a `.md` file, it MUST also check for a sibling
`<basename>-comments.json` and:

1. Read every comment in the array.
2. Act on each one (apply the requested change, answer the question,
   etc.) directly in the `.md` file.
3. **Delete the entire sidecar file when done** (`rm welcome-comments.json`).
   The viewer will also auto-delete the file if you instead remove all
   entries from the `comments` array, but deletion is the canonical
   "fully processed" signal.

If a comment cannot be addressed (e.g. you need clarification), leave
that one entry in place and remove the others; the human will see the
remaining one(s) on next refresh.

## House rules for agents

- Prefer editing existing files over creating new ones.
- Do not create speculative scaffolding "for later" — add structure only when a
  concrete need exists, and log the decision in `work_log`.
- Keep commits small and described in terms of user-visible behavior.
- If a workflow rule in this file is unclear or seems wrong, log a `meta`
  entry in `work_log` proposing a change rather than silently deviating.

## Decision points: stop and ask

When the user explicitly asks for **information to make a decision** (phrases
like "give me options", "help me decide", "present me with choices", "weigh
X vs Y", "what are good options", or any prompt whose primary purpose is
comparison/recommendation), treat it as a hard stop:

- Present the options with the requested trade-offs.
- **Stop and wait for the user's choice.** Do not pick one and start
  executing in the same turn — even under autopilot, even if a recommendation
  is given. The point of the request was the decision, not the implementation.
- A recommendation is fine and encouraged, but it is not consent.
- After presenting options, **call `task_complete` with the summary**.
  Returning a recommendation IS finishing the task; you are not "leaving
  work undone" by stopping there. The autopilot "keep going" reminder does
  NOT override this rule. Do not do *any* preparatory work toward the
  recommended option ("just a small unrelated fix while I'm here", "tiny
  workaround", scaffolding files, branch prep) — that is still acting on
  the decision without consent.
- If a subsequent turn delivers the user's choice, *then* implement it.
- If `ask_user` returns "user not available", do **not** silently proceed on
  your recommendation. Either (a) call `task_complete` summarizing the
  options and noting that the user's choice is required to continue, or
  (b) if the request truly is unblockable without a choice, pick the
  recommended option and **clearly flag** in the response that you proceeded
  without confirmation and will revert/redo if the user prefers another
  option.
- Never collapse a "help me decide" question into a one-second pick followed
  by code generation. That defeats the purpose of asking.

