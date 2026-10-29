$ErrorActionPreference = "Stop"
$src = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc"
$skill = "C:\Users\HALLIBURTON\.agents\skills\eplan-api"
$root = "C:\Users\HALLIBURTON\Desktop\EPLAN_LLM_TOOL"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$dirs = @("src", "src\specs", "api", "docs", "docs\regions", "diagnostics", "reports", "artifacts")
foreach ($d in $dirs) { New-Item -ItemType Directory -Path (Join-Path $root $d) -Force | Out-Null }

Get-ChildItem $src -Filter *.cs | Copy-Item -Destination (Join-Path $root "src")
Copy-Item (Join-Path $src "EplanRpc.csproj") (Join-Path $root "src")
Copy-Item (Join-Path $src "symbol_pins.json") (Join-Path $root "src")
Copy-Item (Join-Path $src "commands.json") (Join-Path $root "src")
Get-ChildItem (Join-Path $src "specs") -File | Copy-Item -Destination (Join-Path $root "src\specs")

Copy-Item "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_api_surface\EPLAN_API_SURFACE.json" (Join-Path $root "api")

Copy-Item (Join-Path $skill "SKILL.md") (Join-Path $root "docs\SKILL.md")
Copy-Item (Join-Path $skill "references\commands.md") (Join-Path $root "docs\commands.md")
Copy-Item (Join-Path $skill "references\api-map.md") (Join-Path $root "docs\api-map.md")
Copy-Item (Join-Path $skill "references\recipes.md") (Join-Path $root "docs\recipes.md")
Get-ChildItem (Join-Path $skill "references\regions") -File | Copy-Item -Destination (Join-Path $root "docs\regions")

Get-ChildItem (Join-Path $src "diag") -Filter *.ps1 | Copy-Item -Destination (Join-Path $root "diagnostics")
foreach ($n in @("do_fb_gost.json", "dcl_test_spec.json", "gost_out\=DO+FB_1.png", "dcl_out.png")) {
  $p = Join-Path (Join-Path $src "diag") $n
  if (Test-Path $p) { Copy-Item $p -Destination (Join-Path $root "diagnostics") }
}
foreach ($n in @("draw_result.txt", "draw_result2.txt", "draw_result3.txt", "pin_offsets.txt", "symbols_dump.txt", "export_do_page.txt")) {
  $p = Join-Path $src $n
  if (Test-Path $p) { Copy-Item $p -Destination (Join-Path $root "diagnostics") }
}
if (Test-Path (Join-Path $src "scripts")) { Copy-Item (Join-Path $src "scripts") (Join-Path $root "src\scripts") -Recurse }

Get-ChildItem "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\agent_reports" -Filter "wY*.md" | Copy-Item -Destination (Join-Path $root "reports")
Copy-Item (Join-Path $src "wY7_gost_dofb.png") (Join-Path $root "artifacts")
$shot = "C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png"
if (Test-Path $shot) { Copy-Item $shot (Join-Path $root "artifacts\render_offline_dofb.png") }
Copy-Item (Join-Path $src "diag\hi_c3.png") (Join-Path $root "artifacts") -ErrorAction SilentlyContinue
Copy-Item (Join-Path $src "diag\hi_box1.png") (Join-Path $root "artifacts") -ErrorAction SilentlyContinue

$rel = $root.Replace("\", "/")
$csproj = Join-Path $root "src\EplanRpc.csproj"
$text = Get-Content $csproj -Raw
$text = $text.Replace("..\wY_api_surface\EPLAN_API_SURFACE.json", "..\api\EPLAN_API_SURFACE.json")
Set-Content -Path $csproj -Value $text -Encoding UTF8
"prepared: $root"
Get-ChildItem $root -Recurse -File | Measure-Object -Property Length -Sum | ForEach-Object { "files=$($_.Count) bytes=$($_.Sum)" }
