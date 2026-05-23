# Shipping an update manually

How to push a new version of an *already-live* Note Aerator build
through the Partner Center UI, without the CI pipeline.

> The pipeline design (for when you do set it up) lives at
> `POC/microsoft-store-pipeline.md`. This file covers the
> entirely-manual path that doesn't depend on the tenant /
> service-principal work.

---

## Prerequisites for each release

1. Bump the version in `packaging/store/Package.appxmanifest`:
   ```xml
   <Identity ... Version="0.1.3.0" ... />
   ```
   The Store enforces strictly-increasing four-part versions, so a
   resubmission of the same version will be rejected.
2. Edit `packaging/store/listing/whats-new.md` to describe what
   changed (only the **What's new in this version** field gets
   updated for most releases; the description carries over).
3. Build the bundle (this fills the three identity placeholders in
   the manifest at build time, then restores them when you're done):
   ```powershell
   # 1. Fill the placeholders with your real values
   $m = "packaging\store\Package.appxmanifest"
   (Get-Content -Raw $m) `
     -replace '__PARTNER_CENTER_IDENTITY_NAME__','DuncanSolutions.NoteAerator' `
     -replace '__PARTNER_CENTER_PUBLISHER_ID__','CN=402B1CF2-A864-4A8C-8C79-741367A5B224' `
     -replace '__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__','Duncan Solutions' `
     | Set-Content $m -Encoding UTF8

   # 2. Build
   .\packaging\store\build-msix.ps1

   # 3. Restore placeholders so the committed file stays generic
   (Get-Content -Raw $m) `
     -replace 'Name="DuncanSolutions\.NoteAerator"','Name="__PARTNER_CENTER_IDENTITY_NAME__"' `
     -replace 'Publisher="CN=402B1CF2-A864-4A8C-8C79-741367A5B224"','Publisher="__PARTNER_CENTER_PUBLISHER_ID__"' `
     -replace '<PublisherDisplayName>Duncan Solutions</PublisherDisplayName>','<PublisherDisplayName>__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__</PublisherDisplayName>' `
     | Set-Content $m -Encoding UTF8
   ```
   Output: `packaging\store\dist\NoteAerator-<version>.msixbundle`.

---

## Partner Center UI — push the update

1. **Open the product:**
   <https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/overview>

   (If that lands on a generic dashboard, sign in as your MSA
   `rjduncan19@hotmail.com`, then re-open the link. If Partner Center
   ever insists you sign in as a tenant account, ignore the
   suggestion and pick **Personal account** at the sign-in page.)

2. **Start a new submission.** On the product Overview, click
   **Update** (or **Start your submission** / **New submission** —
   wording varies depending on whether a submission is already in
   progress). Direct link:
   <https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions>
   then **New submission**.

   The new submission is pre-filled with everything from the
   previous one — pricing, properties, description, screenshots,
   age rating, etc. You only need to touch the two sections below.

3. **Packages section:**
   <https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/packages>
   - Remove the previous `.msixbundle` (X button next to it).
   - Drag and drop your new
     `packaging\store\dist\NoteAerator-<version>.msixbundle` onto
     the upload zone — or click **Browse** and pick it.
   - Wait for Partner Center to validate the package (10-60 seconds).
     It will reject the upload if the version isn't higher than
     what's live, or if the identity in the manifest doesn't match
     the registered app.
   - Click **Save**.

4. **Store listings → English (United States):**
   <https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/listings/en-us>
   - Scroll to **What's new in this version**.
   - Paste the contents of `packaging\store\listing\whats-new.md`.
   - Click **Save**.

   Don't touch the Description, screenshots, store logo, or
   features unless something has actually changed about them —
   they carry over correctly from the previous submission.

5. **Submit:** at the bottom of the submission overview, click
   **Submit to the Store**. Direct submission landing:
   <https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions>

6. Watch the status. First few updates after a successful launch
   typically clear certification in **1-2 business days**; once
   Microsoft has confidence in the app, later releases often clear
   the same day. You'll get email notifications at each stage.

---

## After certification

Existing users get the update automatically through Store auto-update.
You can verify the new version is live by opening the listing on a
clean machine and checking the version number:
<https://apps.microsoft.com/detail/9N5DTC0FZP7M>

---

## Troubleshooting

| Symptom                                              | What's wrong                                                                                                            |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Version must be greater than the highest published` | You forgot to bump `<Identity Version="..."/>`. Bump the patch component, re-run `build-msix.ps1`, re-upload.            |
| `Package identity does not match the product`        | The placeholder fill in the manifest didn't run or used the wrong values. Re-do step 1 of "Prerequisites" carefully.    |
| `A submission is in progress`                        | There's a draft submission for this product already. Either continue it (Submissions page → the in-progress one), or cancel it from that page and start fresh. |
| Bundle validation hangs                              | Refresh the Packages page. Validation runs asynchronously on Microsoft's side; sometimes the UI doesn't update without a reload. |
| "What's new" text doesn't appear after publish       | Make sure you clicked **Save** at the bottom of the Store listings page *before* clicking **Submit to the Store**. The submit flow does NOT auto-save unsaved field edits. |

---

## When you do build the pipeline

Once you've finished the steps in `POC/tenant-setup.md` and
`POC/microsoft-store-pipeline.md`, this whole manual ritual collapses
to:

```powershell
git tag v0.1.4
git push --follow-tags
```

…and GitHub Actions does steps 1-3 of "Prerequisites" and steps 1-5
of the Partner Center walkthrough automatically.
