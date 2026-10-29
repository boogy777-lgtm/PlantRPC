Add-Type -AssemblyName System.Drawing
$path = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag\\gost_out\\=DO+FB_1.png'
$bmp = New-Object System.Drawing.Bitmap ([System.Drawing.Image]::FromFile($path))
try {
    $w = $bmp.Width; $h = $bmp.Height
    for ($x = 0; $x -lt $w; $x++) {
        $best = 0; $bStart = -1; $start = -1
        for ($y = 0; $y -lt $h; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $ink = -not ($c.R -gt 230 -and $c.G -gt 230 -and $c.B -gt 230)
            if ($ink) { if ($start -lt 0) { $start = $y } }
            else { if ($start -ge 0) { $len = $y - $start; if ($len -gt $best) { $best = $len; $bStart = $start }; $start = -1 } }
        }
        if ($start -ge 0) { $len = $h - $start; if ($len -gt $best) { $best = $len; $bStart = $start } }
        if ($best -ge 40) { Write-Output ("x={0} run {1}..{2} len={3}" -f $x, $bStart, ($bStart+$best-1), $best) }
    }
} finally { $bmp.Dispose() }

