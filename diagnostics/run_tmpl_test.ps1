$ErrorActionPreference = 'Stop'
$root = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc'
$exe = Join-Path $root 'bin\Release\EplanRpc.exe'
$work = Join-Path $env:TEMP 'eplanrpc_gost'
$tmpl = Join-Path $work 'project1.elk'
$newelk = Join-Path $env:TEMP 'gost_from_tmpl.elk'
Remove-Item $newelk -Recurse -Force -ErrorAction SilentlyContinue
$spec = Join-Path $root 'diag\do_fb_gost.json'

$port = 45473
$proc = Start-Process -FilePath $exe -ArgumentList @('serve','--mode','offline','--port',"$port") -RedirectStandardOutput (Join-Path $root 'diag\server3.out.txt') -RedirectStandardError (Join-Path $root 'diag\server3.err.txt') -PassThru -WindowStyle Hidden
$client = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 60; $i++) { try { $client.Connect('127.0.0.1', $port); break } catch { Start-Sleep -Milliseconds 500 } }
if (-not $client.Connected) { throw 'server did not start' }
$stream = $client.GetStream(); $reader = New-Object System.IO.StreamReader($stream); $writer = New-Object System.IO.StreamWriter($stream); $writer.AutoFlush = $true
function Send([string]$cmd) { $writer.WriteLine($cmd); Write-Output $reader.ReadLine() }
try {
    Send ('project.create link="{0}" template="{1}"' -f $newelk, $tmpl)
    Send 'project.pages'
    Send ('schematic.draw spec="{0}"' -f $spec)
} finally {
    try { $writer.WriteLine('shutdown'); $reader.ReadLine() | Out-Null } catch {}
    Start-Sleep -Milliseconds 400
    if (-not $proc.HasExited) { $proc.Kill() }
    $client.Close()
}
Get-Content (Join-Path $root 'diag\server3.err.txt') -ErrorAction SilentlyContinue
