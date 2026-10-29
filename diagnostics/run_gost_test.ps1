$ErrorActionPreference = 'Stop'
$root = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc'
$exe = Join-Path $root 'bin\Release\EplanRpc.exe'
$work = Join-Path $env:TEMP 'eplanrpc_gost'
$elk = Join-Path $work 'project1.elk'
$spec = Join-Path $root 'diag\do_fb_gost.json'
$outdir = Join-Path $root 'diag\gost_out'
Remove-Item $outdir -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $work -Recurse -Include *.lck -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$port = 45472
$proc = Start-Process -FilePath $exe -ArgumentList @('serve','--mode','offline','--port',"$port") -RedirectStandardOutput (Join-Path $root 'diag\server2.out.txt') -RedirectStandardError (Join-Path $root 'diag\server2.err.txt') -PassThru -WindowStyle Hidden

$client = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 60; $i++) {
    try { $client.Connect('127.0.0.1', $port); break } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $client.Connected) { throw 'server did not start' }
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

function Send([string]$cmd) { $writer.WriteLine($cmd); Write-Output $reader.ReadLine() }

try {
    Send ('project.open link="{0}"' -f $elk)
    Send 'project.pages'
    Send ('symbol.check library=GOST_symbol symbol=X variant=0')
    Send ('schematic.draw spec="{0}"' -f $spec)
    Send 'function.pins'
    Send 'connection.read'
    Send 'stats'
} finally {
    try { $writer.WriteLine('shutdown'); $reader.ReadLine() | Out-Null } catch {}
    Start-Sleep -Milliseconds 500
    if (-not $proc.HasExited) { $proc.Kill() }
    $client.Close()
}
Write-Output '--- server.err ---'
Get-Content (Join-Path $root 'diag\server2.err.txt') -ErrorAction SilentlyContinue
Write-Output '--- png ---'
Get-ChildItem $outdir -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length
