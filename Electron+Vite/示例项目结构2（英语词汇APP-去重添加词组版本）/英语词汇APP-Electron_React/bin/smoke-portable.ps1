<#
  Portable-target smoke: stdout does not bubble out of the portable host, so liveness is proven
  by marker files written by the app (WORD_APP_MARKERS). Reports time-to-first-marker.
  ASCII-only for Windows PowerShell 5.1.
#>

param(
  [string]$Exe = '',
  [int]$TimeoutSec = 240
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $root 'release\WordStudy_portable.exe' }
if (-not (Test-Path $Exe)) { Write-Host "!! exe not found: $Exe"; exit 1 }

$stamp = Join-Path $env:TEMP ("word-app-portable-" + $PID)
$markerDir = Join-Path $stamp 'markers'
$userdata = Join-Path $stamp 'userdata'
$outFile = Join-Path $stamp 'stdout.txt'
New-Item -ItemType Directory -Force -Path $markerDir, $userdata | Out-Null

$env:WORD_APP_MARKERS = $markerDir
$env:SELFTEST_USERDATA = $userdata

$start = Get-Date
$proc = Start-Process -FilePath $Exe -ArgumentList '--selftest' -PassThru -WindowStyle Hidden `
  -RedirectStandardOutput $outFile -RedirectStandardError (Join-Path $stamp 'stderr.txt')
Write-Host ("started pid={0} exe={1}" -f $proc.Id, (Split-Path -Leaf $Exe))

function Wait-Marker([string]$name, [int]$limitSec) {
  $deadline = (Get-Date).AddSeconds($limitSec)
  while ((Get-Date) -lt $deadline) {
    if (Test-Path (Join-Path $markerDir $name)) {
      return [int]((Get-Date) - $start).TotalMilliseconds
    }
    if ($proc.HasExited) { return -1 }
    Start-Sleep -Milliseconds 250
  }
  return -2
}

$shown = Wait-Marker 'window-shown' $TimeoutSec
$probed = if ($shown -ge 0) { Wait-Marker 'probed' 60 } else { -3 }
$done = if ($probed -ge 0) { Wait-Marker 'checks-done' 120 } else { -3 }

$markers = @(Get-ChildItem -Path $markerDir -File -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
Write-Host ("markers=" + ($markers -join ','))
Write-Host ("window-shown-ms={0} probed-ms={1} checks-done-ms={2}" -f $shown, $probed, $done)

$stdoutLen = if (Test-Path $outFile) { (Get-Item $outFile).Length } else { 0 }
Write-Host ("portable-stdout-bytes={0} (0 is expected: the host does not bubble child stdout)" -f $stdoutLen)

if (-not $proc.HasExited) {
  Write-Host "still running after markers, stopping pid $($proc.Id)"
  Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 2
}

$leftover = @(Get-Process -Name 'WordStudy' -ErrorAction SilentlyContinue)
Write-Host ("remaining-processes={0}" -f $leftover.Count)
if ($leftover.Count -gt 0) {
  $leftover | ForEach-Object { Write-Host ("  leftover pid={0} path={1}" -f $_.Id, $_.Path) }
}

$ok = ($shown -ge 0) -and ($done -ge 0) -and ($leftover.Count -eq 0)
if ($ok) { Write-Host 'smoke-portable=PASS'; exit 0 }
Write-Host 'smoke-portable=FAIL'
exit 1
