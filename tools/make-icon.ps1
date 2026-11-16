# Generates thunderstore/icon.png (256x256). Run with Windows PowerShell: powershell -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\thunderstore\icon.png"
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 16, 18, 26))

$frame = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 216, 224, 242)), 8
$g.DrawRectangle($frame, 20, 20, 216, 216)

$ground = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 92, 104, 120))
$edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 200, 210, 225)), 4
$pts = [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point 50, 90), (New-Object System.Drawing.Point 110, 48),
    (New-Object System.Drawing.Point 200, 70), (New-Object System.Drawing.Point 210, 170),
    (New-Object System.Drawing.Point 150, 210), (New-Object System.Drawing.Point 70, 196),
    (New-Object System.Drawing.Point 44, 140))
$g.FillPolygon($ground, $pts)
$g.DrawPolygon($edge, $pts)

$red = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 64, 52))
foreach ($p in @(@(80,80),@(170,95),@(185,150),@(95,170),@(140,185),@(70,125))) { $g.FillEllipse($red, $p[0], $p[1], 12, 12) }
$orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 165, 40))
$g.FillEllipse($orange, 160, 120, 20, 20)
$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 217, 77))
$g.FillPolygon($gold, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 120, 62), (New-Object System.Drawing.Point 134, 76), (New-Object System.Drawing.Point 120, 90), (New-Object System.Drawing.Point 106, 76)))

$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$dark = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 20, 20, 20)), 4
$arrow = [System.Drawing.Point[]]@((New-Object System.Drawing.Point 128, 104), (New-Object System.Drawing.Point 152, 160), (New-Object System.Drawing.Point 128, 146), (New-Object System.Drawing.Point 104, 160))
$g.FillPolygon($white, $arrow)
$g.DrawPolygon($dark, $arrow)

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Wrote $out"
