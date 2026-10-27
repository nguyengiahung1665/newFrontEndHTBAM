# Run from any directory with Windows PowerShell. No downloads or image dependencies.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$publicRoot = Join-Path $repoRoot 'apps/web/public'
$brandingRoot = Join-Path $publicRoot 'assets/branding'
New-Item -ItemType Directory -Force $brandingRoot | Out-Null
$source = [System.Drawing.Bitmap]::new((Join-Path $publicRoot 'assets/huit-logo.png'))
try {
    # The supplied square logo already fills the canvas. Preserve its complete
    # circular border, lettering and transparent corners; do not crop the emblem.
    $frames = @{}
    foreach ($size in @(16, 32, 48, 180)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $scale = [Math]::Min($size / $source.Width, $size / $source.Height)
            $width = [int][Math]::Round($source.Width * $scale)
            $height = [int][Math]::Round($source.Height * $scale)
            $destination = [System.Drawing.Rectangle]::new([int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height)
            $graphics.DrawImage($source, $destination, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames[$size] = $stream.ToArray()
        } finally {
            $stream.Dispose()
            $attributes.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
    [System.IO.File]::WriteAllBytes((Join-Path $brandingRoot 'favicon-16x16.png'), $frames[16])
    [System.IO.File]::WriteAllBytes((Join-Path $brandingRoot 'favicon-32x32.png'), $frames[32])
    [System.IO.File]::WriteAllBytes((Join-Path $brandingRoot 'apple-touch-icon.png'), $frames[180])

    # ICO directory with three lossless, alpha-preserving PNG frames.
    $iconStream = [System.IO.File]::Create((Join-Path $publicRoot 'favicon.ico'))
    $writer = [System.IO.BinaryWriter]::new($iconStream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]3)
        $offset = 6 + 16 * 3
        foreach ($size in @(16, 32, 48)) {
            $writer.Write([byte]$size)
            $writer.Write([byte]$size)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$size].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$size].Length
        }
        foreach ($size in @(16, 32, 48)) { $writer.Write([byte[]]$frames[$size]) }
    } finally { $writer.Dispose() }
} finally { $source.Dispose() }
Write-Output 'Generated HUIT favicon.ico (16/32/48), PNG 16/32 and Apple 180 from assets/huit-logo.png.'
