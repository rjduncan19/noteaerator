<#
.SYNOPSIS
  Uninstall Noteaerator (removes the install directory and Start Menu shortcut).

.DESCRIPTION
  Reads the .install-manifest.json that install.ps1 left behind to find
  exactly what was installed where, then removes both the install dir
  and the Start Menu shortcut.

  If the manifest is missing, falls back to the default locations:
    - C:\Program Files\Noteaerator
    - $Env:LocalAppData\Programs\Noteaerator

.PARAMETER InstallPath
  Override the install location to remove.
#>
[CmdletBinding()]
param(
  [string]$InstallPath
)

$ErrorActionPreference = 'Stop'
$AppName = 'Noteaerator'

# Resolve install path: parameter > known defaults
$candidates = @()
if ($InstallPath) { $candidates += $InstallPath }
$candidates += @(
  (Join-Path $Env:ProgramFiles  $AppName),
  (Join-Path $Env:LocalAppData  "Programs\$AppName")
)
$found = $candidates | Where-Object { Test-Path (Join-Path $_ "$AppName.exe") } | Select-Object -First 1
if (-not $found) {
  Write-Warning "Could not find an installed $AppName. Tried:`n$($candidates -join "`n")"
  exit 1
}
$InstallPath = $found

# Read manifest (best-effort)
$manifestPath = Join-Path $InstallPath '.install-manifest.json'
$manifest = $null
if (Test-Path $manifestPath) {
  try { $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json } catch { }
}
$perUser = if ($manifest) { [bool]$manifest.perUser } else { $InstallPath -like "$Env:LocalAppData*" }

# Self-elevate if removing system-wide install and not admin
if (-not $perUser) {
  $isAdmin = ([Security.Principal.WindowsPrincipal]`
              [Security.Principal.WindowsIdentity]::GetCurrent()`
             ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if (-not $isAdmin) {
    Write-Host "Re-launching as Administrator..." -ForegroundColor Yellow
    $argList = @('-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$PSCommandPath`"",
                 '-InstallPath',"`"$InstallPath`"")
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs -Wait
    exit $LASTEXITCODE
  }
}

# Stop running instances
Get-Process -Name $AppName -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Host "Stopping running $AppName (pid=$($_.Id))..." -ForegroundColor Yellow
  try { Stop-Process -Id $_.Id -Force; Start-Sleep -Milliseconds 500 } catch { }
}

# Remove Start Menu shortcut
$startMenuRoot = if ($perUser) {
  Join-Path $Env:AppData     'Microsoft\Windows\Start Menu\Programs'
} else {
  Join-Path $Env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
}
$lnk = if ($manifest -and $manifest.shortcut) { $manifest.shortcut } else { $null }
$candidateLnks = @(
  $lnk,
  (Join-Path $startMenuRoot 'Note Aerator.lnk'),
  (Join-Path $startMenuRoot "$AppName.lnk")
) | Where-Object { $_ -and (Test-Path $_) } | Sort-Object -Unique
foreach ($l in $candidateLnks) {
  Remove-Item $l -Force -ErrorAction SilentlyContinue
  Write-Host "Removed shortcut $l"
}

# Remove install dir
Remove-Item -Recurse -Force $InstallPath
Write-Host "Removed $InstallPath" -ForegroundColor Green
Write-Host ""
Write-Host "Note: per-user state in %APPDATA%\noteaerator is left in place"
Write-Host "      (it holds your projects.json list of opened folders)."
