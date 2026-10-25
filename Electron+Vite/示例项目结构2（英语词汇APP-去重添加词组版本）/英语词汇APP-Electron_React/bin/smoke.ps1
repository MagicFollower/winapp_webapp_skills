<#
  Runs a built exe with --selftest and asserts the marker line, then reports leftover processes.
  dir-target only: portable hosts do not bubble child stdout (see doc).
  ASCII-only for Windows PowerShell 5.1.
#>

param(
  [string]$Exe = '',
  [string]$OutFile = '',
  [int]$TimeoutSec = 120
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $root 'release\win-unpacked\WordStudy.exe' }
if (-not (Test-Path $Exe)) { Write-Host "!! exe not found: $Exe"; exit 1 }
if ([string]::IsNullOrWhiteSpace($OutFile)) { $OutFile = Join-Path $root 'smoke-out.txt' }
if (Test-Path $OutFile) { Remove-Item $OutFile -Force }

$name = (Get-Item $Exe).BaseName
# 基线：把本次启动前就在跑的实例记下来，避免把用户自己开着的窗口算成本轮的残留。
$baseline = @(Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$start = Get-Date
$proc = Start-Process -FilePath $Exe -ArgumentList '--selftest' -PassThru -RedirectStandardOutput $OutFile `
  -RedirectStandardError (Join-Path $env:TEMP "word-app-smoke-err-$PID.txt") -WindowStyle Hidden

if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
  Write-Host "!! still running after $TimeoutSec s, killing pid $($proc.Id)"
  Stop-Process -Id $proc.Id -Force
}
$elapsed = [int]((Get-Date) - $start).TotalMilliseconds

$text = if (Test-Path $OutFile) { Get-Content $OutFile -Raw -Encoding UTF8 } else { '' }
$lines = $text -split "`r?`n" | Where-Object { $_ -match '^(check |SELFTEST|db-ready|ready-to-show|import |title=|adaptive|renderer-|shot |data )' }
$lines | ForEach-Object { Write-Host "    $_" }

$remaining = @(Get-Process -Name $name -ErrorAction SilentlyContinue).Count
$marker = if ($text -match 'SELFTEST-OK') { 'PASS' } else { 'FAIL' }
$summary = ($lines | Where-Object { $_ -match '^SELFTEST-SUMMARY' }) -join ''

Write-Host ("marker={0} elapsed-ms={1} {2} remaining-processes={3}" -f $marker, $elapsed, $summary, $remaining)
if ($marker -ne 'PASS' -or $remaining -ne 0) { exit 1 }
Write-Host 'smoke=PASS'
