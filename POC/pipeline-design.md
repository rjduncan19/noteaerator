# Pipeline & Microsoft Store -- design discussion

Captured 2026-05-03. **No path is committed yet.** This doc captures
the analysis from the user's "talk to me" question about Store
publishing and a more professional CI/CD pipeline so the discussion is
preserved for later.

## Where we are today

One workflow (`.github/workflows/release.yml`) triggered only on `v*`
tag push: builds both Windows architectures self-contained, zips
each, creates a GitHub Release with the zips attached. **No tests, no
signing, no PR gating, no quality bar.** That is a POC pipeline, not a
real one.

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

Layered by ambition. **Layer A is the highest-leverage starting
point.**

### Layer A -- PR validation (highest value, smallest effort)

- New `pr-ci.yml` triggered on `pull_request`:
  - `dotnet restore` + `dotnet build` (Debug, both arches)
  - `dotnet test` -- **we have zero tests today**, so step one is
    writing them. Easy targets: `SearchEngine`, `CommentStore`,
    anchor `slugify`, `FormatRelative`, sidecar atomic-write.
  - Roslyn analyzers (`Microsoft.CodeAnalysis.NetAnalyzers`) with
    `TreatWarningsAsErrors`
  - Optional: StyleCop
- Branch protection on `main`: require this workflow to pass before
  merge.

### Layer B -- Release-quality gates

- Same checks as Layer A run on the tag push **before** publishing.
- Test coverage report (Coverlet -> upload to Codecov).
- SBOM generation (Microsoft.Sbom.Tool produces a SPDX manifest,
  attach to release).

### Layer C -- Code signing

- Get a code-signing cert (Sectigo OV ~$70-200/year, DigiCert EV
  ~$300-500/year). Without it, Windows SmartScreen warns end users.
- Store cert in a GitHub repo secret, or use Azure Trusted Signing
  (newer, ~$10/month).
- `signtool` step after publish, before zip.
- **Skip if going Store-only** -- Store does this for you.

### Layer D -- MSIX packaging

- Add a `.wapproj` or set `<WindowsPackageType>MSIX</WindowsPackageType>`
  on the WPF project.
- Generate icon assets via a small script (extend our existing
  `svg_to_ico.py` to also produce the MSIX asset set).
- CI step produces `.msix` per arch; bundle into a `.msixbundle`.
- Upload as a Release asset for sideload via `.appinstaller`, OR
  upstream to Store.

### Layer E -- Distribution beyond GitHub Releases

- **winget**: PR a manifest to `microsoft/winget-pkgs` pointing at
  the GitHub Release. Users then `winget install NoteAerator`. Tiny
  effort once.
- **Chocolatey/Scoop**: similar, smaller audiences.
- **Microsoft Store**: as discussed above.

### Layer F -- Release engineering polish

- **Versioning**: `release-please` or `GitVersion` to auto-derive
  semver from commit messages -- no more hand-tagging.
- **Changelog**: auto-generated from commit messages.
- **CODEOWNERS** + branch protection + required reviewers.
- **Dependabot** for NuGet + GitHub Actions versions.
- **CodeQL** SAST (free for public repos).

### Layer G -- Runtime observability (optional)

- **Crash reporting**: Sentry, App Center, or self-host. Would need
  an opt-in toggle given the project's privacy ethos.
- **Auto-update**: Velopack or Squirrel for non-Store builds; Store
  handles its own users.

## Suggested sequencing

The single highest-leverage step is **writing tests** (Layer A).
Everything else is sand without that. The current pipeline already
does the build + release; the missing piece is *gating* on something
other than "did the build compile".

| # | Step | Investment | Value |
|---|---|---|---|
| 1 | Test project + cover SearchEngine, CommentStore, anchor logic | 1 day | Stops the next regression cold |
| 2 | PR validation workflow + branch protection | 2-3 hours | Forces gates to actually run |
| 3 | SBOM + warning-as-error in release flow | 1-2 hours | Free quality signal |
| 4 | Code-sign cert + signtool step | Half day + cert procurement | No more SmartScreen warnings |
| 5 | MSIX packaging (sideload only) | 1 day | Modern install + AppInstaller auto-update |
| 6 | winget manifest PR | 1 hour | Big distribution win for tiny effort |
| 7 | Microsoft Store submission | 2-3 days + review | Broadest distribution, Store signs |

**Recommended order**: 1 -> 2 -> 3 -> 6 -> 4 -> 5 -> 7. Tests first,
distribution before signing (because Store signs for you anyway),
signing/MSIX/Store last because they have real costs.

## Decision

_Pending -- to be filled in by the user._

When deciding, a relevant question: **do you already have a
Microsoft Partner Center / Microsoft Store dev account?** That gates
the Store path. Without one, only steps 1-6 are immediately
actionable.
