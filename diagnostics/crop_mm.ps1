param(
  [string]$Src = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png",
  [string]$OutDir = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag",
  [int]$Scale = 8
)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Src)
"src=$Src size=$($bmp.Width)x$($bmp.Height)"
$mm = $bmp.Width / 420.0
function Px([double]$xmm) { [int][Math]::Round($xmm * $mm) }
function Py([double]$ymm) { [int][Math]::Round((297.0 - $ymm) * $mm) }
$regions = @{
  "box1" = @(85, 143, 97, 158)
  "pin1" = @(85, 160, 97, 174)
  "col3" = @(244, 105, 262, 175)
}
foreach ($k in $regions.Keys) {
  $r = $regions[$k]
  $x0 = Px $r[0]; $x1 = Px $r[1]; $y0 = Py $r[3]; $y1 = Py $r[2]
  $w = $x1 - $x0; $h = $y1 - $y0
  $dst = New-Object System.Drawing.Bitmap ($w * $Scale), ($h * $Scale)
  $g = [System.Drawing.Graphics]::FromImage($dst)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
  $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, ($w * $Scale), ($h * $Scale)), (New-Object System.Drawing.Rectangle $x0, $y0, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()
  $p = Join-Path $OutDir ("hi_" + $k + ".png")
  $dst.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
  $dst.Dispose()
  $p
}
$bmp.Dispose()
