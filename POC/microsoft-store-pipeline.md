# Microsoft Store release pipeline

How to wire a GitHub Actions workflow that builds the MSIX bundle and
publishes a new version of Note Aerator to the Microsoft Store whenever
you push a `v*` tag.

---

## Step 0 — prerequisite: the first submission must already be live

> **You have to ship the very first version of the app through the
> Partner Center UI before any of this automation can work.** ✅ This
> has already been done for Note Aerator:
>
> - **Live Store listing:** <https://apps.microsoft.com/detail/9N5DTC0FZP7M>
> - **Windows deep link:** `ms-windows-store://pdp/?productid=9N5DTC0FZP7M`
> - **Product ID:** `9N5DTC0FZP7M`
> - **First version published:** `v0.1.2` (May 2026)

The Store Submission API can only create *follow-up* submissions for a
product that already exists. The first-ever submission must be done
manually because that's when you set the app's identity, age rating,
pricing, listing copy, screenshots, and capabilities — none of which
the API is allowed to bootstrap from scratch.

If you are reading this for a different product, go submit v1
manually first using `packaging/store/SUBMISSION.md`, then come back
here.

---

## Step 1 — get API credentials for Partner Center

The Submission API authenticates with an **Azure AD service principal**
(now branded **Microsoft Entra ID**) that has been granted access to
your Partner Center account.

> ⚠️ **Prerequisite: you need a real Entra tenant first.**
> If you signed up for Partner Center with a personal Microsoft
> account, you do not have one by default. The setup is a separate
> one-time chore — see **[`tenant-setup.md`](./tenant-setup.md)**
> for the MSA-vs-tenant mental model, the free-Azure-account signup,
> MFA registration (including a workaround if Microsoft Authenticator
> push doesn't work for you), and tenant-to-Partner-Center
> association. Come back here once `tenant-setup.md` step 3 is done.

1. Go to the Entra **App registrations** blade:
   <https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade>
   (or via the Azure portal: <https://portal.azure.com> → search
   "App registrations"). Click **+ New registration**. Name it
   something like `noteaerator-store-publisher`. Single tenant. No
   redirect URI needed. After it's created, copy from the Overview
   page:
   - **Directory (tenant) ID** (this should now be populated — if it
     shows "N/A" you are still signed in as the tenantless MSA; go
     back and do the prerequisite above)
   - **Application (client) ID**
2. In the new app's left nav: **Certificates & secrets → Client
   secrets → + New client secret**
   (<https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Credentials>
   for the app you just created). Pick a 24-month expiry. Copy the
   secret **value** (you only see it once). This is your
   `client_secret`.
3. Open Partner Center → Account settings → User management →
   Azure AD applications:
   <https://partner.microsoft.com/dashboard/account/v3/usermanagement#azureadapplications>
   Click **Add Azure AD applications**.
4. Pick the AAD application you just created. Assign it the role
   **Manager** (the Submission API requires it).
5. (One-time, only if you skipped the prerequisite above.) Confirm
   your tenant is associated under Partner Center → Account
   settings → Tenants:
   <https://partner.microsoft.com/dashboard/account/v3/tenants/associated>
   If it isn't, click **Associate Azure AD** and follow the prompt.

If you previously created a tenantless "NA" app under your personal
MSA, you can safely delete it now — it isn't usable for the API.

You now have three secrets:

| Name              | Looks like                                       |
| ----------------- | ------------------------------------------------ |
| `AZURE_TENANT_ID` | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`           |
| `AZURE_CLIENT_ID` | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`           |
| `AZURE_CLIENT_SECRET` | a random ~40-character blob                  |

And one identifier you will hard-code into the workflow (it's not a
secret — it's already on the public Store listing):

| `STORE_PRODUCT_ID` | `9N5DTC0FZP7M` |

---

## Step 2 — store the secrets in GitHub

In the GitHub repo, open
<https://github.com/rjduncan19/noteaerator/settings/secrets/actions>
and click **New repository secret**. Add three secrets with the names
from the table above. Do *not* add `STORE_PRODUCT_ID` as a secret —
set it as a repository **variable**
(<https://github.com/rjduncan19/noteaerator/settings/variables/actions>),
or just inline it in the workflow YAML since it's public.

---

## Step 3 — add the publish workflow

Tool of choice: the official **[msstore CLI](https://github.com/microsoft/msstore-cli)**
(`Microsoft.StoreServices.DeveloperCenter.CLI`). It's a .NET tool that
wraps the Submission API: it knows how to log in with the AAD service
principal, create a new submission, upload an MSIX bundle, swap in
"What's new" copy, and commit the submission for certification.

Create `.github/workflows/publish-store.yml`:

```yaml
name: Publish to Microsoft Store

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      tag:
        description: 'Existing tag to publish (e.g. v0.1.3)'
        required: true

jobs:
  publish:
    runs-on: windows-latest
    permissions:
      contents: read
    env:
      STORE_PRODUCT_ID: 9N5DTC0FZP7M

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.inputs.tag || github.ref }}
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      # 1. Build the MSIX bundle exactly the way the manual flow does.
      #    The build script auto-restores the Windows SDK BuildTools.
      - name: Fill MSIX manifest identity
        shell: pwsh
        run: |
          $m = 'packaging/store/Package.appxmanifest'
          (Get-Content -Raw $m) `
            -replace '__PARTNER_CENTER_IDENTITY_NAME__','DuncanSolutions.NoteAerator' `
            -replace '__PARTNER_CENTER_PUBLISHER_ID__','CN=402B1CF2-A864-4A8C-8C79-741367A5B224' `
            -replace '__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__','Duncan Solutions' `
            | Set-Content $m -Encoding UTF8

      - name: Compute version from tag
        id: ver
        shell: pwsh
        run: |
          $tag = '${{ github.event.inputs.tag || github.ref_name }}'
          $v = $tag.TrimStart('v')
          if ($v -notmatch '^\d+\.\d+\.\d+$') { throw "Tag must be vMAJOR.MINOR.PATCH" }
          $full = "$v.0"
          "version=$full" >> $env:GITHUB_OUTPUT
          Write-Host "MSIX version: $full"

      - name: Build MSIX bundle
        shell: pwsh
        run: |
          ./packaging/store/build-msix.ps1 -Version ${{ steps.ver.outputs.version }}

      # 2. Install msstore CLI and log in with the service principal.
      - name: Install msstore CLI
        run: dotnet tool install --global Microsoft.StoreServices.DeveloperCenter.CLI

      - name: Login to Partner Center
        env:
          MS_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
          MS_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
          MS_CLIENT_SECRET: ${{ secrets.AZURE_CLIENT_SECRET }}
        run: |
          msstore reconfigure `
            --tenantId  $env:MS_TENANT_ID `
            --clientId  $env:MS_CLIENT_ID `
            --clientSecret $env:MS_CLIENT_SECRET

      # 3. Create the submission, upload the bundle, and commit.
      - name: Publish submission
        shell: pwsh
        run: |
          $bundle = "packaging/store/dist/NoteAerator-${{ steps.ver.outputs.version }}.msixbundle"
          if (-not (Test-Path $bundle)) { throw "Bundle not found: $bundle" }
          msstore publish $bundle --productId $env:STORE_PRODUCT_ID --verbose

      - name: Upload bundle as workflow artifact
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: msixbundle-${{ steps.ver.outputs.version }}
          path: packaging/store/dist/NoteAerator-*.msixbundle
          if-no-files-found: error
```

### Notes on the workflow

- The identity values (`DuncanSolutions.NoteAerator`, the publisher
  `CN=`, and the publisher display name) are **not secrets** — they
  are visible to anyone who installs the bundle from the Store — so
  inlining them in the workflow is fine and keeps the source-tree
  manifest generic. If you'd prefer, move them into repo variables.
- `msstore publish` does the create-new-submission / upload / commit
  dance in one call. Add `--flightName <name>` to push to a Store
  flight (private channel) instead of the public ring while you're
  experimenting.
- The Store will still re-sign and re-certify the bundle. Expect 1-3
  business days for the first few automated submissions and faster
  thereafter (Microsoft prioritises updates from known-good apps).
- The `Upload bundle as workflow artifact` step lets you grab the
  exact `.msixbundle` that was submitted, for sideloading or
  archiving.

---

## Step 4 — first end-to-end run

1. Bump the version in `POC/Noteaerator/Noteaerator.csproj` (the
   `<Version>` property).
2. Commit, then tag:
   ```powershell
   git commit -am "release v0.1.3"
   git tag v0.1.3
   git push --follow-tags
   ```
3. Watch the workflow under the Actions tab. The `Publish submission`
   step should end with something like
   `Submission 1152921504606851234 committed.`
4. In Partner Center, the new submission will appear under
   Note Aerator → Submissions
   (<https://partner.microsoft.com/dashboard/products/9N5DTC0FZP7M/submissions>)
   with the status **Certification → In progress**.

Subsequent releases are just: bump version, tag, push.

---

## What this pipeline still does NOT do

These are intentional gaps; add them when you need them.

- **Listing copy / screenshot updates.** The workflow only swaps the
  package. Description/screenshot/age-rating edits still go through
  Partner Center (or a separate `msstore` invocation against the
  listing endpoint). For the typical "ship a code change" release
  that's exactly what you want.
- **Release-notes automation.** "What's new" still comes from the
  previous submission unless you pass `--release-notes-file`. Easy
  add-on: parse the `## v<x.y.z>` block out of `CHANGELOG.md` and
  pass it via that flag.
- **arm64.** The workflow builds x64 only. Add `-IncludeArm64` to the
  `build-msix.ps1` call once you have an arm64 dev box to smoke-test
  on.
- **Code signing.** Not needed — the Store signs the bundle on
  ingestion. If you ever want to distribute the same bundle outside
  the Store, add a `signtool sign` step after `build-msix.ps1`.

---

## Troubleshooting

| Symptom                                                | Likely cause                                                                                                                                    |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `401 Unauthorized` during `msstore publish`            | The AAD app isn't a Manager in Partner Center, or the client secret has expired.                                                                |
| `409 Conflict` saying "a submission is in progress"    | You started a submission manually in Partner Center and didn't finish it. Cancel it there, then re-run the workflow.                            |
| `Package identity does not match the product identity` | The three identity values in the workflow don't match the ones in Partner Center → Product identity. Re-copy them and update the workflow YAML. |
| `Version must be greater than the highest published`   | Bump the patch component and re-tag. The Store enforces strictly-increasing 4-part versions.                                                    |
