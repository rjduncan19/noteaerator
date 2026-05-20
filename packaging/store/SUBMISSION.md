# Microsoft Store submission walkthrough

End-to-end instructions for the first Note Aerator submission to the
Microsoft Store under an **individual Partner Center account**.

> **Everything you need is in this folder** (`packaging/store/`).
> The only things you have to provide yourself are: (a) three identity
> values from Partner Center, and (b) at least one app screenshot.

---

## 0. Prerequisites (one-time)

- A Partner Center account (the **Individual** plan, $19 one-time, is
  fine for this app).
- The app name **"Note Aerator"** reserved in Partner Center
  (My apps → New product → MSIX or PWA app → Reserve a name).
- A working build environment on your PC: .NET 8 SDK, PowerShell 5.1+,
  internet access (the build script downloads the Windows SDK BuildTools
  NuGet package on demand).

You do **not** need:

- A code-signing certificate. The Store re-signs the bundle during
  ingestion.
- A local install of the full Windows SDK. The build script pulls
  `Microsoft.Windows.SDK.BuildTools` into `packaging/store/.tools/`
  automatically.

---

## 1. Get the three identity values from Partner Center

In Partner Center, open your reserved app and go to:

    My apps → Note Aerator → Product management → Product identity

Copy the three values shown there. They look roughly like:

| Field                    | Example                                          |
| ------------------------ | ------------------------------------------------ |
| Package/Identity Name    | `12345AppName.NoteAerator`                       |
| Publisher                | `CN=ABCDEF12-3456-7890-ABCD-1234567890AB`        |
| Publisher display name   | `Richard Duncan`                                 |

## 2. Paste them into `Package.appxmanifest`

Open `packaging/store/Package.appxmanifest` and replace the three
placeholder tokens:

- `__PARTNER_CENTER_IDENTITY_NAME__`         → your Identity Name
- `__PARTNER_CENTER_PUBLISHER_ID__`          → your Publisher (`CN=...`)
- `__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__`→ your Publisher display name

Save. Do **not** commit these values - they are tied to your account.

## 3. Build the MSIX bundle

From the repo root:

```powershell
.\packaging\store\build-msix.ps1
```

Optional flags:

- `-Version 0.1.3.0` to override the version (default reads from the
  `.csproj` and appends `.0`).
- `-IncludeArm64` to also build an arm64 .msix and bundle both.

What the script does:

1. Validates that you have replaced all three manifest placeholders.
2. Restores `Microsoft.Windows.SDK.BuildTools` into
   `packaging/store/.tools/` (only on first run, ~50 MB).
3. Runs `dotnet publish -c Release -r win-x64 --self-contained` and
   stages the payload + the `Assets/` folder + your filled-in
   `AppxManifest.xml`.
4. Runs `makeappx pack` to produce `dist/NoteAerator-<ver>-x64.msix`.
5. Runs `makeappx bundle` to wrap it in
   `dist/NoteAerator-<ver>.msixbundle`.

Expected output (relative to repo root):

    packaging\store\dist\NoteAerator-0.1.2.0-x64.msix      (~69 MB)
    packaging\store\dist\NoteAerator-0.1.2.0.msixbundle    (~69 MB)

The `.msixbundle` is what you upload.

## 4. Capture screenshots

Partner Center requires **at least one screenshot** per device family.
Screenshot requirements: 1366×768 minimum, PNG or JPG.

Quick recipe:

1. Launch the installed app.
2. Open a folder of nice-looking Markdown (the repo's own `docs/`
   folder works well, or any project with diagrams/code).
3. Take 3-5 screenshots covering:
   - The viewer with a rendered note (showing typography + a heading
     outline / link).
   - A Mermaid diagram or code block.
   - The projects sidebar with multiple files.
   - (Optional) The native Ctrl+F find bar in action.
4. Save them somewhere you can find them at upload time, e.g.
   `packaging/store/screenshots/` (gitignored).

This is the one part of the submission no script can do for you.

---

## 5. Submit in Partner Center

In Partner Center, open the app and start a new submission. Fill out
each section as follows.

### 5a. Pricing and availability

- **Markets**: All available markets.
- **Visibility**: Public.
- **Schedule**: Release as soon as it passes certification.
- **Pricing**: Free.
- **Free trial**: No.
- **In-app purchases**: No.
- **Organizational licensing**: leave defaults.

### 5b. Properties

- **Category**: Productivity → Personal finance / File managers.
  (Closest fit: **Productivity** with subcategory "File managers" if the
  Store still offers it; otherwise leave subcategory blank.)
- **Privacy policy URL**:
  `https://github.com/rjduncan19/noteaerator/blob/main/PRIVACY.md`
- **Website**:
  `https://github.com/rjduncan19/noteaerator`
- **Support contact info**:
  `https://github.com/rjduncan19/noteaerator/issues`
- **Hardware preferences**: Keyboard + Mouse required.
- **App declarations**:
  - Accesses arbitrary file paths chosen by the user: **Yes** (this is
    inherent to a Markdown viewer; the user picks the folder).
  - All other declarations: **No**.

### 5c. Age ratings

Open `listing/age-rating-answers.md` and use the answers there. The
expected outcome is "All ages" / IARC 3+.

### 5d. Packages

Upload `packaging/store/dist/NoteAerator-<ver>.msixbundle`.

Partner Center will validate the bundle (a few minutes). It should
recognize:

- Identity matches your reserved app.
- Architecture x64 (and arm64 if you used `-IncludeArm64`).
- Target device family Windows.Desktop, min build 17763.

### 5e. Store listings (English (United States))

| Field                       | Where to find / paste                              |
| --------------------------- | -------------------------------------------------- |
| Display name                | `Note Aerator`                                     |
| Description                 | Paste from `listing/description.md`                |
| What's new in this version  | Paste from `listing/whats-new.md`                  |
| Product features            | One bullet per line from `listing/features.txt`    |
| Short description           | Paste from `listing/short-description.txt`         |
| Search terms                | `markdown, markdown viewer, notes, knowledge base, copilot, ai notes, obsidian alternative` |
| Copyright / trademark info  | `© 2026 Richard Duncan. MIT licensed.`             |
| Additional license terms    | (leave blank - the MIT license is on GitHub)       |
| Developed by                | (leave blank, or your name if you want it shown)   |
| Published by                | (matches Publisher display name from §1)           |

**Store logos / images** (under "Store logos"):

- **Store logo (300×300)**: upload `listing/StoreLogo-300x300.png`.
- All other store logos are optional - skip them for the first
  submission.

**Screenshots**:

- Upload the screenshots you captured in §4 under
  Desktop screenshots. One is the minimum; 4-8 is better.

### 5f. System requirements

Paste from `listing/system-requirements.txt` into the
"System requirements" → "Recommended hardware" / "Notes" fields. The
hard package-level minimum (Windows 10 build 17763) is already declared
in the manifest, so the Store will enforce it automatically.

### 5g. Submission options

In **Notes for certification**, paste:

```
Note Aerator is a WPF/.NET 8 desktop application packaged as MSIX. It
uses the runFullTrust restricted capability (standard for desktop bridge
apps). The app renders Markdown files from folders the user chooses; it
does not access the network beyond the embedded WebView2 control loading
local file:// URIs and a small number of CDN-hosted libraries
(Marked, highlight.js, Mermaid, KaTeX) used purely for client-side
rendering. No data is uploaded. WebView2 runtime is a dependency and is
available on all supported Windows 10 / 11 systems.

If WebView2 is missing on the target machine, the app shows a one-click
download prompt on launch.
```

## 6. Publish

Click **Submit to the Store**.

First-time submissions typically take **1–3 business days** to clear
certification. You'll get email notifications at each stage.

---

## 7. After certification

- Tag the release in git: `git tag v0.1.2 && git push --tags`.
- Update `README.md` with a Microsoft Store badge / link.
- Add a `winget` manifest update (the existing `packaging/winget/`
  layout can be updated to point at the Store package, or kept as a
  parallel distribution).

---

## Files in this folder

```
packaging/store/
├── Package.appxmanifest        # MSIX manifest (fill in 3 values)
├── build-msix.ps1              # End-to-end build script
├── generate-assets.py          # Regenerates Assets/ from the source SVG
├── Assets/                     # 47 generated PNG visual assets
├── listing/
│   ├── description.md
│   ├── short-description.txt
│   ├── features.txt
│   ├── whats-new.md
│   ├── system-requirements.txt
│   ├── age-rating-answers.md
│   └── StoreLogo-300x300.png
├── SUBMISSION.md               # This file
└── .gitignore                  # Ignores .tools/ .stage/ dist/ etc.
```
