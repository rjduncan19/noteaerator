<#
.SYNOPSIS
  Build a Release publish of Noteaerator and install it to Program Files
  with a Start Menu entry.

.DESCRIPTION
  Steps performed:
    1. Self-elevates to Administrator (required to write to Program Files
       and the All-Users Start Menu).
    2. Runs `dotnet publish -c Release -r win-x64 --self-contained` to
       produce a **framework-independent** build that embeds the .NET 8
       runtime -- the target machine does NOT need .NET installed.
       Pass -FrameworkDependent for a smaller install (~2 MB instead of
       ~80 MB) on machines that already have the .NET 8 Desktop runtime.
    3. Stops any running Noteaerator.exe so files can be replaced.
    4. Copies the publish output to the install directory
       (default: C:\Program Files\Noteaerator).
    5. Creates a Start Menu shortcut
       (default: All Users -> Programs\Noteaerator.lnk).

  WebView2 runtime is REQUIRED at runtime but is shipped with Edge on
  Windows 10/11 so no separate install is needed for those OS versions.

  This is a development-style installer -- it produces a real installed
  app (with Start Menu entry) but is not an MSI / signed package.

.PARAMETER InstallPath
  Where to install. Defaults to "$Env:ProgramFiles\Noteaerator".

.PARAMETER FrameworkDependent
  Build a smaller framework-dependent publish (~3 MB) that requires the
  .NET 8 Desktop runtime on the target machine. Default is self-contained
  (framework-independent), which embeds the runtime and is ~160 MB.

.PARAMETER PerUser
  Create the Start Menu entry under the current user only instead of
  All Users. Implies a per-user install path
  ("$Env:LocalAppData\Programs\Noteaerator") and skips elevation.

.EXAMPLE
  # Standard install (admin, framework-independent / self-contained)
  .\install.ps1

.EXAMPLE
  # Smaller install assuming .NET 8 Desktop runtime is already present
  .\install.ps1 -FrameworkDependent

.EXAMPLE
  # Per-user install, no admin needed
  .\install.ps1 -PerUser
#>
[CmdletBinding()]
param(
  [string]$InstallPath,
  [switch]$FrameworkDependent,
  [switch]$PerUser
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $here 'Noteaerator\Noteaerator.csproj'
$AppName = 'Noteaerator'

if (-not (Test-Path $proj)) {
  throw "Cannot find $proj. Run install.ps1 from the POC folder."
}

# Resolve defaults
if (-not $InstallPath) {
  $InstallPath = if ($PerUser) {
    Join-Path $Env:LocalAppData "Programs\$AppName"
  } else {
    Join-Path $Env:ProgramFiles $AppName
  }
}

# Self-elevate when needed (system-wide install)
if (-not $PerUser) {
  $isAdmin = ([Security.Principal.WindowsPrincipal]`
              [Security.Principal.WindowsIdentity]::GetCurrent()`
             ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if (-not $isAdmin) {
    Write-Host "Re-launching as Administrator..." -ForegroundColor Yellow
    $argList = @('-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$PSCommandPath`"")
    if ($PSBoundParameters.ContainsKey('InstallPath')) { $argList += @('-InstallPath',"`"$InstallPath`"") }
    if ($FrameworkDependent) { $argList += '-FrameworkDependent' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs -Wait
    exit $LASTEXITCODE
  }
}

# 1. Verify .NET 8 SDK (needed to BUILD; not needed on target if self-contained)
try { $sdks = & dotnet --list-sdks 2>$null } catch { throw ".NET 8 SDK is required to build. Install from https://dotnet.microsoft.com/download" }
if (-not ($sdks | Where-Object { $_ -match '^[89]\.' -or $_ -match '^\d{2,}\.' })) {
  throw ".NET 8 SDK (or newer) not found. Installed:`n$($sdks -join "`n")"
}

# 2. Publish -- self-contained by default (framework-independent)
$publishDir = Join-Path $here "Noteaerator\bin\Release\publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

$publishArgs = @(
  'publish', $proj,
  '-c', 'Release',
  '-r', 'win-x64',
  "-p:PublishDir=$publishDir\",
  '-p:UseAppHost=true',
  '--nologo'
)
if ($FrameworkDependent) {
  $publishArgs += @('--self-contained','false')
  $kind = 'framework-dependent (~3 MB; needs .NET 8 Desktop on target)'
} else {
  $publishArgs += @('--self-contained','true','-p:PublishSingleFile=false')
  $kind = 'self-contained / framework-independent (~160 MB)'
}

Write-Host "Building Release publish -- $kind ..." -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

if (-not (Test-Path "$publishDir\$AppName.exe")) {
  throw "Publish output is missing $AppName.exe at $publishDir."
}

# 3. Stop any running instance so we can overwrite the binaries
Get-Process -Name $AppName -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Host "Stopping running $AppName (pid=$($_.Id))..." -ForegroundColor Yellow
  try { Stop-Process -Id $_.Id -Force; Start-Sleep -Milliseconds 500 } catch { }
}

# 4. Copy to install dir
Write-Host "Installing to $InstallPath ..." -ForegroundColor Cyan
if (Test-Path $InstallPath) {
  # Wipe everything except a possible user-data folder we never write here anyway
  Get-ChildItem -Force $InstallPath | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
  New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}
Copy-Item -Recurse -Force "$publishDir\*" $InstallPath

$installedExe = Join-Path $InstallPath "$AppName.exe"
if (-not (Test-Path $installedExe)) { throw "Install incomplete: $installedExe missing." }

# 5. Start Menu shortcut
$startMenuRoot = if ($PerUser) {
  Join-Path $Env:AppData 'Microsoft\Windows\Start Menu\Programs'
} else {
  Join-Path $Env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
}
$lnkPath = Join-Path $startMenuRoot 'Note Aerator.lnk'
# Best-effort cleanup of legacy shortcut from earlier install
$legacyLnk = Join-Path $startMenuRoot "$AppName.lnk"
if (Test-Path $legacyLnk) { Remove-Item -Force $legacyLnk -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $startMenuRoot -Force | Out-Null

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($lnkPath)
$shortcut.TargetPath       = $installedExe
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description      = 'Note Aerator -- AI-first Markdown viewer'
# Use the icon embedded in the exe (set by <ApplicationIcon> in the csproj)
$shortcut.IconLocation     = "$installedExe,0"
$shortcut.Save()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($wsh) | Out-Null

# 6. Write a tiny manifest so uninstall.ps1 can find what we installed
$manifest = @{
  installPath = $InstallPath
  shortcut    = $lnkPath
  perUser     = [bool]$PerUser
  installedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Depth 3
Set-Content -Path (Join-Path $InstallPath '.install-manifest.json') -Value $manifest -Encoding UTF8

Write-Host ""
Write-Host "[OK] Installed to:    $InstallPath"   -ForegroundColor Green
Write-Host "[OK] Start Menu:      $lnkPath"        -ForegroundColor Green
Write-Host "[OK] Run by typing:   note aerator     (Win key, then type)"
Write-Host ""
Write-Host "Note: WebView2 runtime is required at runtime. It is included with Edge"
Write-Host "      on Windows 10/11, so no separate install is needed there."
