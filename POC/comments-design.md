# Comments — UX and persistence

The viewer already understands the `<!-- @ai: ... -->` /
`<!-- @ai-done: ... -->` markers from `AGENTS.md` and renders them as
sticky-note callouts. What's missing is a **read-mode UX for adding a
new comment without typing the syntax** and a clear story for **how
those comments persist**.

This doc presents options for both. No picks yet.

---

## Persistence: is there a standard for comments in Markdown?

Honest answer: **there is no single universal standard**, but there are
several established conventions, and the one we're already using is
right in the middle of them. We are not making something up from
nothing.

| Convention | Syntax | Status | Notes |
|---|---|---|---|
| **HTML comments** | `<!-- text -->` | ✅ Universal in HTML; pass through every CommonMark/GFM renderer as no-op | What GitHub uses for hidden notes in issue/PR templates. Our `<!-- @ai: ... -->` is an HTML comment with a custom prefix. |
| **CriticMarkup** | `{>>comment<<}`, `{++ins++}`, `{--del--}`, `{~~old~>new~~}`, `{==hl==}{>>note<<}` | 🟡 *De-facto* standard for editorial markup. Supported by iA Writer, Marked 2, MultiMarkdown, several Pandoc filters. | The closest thing to a real standard for comments / changes in MD. Not in CommonMark/GFM core, so most renderers show the raw braces unless they have a plugin. |
| **Obsidian comments** | `%%text%%` | 🟠 Obsidian-only. Hidden in preview, visible in source. | Tool-specific. |
| **Pandoc spans** | `[text]{.comment}` | 🟠 Pandoc-only. Renderable to HTML/PDF with custom styling. | Heavier syntax, niche. |
| **Sidecar files** | `notes.md.comments.json` next to `notes.md` | 🟠 No standard. **Hypothes.is** uses a server-side equivalent with text-quote selectors. | More flexible (anchor + author + timestamp + resolved state) but doubles the sync surface and can drift if the source file edits invalidate anchors. |

### What we already chose, and why

`AGENTS.md` standardized on `<!-- @ai: ... -->` HTML comments. That is:

- **Standard syntax** (HTML comments — universally renderable as no-op).
- **A custom semantic prefix** (`@ai:`) that's our convention, not a
  standard, and that's fine — it's just a discriminator inside a
  standard wrapper.
- **Trivially git-diffable** and survives any sync (it's just text in
  the file).
- **Round-trips** through every Markdown editor / converter without
  loss.

So we're not inventing the syntax, only the semantic prefix. The same
principle is what GitHub and many tools do.

### Options for *where* comments live

1. **Inline HTML comments only** *(current convention)*
   - **Pros:** Single source of truth; trivial sync; no anchor drift;
     git diff is meaningful; works in any other Markdown tool.
   - **Cons:** Hard to attach to a specific *span* of text without
     cluttering the source. Author / timestamp / resolved state would
     need to be encoded inline, e.g.
     `<!-- @ai: 2026-05-02 rd | tighten this paragraph -->`.

2. **Inline HTML comments + opt-in sidecar**
   - Inline comments stay simple and human-friendly.
   - A sidecar `<file>.comments.json` is *only* generated when a comment
     needs richer metadata (a span anchor, a thread of replies, a
     "resolved" toggle that's not just a delete).
   - **Pros:** Best of both — simple cases stay simple; rich cases get
     a real structure; sidecars are gitignorable if you don't want
     them.
   - **Cons:** Two storage paths to keep in sync; sidecar anchors can
     drift when source edits.

3. **CriticMarkup**
   - Use `{>>...<<}` for comments and the rest of CriticMarkup for
     suggested edits.
   - **Pros:** Closest thing to a real standard; opens the door to
     "track changes" semantics later.
   - **Cons:** Renders ugly in any tool that doesn't understand it
     (most tools); a stronger lock-in than HTML comments.

4. **Sidecar-only** (Hypothes.is-style)
   - **Pros:** Source `.md` stays pristine; rich metadata trivial.
   - **Cons:** Anchor brittleness, doubled sync surface, comments
     invisible in any other tool that opens the file.

---

## UX options for adding a comment in read-only mode

The viewer is read-only on `.md` files **today**. Adding a comment is a
*write* — so any UX here implies adding a careful, scoped write path
(append a comment to the file; nothing else). This is much smaller in
scope than full WYSIWYG editing.

### Option A — Right-click context menu
- Selection (or right-click on a paragraph) → "Add @ai comment here".
- A small modal/inline form appears, you type, hit Enter.
- The viewer atomically appends `<!-- @ai: ... -->` immediately after
  the targeted block in the source `.md`.
- **Pros:** Familiar Windows pattern; zero chrome until you need it;
  works without a selection (paragraph anchor).
- **Cons:** Less discoverable; right-click isn't obvious to all users.

### Option B — Floating "Comment" button on text selection
- Like Medium / Google Docs: when you select text, a small
  "💬 Comment" pill appears near the selection.
- Click it → inline editor → save → appended to source.
- **Pros:** Highly discoverable; natural mental model ("I want to
  comment on *this*").
- **Cons:** Requires a real text selection (more cumbersome on
  paragraphs you just want to flag); needs a JS bridge from the
  WebView2 page back to C# to write the file.

### Option C — Margin / gutter affordance ("+" on hover)
- On hover over a paragraph, a small "+" appears in the left margin
  (GitHub PR style). Click it → comment for that block.
- **Pros:** Discoverable, calm UI, block-anchored (no need to select
  text).
- **Cons:** More layout work; needs the renderer to anchor each block
  with a stable id (line number or heading slug).

### Option D — Top toolbar button + "comment mode"
- Add a "💬 Comment" toggle button in the top toolbar. When on, the
  cursor changes and clicking any block opens a comment input for it.
- **Pros:** Simple to implement; very obvious.
- **Cons:** Modal feel ("you're now in comment mode"); two-step.

### Option E — Keyboard shortcut + inline prompt
- `Ctrl+/` opens a comment prompt anchored to the currently scrolled-to
  block (or a chosen heading from a dropdown).
- **Pros:** Fast for keyboard users; minimal UI surface.
- **Cons:** Invisible until you know the shortcut.

### Combinations are reasonable
- **A + B + E** is a natural set: right-click for casual users,
  selection-pill for discoverability, shortcut for power users — all
  three feeding the same "append comment to source" code path.
- **C** is more architecturally invasive (block anchoring) but pays off
  if we later want margin-anchored *threads*.

---

## What I'd recommend (advisory, not picked)

For the POC I'd lean toward:

- **Persistence:** stay on **inline HTML comments** today. Defer
  sidecars until we hit a concrete need (multi-author threads,
  per-comment resolved state, or comments on a *span* rather than a
  block).
- **UX:** start with **Option A (right-click) + Option B (selection
  pill)** because they share the same write path and cover both
  "comment on this paragraph" and "comment on this exact phrase". Add
  the shortcut later for free.

But this is a real fork in the road — tell me which persistence and
which UX (or which combination) you want, and I'll implement.
