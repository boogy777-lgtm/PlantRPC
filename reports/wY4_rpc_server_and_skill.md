# wY4 — EplanRpc: единый C# RPC (offline/online) + скилл eplan-api

**Дата:** 2026-10-28. **Режим:** READ-ONLY (анализ/сборка; запись только в артефакты волны).
**Цель:** выполнить `EPLAN_API_RPC_PLAN.md` — починить C# gRPC Remoting, собрать один C#
RPC-сервер с режимами `offline|online` и generic `api.invoke`, сгенерировать ленивый скилл.
**Продукт:** EPLAN Electric P8 **2026.0.3.25702** (вариант `C:\Program Files\EPLAN\Electric P8\2026.0.3\Bin`).
**Инструмент:** .NET SDK **8.0.423** (target `net481`, x64), Python 3.14 (client/генератор).

---

## TL;DR

1. **A1 решён.** Причина `Failed to deserialize response message` — НЕ protobuf, а резолв
   `System.Memory, Version=4.0.1.1` (и `System.Buffers` / `System.Runtime.CompilerServices.Unsafe`),
   которые лежат в `C:\Program Files\EPLAN\Common\IdentityClient`, а не в `Platform\Bin`.
   Пробинг только `Platform\Bin` давал `FileNotFoundException`. Добавил `IdentityClient` в
   `AssemblyResolve`-пробинг + `SetDllDirectory` → `EplanRemoteClient.Connect + Ping() == True`
   из чистого C# (`Remoting.Probe()`).
2. **Собран `EplanRpc.exe`** (`artifacts\wY_rpc\`): net481, x64, без compile-time ссылок на gRPC
   (runtime-load), оффлайн-поверхность — compile-refs на `Eplan.EplApi.*u.dll` (`Private=false`).
3. **Протокол:** компактная строка `region.verb k=v...` + JSON-эквивалент; `@page`/`@project`/`@handle`
   токены; ответ всегда JSON-строка `{"ok":..,"result":..}`.
4. **Generic 100% API:** `api.regions / namespaces / types / members / desc` — из заранее снятого
   дампа `EPLAN_API_SURFACE.json` (7388 типов), без EPLAN-runtime; `api.invoke` — живой reflective
   вызов (static/ctor/instance на `@page`/`@project`).
5. **Offline-цепочка проверена end-to-end** (см. §3): `project.create → page.create →
   symbol.check → function.place (tag/vis/v) → generate.connections → export.image → stats`.
6. **Online-цепочка проверена end-to-end** (см. §4): `remoting.connect → api.invoke
   @project.ProjectName` (вернул `project1` — проект, открытый в GUI) и `api.invoke
   Eplan.EplApi.Base.PointD.new x=1 y=2`.
7. **Скилл сгенерирован** в `~/.agents/skills/eplan-api/` (`SKILL.md` + `references/commands.md`,
   `api-map.md`, `recipes.md`, `regions/*.md` — 46 команд, 12 регионов), ленивый роутер.

---

## 1. A1 — починка C# gRPC Remoting

Файл: `artifacts\wY_rpc\Remoting.cs` (классы `Remoting`, `Refl`).

Корневая причина (доказано итерациями `EplanRpc.exe probe`):

- Первая попытка (compile-refs на remoting + `AutoGenerateBindingRedirects` + копии
  `System.Memory 4.6.31308` в app-dir) → `Failed to deserialize response message`.
- После перехода на runtime-load и пробинг только `Platform\Bin` →
  `CommunicationException: Could not load System.Memory 4.0.1.1` (точная диагностика).
- Версии: `Grpc.Core 2.46.6`, `Grpc.Core.Api 2.55.0`, `Google.Protobuf 3.24.1`,
  нативный `grpc_csharp_ext.x64.dll` (12.6 MB) — всё в `Platform\Bin`; `System.Memory`
  (AssemblyVersion 4.0.1.1, FileVersion 4.6.31308.01) — только в `IdentityClient`.

Фикс: `Remoting.EnsureResolver()` регистрирует `AssemblyResolve` с пробингом
`{Platform\Bin, Common\IdentityClient}` + `SetDllDirectory(Platform\Bin)`, вызывается в
`Main` до любого JIT-касания Eplan-типов. `Remoting.Boot()` дополнительно грузит
`Starteru/RemoteClientu/Remotingu` через `Assembly.LoadFrom`.

Критерий выполнен:
```
try 127.0.0.1:49152 ...  Server response: Hello my friend ...  ping=True  CONNECTED
```

`Refl` (рефлексионная обёртка) — `Find/New/Call/GetProp/SetProp/OutList`; `Call` уплощает
`TargetInvocationException`, `Match` учитывает `ByRef`-параметры (нужно для
`ExecuteAction(string, ref CallingContext)`).

## 2. Архитектура сервера

Файлы: `Program.cs` (режимы `probe/serve/dump`), `Server.cs` (TCP, одна строка = одна команда),
`Protocol.cs` (компактный + JSON парсер), `Commands.cs` (`Cmd.Dispatch` + таблица `Cmd.Meta`),
`ApiSurface.cs` (метаданные из дампа), `Offline.cs`, `Online.cs`, `State.cs`.

- `serve --mode offline|online --port N --surface <dump.json>`.
- `dump <out.json>` — выгружает `Cmd.Meta` (источник истины для генератора доков).
- Offline: `EplApplication.Init` лениво; типизированные регионы + generic `api.invoke`.
- Online: `remoting.connect` → `EplanRemoteClient`; `api.invoke`/`remoting.script` → генерируемый
  reflection-скрипт (`ExecuteScript`), результат читается из файла (шаблон `Newtonsoft.Json`).
- Регионы DataModel (project/page/function/…) — только offline; в online длинный хвост закрывают
  `api.invoke` и `remoting.script` (задокументировано в скилле).

## 3. Проверка offline (команды и результаты)

Сервер `serve --mode offline --port 45455`; клиент `client.py`.

```
project.create link=out\rpc_test.elk          -> {"project": ...\rpc_test.elk}
page.create name=1                            -> {"page":"1"}
symbol.check library=IEC_symbol name=X variant=3 -> {"conpoints":2}
function.place symbol=X tag=-X1 vis=L x=40 y=180 v=3 -> {"placed":"=+-X1","handle":"68/17/1/0"}
function.place symbol=F1 tag=-F1 x=100 y=180 v=1 -> {"placed":"=+-F1"}
generate.connections                          -> "generated"
export.image path=out\rpc_test.png width=800  -> {"image": ...\rpc_test.png}
stats                                         -> (fn/conn counts)
```

Замечание (важно для скилла): после close/reopen имя листа перечитывается — в тесте
`project.pages` вернул `/1` и `=CA1+EAA/1`, а не `1`. Отражено в recipes.md.

Generic offline: `api.invoke @project.ProjectName` → `rpc_test`; `api.members type=...
PointD contains=ctor` → `ctor PointD(double x, double y)`.

## 4. Проверка online (GUI PID 5572, порт 49152)

```
remoting.connect host=127.0.0.1 port=49152 -> {"mode":"online","onlinePort":"49152"}
remoting.ping                             -> {"alive":true}
api.invoke @project.ProjectName           -> {"result":"project1"}
api.invoke Eplan.EplApi.Base.PointD.new x=1 y=2 -> {"result":"Eplan.EplApi.Base.PointD"}
```

`@project.ProjectName` вернул `project1` — проект, реально открытый в GUI. Это доказывает всю
онлайн-цепочку: gRPC connect → генерация reflection-скрипта → `ExecuteScript` → чтение результата.

## 5. Скилл

`~/.agents/skills/eplan-api/SKILL.md` (ленивый роутер: 2 режима, грамматика, регионы одной
строкой, токен-бюджет) + `references/`:
- `commands.md` — индекс 46 команд (таблица ID|args|returns|example).
- `api-map.md` — регион → namespace → ключевые типы.
- `recipes.md` — 3 цепочки-однострочника (offline PSU, online read, explore API).
- `regions/<region>.md` — 12 файлов, лениво.

Генератор `gen_commands.py` запускает `EplanRpc.exe dump` и строит доки из `Cmd.Meta`,
поэтому словарь команд не может разойтись с кодом.

---

## Open items / следующие шаги

1. **`connection.connect` / `connection.route` (электрические соединения, `DynamicConnectionLine`)**
   — offline ещё не реализован (нужна geometry выводов); online доступен через MCP
   `live_connect_pins(_routed)` или `remoting.script`.
2. **`masterdata.parts` и `action.run/describe`** — объявлены в региональной карте, но команд
   ещё нет; добавить обёртки над `MDPartsManagement` / `ActionManager`.
3. **Типизированные регионы в online-режиме** — сейчас длинный хвост закрывают `api.invoke`
   и `remoting.script`; при необходимости транслировать `page.create/function.place/...` в
   скрипты (шаблоны из MCP `schematic.py`/`live.py`).
4. **`api.invoke`** — поддержка indexed-property / `@handle` / `@variant` токенов; сейчас
   static/ctor/instance(`@page`,`@project`) + `value=` для set.
5. **Развёртывание** — зафиксировать `--surface` путь (сейчас абсолютный dev-путь в
   `Program.SurfaceDefault`), `--port` по умолчанию 45455.

## 6. Воспроизведение

```
# сборка
dotnet build artifacts\wY_rpc\EplanRpc.csproj -c Release

# A1
artifacts\wY_rpc\bin\Release\EplanRpc.exe probe

# offline
artifacts\wY_rpc\bin\Release\EplanRpc.exe serve --mode offline --port 45455
py artifacts\wY_rpc\client.py "ping" "state" "api.namespaces contains=DataModel" "shutdown"

# online (GUI запущен, порт 49152)
artifacts\wY_rpc\bin\Release\EplanRpc.exe serve --mode online --port 45456
py artifacts\wY_rpc\client.py --port 45456 "remoting.connect host=127.0.0.1 port=49152" "api.invoke @project.ProjectName" "shutdown"

# доки
py artifacts\wY_rpc\gen_commands.py
```

Артефакты: `artifacts\wY_rpc\` (исходники, `bin\Release\EplanRpc.exe`, `client.py`,
`gen_commands.py`, `commands.json`). Скилл: `~/.agents/skills/eplan-api\`.
