---
name: eplan-api
description: Drive EPLAN Electric P8 via the EplanRpc server (one C# exe, modes offline|online). Build or read schematics (place functions, wire, generate, export), or call any of the 7388-type public API through the generic api.invoke long tail. Use when a task needs EPLAN object-model access rather than the action-only MCP surface.
metadata:
  version: "2"
---

# eplan-api

Drive EPLAN Electric P8 through **EplanRpc** — a long-lived C# TCP server
(`serve --mode offline|online`). One JSON/compact command per line in, one JSON
line out. This skill teaches the *grammar*; the per-region command detail is
loaded lazily from `references/regions/<region>.md`.

## Two modes

| mode | mechanism | when |
|---|---|---|
| `offline` | `EplApplication.Init` in its own process; direct typed calls + reflection | no GUI needed; own project file |
| `online` | gRPC remoting into a running EPLAN GUI; commands run as reflection scripts (`ExecuteScript`) | project already open in the GUI (`project1.elk`) |

One command set in both modes; only the transport differs. `remoting.*` exists
only online; the DataModel regions (`project/page/symbol/function/...`) exist
only offline. In online mode reach the long tail with `api.invoke` or
`remoting.script`.

## Server layers

- `Server` / `Protocol` — framing only.
- `CommandRegistry` — command identity, mode admission, metadata.
- `*Commands` modules — one thin domain layer each.
- `Offline` / `Online` — backend adapters.
- `ApiSurface` — embedded public API map; `api.invoke` is the universal long tail.
- `Docs` / `Smoke` — C# doc generator and offline self-test (no Python).

The server is pure C#. Adding a command touches one module; no dispatcher switch
and no second metadata list. The 7388-type surface is not expanded into 7388
commands.

## Grammar

Commands are `region.verb` + named `key=value` args. A value with spaces/commas
is double-quoted. Objects are `@`-tokens. The JSON form
`{"cmd":"...","args":{...}}` is also accepted.

```
function.place  symbol=X tag=-X1 x=40 y=180 v=3
api.invoke      DataModel.Function.Create page=@page variant=@variant:X:0
api.invoke      @project.ProjectName
```

- `@page` / `@project` = server state (set by `project.open`/`page.use` offline, or the GUI's focused project online).
- `@handle:<h>` = a session-scoped object handle returned by a previous call.
- `api.invoke <Namespace.Type.Member>` is the **only** place with `Type.Member` syntax — everything else is an ID + named args.

## Regions

`system project page symbol function connection graphics generate export schematic action settings masterdata remoting api`

Before working in region X, read `references/regions/X.md`. The generic surface
lives behind `api.*` (no file needed to *use* it).

## Token budget

1. Write IDs + named args only — no `using`, no full signatures.
2. Read one `regions/<r>.md` at a time; never load the full API dump.
3. `project`/`page` are server state — don't repeat them per command.
4. Responses are already terse; use `limit=`/`contains=` to trim.
5. Copy recipes from `references/recipes.md` one line at a time.
6. Quote values containing spaces or commas.

## Quick reference

- Offline: `serve --mode offline` → `project.create` → `page.create` → `function.place` → `generate.connections` → `export.image`.
- Online: `serve --mode online` → `remoting.connect` → `api.invoke ...` / `remoting.script`.
- Explore API: `api.namespaces`, `api.types`, `api.members`, `api.desc`.
- Self-contained draw: `project.create link=x.elk template=GOST_bas001` → `schematic.draw spec=specs\do_feedback.json` (offline; placements + real DynamicConnectionLine wires on actual pins + texts + generate.connections + PNG; no template/Python/remoting). The result's `wireCheck.locationOk` proves each line starts on its pin; `export.path` is a **directory** (the file is named after the page). A bare template name resolves in the installed EPLAN template folder (`GOST_bas001` → GOST_symbol, `IEC_bas001` → IEC_symbol).
- Maintenance (C#): `EplanRpc.exe docs` regenerates references, `EplanRpc.exe smoke` runs an offline self-test.
- See `references/commands.md` (index) and `references/api-map.md` (region→namespace).
