Add-Type -AssemblyName System.Drawing
$path = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png'
$bmp = New-Object System.Drawing.Bitmap ([System.Drawing.Image]::FromFile($path))
try {
    # color histogram (coarse)
    $hist = @{}
    for ($y = 0; $y -lt $bmp.Height; $y += 3) {
        for ($x = 0; $x -lt $bmp.Width; $x += 3) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245) { continue }
            $k = "{0},{1},{2}" -f $c.R, $c.G, $c.B
            if ($hist.ContainsKey($k)) { $hist[$k]++ } else { $hist[$k] = 1 }
        }
    }
    $hist.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 12 | ForEach-Object { Write-Output ("COLOR {0} x{1}" -f $_.Key, $_.Value) }

    $cols = 330,345,355,360,415,420,430,650,660,670,700,900,960,1050,1240
    foreach ($x in $cols) {
        if ($x -ge $bmp.Width) { continue }
        $runs = New-Object System.Collections.Generic.List[string]
        $start = -1
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $ink = -not ($c.R -gt 230 -and $c.G -gt 230 -and $c.B -gt 230)
            if ($ink) { if ($start -lt 0) { $start = $y } }
            elseif ($start -ge 0) { if (($y - $start) -ge 3) { $runs.Add("$start-$($y-1)") }; $start = -1 }
        }
        if ($start -ge 0) { $runs.Add("$start-$($bmp.Height-1)") }
        Write-Output ("x={0}: {1}" -f $x, ($runs -join ', '))
    }
} finally { $bmp.Dispose() }
