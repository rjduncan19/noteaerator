# Installing Note Aerator

This is an early POC. There is no signed installer yet — but there are
**self-contained Windows builds** for both x64 and arm64 that need
nothing on the target machine except a recent Edge / WebView2 runtime
(present by default on Windows 10/11).

## Download

Grab the zip that matches your machine from the latest release:

[**→ Latest release**](https://github.com/rjduncan19/noteaerator/releases/latest)

| File | For | Size |
|---|---|---|
| `NoteAerator-<version>-win-x64.zip`   | Most Windows PCs (Intel/AMD) | ~160 MB |
| `NoteAerator-<version>-win-arm64.zip` | Windows on Arm (Surface Pro X, Snapdragon laptops, etc.) | ~175 MB |

Not sure which you have? In PowerShell:

```pwsh
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
# X64  -> grab win-x64
# Arm64 -> grab win-arm64
```

## Quick install (no admin needed)

1. Download the zip and **right-click → Properties → Unblock**
   (Windows marks downloads from the internet as untrusted; this is
   per-file and only needed once).
2. Extract the zip somewhere persistent — e.g.
   `%LOCALAPPDATA%\Programs\NoteAerator\`.
3. Double-click `Noteaerator.exe` to launch.

That's it. The first launch will create
`%LOCALAPPDATA%\noteaerator\WebView2\` for the embedded browser cache
and `%APPDATA%\noteaerator\viewer\projects.json` for your project list.

## Optional: Start Menu entry

If you want a Start Menu shortcut, run this once after extraction
(adjust `$installPath` if you put it somewhere else):

```pwsh
$installPath = "$Env:LocalAppData\Programs\NoteAerator"
$exe = Join-Path $installPath 'Noteaerator.exe'
$lnk = Join-Path $Env:AppData 'Microsoft\Windows\Start Menu\Programs\Note Aerator.lnk'
$wsh = New-Object -ComObject WScript.Shell
$sc  = $wsh.CreateShortcut($lnk)
$sc.TargetPath       = $exe
$sc.WorkingDirectory = $installPath
$sc.IconLocation     = "$exe,0"
$sc.Description      = 'Note Aerator -- AI-first Markdown viewer'
$sc.Save()
```

Then hit the **Win** key and start typing `note aerator`.

## Building from source (developers)

If you'd rather build it yourself instead of downloading a zip:

```pwsh
# requires .NET 8 SDK + a modern Edge / WebView2 runtime

# Dev launcher (rebuilds + runs from bin\Debug)
cd POC
.\launch.ps1

# Or do a real install with Start Menu entry (admin for system-wide install)
.\install.ps1                 # framework-independent (~160 MB)
.\install.ps1 -FrameworkDependent   # smaller (~3 MB), needs .NET 8 Desktop runtime
.\install.ps1 -PerUser        # no admin, installs to %LOCALAPPDATA%\Programs\
```

To remove a script-installed copy:

```pwsh
.\uninstall.ps1
```

## What you'll see on first launch

- Empty window with "Aerate your Notes" header.
- Click **Add project folder…** and pick any folder containing `.md`
  files. The bundled `POC\sample-notes` folder (only present if you
  cloned the repo) demonstrates Mermaid diagrams, GFM tables, the
  inline `<!-- @ai: -->` marker, and three demo sidecar comments.
- The folder you pick appears as a **horizontal tab** at the top; the
  `.md` files inside show up as a **vertical scrollable list** on the
  left. The right pane is the rendered view.
- Hit **Ctrl+F** (or click the magnifier in the top-right) to search
  across all files in the project (default) or just the current file.

## WebView2 runtime

Note Aerator embeds the **Evergreen** Microsoft Edge WebView2 runtime
(the system-shared install that ships with Edge on Windows 10/11).
No fixed runtime is bundled, so the installer stays small. If the
runtime is somehow missing on your machine (rare; some stripped
images and older Server SKUs), the app will show a friendly dialog
at startup with a download link rather than crashing.

If you ever need to reinstall it manually:
<https://developer.microsoft.com/microsoft-edge/webview2/>.

## Privacy

Note Aerator runs entirely on your machine and **does not collect
any data** about you, your notes, your usage, or anything else.
The only network calls it makes are to fetch a few Markdown rendering
libraries from a public CDN the first time you open a note. Full
policy: [`PRIVACY.md`](https://github.com/rjduncan19/noteaerator/blob/main/PRIVACY.md).

## Uninstall

If you used the zip:

1. Quit Note Aerator.
2. Delete the install folder.
3. (Optional) Delete the Start Menu shortcut at
   `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Note Aerator.lnk`.
4. (Optional) Delete per-user state:
   - `%APPDATA%\noteaerator\` — your project list
   - `%LOCALAPPDATA%\noteaerator\` — WebView2 cache

If you used `install.ps1`, run `.\uninstall.ps1` from the `POC` folder.

## Reporting issues

Open an issue at
<https://github.com/rjduncan19/noteaerator/issues>. Include your
Windows version, architecture, and the steps to reproduce.
