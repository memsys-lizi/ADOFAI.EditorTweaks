param(
    [string]$OutDir = "src_unity\Assets\EditorTweaks\Toolbar\Icons",
    [int]$Size = 128
)

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$outPath = Join-Path $root $OutDir
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

function New-IconCanvas {
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return @($bitmap, $graphics)
}

function New-Pen([int]$r, [int]$g, [int]$b, [float]$width) {
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(245, $r, $g, $b)), $width
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function New-Brush([int]$r, [int]$g, [int]$b, [int]$a = 245) {
    return New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $r, $g, $b))
}

function Save-Icon($name, [scriptblock]$draw) {
    $canvas = New-IconCanvas
    $bitmap = $canvas[0]
    $graphics = $canvas[1]
    try {
        & $draw $graphics
        $file = Join-Path $outPath "$name.png"
        $bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Points([float[]]$values) {
    $points = New-Object System.Drawing.PointF[] ($values.Length / 2)
    for ($i = 0; $i -lt $values.Length; $i += 2) {
        $points[$i / 2] = New-Object System.Drawing.PointF $values[$i], $values[$i + 1]
    }
    return $points
}

$white = New-Brush 248 248 248
$muted = New-Brush 180 184 192
$red = New-Brush 255 75 75
$green = New-Brush 90 245 95
$blue = New-Brush 95 145 255
$whitePen = New-Pen 248 248 248 10
$mutedPen = New-Pen 180 184 192 8
$redPen = New-Pen 255 75 75 8
$greenPen = New-Pen 90 245 95 8
$bluePen = New-Pen 95 145 255 8

Save-Icon "tool_select" {
    param($g)
    $g.FillPolygon($white, (Points @(34, 18, 92, 74, 65, 80, 78, 108, 62, 115, 49, 86, 29, 105)))
    $g.DrawPolygon((New-Pen 30 30 30 5), (Points @(34, 18, 92, 74, 65, 80, 78, 108, 62, 115, 49, 86, 29, 105)))
}

Save-Icon "tool_move" {
    param($g)
    $g.DrawLine($whitePen, 64, 22, 64, 106)
    $g.DrawLine($whitePen, 22, 64, 106, 64)
    $g.FillPolygon($white, (Points @(64, 8, 48, 30, 80, 30)))
    $g.FillPolygon($white, (Points @(64, 120, 48, 98, 80, 98)))
    $g.FillPolygon($white, (Points @(8, 64, 30, 48, 30, 80)))
    $g.FillPolygon($white, (Points @(120, 64, 98, 48, 98, 80)))
}

Save-Icon "tool_rotate" {
    param($g)
    $arcPen = New-Pen 248 248 248 11
    $g.DrawArc($arcPen, 28, 28, 72, 72, 35, 285)
    $g.FillPolygon($white, (Points @(94, 21, 103, 55, 70, 46)))
    $g.DrawEllipse($mutedPen, 47, 47, 34, 34)
}

Save-Icon "tool_scale" {
    param($g)
    $g.DrawRectangle($mutedPen, 28, 46, 54, 54)
    $g.DrawLine($whitePen, 45, 83, 96, 32)
    $g.FillPolygon($white, (Points @(103, 22, 97, 54, 72, 29)))
    $g.FillRectangle($white, 20, 88, 22, 22)
    $g.FillRectangle($white, 76, 32, 22, 22)
}

Save-Icon "tool_rect" {
    param($g)
    $g.DrawRectangle($whitePen, 30, 30, 68, 68)
    foreach ($p in @(@(24,24), @(56,24), @(90,24), @(24,56), @(90,56), @(24,90), @(56,90), @(90,90))) {
        $g.FillRectangle($muted, $p[0], $p[1], 14, 14)
    }
}

Save-Icon "tool_pivot" {
    param($g)
    $g.DrawEllipse($mutedPen, 26, 26, 76, 76)
    $g.DrawLine($whitePen, 64, 18, 64, 110)
    $g.DrawLine($whitePen, 18, 64, 110, 64)
    $g.FillEllipse($white, 52, 52, 24, 24)
}

Save-Icon "tool_space" {
    param($g)
    $g.DrawEllipse($mutedPen, 28, 28, 72, 72)
    $g.DrawArc($whitePen, 34, 34, 60, 60, 200, 140)
    $g.DrawLine($redPen, 64, 64, 105, 64)
    $g.DrawLine($greenPen, 64, 64, 64, 23)
    $g.FillPolygon($red, (Points @(115, 64, 96, 52, 96, 76)))
    $g.FillPolygon($green, (Points @(64, 13, 52, 32, 76, 32)))
}

Save-Icon "tool_snap" {
    param($g)
    $g.DrawArc($whitePen, 28, 28, 72, 72, 180, 180)
    $g.DrawLine($whitePen, 28, 64, 28, 92)
    $g.DrawLine($whitePen, 100, 64, 100, 92)
    $g.DrawLine($mutedPen, 22, 92, 46, 92)
    $g.DrawLine($mutedPen, 82, 92, 106, 92)
    $g.FillRectangle($white, 22, 88, 24, 14)
    $g.FillRectangle($white, 82, 88, 24, 14)
}

Save-Icon "tool_grid" {
    param($g)
    foreach ($x in @(32, 56, 80, 104)) {
        $g.DrawLine($mutedPen, $x, 24, $x, 104)
    }
    foreach ($y in @(32, 56, 80, 104)) {
        $g.DrawLine($mutedPen, 24, $y, 104, $y)
    }
    $g.DrawRectangle($whitePen, 24, 24, 80, 80)
}

Save-Icon "tool_pan" {
    param($g)
    $g.DrawLine($whitePen, 38, 68, 38, 42)
    $g.DrawLine($whitePen, 54, 66, 54, 31)
    $g.DrawLine($whitePen, 70, 67, 70, 38)
    $g.DrawLine($whitePen, 86, 75, 86, 50)
    $g.DrawArc($whitePen, 35, 58, 56, 50, 5, 170)
    $g.DrawLine($whitePen, 37, 84, 60, 112)
    $g.DrawLine($whitePen, 86, 75, 91, 102)
}

Save-Icon "tool_settings" {
    param($g)
    foreach ($y in @(36, 64, 92)) {
        $g.DrawLine($mutedPen, 25, $y, 103, $y)
    }
    $g.FillEllipse($white, 43, 25, 22, 22)
    $g.FillEllipse($white, 72, 53, 22, 22)
    $g.FillEllipse($white, 34, 81, 22, 22)
}

Save-Icon "tool_visibility" {
    param($g)
    $g.FillClosedCurve($muted, (Points @(18, 64, 38, 38, 64, 30, 90, 38, 110, 64, 90, 90, 64, 98, 38, 90)))
    $g.FillEllipse($white, 46, 46, 36, 36)
    $g.FillEllipse((New-Brush 20 20 20 245), 58, 58, 12, 12)
}

$white.Dispose()
$muted.Dispose()
$red.Dispose()
$green.Dispose()
$blue.Dispose()
$whitePen.Dispose()
$mutedPen.Dispose()
$redPen.Dispose()
$greenPen.Dispose()
$bluePen.Dispose()
