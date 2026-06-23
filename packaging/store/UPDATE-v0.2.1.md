# Update Note Aerator to v0.2.1 on the Microsoft Store

Minor bug-fix patch over v0.2.0. Same Partner Center ritual as v0.2.0 — the
differences are the bundle filename, the SHA256, and the "What's new" text.

> **If you have not yet submitted v0.2.0 to the Store:** you can skip v0.2.0
> entirely and submit this v0.2.1 bundle instead — it contains the full v0.2.0
> feature set (Show folders, Hide files/folders, Help button, dual-arch) plus
> the refresh fix. In that case, use the v0.2.0 "What's new" text from
> `UPDATE-v0.2.0.md` and add the refresh-fix bullet below.

## What changed in this release

One reliability fix on top of v0.2.0:

- **Auto-refresh now works for files in sub-directories** ("Show folders"
  mode). Previously, when you opened a file that lived in a sub-folder and it
  changed on disk, the view and file list didn't update until you clicked
  **Refresh**. The folder watcher now follows sub-directories while Show-folders
  mode is active, so edits show up automatically again. Changes inside hidden
  folders (e.g. `node_modules`) are intentionally ignored unless **Show hidden
  files** is on, so busy folders don't cause needless refreshes.

Everything from v0.2.0 — Show folders, Hide files/folders, the Help button, and
the x64 + arm64 packages — is unchanged.

---

## Step 0 — fill the manifest identity (one-time per build)

The committed `packaging\store\Package.appxmanifest` ships with **placeholder**
identity values and the previous version number, so the build script refuses to
run until they're filled. Use the same fill → build → restore ritual documented
in [`UPDATE.md`](./UPDATE.md): the real values are

| Field                  | Value                                       |
| ---------------------- | ------------------------------------------- |
| `Identity Name`        | `DuncanSolutions.NoteAerator`               |
| `Identity Publisher`   | `CN=402B1CF2-A864-4A8C-8C79-741367A5B224`   |
| `PublisherDisplayName` | `Duncan Solutions`                          |

Pass the new version to the build script with `-Version 0.2.1.0` (below) rather
than editing the manifest's `Version`, and **restore the placeholders** after
building so the committed manifest stays generic.

---

## Step 1 — build and open the bundle's folder

This release ships both architectures, so build with `-IncludeArm64`:

```powershell
.\packaging\store\build-msix.ps1 -IncludeArm64 -Version 0.2.1.0
```

Then open the build output folder in File Explorer:

[📁 Open dist folder](file:///C:/Users/richardd/source-rjduncan19/noteaerator/packaging/store/dist)

You should see:

| File                              | Size     | Notes                  |
| --------------------------------- | -------- | ---------------------- |
| `NoteAerator-0.2.1.0.msixbundle`  | 133.5 MB | **upload this**        |
| `NoteAerator-0.2.1.0-x64.msix`    | 68.7 MB  | bundled — don't upload |
| `NoteAerator-0.2.1.0-arm64.msix`  | 64.8 MB  | bundled — don't upload |

SHA256 of the bundle (for your records):
`D3FCCC85787183C6C295083410A01FE2D26F2291C340F3F178ED4629DACC6977`

---

## Step 2 — start a new Partner Center submission

[🌐 New submission](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

On the Submissions page, click **New submission**. The form is
pre-filled with everything from your previous submission — pricing,
properties, description, screenshots, age rating, etc. **Only the
two sections below need changes.**

> If Partner Center bounces you to a sign-in page and asks "Work or
> school account vs Personal account", pick **Personal account** and
> sign in as `rjduncan19@hotmail.com`. Do NOT pick work/school.

---

## Step 3 — replace the package

[🌐 Open Packages section](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/packages)

1. Remove the previous `.msixbundle` (X next to it).
2. Drag `NoteAerator-0.2.1.0.msixbundle` from the dist folder (Step 1)
   onto the upload zone.
3. Wait 10-60 seconds for Partner Center to validate. The architectures
   should show `x64` **and** `arm64`, identity `DuncanSolutions.NoteAerator`,
   version `0.2.1.0`.
4. Click **Save**.

---

## Step 4 — update the "What's new in this version" field

[🌐 Open Store listings](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/listings/en-us)

Scroll down to **What's new in this version** and paste exactly this
text (already formatted for the field — no Markdown syntax that the
field can't render):

```text
Release 0.2.1.0 — bug-fix patch:

- Fixed auto-refresh for files in sub-folders ("Show folders" view).
  When a file in a sub-folder changed on disk, the view used to require
  a manual Refresh; it now updates automatically again. Changes inside
  hidden folders are ignored unless "Show hidden files" is on.
```

> If you skipped submitting v0.2.0, prepend the v0.2.0 "What's new" text
> (Show folders, Hide files/folders, Help button, dual-arch) from
> `UPDATE-v0.2.0.md` so users on 0.1.4 see the full set of changes.

Click **Save** at the bottom of the Store listings page. *Do not skip
this — the submit flow does NOT auto-save unsaved field edits.*

---

## Step 5 — submit

Go back to the submission overview and click **Submit to the Store**.

[🌐 Submissions overview](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

Expected certification time: **same business day to 1 day** for a small
patch like this. You'll get email notifications at each stage.

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
| `manifest has unfilled placeholders`                 | The build script found `__PARTNER_CENTER_*__` placeholders. Fill the three identity fields per Step 0 before re-running. |
| `Version must be greater than the highest published` | The bundle in dist is from an older build. Re-run with `-Version 0.2.1.0`, or confirm `Package.appxmanifest` says `Version="0.2.1.0"`. |
| `Package identity does not match the product`        | The publisher / identity in the manifest was wrong when the bundle was built. Fix per Step 0 and re-run the build script. |
| Only one architecture shows after upload             | You built without `-IncludeArm64`. Re-run `build-msix.ps1 -IncludeArm64 -Version 0.2.1.0` and re-upload the bundle. |
| `A submission is in progress`                        | Open the [Submissions page](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions); either continue the in-progress draft or cancel it and start fresh. |
| Bundle validation hangs                              | Refresh the Packages page. Validation runs async on Microsoft's side and the UI sometimes doesn't update without a reload. |
| "What's new" text doesn't appear in the live listing | You forgot to click **Save** on the Store listings page before clicking **Submit**. Start a new submission and re-do Step 4 carefully. |
