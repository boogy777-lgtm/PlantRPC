Add-Type -AssemblyName System.Drawing
$path = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png'
$img = [System.Drawing.Image]::FromFile($path)
Write-Output ("size {0}x{1}" -f $img.Width, $img.Height)
$bmp = New-Object System.Drawing.Bitmap $img
try {
    # find dark (ink) pixels; scan columns 300..1400 in steps, report vertical runs per column sample
    $cols = 300,345,360,420,660,700,880,960,1050,1100,1400
    foreach ($x in $cols) {
        if ($x -ge $bmp.Width) { continue }
        $runs = New-Object System.Collections.Generic.List[string]
        $start = -1
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $ink = ($c.R -lt 160 -and $c.G -lt 160 -and $c.B -lt 200)
            if ($ink) { if ($start -lt 0) { $start = $y } }
            elseif ($start -ge 0) { if (($y - $start) -ge 3) { $runs.Add("$start-$($y-1)") }; $start = -1 }
        }
        if ($start -ge 0) { $runs.Add("$start-$($bmp.Height-1)") }
        Write-Output ("x={0}: {1}" -f $x, ($runs -join ', '))
    }
} finally { $bmp.Dispose(); $img.Dispose() }
