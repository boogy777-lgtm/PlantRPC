param([int]$Port = 45490, [string]$Spec = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag\do_fb_gost.json")
$ErrorActionPreference = "Stop"
$exe = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\bin\Release\EplanRpc.exe"
$root = Join-Path $env:TEMP "eplan_fix"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
New-Item -ItemType Directory -Path $root | Out-Null
$log = Join-Path $root "srv.out"
$err = Join-Path $root "srv.err"
$p = Start-Process -FilePath $exe -ArgumentList "serve","--mode","offline","--port",$Port -PassThru -RedirectStandardOutput $log -RedirectStandardError $err -WindowStyle Hidden
$c = New-Object System.Net.Sockets.TcpClient
for ($i = 0; $i -lt 120; $i++) {
  try { $c.Connect("127.0.0.1", $Port); break } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $c.Connected) { throw "server did not start: " + (Get-Content $err -Raw) }
$ns = $c.GetStream(); $sw = New-Object System.IO.StreamWriter($ns); $sw.AutoFlush = $true
$sr = New-Object System.IO.StreamReader($ns)
function Send([string]$line) {
  $sw.WriteLine($line)
  $r = $sr.ReadLine()
  ">>> $line"
  $r
}
Send "system.init" | Out-Null
Send ("project.create link=" + (Join-Path $root "p.elk") + " template=GOST_bas001") | Out-Null
$draw = Send ("schematic.draw spec=" + $Spec)
$draw
Send "connection.read"
Send "shutdown" | Out-Null
$c.Close()
$p.WaitForExit(20000) | Out-Null
"--- server stderr ---"
if (Test-Path $err) { Get-Content $err -Raw }
