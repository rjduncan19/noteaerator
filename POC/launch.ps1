<#
.SYNOPSIS
  Build and launch the noteaerator POC.

.DESCRIPTION
  One-stop launcher for the noteaerator POC. Verifies the .NET 8 SDK is
  installed, restores + builds the WPF app on first run (or when sources
  change), then starts it. Pass -Rebuild to force a clean rebuild.

  This is a development launcher, not a packaged installer. There is no
  MSI / system-wide install — the app runs from the build output under
  POC\Noteaerator\bin\.

.EXAMPLE
  .\launch.ps1

.EXAMPLE
  .\launch.ps1 -Rebuild
#>
[CmdletBinding()]
param(
  [switch]$Rebuild,
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $here 'Noteaerator\Noteaerator.csproj'

if (-not (Test-Path $proj)) {
  Write-Error "Could not find $proj. Run this from the POC folder."
  exit 1
}

# Check for .NET SDK 8+
try {
  $sdks = & dotnet --list-sdks 2>$null
} catch {
  Write-Error ".NET SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download"
  exit 1
}
$has8 = $sdks | Where-Object { $_ -match '^[89]\.' -or $_ -match '^\d{2,}\.' }
if (-not $has8) {
  Write-Error ".NET 8 SDK (or newer) not found. Installed SDKs:`n$($sdks -join "`n")"
  exit 1
}

Push-Location $here
try {
  if ($Rebuild) {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    & dotnet clean $proj -c $Configuration | Out-Null
  }

  Write-Host "Building ($Configuration)..." -ForegroundColor Cyan
  & dotnet build $proj -c $Configuration --nologo -v minimal
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit $LASTEXITCODE
  }

  Write-Host "Launching Note Aerator..." -ForegroundColor Green
  # --no-build: we just built. Start detached so closing this terminal doesn't kill the app.
  $exe = Join-Path $here "Noteaerator\bin\$Configuration\net8.0-windows\Noteaerator.exe"
  if (Test-Path $exe) {
    Start-Process -FilePath $exe
  } else {
    # Fallback: dotnet run
    Start-Process -FilePath 'dotnet' -ArgumentList @('run','--project',$proj,'-c',$Configuration,'--no-build')
  }
}
finally {
  Pop-Location
}
