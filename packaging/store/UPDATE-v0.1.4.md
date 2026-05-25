# Update Note Aerator to v0.1.4 on the Microsoft Store

Patch release covering three GitHub issues. Same Partner Center ritual as
v0.1.3 — the only differences are the bundle filename, the SHA256, and
the "What's new" text.

## What changed in this release

Three bug fixes against the v0.1.3 baseline:

- **No more silently missing files** (issue
  [#6](https://github.com/rjduncan19/noteaerator/issues/6)). When two
  filenames in the same folder shared their first three
  `-`-separated tokens (for example `71-giac-ab-post-draft.md` and
  `72-giac-ab-post-draft-v2-with-feedback.md`), the second file used
  to vanish from the project file list. The prefix-grouping engine
  now extends past its depth cap just enough to keep every file
  reachable.
- **AGENTS.md no longer clutters the top of the file list** (issue
  [#5](https://github.com/rjduncan19/noteaerator/issues/5)). It is
  now always sorted to the bottom of the project. Files with a
  numeric prefix like `30-foo.md` keep coming first, and the rest
  stay alphabetical in the middle.
- **`projects.json` is forward-compatible** (issue
  [#4](https://github.com/rjduncan19/noteaerator/issues/4)). Unknown
  properties added by a future version are now read tolerantly and
  round-tripped on save instead of being silently dropped, so editing
  the project list in an older build can no longer corrupt
  newer-format data. Backwards compat with the legacy string-array
  and current object form is preserved.

---

## Step 1 — build and open the bundle's folder

```powershell
.\packaging\store\build-msix.ps1
```

Then open the build output folder in File Explorer:

[📁 Open dist folder](file:///C:/Users/richardd/source-rjduncan19/noteaerator/packaging/store/dist)

You should see:

| File                                | Size    | Notes                  |
| ----------------------------------- | ------- | ---------------------- |
| `NoteAerator-0.1.4.0.msixbundle`    | 68.8 MB | **upload this**        |
| `NoteAerator-0.1.4.0-x64.msix`      | 68.8 MB | bundled — don't upload |

SHA256 of the bundle (for your records):
`927AD5531E4BA89DD8FC31361829774C337D65AA7C3CEB49BCC89F4467FF5058`

---

## Step 2 — start a new Partner Center submission

[🌐 New submission](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

On the Submissions page, click **New submission**. The form is
pre-filled with everything from your live v0.1.3 — pricing,
properties, description, screenshots, age rating, etc. **Only the
two sections below need changes.**

> If Partner Center bounces you to a sign-in page and asks "Work or
> school account vs Personal account", pick **Personal account** and
> sign in as `rjduncan19@hotmail.com`. Do NOT pick work/school.

---

## Step 3 — replace the package

[🌐 Open Packages section](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/packages)

1. Remove the existing v0.1.3 `.msixbundle` (X next to it).
2. Drag `NoteAerator-0.1.4.0.msixbundle` from the dist folder (Step 1)
   onto the upload zone.
3. Wait 10-60 seconds for Partner Center to validate. The architecture
   should show `x64`, identity `DuncanSolutions.NoteAerator`, version
   `0.1.4.0`.
4. Click **Save**.

---

## Step 4 — update the "What's new in this version" field

[🌐 Open Store listings](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/listings/en-us)

Scroll down to **What's new in this version** and paste exactly this
text (already formatted for the field — no Markdown syntax that the
field can't render):

```text
Release 0.1.4.0 — bug-fix patch:

- AGENTS.md no longer clutters the top of the file list. It is now
  always sorted to the bottom of the project (issue #5). Files with
  a numeric prefix like 30-foo.md keep coming first, and the rest
  stay alphabetical in the middle.

- projects.json is forward-compatible. Unknown properties added by a
  future version are now read tolerantly and round-tripped on save
  instead of being silently dropped, so editing your project list in
  an older build can no longer corrupt newer-format data (issue #4).
```

Click **Save** at the bottom of the Store listings page. *Do not skip
this — the submit flow does NOT auto-save unsaved field edits.*

---

## Step 5 — submit

Go back to the submission overview and click **Submit to the Store**.

[🌐 Submissions overview](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

Expected certification time: **same business day to 1 day** for a
small patch like this (Microsoft tends to fast-track patches after
the first successful submission). You'll get email notifications at
each stage.

---

## Verify after it goes live

Open the public listing on any machine to confirm the version updated:

[🌐 Note Aerator on the Microsoft Store](https://apps.microsoft.com/detail/9N5DTC0FZP7M)

Existing customers' installs will update automatically via Store
auto-update — they don't need to do anything.

---

## Troubleshooting

| Symptom                                              | Fix                                                                                                  |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `Version must be greater than the highest published` | The bundle in dist is from an older build. Re-run `packaging\store\build-msix.ps1` after confirming `Package.appxmanifest` says `Version="0.1.4.0"`. |
| `Package identity does not match the product`        | The publisher / identity in the manifest was wrong when the bundle was built. Re-run the build script — it injects the right values. |
| `A submission is in progress`                        | Open the [Submissions page](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions); either continue the in-progress draft or cancel it and start fresh. |
| Bundle validation hangs                              | Refresh the Packages page. Validation runs async on Microsoft's side and the UI sometimes doesn't update without a reload. |
| "What's new" text doesn't appear in the live listing | You forgot to click **Save** on the Store listings page before clicking **Submit**. Start a new submission and re-do Step 4 carefully. |
