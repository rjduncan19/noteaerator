# Update Note Aerator to v0.2.0 on the Microsoft Store

Feature release. Same Partner Center ritual as v0.1.4 — the differences are
the bundle filename, the SHA256, the fact that this release ships **both x64
and arm64**, and the "What's new" text.

## What changed in this release

Three new features on top of the v0.1.4 baseline:

- **"Show folders" view mode.** A new per-project view (right-click the
  project tab → **Show folders**) lists the folder's own `.md` files flat at
  the top and shows its sub-directories as expandable chevron rows — recursively,
  to any depth, with a `(N)` file count on each folder. It is mutually
  exclusive with **Group by prefix**, so each project picks the layout that
  suits it.
- **Hide files and folders.** Right-click any file (or, in Show-folders mode,
  any sub-directory) and choose **Hide** to remove clutter from the list. A
  per-project **Show hidden files** toggle (default off) brings them back,
  rendered dimmed and italic, where they can be **Unhidden**. Your choices are
  remembered between sessions.
- **Help / About button.** A new ℹ button in the toolbar opens the project's
  GitHub page for docs, release notes, and issue reporting.

Everything from v0.1.4 — prefix grouping (including the issue #6 fix),
AGENTS.md sorting, forward-compatible `projects.json` — is unchanged.

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

Pass the new version to the build script with `-Version 0.2.0.0` (below) rather
than editing the manifest's `Version`, and **restore the placeholders** after
building so the committed manifest stays generic.

---

## Step 1 — build and open the bundle's folder

This release ships both architectures, so build with `-IncludeArm64`:

```powershell
.\packaging\store\build-msix.ps1 -IncludeArm64 -Version 0.2.0.0
```

Then open the build output folder in File Explorer:

[📁 Open dist folder](file:///C:/Users/richardd/source-rjduncan19/noteaerator/packaging/store/dist)

You should see:

| File                              | Size     | Notes                  |
| --------------------------------- | -------- | ---------------------- |
| `NoteAerator-0.2.0.0.msixbundle`  | 133.5 MB | **upload this**        |
| `NoteAerator-0.2.0.0-x64.msix`    | 68.7 MB  | bundled — don't upload |
| `NoteAerator-0.2.0.0-arm64.msix`  | 64.8 MB  | bundled — don't upload |

SHA256 of the bundle (for your records):
`0827CB1B305F152EADAAD8B3B36FAC16917015ED46295FE0FD72E3C076DE6551`

---

## Step 2 — start a new Partner Center submission

[🌐 New submission](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

On the Submissions page, click **New submission**. The form is
pre-filled with everything from your live v0.1.4 — pricing,
properties, description, screenshots, age rating, etc. **Only the
two sections below need changes.**

> If Partner Center bounces you to a sign-in page and asks "Work or
> school account vs Personal account", pick **Personal account** and
> sign in as `rjduncan19@hotmail.com`. Do NOT pick work/school.

---

## Step 3 — replace the package

[🌐 Open Packages section](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/packages)

1. Remove the existing v0.1.4 `.msixbundle` (X next to it).
2. Drag `NoteAerator-0.2.0.0.msixbundle` from the dist folder (Step 1)
   onto the upload zone.
3. Wait 10-60 seconds for Partner Center to validate. The architectures
   should show `x64` **and** `arm64`, identity `DuncanSolutions.NoteAerator`,
   version `0.2.0.0`.
4. Click **Save**.

---

## Step 4 — update the "What's new in this version" field

[🌐 Open Store listings](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/listings/en-us)

Scroll down to **What's new in this version** and paste exactly this
text (already formatted for the field — no Markdown syntax that the
field can't render):

```text
Release 0.2.0.0 — new features:

- Show folders: a new per-project view lists a folder's own files flat
  and shows its sub-directories as expandable chevrons, to any depth,
  with a file count on each folder. Right-click a project tab to switch
  between "Show folders" and "Group by prefix".

- Hide files and folders: right-click any file (or a sub-folder in Show
  folders mode) and choose Hide to declutter the list. A "Show hidden
  files" toggle brings them back, shown dimmed, where you can unhide
  them. Your choices are remembered.

- Help button: a new info button in the toolbar opens the project's
  GitHub page for docs and issue reporting.

- Now ships for both Intel (x64) and ARM (arm64) PCs.
```

Click **Save** at the bottom of the Store listings page. *Do not skip
this — the submit flow does NOT auto-save unsaved field edits.*

---

## Step 5 — submit

Go back to the submission overview and click **Submit to the Store**.

[🌐 Submissions overview](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions)

Expected certification time: **same business day to a couple of days**.
A feature release with a new architecture (arm64) may take slightly
longer to certify than a pure patch. You'll get email notifications at
each stage.

---

## Verify after it goes live

Open the public listing on any machine to confirm the version updated:

[🌐 Note Aerator on the Microsoft Store](https://apps.microsoft.com/detail/9N5DTC0FZP7M)

Existing customers' installs will update automatically via Store
auto-update — they don't need to do anything. ARM device owners who
previously couldn't install will now get a native arm64 build.

---

## Troubleshooting

| Symptom                                              | Fix                                                                                                  |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `manifest has unfilled placeholders`                 | The build script found `__PARTNER_CENTER_*__` placeholders. Fill the three identity fields per Step 0 before re-running. |
| `Version must be greater than the highest published` | The bundle in dist is from an older build. Re-run with `-Version 0.2.0.0`, or confirm `Package.appxmanifest` says `Version="0.2.0.0"`. |
| `Package identity does not match the product`        | The publisher / identity in the manifest was wrong when the bundle was built. Fix per Step 0 and re-run the build script. |
| Only one architecture shows after upload             | You built without `-IncludeArm64`. Re-run `build-msix.ps1 -IncludeArm64 -Version 0.2.0.0` and re-upload the bundle. |
| `A submission is in progress`                        | Open the [Submissions page](https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions); either continue the in-progress draft or cancel it and start fresh. |
| Bundle validation hangs                              | Refresh the Packages page. Validation runs async on Microsoft's side and the UI sometimes doesn't update without a reload. |
| "What's new" text doesn't appear in the live listing | You forgot to click **Save** on the Store listings page before clicking **Submit**. Start a new submission and re-do Step 4 carefully. |
