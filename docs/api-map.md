# API map: region -> namespace -> key types

- **system** — server state + mode — `EplApplication (Eplan.EplApi.System)`
- **project** — create/open/list projects — `ProjectManager, Project (Eplan.EplApi.DataModel)`
- **page** — create/select pages — `Page, PagePropertyList (Eplan.EplApi.DataModel)`
- **symbol** — enumerate symbol libraries — `SymbolLibrary, Symbol, SymbolVariant (Eplan.EplApi.DataModel.MasterData)`
- **function** — place/tag devices — `Function (Eplan.EplApi.DataModel)`
- **connection** — generate/read connections — `Connection, DynamicConnectionLine (Eplan.EplApi.DataModel)`
- **graphics** — graphic primitives (NOT electrical) — `Graphics.* (Eplan.EplApi.DataModel.Graphics)`
- **generate** — Generate.Connections — `Generate (Eplan.EplApi.HEServices)`
- **export** — PDF/PNG export — `Export (Eplan.EplApi.HEServices)`
- **schematic** — draw a full page from a JSON spec (self-contained offline) — `Function, DynamicConnectionLine, Generate, Export (Eplan.EplApi.DataModel/HEServices)`
- **action** — run EPLAN actions — `ActionManager (Eplan.EplApi.ApplicationFramework)`
- **settings** — read/write settings — `Settings (Eplan.EplApi.Base)`
- **masterdata** — parts database — `MDPartsManagement (Eplan.EplApi.MasterData)`
- **remoting** — online: connect/script — `EplanRemoteClient (Eplan.EplApi.RemoteClient)`

The full surface (7388 types) is behind `api.types` / `api.members` / `api.desc`.
`api.desc <Type.Member>` is the lazy source for a single signature.
The surface is embedded in `EplanRpc.exe`; `--surface` is a development override.
