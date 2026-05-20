<#
.SYNOPSIS
  Build a Microsoft Store-ready MSIX bundle for Note Aerator.

.DESCRIPTION
  Publishes the WPF host self-contained for x64 and (optionally) arm64,
  copies Package.appxmanifest + the visual-asset set into each per-arch
  staging directory, packs each into a .msix with `makeappx pack`, and
  combines them into a single `.msixbundle` ready for upload to Partner
  Center.

  makeappx.exe is obtained automatically from the
  `Microsoft.Windows.SDK.BuildTools` NuGet package — no separate Windows
  SDK install required. The package is restored into
  `packaging\store\.tools\` (gitignored).

  Output files land in `packaging\store\dist\`:
    NoteAerator-<version>-x64.msix
    NoteAerator-<version>-arm64.msix    (when -IncludeArm64)
    NoteAerator-<version>.msixbundle

  The .msixbundle is what you upload to Partner Center.

  IMPORTANT: Before running, fill in the three placeholder values in
  `packaging\store\Package.appxmanifest` (search for
  __PARTNER_CENTER_*__ and replace with the values from Partner Center
  → Product management → Product identity). The script fails fast if
  any placeholder is still present.

.PARAMETER Version
  Four-part version (Major.Minor.Build.Revision). Defaults to the value
  embedded in Package.appxmanifest's <Identity Version="..."/>. Pass
  this to override at build time (e.g. -Version 0.2.0.0).

.PARAMETER IncludeArm64
  Also build the arm64 .msix and include it in the bundle. Default off
  to keep first-run iterations fast.

.PARAMETER SkipPublish
  Reuse the existing publish output (useful for iterating on the manifest
  without re-publishing 160 MB per arch).
#>
[CmdletBinding()]
param(
  [string]$Version,
  [switch]$IncludeArm64,
  [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $here '..\..')
$wpfProj = Join-Path $repoRoot 'POC\Noteaerator\Noteaerator.csproj'
$manifestSrc = Join-Path $here 'Package.appxmanifest'
$assetsSrc = Join-Path $here 'Assets'
$distDir = Join-Path $here 'dist'
$toolsDir = Join-Path $here '.tools'

if (-not (Test-Path $wpfProj)) { throw "WPF csproj not found: $wpfProj" }
if (-not (Test-Path $manifestSrc)) { throw "Manifest not found: $manifestSrc" }
if (-not (Test-Path $assetsSrc)) { throw "Assets directory not found: $assetsSrc" }

# --- 1. Sanity-check manifest placeholders ---
$manifestText = Get-Content -Raw -LiteralPath $manifestSrc
$placeholders = @(
  '__PARTNER_CENTER_IDENTITY_NAME__',
  '__PARTNER_CENTER_PUBLISHER_ID__',
  '__PARTNER_CENTER_PUBLISHER_DISPLAY_NAME__'
)
$unfilled = $placeholders | Where-Object { $manifestText -match [Regex]::Escape($_) }
if ($unfilled.Count -gt 0) {
  Write-Host ""
  Write-Host "ERROR: Package.appxmanifest still has placeholder(s):" -ForegroundColor Red
  foreach ($p in $unfilled) { Write-Host "  $p" -ForegroundColor Red }
  Write-Host ""
  Write-Host "Fill them in from Partner Center -> My apps -> Note Aerator" -ForegroundColor Yellow
  Write-Host "                 -> Product management -> Product identity" -ForegroundColor Yellow
  Write-Host "before re-running this script." -ForegroundColor Yellow
  throw 'manifest has unfilled placeholders'
}

# --- 2. Read / override version ---
if (-not $Version) {
  if ($manifestText -match 'Identity[^>]*\sVersion="([\d\.]+)"') {
    $Version = $matches[1]
  } else {
    throw 'Could not read Identity Version from Package.appxmanifest.'
  }
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
  throw "Version must be four-part (got: $Version)."
}
Write-Host "Note Aerator MSIX builder" -ForegroundColor Cyan
Write-Host "  version : $Version"
$includesLabel = if ($IncludeArm64) { 'x64 + arm64' } else { 'x64' }
Write-Host "  includes: $includesLabel"
Write-Host ""

# --- 3. Locate makeappx.exe (download Microsoft.Windows.SDK.BuildTools on demand) ---
function Get-MakeAppxPath {
  $sdkToolVersion = '10.0.26100.1'
  $sdkPkgDir = Join-Path $toolsDir "Microsoft.Windows.SDK.BuildTools.$sdkToolVersion"
  if (-not (Test-Path $sdkPkgDir)) {
    Write-Host "Restoring Microsoft.Windows.SDK.BuildTools $sdkToolVersion ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force $toolsDir | Out-Null
    $restoreCsproj = Join-Path $toolsDir 'restore.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RestorePackagesPath>$toolsDir</RestorePackagesPath>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
    <NoWarn>NU1503;NU1701;NETSDK1057;NETSDK1059</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageDownload Include="Microsoft.Windows.SDK.BuildTools" Version="[$sdkToolVersion]" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $restoreCsproj -Encoding UTF8
    & dotnet restore $restoreCsproj --packages $toolsDir --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore of Microsoft.Windows.SDK.BuildTools failed." }
  }
  # NuGet lowercases the on-disk folder.
  $candidate = Get-ChildItem -Path $toolsDir -Recurse -Filter 'makeappx.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Select-Object -First 1
  if (-not $candidate) { throw "makeappx.exe not found after restore. Check $toolsDir." }
  return $candidate.FullName
}
$makeappx = Get-MakeAppxPath
Write-Host "Using makeappx: $makeappx"
Write-Host ""

# --- 4. For each arch: publish, stage, pack ---
$archs = @('win-x64')
if ($IncludeArm64) { $archs += 'win-arm64' }
$msixOut = @{}

if (Test-Path $distDir) {
  Get-ChildItem -Force $distDir -Filter '*.msix*' | Remove-Item -Force -ErrorAction SilentlyContinue
} else {
  New-Item -ItemType Directory -Force $distDir | Out-Null
}

foreach ($rid in $archs) {
  $archShort = if ($rid -eq 'win-x64') { 'x64' } else { 'arm64' }
  Write-Host "==[$archShort]==" -ForegroundColor Cyan

  $publishDir = Join-Path $repoRoot "POC\Noteaerator\bin\Release\msix-publish\$rid"
  if (-not $SkipPublish -or -not (Test-Path $publishDir)) {
    Write-Host "Publishing self-contained $rid ..."
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    & dotnet publish $wpfProj `
      -c Release `
      -r $rid `
      --self-contained true `
      -p:PublishSingleFile=false `
      -p:PublishDir=$publishDir\ `
      --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid (exit $LASTEXITCODE)." }
  } else {
    Write-Host "Reusing existing publish at $publishDir"
  }

  $stageDir = Join-Path $here ".stage\$archShort"
  if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
  New-Item -ItemType Directory -Force $stageDir | Out-Null

  Write-Host "Staging payload..."
  # 4a. App payload
  Copy-Item -Recurse -Force "$publishDir\*" $stageDir
  # 4b. Visual assets
  $stageAssets = Join-Path $stageDir 'Assets'
  New-Item -ItemType Directory -Force $stageAssets | Out-Null
  Copy-Item -Recurse -Force "$assetsSrc\*" $stageAssets
  # 4c. Manifest, with ProcessorArchitecture + Version pinned for this arch
  $perArchManifest = $manifestText `
    -replace 'ProcessorArchitecture="[^"]+"', "ProcessorArchitecture=`"$archShort`"" `
    -replace '(Identity[^>]*\sVersion=)"[\d\.]+"', "`$1`"$Version`""
  Set-Content -LiteralPath (Join-Path $stageDir 'AppxManifest.xml') -Value $perArchManifest -Encoding UTF8

  $msixPath = Join-Path $distDir "NoteAerator-$Version-$archShort.msix"
  Write-Host "Packing $msixPath ..."
  & $makeappx pack /d $stageDir /p $msixPath /o | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $rid." }
  $msixOut[$archShort] = $msixPath
}

# --- 5. Bundle ---
$bundleStage = Join-Path $here '.bundlestage'
if (Test-Path $bundleStage) { Remove-Item -Recurse -Force $bundleStage }
New-Item -ItemType Directory -Force $bundleStage | Out-Null
foreach ($p in $msixOut.Values) { Copy-Item -Force $p $bundleStage }

$bundlePath = Join-Path $distDir "NoteAerator-$Version.msixbundle"
Write-Host ""
Write-Host "==[bundle]==" -ForegroundColor Cyan
Write-Host "Bundling $bundlePath ..."
& $makeappx bundle /d $bundleStage /p $bundlePath /bv $Version /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed." }
Remove-Item -Recurse -Force $bundleStage

Write-Host ""
Write-Host "[OK] Built MSIX bundle:" -ForegroundColor Green
Write-Host "       $bundlePath"
Write-Host ""
Write-Host "Per-arch packages:"
foreach ($a in $msixOut.Keys | Sort-Object) {
  $sz = '{0:N1} MB' -f ((Get-Item $msixOut[$a]).Length / 1MB)
  Write-Host "       $a   $($msixOut[$a])   ($sz)"
}
$bundleSize = '{0:N1} MB' -f ((Get-Item $bundlePath).Length / 1MB)
Write-Host ""
Write-Host "Upload $($bundlePath | Split-Path -Leaf) ($bundleSize) to:"
Write-Host "       Partner Center -> Note Aerator -> Submission -> Packages"
Write-Host ""
Write-Host "The Store will sign the bundle during ingestion; no signtool step is needed."
