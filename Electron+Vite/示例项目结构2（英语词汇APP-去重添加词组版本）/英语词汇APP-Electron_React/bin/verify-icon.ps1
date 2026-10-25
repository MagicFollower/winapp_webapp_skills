<#
  Verifies the icon actually embedded in a built exe (or an .ico file) by reading pixels back,
  not by eye. Samples follow the same 11-cell rule as bin/make-icon.mjs:
    cell = floor(size / 11), span = cell * 11, origin = floor((size - span) / 2)
  ASCII-only: Windows PowerShell 5.1 decodes BOM-less files as ANSI.
#>

param(
  [string]$Exe = '',
  [string]$Ico = '',
  [string]$Letter = 'W',
  [string]$Bg = '#2f6fed',
  [string]$Fg = '#ffffff',
  [int]$MinBlueFractionPercent = 40
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Convert-HexToArgb([string]$hex) {
  $s = $hex.TrimStart('#')
  if ($s.Length -eq 3) { $s = ($s.ToCharArray() | ForEach-Object { "$_$_" }) -join '' }
  if ($s.Length -ne 6) { Write-Host "!! bad color literal: $hex"; exit 1 }
  $r = [Convert]::ToInt32($s.Substring(0, 2), 16)
  $g = [Convert]::ToInt32($s.Substring(2, 2), 16)
  $b = [Convert]::ToInt32($s.Substring(4, 2), 16)
  return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

if (-not [string]::IsNullOrWhiteSpace($Exe)) {
  if (-not (Test-Path $Exe)) { Write-Host "!! not found: $Exe"; exit 1 }
  $assoc = [System.Drawing.Icon]::ExtractAssociatedIcon($Exe)
  if ($null -eq $assoc) { Write-Host '!! ExtractAssociatedIcon returned null'; exit 1 }
  $bmp = $assoc.ToBitmap()
  Write-Host ("icon-loaded size={0}x{1} from {2}" -f $bmp.Width, $bmp.Height, (Split-Path -Leaf $Exe))
} elseif (-not [string]::IsNullOrWhiteSpace($Ico)) {
  if (-not (Test-Path $Ico)) { Write-Host "!! not found: $Ico"; exit 1 }
  $icoObj = New-Object System.Drawing.Icon($Ico)
  $bmp = New-Object System.Drawing.Bitmap($icoObj.Width, $icoObj.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.DrawIcon($icoObj, 0, 0)
  $g.Dispose()
  Write-Host ("icon-loaded size={0}x{1} from {2}" -f $bmp.Width, $bmp.Height, (Split-Path -Leaf $Ico))
} else {
  Write-Host '!! pass -Exe <path> or -Ico <path>'
  exit 1
}

$size = $bmp.Width
$cell = [int][Math]::Floor($size / 11)
$span = $cell * 11
$origin = [int][Math]::Floor(($size - $span) / 2)
if ($cell -lt 1) { Write-Host "!! size $size too small for the 11-cell grid"; exit 1 }

$bgColor = Convert-HexToArgb $Bg
$fgColor = Convert-HexToArgb $Fg

function Get-Pixel([int]$x, [int]$y) { return $bmp.GetPixel($x, $y) }
function Same([System.Drawing.Color]$a, [System.Drawing.Color]$b) {
  return ([Math]::Abs($a.R - $b.R) -le 8) -and ([Math]::Abs($a.G - $b.G) -le 8) -and ([Math]::Abs($a.B - $b.B) -le 8)
}

# Glyph cell (3,2) is the top-left stroke of W in the 5x7 matrix; (7,2) the top-right stroke.
$gx = $origin + (3 * $cell) + [int][Math]::Floor($cell / 2)
$gy = $origin + (2 * $cell) + [int][Math]::Floor($cell / 2)
$strokeRightX = $origin + (7 * $cell) + [int][Math]::Floor($cell / 2)
$bgX = $origin + [int][Math]::Floor($cell / 2)
$bgY = $origin + (10 * $cell) + [int][Math]::Floor($cell / 2)

$pStrokeLeft = Get-Pixel $gx $gy
$pStrokeRight = Get-Pixel $strokeRightX $gy
$pBg = Get-Pixel $bgX $bgY

$fail = 0
if (Same $pStrokeLeft $fgColor) { Write-Host "stroke-left PASS rgb($($pStrokeLeft.R),$($pStrokeLeft.G),$($pStrokeLeft.B))" }
else { Write-Host "stroke-left FAIL rgb($($pStrokeLeft.R),$($pStrokeLeft.G),$($pStrokeLeft.B)) expected $Fg" -ForegroundColor Red; $fail = 1 }

if (Same $pStrokeRight $fgColor) { Write-Host "stroke-right PASS rgb($($pStrokeRight.R),$($pStrokeRight.G),$($pStrokeRight.B))" }
else { Write-Host "stroke-right FAIL rgb($($pStrokeRight.R),$($pStrokeRight.G),$($pStrokeRight.B)) expected $Fg" -ForegroundColor Red; $fail = 1 }

if (Same $pBg $bgColor) { Write-Host "plate-bg PASS rgb($($pBg.R),$($pBg.G),$($pBg.B))" }
else { Write-Host "plate-bg FAIL rgb($($pBg.R),$($pBg.G),$($pBg.B)) expected $Bg" -ForegroundColor Red; $fail = 1 }

$blue = 0
$total = 0
for ($y = 0; $y -lt $size; $y += 1) {
  for ($x = 0; $x -lt $size; $x += 1) {
    $p = Get-Pixel $x $y
    $total += 1
    if (Same $p $bgColor) { $blue += 1 }
  }
}
$percent = [Math]::Round(100 * $blue / $total, 1)
Write-Host ("plate-blue-fraction={0}% (min {1}%)" -f $percent, $MinBlueFractionPercent)
if ($percent -lt $MinBlueFractionPercent) { $fail = 1 }

$bmp.Dispose()
if ($fail) { Write-Host 'icon-verify=FAIL'; exit 1 }
Write-Host 'icon-verify=PASS'
