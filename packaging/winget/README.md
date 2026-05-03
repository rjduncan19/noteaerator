# winget manifest

This folder holds the [winget] package manifest for **Note Aerator**.
It targets the published GitHub Release (`v0.1.1-poc`) zip assets and
treats the app as a `portable` installer (no MSI / MSIX yet -- the
zip is extracted into winget's per-package folder and `noteaerator`
is added as a command alias).

[winget]: https://learn.microsoft.com/windows/package-manager/

## Layout

```
packaging/winget/
└── rjduncan19.NoteAerator/
    └── 0.1.1-poc/
        ├── rjduncan19.NoteAerator.yaml                  # version
        ├── rjduncan19.NoteAerator.locale.en-US.yaml     # name / description / tags
        └── rjduncan19.NoteAerator.installer.yaml        # arch + URL + SHA256
```

## Try it locally (no submission needed)

```pwsh
winget install --manifest packaging\winget\rjduncan19.NoteAerator\0.1.1-poc
```

After install, run the command alias `noteaerator` from any shell.

To uninstall: `winget uninstall rjduncan19.NoteAerator`.

## Submitting to the public repo

To make `winget install rjduncan19.NoteAerator` work for everyone,
the manifest needs to be merged into
<https://github.com/microsoft/winget-pkgs>:

```pwsh
# Once, install the validation tool
winget install --id Microsoft.WingetCreate -e

# Validate the schema
winget validate packaging\winget\rjduncan19.NoteAerator\0.1.1-poc

# Submit a PR against microsoft/winget-pkgs
wingetcreate submit `
  --token <gh-personal-access-token-with-public_repo-scope> `
  --prtitle "Add rjduncan19.NoteAerator 0.1.1-poc" `
  packaging\winget\rjduncan19.NoteAerator\0.1.1-poc
```

The winget-pkgs CI runs schema and installer validation; once it
passes and a maintainer reviews it, the package becomes globally
installable.

## Updating for a new release

1. Cut the new GitHub Release (push a `v*` tag; the workflow does
   the rest).
2. Compute the SHA256 of each released zip:
   ```pwsh
   gh release download <tag> -R rjduncan19/noteaerator -p "*.zip" -D tmp
   Get-FileHash tmp\NoteAerator-<tag>-win-x64.zip   -Algorithm SHA256
   Get-FileHash tmp\NoteAerator-<tag>-win-arm64.zip -Algorithm SHA256
   ```
3. Copy this folder to `packaging/winget/rjduncan19.NoteAerator/<tag>/`.
4. Update `PackageVersion`, `InstallerUrl`, and `InstallerSha256` in
   the three YAMLs.
5. Validate + submit as above.

A future improvement is to wire this into the release workflow so the
manifest is updated automatically on every tag push.
