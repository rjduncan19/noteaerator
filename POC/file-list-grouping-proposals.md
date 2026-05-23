# File-list prefix grouping — UX proposals

Treating dash-separated prefixes (e.g. `corp-anthro-app-staff`,
`corp-anthro-plan`, `corp-crwd-cory`) as nested folders in the file
list pane, with expand/collapse controls.

> Status: **awaiting decision**. This document captures three
> proposals + the up-front design questions so we can pick one path
> before any code lands.

---

## What we'd actually see in your Security folder

The files there use numeric-prefix sort keys (`00-`, `01-`, `20-`, …),
not the `corp-anthro-*` pattern of the example, so the example was
illustrative. If we split on `-`, skipped the numeric sort key, and
grouped where ≥ 3 files share a token, your `OneDrive\Career\Security`
folder would naturally produce groups like:

| Group       | Members                                                                                            |
| ----------- | -------------------------------------------------------------------------------------------------- |
| `anthropic` | `30-anthropic-deep-dive`, `34-anthropic-cover-letter-draft`, `40-anthropic-full-job-audit`         |
| `meeting`   | `20-stuart-meeting-prep`, `21-kangsu-meeting-prep`, `23-brian-meeting-prep` (matches 2nd token)    |
| `target`    | `04-target-roles`, `14-target-positions`                                                           |
| `deep-dive` | `27-sentinelone-deep-dive`, `28-openai-deep-dive`, `30-anthropic-deep-dive`                        |

Some files would belong to two potential groups (`30-anthropic-deep-dive`
is both an "anthropic" and a "deep-dive"). How we resolve that is one
of the design decisions below.

---

## Design decisions to lock first (D1–D6)

| #  | Question                                                                                                         | Recommended default                                   |
| -- | ---------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| D1 | Treat a leading numeric token (`30-`) as a sort key or as the first grouping token?                              | **Sort key only** — never group by it.                |
| D2 | One level of nesting only, or recurse until prefixes diverge (arbitrary depth like `corp-anthro-app-staff`)?     | **Recurse, cap at 3 levels.**                         |
| D3 | A file whose first token is unique — 1-item group, or stay at top level?                                         | **Stay at top level** (1-item groups are noise).      |
| D4 | Inside a group, show the full filename or strip the redundant prefix?                                            | **Strip** — otherwise the hierarchy is unreadable.    |
| D5 | Persist expand/collapse state? Per-folder or global?                                                             | **Per-folder, persisted in `projects.json`.**         |
| D6 | Always on, opt-in per project, or auto-detect when ≥ 3 files share a prefix?                                     | See proposals below — this is what we're choosing.    |

---

## Proposal A — "Quiet grouping" (auto, minimal chrome)

Group whenever ≥ 3 files share a leading token. Render as collapsible
disclosure triangles with the children slightly indented. No toggle,
no settings — it just happens.

```
📁 POC
├─ ▾ anthropic                    (3)
│    30 deep-dive
│    34 cover-letter-draft
│    40 full-job-audit
├─ ▾ meeting-prep                 (3)
│    20 stuart
│    21 kangsu
│    23 brian
├─ ▸ target                       (2)        ← collapsed
├─ 00 running-status
├─ 01 profile-value-proposition
├─ 02 2026-security-landscape
└─ …
   ⌄ Archive
```

- **Pros**: Zero configuration. Behaves like a file explorer.
- **Cons**: Files reshuffle as you add/remove things — adding a 3rd
  file with prefix X suddenly creates a group; deleting it un-groups
  again. No escape hatch for "I don't want this grouped."
- **Thresholds**: count ≥ 3, max depth 3, singletons top-level.

## Proposal B — "Explicit grouping" (toggle per project) ⭐

Same visual model as A, but it's opt-in. Add a small **⤢ Group**
toggle button at the top of the file list (next to the project tab
strip). Off by default; user flips it on per project, state is saved
to `projects.json`.

```
┌──────────────────────────────────────────┐
│ POC | testing | Security | Food          │
│  ◯ Group  ⓘ                              │   ← compact toolbar above the file list
├──────────────────────────────────────────┤
│ ▾ anthropic                              │
│     30 deep-dive                         │
│     34 cover-letter-draft                │
│     40 full-job-audit                    │
│ ▾ meeting-prep                           │
│ …                                        │
└──────────────────────────────────────────┘
```

- **Pros**: Predictable — no surprise reshuffles. Easy to A/B for
  yourself before committing.
- **Cons**: One more knob. People forget it exists.
- **Defaults**: same thresholds as A, exposed in a "⚙ Grouping…"
  popover (min count, max depth, ignore numeric prefix, strip
  redundant tokens).

## Proposal C — "Pivot view" (segmented control, three modes)

Replace today's flat list with a segmented control offering
**List · Grouped · Tags**, all driven from the same underlying file
set.

```
┌──────────────────────────────────────────┐
│ View:  [List] [Grouped] [Tags]           │
├──────────────────────────────────────────┤
│ List:    flat alphabetical (today)       │
│ Grouped: prefix-based tree (proposal A/B)│
│ Tags:    flat list, but pills next to    │
│          each file showing its tokens —  │
│          click a pill to filter          │
└──────────────────────────────────────────┘
```

- **Pros**: Doesn't force a hierarchy on anyone. Tags mode is great
  when prefixes overlap multiple groupings (e.g. "anthropic" *and*
  "deep-dive") — you can pivot either way.
- **Cons**: More to build (tags pills + click-to-filter is a real
  feature). Probably overkill for v1.

---

## Recommendation

Start with **Proposal B (explicit toggle, off by default)**, defaults
`min=3, depth=3, strip-prefix=yes, ignore-leading-numeric=yes`,
persisted per-project. It captures 90% of the value of A with none of
the "why did my list suddenly change?" surprise, and leaves the door
open to layer C's pivot view later without ripping anything out.

<!-- @ai: pick one of the three proposals (or tweak the defaults) and
     I'll implement it. -->
