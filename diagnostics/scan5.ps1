Add-Type -AssemblyName System.Drawing
$path = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag\gost_out\=DO+FB_1.png'
$bmp = New-Object System.Drawing.Bitmap ([System.Drawing.Image]::FromFile($path))
try {
    Write-Output ("size {0}x{1}" -f $bmp.Width, $bmp.Height)
    foreach ($x in 385,728,1071) {
        Write-Output ("--- column x={0} (mm {1:N1}) ---" -f $x, ($x/4.2857))
        $start = -1
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $ink = -not ($c.R -gt 230 -and $c.G -gt 230 -and $c.B -gt 230)
            if ($ink) { if ($start -lt 0) { $start = $y } }
            elseif ($start -ge 0) {
                $len = $y - $start
                if ($len -ge 3) { Write-Output ("  px {0}..{1} len={2}  mm {3:N2}..{4:N2}" -f $start, ($y-1), $len, ((297-($start+$len-1)/4.2857)), (297-$start/4.2857)) }
                $start = -1
            }
        }
    }
} finally { $bmp.Dispose() }
