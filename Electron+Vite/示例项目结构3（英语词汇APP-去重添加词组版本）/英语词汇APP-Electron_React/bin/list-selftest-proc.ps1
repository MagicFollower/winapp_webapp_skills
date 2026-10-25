param([string]$Match = 'selftest', [switch]$Kill)
$procs = Get-CimInstance Win32_Process -Filter "Name='electron.exe'"
$killed = 0
foreach ($p in $procs) {
  $cl = $p.CommandLine
  if ($null -ne $cl -and $cl -like "*$Match*") {
    if ($Kill) {
      Stop-Process -Id $p.ProcessId -Force
      $killed++
    }
    Write-Output ("PID=" + $p.ProcessId)
  }
}
if ($Kill) { Write-Output ("killed=" + $killed) }
Write-Output ("total-electron=" + @($procs).Count)
