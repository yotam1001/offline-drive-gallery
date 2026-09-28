Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $projectRoot 'docs\social-card.png'
$bitmap = [System.Drawing.Bitmap]::new(1200, 630)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function Brush([string]$hex) { return [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function Font([int]$size, [System.Drawing.FontStyle]$style) { return [System.Drawing.Font]::new('Segoe UI', $size, $style, [System.Drawing.GraphicsUnit]::Pixel) }

try {
    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, 1200, 630),
        [System.Drawing.ColorTranslator]::FromHtml('#14243e'),
        [System.Drawing.ColorTranslator]::FromHtml('#294f83'), 0.0)
    $graphics.FillRectangle($background, 0, 0, 1200, 630)
    $background.Dispose()

    $accent = Brush '#86b8ff'
    $white = Brush '#ffffff'
    $muted = Brush '#c6d7ee'
    $card = Brush '#f7faff'
    $cardLine = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#b8cbe7'), 2)
    $blue = Brush '#e2efff'
    $dark = Brush '#244568'
    $pillFont = Font 22 ([System.Drawing.FontStyle]::Bold)
    $titleFont = Font 63 ([System.Drawing.FontStyle]::Bold)
    $subtitleFont = Font 26 ([System.Drawing.FontStyle]::Regular)
    $cardFont = Font 18 ([System.Drawing.FontStyle]::Bold)
    try {
        $graphics.FillRectangle($accent, 66, 64, 56, 56)
        $graphics.FillRectangle($white, 79, 78, 30, 4)
        $graphics.FillRectangle($white, 79, 88, 24, 4)
        $graphics.FillRectangle($white, 79, 98, 18, 4)
        $graphics.DrawString('OFFLINE DRIVE GALLERY', $pillFont, $muted, 137, 78)
        $graphics.DrawString('Find the drive.', $titleFont, $white, 66, 207)
        $graphics.DrawString('Recognize the file.', $titleFont, $white, 66, 282)
        $graphics.DrawString('Search unplugged drives. Browse saved thumbnails.', $subtitleFont, $muted, 69, 397)
        $graphics.DrawString('Free  |  Windows  |  Open source', $subtitleFont, $accent, 69, 476)

        $graphics.FillRectangle($card, 755, 123, 380, 371)
        $graphics.DrawRectangle($cardLine, 755, 123, 380, 371)
        $graphics.FillRectangle($dark, 757, 125, 376, 52)
        $graphics.DrawString('FAMILY PHOTOS  |  OFFLINE', $cardFont, $white, 780, 138)
        $graphics.FillRectangle($blue, 779, 193, 330, 39)
        $graphics.DrawString('Search  beach sunset', $cardFont, $dark, 793, 201)

        $warm = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.Rectangle]::new(779, 251, 150, 135),
            [System.Drawing.ColorTranslator]::FromHtml('#ffbc82'),
            [System.Drawing.ColorTranslator]::FromHtml('#3f5884'), 65.0)
        $sea = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.Rectangle]::new(945, 251, 150, 135),
            [System.Drawing.ColorTranslator]::FromHtml('#98d8e4'),
            [System.Drawing.ColorTranslator]::FromHtml('#2990a8'), 90.0)
        $graphics.FillRectangle($warm, 779, 251, 150, 135)
        $graphics.FillRectangle($sea, 945, 251, 150, 135)
        $warm.Dispose()
        $sea.Dispose()
        $graphics.DrawString('beach sunset.mp4', $cardFont, $dark, 779, 400)
        $graphics.DrawString('coastline.mov', $cardFont, $dark, 945, 400)
    }
    finally {
        foreach ($resource in @($accent, $white, $muted, $card, $cardLine, $blue, $dark,
                $pillFont, $titleFont, $subtitleFont, $cardFont)) { $resource.Dispose() }
    }

    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output $outputPath
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}
