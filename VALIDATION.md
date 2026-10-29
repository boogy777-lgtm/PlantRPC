# Validation protocol (online, in the GUI)

The human validates **in the running EPLAN GUI**, not in an offline PNG. Treat this file as the
acceptance contract.

## Preconditions

* EPLAN Electric P8 2026.0.3 is running with a project open (historically
  `C:\Users\HALLIBURTON\Documents\Eplan_Test\project1.elk`).
* EPLAN scripting remoting is listening (observed `127.0.0.1:49152`).
* The RPC service is started **in the same shell call** as the commands if the harness kills
  child processes:

```powershell
src\bin\Release\EplanRpc.exe serve --mode online --port 45455
diagnostics\run_online_script.ps1 -Script src\scripts\draw_do_feedback.cs -Port 45493
```

`run_online_script.ps1` starts the service, `remoting.connect host=127.0.0.1 port=49152`,
runs `remoting.script file=<script>`, prints the JSON result and shuts the service down.

## Acceptance criteria (all must hold)

1. **Visual** — at every pin, the connection line touches the symbol's connection point; there is
   no visible gap at terminals (`X1..X6`), coils (`K1`), contact (`13/14`), relay (`Y1`).
   Zoom check: the human inspects the page in the GUI; agents can crop the export with
   `diagnostics\crop2.ps1` (6–10x nearest neighbour) and compare against `artifacts\hi_c3.png`.
2. **Geometry** — for every wire, the distance between each endpoint and the referenced
   placement's corresponding pin is `0.000` mm (reported by the tool, computed from the *read
   back* pin position, not from the same numbers used to draw).
3. **Logic** — after `Generate.Connections`: 7 wires → 7 nets, 14 function-side links:
   `X1–K1–X2`, `X3–K1–X4`, `X5–K1–Y1–X6`.
4. **No regressions** — the page still contains 10 functions and 15 texts; the layout matches the
   approved drawing (`artifacts\render_offline_dofb.png`).
5. **Persistence** — do **not** save the user's project unless explicitly asked. The draw script
   writes to the open project in memory; state that it was left unsaved.

## Failure reporting

If a criterion fails, report: page name, wire index, expected pin `(x,y)`, measured line endpoint
`(x,y)`, delta in mm, and the PNG crop showing it. Do not re-run the same unchanged command more
than once — change an input (e.g. pin source) and retest.

## Evidence to keep

* the JSON result of the script (placements/wires/wireCheck/connections),
* `connection.read` output,
* one full-page PNG and one 8x crop per column,
* SHA-256 of the server exe used (`Get-FileHash`).
