# Task brief for the next model

You are taking over a working prototype that gives an LLM **eyes and hands inside EPLAN Electric
P8 2026.0.3**. The human validates everything **in the running GUI (online mode)**, and the current
acceptance test fails: **the drawn connection lines do not reach the symbol pins** — visible gaps
of 2–3 mm at every terminal/contact on the page `=DO+FB/1`.

## What you have

* `src\` — a complete, buildable C# service (`EplanRpc.exe`, net481/x64) with two backends:
  * `serve --mode offline` — own process, `EplApplication.Init`, typed `Eplan.EplApi.*`,
    47 commands across 13 regions, plus `schematic.draw` (whole page from one JSON spec).
  * `serve --mode online` — gRPC remoting into the running GUI, commands executed as C# scripts.
* `src\scripts\draw_do_feedback.cs` — the online page builder (works, but uses literal
  coordinates; this is the source of the reported defect).
* `api\EPLAN_API_SURFACE.json` — 7388-type public API dump (also embedded in the exe).
* `docs\` — generated command reference, recipes, region notes.
* `diagnostics\` — PowerShell drivers that reproduce both paths and record JSON/PNG output.
* `reports\wY1..wY7` — the research trail (what was proven, and how).

## What is broken (details in `OPEN_ISSUES.md`)

1. **O1** Two wire producers, two different pin geometries. Offline resolves pins from
   `SymbolVariant.ConnectionPoints`; online uses `±4 mm` literals → gaps. Fix by giving the online
   path the same *single* geometry resolver and by asserting endpoint-to-pin distance = 0 using
   read-back positions.
2. **O2** `Function.ConnectionPointPositions` / `function.pins` return zeros for placed functions,
   so the tool cannot *read* geometry it did not just write.
3. **O3** Pin **name** → connection-point **index** is inferred from the spec's array order
   (silently wrong if the order differs).
4. **O4** The offline and online page builders are duplicated; they must share one resolver and one
   spec.

## Constraints

* Pure C#, native EPLAN API, **one exe**, `offline|online`, no Python, no fallback layers.
* Do not rewrite the RPC core per schematic — new symbols/wires go through the JSON spec.
* Wires must be **real** connection objects/lines on **actual** pin coordinates;
  `FUNC_CONNECTIONDESIGNATION` for net names; `Generate.Connections` afterwards.
* Do not save the user's project unless asked. Offline work uses copies under `%TEMP%`
  (the GUI locks its project file).
* Keep `EplanRpc.exe smoke` green; regenerate `docs\` and `commands.json` after registry changes.
* Build must stay 0 errors / 0 warnings.

## Definition of done

* Online draw on `=DO+FB/1` produces 10 placements, 7 wires, 15 texts, 0 mm endpoint-to-pin
  distance on every wire, 7 logical nets, and the human confirms visually in the GUI.
* The geometry resolver is used by both backends; no literal pin coordinates remain in
  `scripts\`.
* `README.md` + `OPEN_ISSUES.md` updated with what changed and what remains.

## Start here

1. `OPEN_ISSUES.md` → O1 and the "Failed probe to fix" section.
2. `src\Offline.cs` → `SchematicDraw`, `PlaceFunction`, `ResolveEndpoint`, `DrawConnectionLine`.
3. `src\scripts\draw_do_feedback.cs` → replace literal offsets with resolved pins.
4. `diagnostics\run_online_script.ps1` → run in the GUI and record evidence.
