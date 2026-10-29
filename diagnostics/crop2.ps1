param(
  [string]$Src = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png",
  [string]$Out = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag\hi_c3.png",
  [double]$X0mm = 238, [double]$X1mm = 266, [double]$Y0mm = 104, [double]$Y1mm = 172, [int]$Scale = 6
)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Src)
$mm = $bmp.Width / 420.0
$x0 = [int][Math]::Round($X0mm * $mm)
$x1 = [int][Math]::Round($X1mm * $mm)
$ytop = [int][Math]::Round((297.0 - $Y1mm) * $mm)
$ybot = [int][Math]::Round((297.0 - $Y0mm) * $mm)
$w = $x1 - $x0
$h = $ybot - $ytop
"crop x=$x0..$x1 y=$ytop..$ybot  ($w x $h) scale=$Scale"
$dst = New-Object System.Drawing.Bitmap ([int]($w * $Scale)), ([int]($h * $Scale))
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$srcRect = New-Object System.Drawing.Rectangle $x0, $ytop, $w, $h
$dstRect = New-Object System.Drawing.Rectangle 0, 0, ([int]($w * $Scale)), ([int]($h * $Scale))
$g.DrawImage($bmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$dst.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$dst.Dispose()
$bmp.Dispose()
$Out
