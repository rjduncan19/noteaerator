# Pipeline & Microsoft Store -- design discussion

Originally captured 2026-05-03 as a "no path picked" analysis.
**Updated 2026-05-03 (later same day)** to reflect what has actually
been built. Status markers used below:

- ✅ **DONE** -- shipped and verified
- 🟡 **PARTIAL** -- some pieces in place, more planned
- ⬜ **NOT STARTED** -- still on the table

## Where we are today (current state)

Two workflows live in `.github/workflows/`:

- **`pr-ci.yml`** ✅ -- on every `pull_request` and push to `main`:
  - Linux job restores + builds **`Noteaerator.Core`** and
    **`Noteaerator.Tests`** with `-warnaserror`, runs `dotnet test`,
    uploads `.trx`.
  - Windows matrix job builds the WPF host
    (`Noteaerator.csproj`) for both `win-x64` and `win-arm64`.
  - Concurrency-grouped per branch.
  - **22 xUnit tests passing.** Verified green on first run.
- **`release.yml`** ✅ -- on `v*` tag push:
  - Publishes self-contained `win-x64` + `win-arm64`.
  - Runs `sbom-tool generate` over each publish output and uploads
    a sibling `NoteAerator-<tag>-<rid>.sbom.zip` (SPDX 2.2).
  - Creates a GitHub Release with the main zips attached.

Plus:

- **`.devcontainer/devcontainer.json`** ✅ -- GitHub Codespaces config
  (Linux .NET 8 base image) so anyone can work on Core + Tests in a
  Codespace without a local machine.
- **`packaging/winget/`** ✅ -- v1.6 manifest for
  `rjduncan19.NoteAerator 0.1.1-poc` (portable installer wrapping
  the GitHub Release zip with x64+arm64 SHA256s). Local installable
  today; submission to `microsoft/winget-pkgs` documented but not
  yet done.
- **`LICENSE`** ✅ -- MIT, referenced by the winget manifest.

Still missing (vs a fully professional pipeline): code signing,
MSIX packaging, Microsoft Store submission, observability/crash
reporting, automated changelog/versioning.

## Microsoft Store: how hard

Technically doable in a few days of focused work for a WPF-on-.NET-8
app. Gating issues are administrative more than technical.

### Real costs

| Item | Cost |
|---|---|
| Partner Center developer account | $19 (individual) / $99 (company), one-time |
| Reserve "Note Aerator" name | Free with account |
| MSIX packaging (`<WindowsPackageType>MSIX</WindowsPackageType>` or `.wapproj`) | ~half day |
| Asset suite (~10 image sizes: Square44/71/150/310, Wide310x150, splash, badge, store logo) | ~half day |
| Code signing | **Store signs for you** during ingestion -- $0 if Store-only |
| Compliance review | 1-3 business days for first submission |
| Store listing (description, >=4 screenshots, age rating, privacy URL) | ~half day |

### Specific gotchas for our app

1. **CDN-loaded JS libs (markdown-it, mermaid, highlight.js) won't fly
   long-term.** A Store-distributed app shipped with no internet on
   first launch should still render -- right now we'd just show empty
   pages. Need to vendor those libs locally first. (Already in
   "known gaps".)
2. **Self-contained .NET adds ~160 MB.** Store handles that fine
   (per-app cap is 25 GB), but it's noticeable. Framework-dependent
   + declaring `Microsoft.NET.CoreFramework.Desktop` as an MSIX
   dependency is leaner -- Store users get .NET auto-installed.
3. **WebView2 runtime declaration.** MSIX needs to declare
   `Microsoft.Web.WebView2` either as a dependency (Evergreen, what
   we already use) or bundle the Fixed Runtime (~150 MB extra).
   Evergreen is fine.
4. **Privacy URL + capabilities.** App declares no special
   capabilities, but Store wants a privacy policy URL. Static page
   is sufficient.

**Bottom line**: maybe 2-3 days of work spread over a calendar week
(waiting on review). Worth it for broad reach; overkill for
personal-use only.

## A "more professional" pipeline

Layered by ambition. **Layers A, B-partial, and E-partial are now
shipped.** Status markers below.

### Layer A -- PR validation ✅ DONE

- ✅ `.github/workflows/pr-ci.yml` triggered on `pull_request` and
  `push: branches: [main]`:
  - ✅ `dotnet restore` + `dotnet build` (Release, both arches via
    Windows matrix; Linux for Core+Tests)
  - ✅ `dotnet test` -- **`Noteaerator.Tests`** project added with
    **22 xUnit tests** covering `SearchEngine`, `CommentStore`,
    sidecar atomic-write + auto-delete, and `TimeFormat`
    boundaries. (Slugify is JS-side only and not yet covered.)
  - ✅ `-warnaserror` on Core + Tests builds (Roslyn analyzers
    are on by default in .NET 8; explicit StyleCop deferred).
  - ✅ Concurrency group + cancel-in-progress per branch.
- ⬜ Branch protection on `main` requiring this workflow to pass
  before merge -- **needs to be enabled in repo Settings**
  (configuration only; can't be done via committed file).

**Prerequisite refactor that landed with this layer:** extracted
**`Noteaerator.Core`** (`net8.0`, cross-platform) so tests run on
Linux runners (and in Codespaces). The WPF host
(`net8.0-windows`) now references Core. New `POC/Noteaerator.sln`.

### Layer B -- Release-quality gates 🟡 PARTIAL

- ⬜ Same checks as Layer A run on the tag push **before** publishing
  (currently the release workflow goes straight to publish).
- ⬜ Test coverage report (Coverlet -> upload to Codecov).
- ✅ **SBOM generation in `release.yml`**: per-arch SPDX 2.2
  manifest via `Microsoft.Sbom.DotNetTool`, published as a sibling
  asset `NoteAerator-<tag>-<rid>.sbom.zip`.

### Layer C -- Code signing ⬜ NOT STARTED

- Get a code-signing cert (Sectigo OV ~$70-200/year, DigiCert EV
  ~$300-500/year). Without it, Windows SmartScreen warns end users.
- Store cert in a GitHub repo secret, or use Azure Trusted Signing
  (newer, ~$10/month).
- `signtool` step after publish, before zip.
- **Skip if going Store-only** -- Store does this for you.

### Layer D -- MSIX packaging ⬜ NOT STARTED

- Add a `.wapproj` or set `<WindowsPackageType>MSIX</WindowsPackageType>`
  on the WPF project.
- Generate icon assets via a small script (extend our existing
  `svg_to_ico.py` to also produce the MSIX asset set).
- CI step produces `.msix` per arch; bundle into a `.msixbundle`.
- Upload as a Release asset for sideload via `.appinstaller`, OR
  upstream to Store.

### Layer E -- Distribution beyond GitHub Releases 🟡 PARTIAL

- ✅ **winget manifest authored** under
  `packaging/winget/rjduncan19.NoteAerator/0.1.1-poc/` (three v1.6
  YAMLs: version + locale + installer; portable installer wrapping
  the GitHub Release zip with x64+arm64 SHA256s). Locally
  installable today via
  `winget install --manifest packaging\winget\rjduncan19.NoteAerator\0.1.1-poc`.
- ⬜ PR to `microsoft/winget-pkgs` so `winget install
  rjduncan19.NoteAerator` works for everyone (procedure documented
  in `packaging/winget/README.md`; not yet executed).
- ⬜ Auto-update the winget manifest from the release workflow on
  each tag push.
- ⬜ Chocolatey / Scoop / Microsoft Store.

### Layer F -- Release engineering polish ⬜ NOT STARTED

- **Versioning**: `release-please` or `GitVersion` to auto-derive
  semver from commit messages -- no more hand-tagging.
- **Changelog**: auto-generated from commit messages.
- **CODEOWNERS** + branch protection + required reviewers.
- **Dependabot** for NuGet + GitHub Actions versions.
- **CodeQL** SAST (free for public repos).

### Layer G -- Runtime observability (optional) ⬜ NOT STARTED

- **Crash reporting**: Sentry, App Center, or self-host. Would need
  an opt-in toggle given the project's privacy ethos.
- **Auto-update**: Velopack or Squirrel for non-Store builds; Store
  handles its own users.

## Codespaces ✅ DONE

- ✅ `.devcontainer/devcontainer.json` based on
  `mcr.microsoft.com/devcontainers/dotnet:1-8.0`. Includes the
  C# Dev Kit, GitHub PR, and YAML extensions; sets the default
  solution; restores `Noteaerator.Core` and `Noteaerator.Tests` on
  `postCreateCommand`. Caveat: WPF/WebView2 cannot run in a Linux
  Codespace, so the WPF host is intentionally NOT restored there
  (it builds and runs on Windows; Core + Tests are the cross-
  platform surface).

## Suggested sequencing

| # | Step | Investment | Value | Status |
|---|---|---|---|---|
| 1 | Test project + cover SearchEngine, CommentStore, anchor logic | 1 day | Stops the next regression cold | ✅ DONE (22 tests) |
| 2 | PR validation workflow + branch protection | 2-3 hours | Forces gates to actually run | ✅ workflow / ⬜ branch protection |
| 3 | SBOM + warning-as-error in release flow | 1-2 hours | Free quality signal | 🟡 SBOM ✅ / warnaserror ⬜ in release |
| 4 | Code-sign cert + signtool step | Half day + cert procurement | No more SmartScreen warnings | ⬜ |
| 5 | MSIX packaging (sideload only) | 1 day | Modern install + AppInstaller auto-update | ⬜ |
| 6 | winget manifest PR | 1 hour | Big distribution win for tiny effort | 🟡 manifest ✅ / submission ⬜ |
| 7 | Microsoft Store submission | 2-3 days + review | Broadest distribution, Store signs | ⬜ |

**What's left, in suggested order:**

1. **Enable branch protection on `main`** in repo Settings (require
   `pr-ci.yml` to pass; require PR review). 5 minutes, no code.
2. **Add the test/warnaserror gate to `release.yml`** so a tag push
   can never publish a broken build. ~1 hour.
3. **Submit the winget manifest** to `microsoft/winget-pkgs`
   (procedure in `packaging/winget/README.md`). ~1 hour.
4. **Code signing**, then **MSIX**, then **Store** (in that order,
   each with real cost as documented above).

## Decision

✅ **2026-05-03**: pursued the "free wins" tier — Layer A (tests +
PR CI + Codespaces refactor), the SBOM piece of Layer B, and the
manifest piece of Layer E. Microsoft Store and code-signing remain
on the table; Layer F polish (CODEOWNERS / Dependabot / CodeQL /
auto-versioning) is an obvious next batch when the user wants more.

_Open question still relevant for the Store path: **do you already
have a Microsoft Partner Center / Microsoft Store dev account?**
That gates Layer 7. The user is a Microsoft FTE; historical FTE
benefits for Partner Center exist but the current state should be
checked via HRweb / company-store benefits / Developer Relations._
