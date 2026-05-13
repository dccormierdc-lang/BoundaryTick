Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$icoPath = Join-Path $root 'BoundaryTick.ico'
$pngPath = Join-Path $root 'BoundaryTick-icon.png'

function New-RoundedRectPath {
  param(
    [float]$X,
    [float]$Y,
    [float]$Width,
    [float]$Height,
    [float]$Radius
  )

  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $diameter = $Radius * 2
  $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
  $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
  $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
  $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
  $path.CloseFigure()
  return $path
}

function New-IconPngBytes {
  param([int]$Size)

  $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $graphics.Clear([System.Drawing.Color]::Transparent)

  $scale = $Size / 256.0

  $bgPath = New-RoundedRectPath (12 * $scale) (12 * $scale) (232 * $scale) (232 * $scale) (52 * $scale)
  $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList @(
    [System.Drawing.RectangleF]::new(0, 0, $Size, $Size),
    [System.Drawing.Color]::FromArgb(255, 25, 92, 170),
    [System.Drawing.Color]::FromArgb(255, 18, 38, 82),
    [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
  )
  $graphics.FillPath($bgBrush, $bgPath)
  $bgBrush.Dispose()

  $edgePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 255, 255, 255)), (6 * $scale)
  $graphics.DrawPath($edgePen, $bgPath)
  $edgePen.Dispose()
  $bgPath.Dispose()

  $monitorBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 244, 255))
  $screenBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 38, 116, 205))
  $tickBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 196, 74))

  $left = New-RoundedRectPath (42 * $scale) (76 * $scale) (75 * $scale) (98 * $scale) (14 * $scale)
  $right = New-RoundedRectPath (139 * $scale) (76 * $scale) (75 * $scale) (98 * $scale) (14 * $scale)
  $graphics.FillPath($monitorBrush, $left)
  $graphics.FillPath($monitorBrush, $right)

  $leftScreen = New-RoundedRectPath (53 * $scale) (89 * $scale) (53 * $scale) (66 * $scale) (7 * $scale)
  $rightScreen = New-RoundedRectPath (150 * $scale) (89 * $scale) (53 * $scale) (66 * $scale) (7 * $scale)
  $graphics.FillPath($screenBrush, $leftScreen)
  $graphics.FillPath($screenBrush, $rightScreen)

  $tick = New-RoundedRectPath (122 * $scale) (53 * $scale) (12 * $scale) (150 * $scale) (6 * $scale)
  $graphics.FillPath($tickBrush, $tick)
  $graphics.FillEllipse($tickBrush, (114 * $scale), (40 * $scale), (28 * $scale), (28 * $scale))
  $graphics.FillEllipse($tickBrush, (114 * $scale), (188 * $scale), (28 * $scale), (28 * $scale))

  $standBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 194, 215, 240))
  $graphics.FillRectangle($standBrush, (76 * $scale), (176 * $scale), (25 * $scale), (10 * $scale))
  $graphics.FillRectangle($standBrush, (157 * $scale), (176 * $scale), (25 * $scale), (10 * $scale))
  $graphics.FillRectangle($standBrush, (62 * $scale), (190 * $scale), (53 * $scale), (10 * $scale))
  $graphics.FillRectangle($standBrush, (143 * $scale), (190 * $scale), (53 * $scale), (10 * $scale))

  $standBrush.Dispose()
  $tick.Dispose()
  $rightScreen.Dispose()
  $leftScreen.Dispose()
  $right.Dispose()
  $left.Dispose()
  $tickBrush.Dispose()
  $screenBrush.Dispose()
  $monitorBrush.Dispose()

  $stream = New-Object System.IO.MemoryStream
  $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
  $bytes = $stream.ToArray()
  $stream.Dispose()
  $graphics.Dispose()
  $bitmap.Dispose()
  return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
  $images += [PSCustomObject]@{
    Size = $size
    Bytes = [byte[]](New-IconPngBytes $size)
  }
}

[System.IO.File]::WriteAllBytes($pngPath, (New-IconPngBytes 256))

$output = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create), ([System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter $output

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)

$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
  $sizeByte = if ($image.Size -eq 256) { 0 } else { $image.Size }
  $writer.Write([Byte]$sizeByte)
  $writer.Write([Byte]$sizeByte)
  $writer.Write([Byte]0)
  $writer.Write([Byte]0)
  $writer.Write([UInt16]1)
  $writer.Write([UInt16]32)
  $writer.Write([UInt32]$image.Bytes.Length)
  $writer.Write([UInt32]$offset)
  $offset += $image.Bytes.Length
}

foreach ($image in $images) {
  $writer.Write($image.Bytes)
}

$writer.Dispose()
$output.Dispose()

Write-Host "Created $icoPath"
Write-Host "Created $pngPath"
