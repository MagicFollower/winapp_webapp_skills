<#
  Publishes the two Windows artifacts into <Share>\<productName>\<version>\ with sha256 sidecars.
  Gate: the exe's FileVersion must equal package.json version, otherwise the release folder is
  holding a stale build and copying is refused.
  ASCII-only for Windows PowerShell 5.1.
#>

param(
  [string]$Share = '',
  [ValidateSet('dir', 'file')]
  [string]$Mode = 'dir',
  [string]$Target = '',
  [int]$KeepVersions = 2,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Share)) { $Share = $Target }
if ([string]::IsNullOrWhiteSpace($Share)) { Write-Host '!! pass -Share <dir or \\server\apps>'; exit 1 }

$pkg = Get-Content (Join-Path $root 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $pkg.version
$product = $pkg.productName

$release = Join-Path $root 'release'
$artifacts = @(
  (Join-Path $release "${product}_portable.exe" -ErrorAction SilentlyContinue),
  (Join-Path $release "${product}_setup.exe" -ErrorAction SilentlyContinue)
) | Where-Object { $_ -and (Test-Path $_) }

if ($artifacts.Count -eq 0) {
  Write-Host "!! no artifacts under $release (expected ${product}_portable.exe / ${product}_setup.exe)"
  exit 1
}

foreach ($a in $artifacts) {
  $fv = (Get-Item $a).VersionInfo.FileVersion
  if ($fv -ne $version) {
    Write-Host "!! version gate: $([IO.Path]::GetFileName($a)) FileVersion=$fv but package.json version=$version"
    Write-Host '   bump-then-repackage: re-run bin/electron-win-build.ps1 -Target all before publishing'
    exit 1
  }
  Write-Host ("    version-ok {0} FileVersion={1}" -f [IO.Path]::GetFileName($a), $fv)
}

$dest = Join-Path (Join-Path $Share $product) $version
Write-Host "==> destination: $dest"

if ($DryRun) {
  $artifacts | ForEach-Object { Write-Host "    dry-run would copy $([IO.Path]::GetFileName($_))" }
  Write-Host 'publish=dry-run'
  exit 0
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
foreach ($a in $artifacts) {
  $leaf = [IO.Path]::GetFileName($a)
  Copy-Item $a (Join-Path $dest $leaf) -Force
  $hash = (Get-FileHash -Path $a -Algorithm SHA256).Hash.ToLower()
  Set-Content -Path (Join-Path $dest "$leaf.sha256") -Value "$hash  $leaf" -Encoding ASCII
  Write-Host ("    copied {0} bytes={1} sha256={2}" -f $leaf, (Get-Item (Join-Path $dest $leaf)).Length, $hash.Substring(0, 16))
}

$base = Join-Path $Share $product
$kept = Get-ChildItem -Path $base -Directory | Sort-Object LastWriteTime -Descending
$retained = @($kept | Select-Object -First $KeepVersions)
$kept | Select-Object -Skip $KeepVersions | ForEach-Object {
  Write-Host "    pruning $($_.Name)"
  Remove-Item -Recurse -Force $_.FullName
}
Write-Host ("retained-versions={0}" -f $retained.Count)
Write-Host 'publish=PASS'
