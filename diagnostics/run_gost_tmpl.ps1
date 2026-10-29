$ErrorActionPreference = 'Stop'
$root = 'C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc'
$exe = Join-Path $root 'bin\Release\EplanRpc.exe'
$tmpl = 'C:\ProgramData\EPLAN\O_Data\Electric P8 Data\2026.0.3\Templates\EPLAN\GOST_bas001.zw9'
$newelk = Join-Path $env:TEMP 'gost_base.elk'
Remove-Item $newelk -Recurse -Force -ErrorAction SilentlyContinue
$spec = Join-Path $root 'diag\do_fb_gost.json'

$port = 45474
$proc = Start-Process -FilePath $exe -ArgumentList @('serve','--mode','offline','--port',"$port") -RedirectStandardOutput (Join-Path $root 'diag\server4.out.txt') -RedirectStandardError (Join-Path $root 'diag\server4.err.txt') -PassThru -WindowStyle Hidden
$client = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 60; $i++) { try { $client.Connect('127.0.0.1', $port); break } catch { Start-Sleep -Milliseconds 500 } }
if (-not $client.Connected) { throw 'server did not start' }
$stream = $client.GetStream(); $reader = New-Object System.IO.StreamReader($stream); $writer = New-Object System.IO.StreamWriter($stream); $writer.AutoFlush = $true
function Send([string]$cmd) { $writer.WriteLine($cmd); Write-Output $reader.ReadLine() }
try {
    Send ('project.create link="{0}" template=GOST_bas001' -f $newelk)
    Send 'symbol.check library=GOST_symbol name=X variant=0'
    Send ('schematic.draw spec="{0}"' -f $spec)
    Send 'project.pages'
} finally {
    try { $writer.WriteLine('shutdown'); $reader.ReadLine() | Out-Null } catch {}
    Start-Sleep -Milliseconds 400
    if (-not $proc.HasExited) { $proc.Kill() }
    $client.Close()
}
Get-Content (Join-Path $root 'diag\server4.err.txt') -ErrorAction SilentlyContinue
