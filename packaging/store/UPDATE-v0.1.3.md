# Update Note Aerator to v0.1.3 on the Microsoft Store

Everything you need for this release, inline. No need to flip between
files.

## What changed in this release

Two big features and one reliability fix:

- **Prefix grouping in the file list.** Files that share dash-separated
  prefixes (e.g. `corp-orcl.md` and `corp-orcl-thomas.md`) now collapse
  into an expandable tree in the sidebar. Right-click a project tab to
  toggle **Group by prefix** on/off; the setting is saved per project.
  Smart touches: leading numeric tokens like `30-` are sort keys (not
  group keys), `<prefix>-overview.md` acts as the anchor for `<prefix>`,
  and meaningless single-child wrappers are auto-collapsed
  (`corp` containing only `orcl` renders as `corp-orcl` directly).
- **Getting-started experience.** First-time users now land on a
  welcoming three-document project at `Documents\Note Aerator\`
  instead of an empty window. Walks through the 30-second tour,
  search, archive, sidecar comments, AI directives, prefix grouping,
  Mermaid, math, and task lists. The seeded project is yours to edit,
  rename, or delete.
- **Reliability fix.** The `projects.json` reader is now
  case-insensitive so existing installs are not affected by the
  schema bump.
- **`file://` links now clickable.** Markdown links to local files
  and folders (e.g. `[Open folder](file:///C:/path/to/folder)`) are
  no longer silently dropped by the renderer. Clicking one opens
  Explorer's **Reveal in Folder** view — files are never auto-launched
  through their default handler, so a `.exe` or `.bat` link can't run
  by accident.

---

## Step 1 — open the bundle's folder

Click the link below — it opens the build output folder in File
Explorer:

[📁 Open dist folder](file:///C:/Users/richardd/source-rjduncan19/noteaerator/packaging/store/dist)

You should see:

| File                              | Size    | Notes                  |
| --------------------------------- | ------- | ---------------------- |
| `NoteAerator-0.1.3.0.msixbundle`  | 68.8 MB | **upload this**        |
| `NoteAerator-0.1.3.0-x64.msix`    | 68.8 MB | bundled — don't upload |

SHA256 of the bundle (for your records):
`5F971D925CCB46661E0DE48F41D0FE5CECE58956566677A99D51816E0C6DEA84`

---

## Step 2 — start a new Partner Center submission

[🌐 New submission](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

On the Submissions page, click **New submission**. The form is
pre-filled with everything from your live v0.1.2 — pricing,
properties, description, screenshots, age rating, etc. **Only the
two sections below need changes.**

> If Partner Center bounces you to a sign-in page and asks "Work or
> school account vs Personal account", pick **Personal account** and
> sign in as `rjduncan19@hotmail.com`. Do NOT pick work/school.

---

## Step 3 — replace the package

[🌐 Open Packages section](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/packages)

1. Remove the existing v0.1.2 `.msixbundle` (X next to it).
2. Drag `NoteAerator-0.1.3.0.msixbundle` from the dist folder (Step 1)
   onto the upload zone.
3. Wait 10-60 seconds for Partner Center to validate. The architecture
   should show `x64`, identity `DuncanSolutions.NoteAerator`, version
   `0.1.3.0`.
4. Click **Save**.

---

## Step 4 — update the "What's new in this version" field

[🌐 Open Store listings](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/listings/en-us)

Scroll down to **What's new in this version** and paste exactly this
text (already correctly formatted for the field):

```text
Release 0.1.3 — major feature update:

- Prefix grouping in the file list. Files that share dash-separated
  prefixes (e.g. corp-orcl.md and corp-orcl-thomas.md) now collapse
  into an expandable tree in the sidebar. Right-click any project tab
  to toggle "Group by prefix" on or off; the setting is saved per
  project. Smart touches: leading numeric tokens like "30-" are sort
  keys (not group keys), "<prefix>-overview.md" acts as the anchor for
  "<prefix>", and meaningless single-child wrappers are auto-collapsed.

- Getting-started experience. First-time users now land on a welcoming
  three-document project at Documents\Note Aerator\ instead of an empty
  window. Walks through the 30-second tour, search, archive, sidecar
  comments, AI directives, prefix grouping, Mermaid, math, and task
  lists. The seeded project is yours - edit, rename, or delete the
  files however you like.

- Reliability fix. The projects.json reader is now case-insensitive
  so existing installs are not affected by the schema bump.

- file:// links now clickable. Markdown links to local files and
  folders (e.g. "[Open folder](file:///C:/path/to/folder)") are no
  longer silently dropped by the renderer. Clicking one opens
  Explorer's Reveal in Folder view - files are never auto-launched
  through their default handler, so a .exe or .bat link can't run
  by accident.
```

Click **Save** at the bottom of the Store listings page. *Do not skip
this — the submit flow does NOT auto-save unsaved field edits.*

---

## Step 5 — submit

Go back to the submission overview and click **Submit to the Store**.

[🌐 Submissions overview](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

Expected certification time: **1-2 business days**. You'll get email
notifications at each stage.

---

## Verify after it goes live

Open the public listing on any machine to confirm the version updated:

[🌐 Note Aerator on the Microsoft Store](https://apps.microsoft.com/detail/9N5DTC0FZP7M)

Existing customers' installs will update automatically via Store
auto-update — they don't need to do anything.

---

## If something goes wrong

| Symptom                                              | Fix                                                                                                  |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `Version must be greater than the highest published` | Rare — the manifest already says 0.1.3.0. If you somehow see this, the bundle in dist is from an older build; re-run `packaging\store\build-msix.ps1`. |
| `Package identity does not match the product`        | The publisher / identity in the manifest was wrong when the bundle was built. Re-run the build script — it already has the right values. |
| `A submission is in progress`                        | You have an in-progress draft. Open the [Submissions page](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions), either continue the existing draft or cancel it and start fresh. |
| Bundle validation hangs                              | Refresh the Packages page. Validation runs async on Microsoft's side and the UI sometimes doesn't update without a reload. |
| "What's new" text doesn't appear in the live listing | You forgot to click **Save** on the Store listings page before clicking **Submit**. Start a new submission and re-do Step 4 carefully. |

---

## When you want to retire this manual flow

`POC/microsoft-store-pipeline.md` and `POC/tenant-setup.md` document
the GitHub-Actions automation that replaces all of the above with:

```powershell
git tag v0.1.4
git push --follow-tags
```

Worth doing once the next 2-3 releases convince you the manual flow
is fine for now.
