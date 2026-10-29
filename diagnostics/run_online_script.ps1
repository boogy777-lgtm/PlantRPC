param([int]$Port = 45493, [string]$Script = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\scripts\probe_pins.cs", [int]$TimeoutSec = 120)
$ErrorActionPreference = "Stop"
$exe = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\bin\Release\EplanRpc.exe"
$root = Join-Path $env:TEMP "eplan_online"
if (-not (Test-Path $root)) { New-Item -ItemType Directory -Path $root | Out-Null }
$log = Join-Path $root "srv.out"; $err = Join-Path $root "srv.err"
$p = Start-Process -FilePath $exe -ArgumentList "serve","--mode","online","--port",$Port -PassThru -RedirectStandardOutput $log -RedirectStandardError $err -WindowStyle Hidden
$c = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 120; $i++) { try { $c.Connect("127.0.0.1", $Port); break } catch { Start-Sleep -Milliseconds 500 } }
if (-not $c.Connected) { throw ("server did not start: " + (Get-Content $err -Raw)) }
$ns = $c.GetStream(); $sw = New-Object System.IO.StreamWriter($ns); $sw.AutoFlush = $true; $sr = New-Object System.IO.StreamReader($ns)
function Send([string]$line) { $sw.WriteLine($line); $r = $sr.ReadLine(); ">>> $line"; $r }
Send "remoting.connect host=127.0.0.1 port=49152 timeout=60"
Send ("remoting.script file=" + $Script + " timeout=" + $TimeoutSec)
Send "shutdown" | Out-Null
$c.Close(); $p.WaitForExit(20000) | Out-Null
"--- stderr ---"
if (Test-Path $err) { Get-Content $err -Raw }
