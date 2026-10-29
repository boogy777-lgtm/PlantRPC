# Open issues — evidence and next probes

Ordered by user impact. "O1" is what the human currently fails in the GUI.

---

## O1 (P0) — wires visibly do not reach the pins in the GUI

**Symptom (human, online mode, GUI validation):** *"линии не соеденены"* — the connection lines
stop a few millimetres short of the terminal circles / contact pins.

**Two independent wire producers disagree about pin offsets.**

*Authoritative (offline `schematic.draw`, reads `SymbolVariant.ConnectionPoints`):*

```
X at y=170  -> pin "2" resolves to (90, 169.25)      i.e. offset -0.75
K at y=150  -> pin A1 (90, 154) / A2 (90, 146)       i.e. offset +4 / -4
X at y=130  -> pin "1" resolves to (90, 132)         i.e. offset +2
S at y=150  -> pin 13 (250, 154) / 14 (250, 146)
Y1 at y=130 -> pin  1 (250, 134) /  2 (250, 126)
```

Recorded in `diagnostics\` (`draw_result*`, `pin_offsets.txt`) and reproducible with
`diagnostics\repro_gost.ps1` (offline, ГОСТ template):

```
wireCheck.count=7 locationOk=7
expect=[90,169.25,90,154]  [90,146,90,132]  ...
connectionsGenerated=true  functions=10  connections=14
connection.read -> 7 nets: X1-K1-X2, X3-K1-X4, X5-K1-Y1-X6
```

*The online twin (`src\scripts\draw_do_feedback.cs`, executed in the GUI) ignores that and uses
literals* — `Place(X, 90,160)` then `Wire(90,156, 90,150)`; `Place(K, 90,146)` then
`Wire(90,142, 90,136)`. I.e. it assumes pins at `±4 mm` around each placement. With the real
offsets above the wire starts 3.25 mm below the top terminal and ends 2 mm above it at the
bottom — a visible gap at every pin, exactly the reported symptom.

**Root cause (structural, not a typo):** pin geometry exists in two places — a resolver in
`Offline.cs` and hard-coded numbers in the GUI script. There is no single mechanism.

**Fix direction (mechanism, not patch):**

1. Give the online path the same resolver: a GUI-side helper that, for each placed function,
   returns absolute pin positions, then wire from those. Never literal coordinates.
2. Preferred source of truth, in order:
   * `Function.Pins[i].Location` after `AreConnectionPointPositionsLocal = false`;
   * `Function.ConnectionPointPositions` (set `AreConnectionPointPositionsLocal=false`) —
     absolute `X/Y`;
   * `Function.SymbolVariant.ConnectionPoints[i].Location` + `Function.Location` (what offline
     uses today; note the odd `-0.75` for pin "2" of `X` — verify it is not an index/offset bug).
3. Add an invariant to the drawing result: for every wire, the distance from each endpoint to
   the nearest connection point of the referenced placement must be `0` (not "the value we
   computed equals the value we computed", which is what `locationOk` does today).
4. Prove it by exporting from the GUI and measuring, not by trusting the API echo.

**Known measurement data (for regression):** A3 page, 1800 px wide → 4.2857 px/mm;
`mm x -> px = x*4.2857`, `mm y -> px = (297-y)*4.2857`. Columns at x = 90 / 170 / 250 mm.
`diagnostics\crop2.ps1` produces 6–10x nearest-neighbour crops for visual diff.

**Do not trust:** `wireCheck.locationOk` (tautological), `function.pins` (zeros, see O2).

---

## O2 (P0) — `function.pins` returns zeros

`Function.ConnectionPointPositions` (and the command built on it) returns `(0,0)` for placed
functions, so the only working source today is `SymbolVariant.ConnectionPoints` via the
placement path. Consequences: the LLM cannot *read* a page's geometry, only the one it just wrote.

Probe next: set `Function.AreConnectionPointPositionsLocal = false` first, then read
`ConnectionPointPositions` / `Pins[i].Location`. If still zero, read `Pins[i]` (`Pin.Index`,
`Pin.Location`) from a *placed* function in the GUI, and compare with
`SymbolVariant.ConnectionPoints` for the same function.

---

## O3 (P1) — pin name → connection-point index mapping is implicit

`ResolveEndpoint` maps a pin **name** (`"A2"`, `"13"`) to a connection point by its position in
the spec's `pins` array. If the spec omits a pin or lists them in another order, the wire silently
lands on the wrong point. `PinBase` exposes only `Index`/`Location`, so the name→index relation is
not available from the variant itself.

Probe next: read `Function.Pins[i]` (which carries the pin's logical name via its properties) or
the symbol's `FunctionDefinition` connection-point names, and build an explicit name→index map
in the resolver.

---

## O4 (P1) — offline and online draw paths are duplicated

`schematic.draw` (offline) and `scripts\draw_do_feedback.cs` (online) implement the same page
twice, with different coordinates. Any fix must land in one place or they will diverge again
(this is the direct cause of O1 surviving a "verified" fix).

Options (choose one, document it):
* expose `schematic.draw` online by running the same resolver inside a generated GUI script
  (script text built from the spec, executed through `remoting.script`);
* or keep two adapters but share one geometry resolver + one JSON spec as the only input.

---

## O5 (P1) — `api.invoke` cannot address indexed properties, handles or variants

Blocked shapes seen in practice:
* `Properties.FUNC_CONNECTIONDESIGNATION[i]` (indexed property),
* `@variant:<LIB>:<NAME>:<N>` (currently only `@variant:X:0` exists),
* returning a session handle from `api.invoke` and reusing it (`@handle:<h>` is read-only today),
* `api.desc` for `Eplan.EplApi.DataModel.MasterData.SymbolVariant` — the type lives in the
  `MasterData` namespace, not `DataModel` (the API dump contains it; the lookup path was wrong).

---

## O6 (P1) — no logical `Connection` creation / routing

Only `DynamicConnectionLine` (graphic) is produced; real routed connections with wire numbers,
cross-references and cable/core data are out of reach. `Generate.Connections` approximates it.
Next: `Eplan.EplApi.DataModel.Connection` (create + route), `Eplan.EplApi.HEServices` generating
actions, connection numbering (`connection.read` currently returns internal ids such as
`68/18/1/0`).

---

## O7 (P2) — missing command families

From the earlier surface study, still absent: `masterdata.parts` (part/device selection),
`action.run` / `action.describe` (run EPLAN actions offline), `settings` index/scope coverage,
`project.export` (project-level), `page.copy/renumber`, `plot.frame`.

---

## O8 (P2) — device naming is incomplete

`fn.Name` becomes `-X1` with no `=` plant `+` location, so `connection.read` shows `=+-X1`.
Set `FUNC_DEVICETAG_*` / plant+location properties if the consumer needs stable device tags.

---

## O9 (P2) — layout has no grid or collision model

Texts overlap symbol/wire areas on the reference page (e.g. `DI вход` near the middle column).
The tool places at raw millimetres from the spec. A grid/occupancy model (or at least a
"does this text bbox intersect an existing object" check via `GetBoundingBox`) belongs in the
generator, not in each spec.

---

## O10 (P3) — dev-path coupling and docs drift

* `Program.SurfaceDefault` is an absolute dev path (`C:\Users\...\wY_api_surface\...json`).
  Harmless because the dump is embedded and the loader falls back, but it should default to `null`.
* `docs\*.md` and `commands.json` are generated; any registry change must be followed by
  `EplanRpc.exe docs` + `EplanRpc.exe dump`, and `SKILL.md` updated by hand.

---

## Failed probe to fix before trusting geometry

`src\scripts\probe_pins.cs` was written to dump, for every function on `=DO+FB/1`:
`Location`, `AreConnectionPointPositionsLocal`, `ConnectionPointPositions`, `Pins[i].Index/Location`
and `SymbolVariant.ConnectionPoints`. It did **not compile inside the GUI**
(`remoting.script` → "script did not write a result file (likely failed to compile)").

Probable cause and fix: it uses compile-time `typeof(Eplan.EplApi.HEServices.SelectionSet)` etc.
EPLAN's script compiler does not reference every EPLAN assembly. `draw_do_feedback.cs` — which
compiles — resolves everything through `Assembly.Load(...)` + `Type.GetType(name)` helpers
(`FindType`, `GetReadable`, `GetIndexed`). Port those helpers, and load
`Eplan.EplApi.HEServicesu` / `Eplan.EplApi.DataModelu` explicitly.

Driver: `diagnostics\run_online_script.ps1 -Script <file> -Port 45493`.
