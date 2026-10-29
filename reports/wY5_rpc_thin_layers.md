# wY5 — RPC thin layers + embedded API surface

**Дата:** 2026-10-28  
**Режим:** implementation (не READ-ONLY)  
**Цель:** убрать giant switch из `EplanRpc`, разнести команды на тонкие слои,
встроить публичный API surface в exe и сохранить поведение offline/online.  
**Продукт:** EPLAN Electric P8 2026.0.3; .NET Framework 4.8.1; SDK 8.0.423.

## TL;DR / Headline

1. `Commands.cs` больше не содержит switch и второй список метаданных: добавлены
   `CommandDef`, `ICommandModule`, `CommandRegistry`.
2. 46 команд разнесены по 11 модулям: system, api, project, page, symbol,
   function, connection, graphics, export, settings, remoting.
3. Дубликат имени команды теперь падает на старте registry — метаданные и
   dispatch не могут разойтись.
4. `EPLAN_API_SURFACE.json` встроен как `EmbeddedResource`; внешний `--surface`
   остался dev-override. Exe работает без внешнего дампа.
5. `net481` собирается: **0 errors, 0 warnings**.
6. Offline smoke: `project.create → page.create → function.place → stats →
   connection.generate` работает. Online smoke: `remoting.connect → ping →
   api.invoke @project.ProjectName` работает.
7. JSON-рисование через refactored server тоже работает: `=DO+FB/1`,
   10 placements, 7 wires, 15 texts, `connectionsGenerated=true`.

## Архитектура

```
Server / Protocol          — TCP framing, JSON/compact, no domain logic
CommandRegistry            — identity, mode admission, metadata
*Commands modules          — thin domain layers, one file/class per region
Offline / Online           — backend adapters (typed / remoting)
ApiSurface                 — embedded public API map
api.invoke                 — universal long tail over reflection
```

Правило расширения: новая команда = один `CommandDef` в одном модуле. Registry
не меняется; второй список метаданных не появляется.

Mode admission:
- `CommandMode.Offline` — project/page/symbol/function/connection/graphics/
  export/settings; в online выдаёт прежнюю ошибку с указанием `api.invoke`.
- `CommandMode.Any` — system/meta/api/remoting; `remoting.connect` может
  переключить сервер в online.
- `CommandMode.Online` — зарезервирован; сейчас не используется.

## Изменённые/новые файлы

- `artifacts\wY_rpc\CommandModel.cs` — `CommandDef`, `ICommandModule`,
  `CommandRegistry`.
- `artifacts\wY_rpc\SystemCommands.cs`
- `artifacts\wY_rpc\ApiCommands.cs`
- `artifacts\wY_rpc\CoreCommands.cs` — project/page/symbol/function/connection/
  graphics/export/settings.
- `artifacts\wY_rpc\RemotingCommands.cs`
- `artifacts\wY_rpc\Commands.cs` — compatibility facade `Cmd`.
- `artifacts\wY_rpc\EplanRpc.csproj` — embedded surface; `scripts\**\*.cs` и
  `draw_template.cs` исключены из компиляции сервера.
- `artifacts\wY_rpc\Program.cs` — surface load с embedded fallback.
- `artifacts\wY_rpc\ApiSurface.cs` — file-or-embedded loader.
- `~\.agents\skills\eplan-api\SKILL.md` — слои сервера и JSON-рецепт.
- `~\.agents\skills\eplan-api\references\*` — перегенерированы из 46 команд.

## Проверки

Сборка:

```powershell
dotnet --version   # 8.0.423
dotnet build artifacts\wY_rpc\EplanRpc.csproj -c Release
# ok: 0 errors, 0 warnings
```

Offline:

```text
ping → pong
api.namespaces contains=DataModel → 10 namespaces
api.desc Eplan.EplApi.DataModel.Page → полный member list
api.invoke Eplan.EplApi.Base.PointD.new x=1 y=2 → {X:1,Y:2}
project.create → page.create → function.place → stats → connection.generate
```

Online:

```text
system.mode → online
remoting.connect host=127.0.0.1 port=49152
remoting.ping → alive:true
api.invoke @project.ProjectName → project1
project.pages → корректная mode-ошибка (offline-only)
```

Embedded surface:

```powershell
EplanRpc.exe serve --mode offline --port 45459 --surface C:\nonexistent\surface.json
# api.desc ... работает из embedded resource
```

JSON drawing:

```powershell
py artifacts\wY_rpc\eplan_schematic.py `
  --spec artifacts\wY_rpc\specs\do_feedback.json
# page =DO+FB/1; placements=10; wires=7; texts=15; connectionsGenerated=true
```

## Open items

1. Серверная команда `schematic.draw` пока не добавлена: JSON→C# остаётся
   клиентским генератором. Это следующий тонкий слой, если нужен полностью
   self-contained рисунок без внешнего шаблона.
2. `CommandMode.Online` зарезервирован, но не используется; online-команды
   сейчас `Any` + проверка `RequireClient`.
3. Проект EPLAN после рисования не сохранялся; страница видна в GUI, сохранить
   `Ctrl+S`.
4. `project.pages` в online теперь даёт явную mode-ошибку (раньше падал в
   offline-ветку); это осознанное ужесточение.

## Воспроизведение

- Сервер: `C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\bin\Release\EplanRpc.exe`
- JSON-спека: `...\artifacts\wY_rpc\specs\do_feedback.json`
- PNG: `...\artifacts\wY_rpc\do_fb_spec_export\=DO+FB_1.png`
- Документация команд: `...\artifacts\wY_rpc\commands.json`
