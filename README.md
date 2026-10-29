# EPLAN LLM Tool — handoff package

A self-contained source handoff for building **eyes and hands for an LLM inside EPLAN Electric P8**:
a long-lived local service the model can call to *read* a project (pages, functions, pins,
connections, geometry, rendered images) and to *write* a schematic (project, pages, symbols,
real wires, texts, exports).

Everything needed is in this folder. It builds standalone and has been smoke-tested.

---

## 0. Mission

An LLM cannot see EPLAN and cannot click it. It needs a tool that:

* **Eyes** — return structured facts, not pixels: what functions exist on a page, where their
  connection points really are, which nets exist, what a symbol variant looks like, plus a PNG
  for visual confirmation.
* **Hands** — create projects/pages, place functions from a symbol library, draw *real*
  connection lines between *actual* pins, add texts, regenerate connections, export.

The existing implementation (`src/`) is exactly that tool, delivered as one C# exe with two
backends (in-process `offline`, GUI-remoting `online`) and one command grammar.

---

## 1. Status — what works today

| Capability | offline | online (GUI) |
|---|---|---|
| start / health / state | yes | yes |
| create + open project | yes (own file) | GUI's open project |
| create page / list / clear | yes | via `remoting.script` |
| place symbol by library+name+variant | yes | via `remoting.script` |
| read pins / connections / stats | yes | partially (`api.invoke`) |
| draw wire on real pin coordinates | yes | only via checked-in scripts |
| texts / graphics | yes | via `remoting.script` |
| generate connections | yes | yes (action/script) |
| export PNG / PDF | yes | yes (script) |
| full page from one JSON spec | **yes** (`schematic.draw`) | not yet |
| generic API long tail | `api.invoke` (limited) | `api.invoke` + `remoting.script` |

Verified numbers from the package build (`src/bin/Release/EplanRpc.exe`, SHA-256
`FE86F792D9271C8A73AC02DCF304CFEA27BA7420F1F803266F7333065E9B6E87`):

```
EplanRpc.exe smoke   -> 3 placements, 2 wires, wireCheck 2/2, connections=4, PNG exported
```

`commands.json`: **47 commands, 13 regions**.

---

## 2. Environment (verified on this machine)

* EPLAN Electric P8 **2026.0.3**, platform bin
  `C:\Program Files\EPLAN\Platform\2026.0.3\Bin` (compile-time references, `Private=false`).
* Project templates
  `C:\ProgramData\EPLAN\O_Data\Electric P8 Data\2026.0.3\Templates\EPLAN\`
  * `IEC_bas001.zw9` registers symbol library `IEC_symbol`
  * `GOST_bas001.zw9` registers symbol library `GOST_symbol`  ← required for ГОСТ drawings
* GUI remoting endpoint observed at `127.0.0.1:49152` (EPLAN's own scripting remoting).
* .NET SDK **8.0.423** (pinned by `src\global.json`; SDK 10 fails with a CET-fatal on this
  host). Target framework is `net481`, `x64`, `LangVersion 7.3`.
  Restore pulls `Microsoft.NETFramework.ReferenceAssemblies.net481` (needs NuGet once).

---

## 3. Build and run

```powershell
dotnet build src\EplanRpc.csproj -c Release          # -> src\bin\Release\EplanRpc.exe
src\bin\Release\EplanRpc.exe smoke                   # offline self-test (IEC)
src\bin\Release\EplanRpc.exe docs                    # regenerate docs\*.md
src\bin\Release\EplanRpc.exe dump src\commands.json  # regenerate command dictionary
src\bin\Release\EplanRpc.exe serve --mode offline --port 45455
src\bin\Release\EplanRpc.exe serve --mode online  --port 45455
```

`probe` prints remoting discovery. `--surface <path>` overrides the API dump; otherwise the
copy embedded in the exe is used, so the exe is self-contained.

**Isolation rules learned the hard way**

* offline runs in its own process; a project file locked by the GUI cannot be opened —
  always copy or create a fresh `.elk` under `%TEMP%`.
* online never initialises `EplApplication` (`Init` would fight the running GUI); it only
  talks remoting.
* the service process must stay inside one shell call in agent harnesses that kill children.

---

## 4. Wire protocol

TCP, one UTF-8 JSON/compact command per line in, one JSON line out.

```text
function.place symbol=X tag=-X1 x=40 y=180 v=3
api.invoke DataModel.Function.Create page=@page variant=@variant:X:0
{"cmd":"project.create","args":{"link":"C:\\tmp\\p.elk","template":"GOST_bas001"}}
```

* `region.verb` + named `key=value`; quote values with spaces/commas.
* Server state: `@project`, `@page` (set by `project.open` / `page.use` / `schematic.draw`).
* Session handles: `@handle:<h>`.
* `api.invoke <Type.Member>` is the only place with dotted type names.

---

## 5. Command inventory

| region | commands |
|---|---|
| system | `ping` `state` `shutdown` `meta.commands` `meta.regions` `system.init` `system.version` `system.mode` `stats` |
| project | `project.create` `project.open` `project.close` `project.pages` `project.current` |
| page | `page.create` `page.list` `page.use` `page.clear` |
| symbol | `symbol.list` `symbol.check` |
| function | `function.place` `function.remove` `function.tag` `function.designation` `function.pins` |
| connection | `connection.generate` `connection.read` |
| generate | `generate.connections` |
| graphics | `graphics.line` `graphics.rect` `graphics.text` |
| export | `export.image` `export.pdf` |
| settings | `settings.get` `settings.set` |
| schematic | `schematic.draw` (whole page from JSON) |
| remoting | `remoting.connect` `remoting.ping` `remoting.disconnect` `remoting.action` `remoting.script` |
| api | `api.regions` `api.namespaces` `api.types` `api.members` `api.desc` `api.invoke` |

Per-command arguments/examples: `docs\commands.md`, `docs\regions\*.md`
(these are generated by `EplanRpc.exe docs` — keep them in sync with the registry).

---

## 6. Architecture map (`src/`)

| file | responsibility |
|---|---|
| `Program.cs` | entry point / modes; hard-codes platform bin + dev surface path |
| `Server.cs` | TCP accept loop, dispatch |
| `Protocol.cs` | line framing, `key=value` tokenizer, quoting, JSON reply |
| `CommandModel.cs` | `Cmd` registry: identity, mode admission, metadata, regions |
| `Commands.cs` | module registration list |
| `SystemCommands.cs` / `CoreCommands.cs` | system + project/page/symbol/function/connection/graphics/export/settings |
| `RemotingCommands.cs` | `remoting.*` (online only) |
| `SchematicCommands.cs` | `schematic.draw` (thin entry to `Offline.SchematicDraw`) |
| `Offline.cs` | **the object-model adapter**: init, project/page CRUD, `PlaceFunction`, pin resolution, `DynamicConnectionLine`, texts, export, connections, spec pipeline |
| `Online.cs` | remoting adapter (gRPC) + script execution |
| `Remoting.cs` | assembly resolver and remoting boot for EPLAN's client DLLs |
| `ApiSurface.cs` | embedded API dump access: namespaces/types/members/desc/invoke |
| `PinMap.cs` | embedded `symbol_pins.json` fallback pin geometry |
| `State.cs` | process state (mode, project, page, dirs, templates) |
| `Docs.cs` | generates `docs\*.md` from the registry |
| `Smoke.cs` | offline self-test (IEC template end-to-end) |
| `scripts\*.cs` | payloads executed **inside the GUI** by `remoting.script` (not compiled into the server) |
| `specs\do_feedback.json` | the reference drawing spec (DO 24 V DC / 220 V AC page) |

Layering rule: one thin module per region; no second metadata list; adding a command touches
`*Commands` + the registry only. The 7388-type API surface is *not* expanded into commands —
it is reachable through `api.invoke` (long tail) and `remoting.script` (arbitrary C#).

---

## 7. Hard-won domain facts (do not re-derive)

1. **Offline init**: `EplApplication.Init` in its own process; then `ProjectManager` /
   `Page`/`Function` straight from `Eplan.EplApi.DataModel`. No license dialog for these calls.
2. **Template resolution**: a bare template name (`GOST_bas001`) resolves inside the installed
   EPLAN `Templates\EPLAN` folder. Offline `Function.Create(page, variant)` only accepts a
   variant from a library registered by the project template — hence `link=... template=GOST_bas001`.
3. **Pin geometry** comes from `SymbolVariant.ConnectionPoints` (`PinBase`: `Index`, `Location`),
   **not** from `Function.ConnectionPointPositions` (returns zeros for placed functions).
   Endpoint = `placement.Location + ConnectionPoints[pinIndex].Location`.
4. **`DynamicConnectionLine`** is EPLAN's own wire object and renders in connection-line colour:
   `dcl.Create(page)`; `dcl.Location` is the **absolute** start; `dcl.SetGraphics(new PointD(0,0),
   new PointD(dx,dy))` is the **relative** segment. `GetGraphics().GetBoundingBox()` confirms it.
5. **`Export.GraphicPage(page, dir, PNG, dpi, width, ...)` takes a DIRECTORY** and names the file
   after the page. Passing `*.png` silently creates a directory with that name (defect fixed in
   `ResolvePngDir`).
6. **`Generate.Connections`** turns touching lines into logical connections;
   `connection.read` then reports real nets (verified: 7 wires -> 7 nets, 14 function-side links).
7. **Coil contact image**: `Function.ContactImageType = None` hides the auto contact mirror for
   a coil placed as `K`.
8. **`Function.Pins`** exists and should be the authority for absolute pin positions
   (`AreConnectionPointPositionsLocal = false`); it currently reports zeros — see
   `OPEN_ISSUES.md` O2.
9. **Quoting**: the tokenizer must strip the closing quote of `key="value"` (fixed); otherwise
   `link="C:\..."` becomes an invalid path and `spec="..."` is "not found".

---

## 8. Drawing pipeline (`schematic.draw`, offline)

```json
{
  "page":   { "library": "GOST_symbol", "plant": "DO", "location": "FB", "counter": 1, "replace": true },
  "placements": [ { "id": "X1", "symbol": "X", "variant": 0, "tag": "-X1",
                    "x": 90, "y": 170, "pins": ["1","2"] } ],
  "wires":  [ { "from": { "placement": "X1", "pin": "2" },
                "to":   { "placement": "K1_coil", "pin": "A1" } } ],
  "texts":  [ { "text": "...", "x": 40, "y": 195, "height": 3.5 } ],
  "export": { "path": "C:\\tmp\\out", "width": 1800 }
}
```

Order: page → placements (capturing `SymbolVariant.ConnectionPoints`) → wires
(pin lookup by **declared pin order**) → texts → `Generate.Connections` → PNG export
(`export.path` is a **directory**).

The result reports `wireCheck{count, locationOk, items[expect, location, bbox]}`, `functions`,
`connections`. `locationOk` only proves the line *starts* on the requested point — it compares
against the same computed value, so it is a self-consistency check, not a proof of connection.

---

## 9. The open fidelity gap (read `OPEN_ISSUES.md` first)

The user's acceptance loop is **online in the GUI**, and the complaint is *"линии не соеденены"*.
Two independent wire producers exist and they disagree on pin offsets:

* `schematic.draw` (offline) uses `SymbolVariant.ConnectionPoints` — e.g. for `X` at `y=170` the
  second pin resolves to `169.25`, for `K` at `150` → `154 / 146`.
* `scripts\draw_do_feedback.cs` (online) uses **hard-coded guesses** `±4 mm` around each
  placement (`160 → 156`, `146 → 150/142`, `132 → 136`), which leaves visible gaps at the
  terminals in the GUI.

That is the concrete defect to fix first: make the online path consume the same authoritative
pin geometry instead of literals.

---

## 10. How to extend

1. Add a method to the matching `*Commands` module and register it in `CommandModel`/`Commands`.
2. Keep replies terse JSON; never dump whole object graphs.
3. For anything not covered, prefer `api.invoke` (offline) or `remoting.script` (online) over
   new bespoke commands.
4. Regenerate docs: `EplanRpc.exe docs` and `EplanRpc.exe dump src\commands.json`.
5. Re-run `EplanRpc.exe smoke`; it must stay green.

---

## 11. Package contents

```
README.md                  this file
BRIEF.md                   task statement to hand to the next model
OPEN_ISSUES.md             evidence-backed defect list, ordered, with next probes
VALIDATION.md              how the human validates the result in the GUI
MANIFEST.md                file inventory with sizes and SHA-256
src\                       full server source + csproj + embedded data + specs + GUI scripts
api\EPLAN_API_SURFACE.json 7388-type public API dump (embedded into the exe)
docs\                      generated command reference, api map, recipes, region notes, SKILL.md
diagnostics\               repro drivers (PowerShell), recorded outputs, sample specs, PNGs
reports\                   the research reports behind this code (wY1..wY7)
artifacts\                 reference renders
```

## 12. Suggested reading order for the next model

1. `OPEN_ISSUES.md` (what is broken and why it matters)
2. `src\Offline.cs` (object model usage; start at `SchematicDraw`, `PlaceFunction`, `DrawConnectionLine`)
3. `src\scripts\draw_do_feedback.cs` (the online twin that still uses literal offsets)
4. `docs\recipes.md` + `docs\regions\schematic.md`
5. `reports\wY6_...` and `reports\wY7_...` (how the current pipeline was proven)
