$ErrorActionPreference = 'Stop'
$root = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc'
$exe = Join-Path $root 'bin\Release\EplanRpc.exe'
$elk = Join-Path $env:TEMP 'dcl_test.elk'
Remove-Item $elk -ErrorAction SilentlyContinue
$spec = Join-Path $root 'diag\dcl_test_spec.json'
$png  = Join-Path $root 'diag\dcl_out.png'

$port = 45471
$proc = Start-Process -FilePath $exe -ArgumentList @('serve','--mode','offline','--port',"$port") -RedirectStandardOutput (Join-Path $root 'diag\server.out.txt') -RedirectStandardError (Join-Path $root 'diag\server.err.txt') -PassThru -WindowStyle Hidden

$client = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 60; $i++) {
    try { $client.Connect('127.0.0.1', $port); break } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $client.Connected) { throw 'server did not start' }
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

function Send([string]$cmd) {
    $writer.WriteLine($cmd)
    $line = $reader.ReadLine()
    Write-Output $line
}

try {
    Send ('project.create link="{0}"' -f $elk)
    Send ('schematic.draw spec="{0}"' -f $spec)
    Send ('function.pins')
    Send ('connection.read')
    Send ('graphics.line x1=60 y1=100 x2=60 y2=60')
    Send ('export.image path="{0}" width=1200' -f $png)
    Send ('stats')
} finally {
    try { $writer.WriteLine('shutdown'); $reader.ReadLine() | Out-Null } catch {}
    Start-Sleep -Milliseconds 500
    if (-not $proc.HasExited) { $proc.Kill() }
    $client.Close()
}
Write-Output '--- server.err ---'
Get-Content (Join-Path $root 'diag\server.err.txt') -ErrorAction SilentlyContinue
Write-Output '--- png ---'
Get-Item $png -ErrorAction SilentlyContinue | Select-Object FullName,Length
