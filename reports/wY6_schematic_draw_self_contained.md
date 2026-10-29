# wY6 — Self-contained `schematic.draw` (offline native)

**Дата:** 2026-10-28. **Режим:** implementation (не READ-ONLY).
**Цель:** убрать клиентскую цепочку JSON→C# (`draw_template.cs` + `eplan_schematic.py`
+ `remoting.script`) и дать серверу одну нативную команду, которая рисует страницу
по JSON-спеке: размещения, реальные `DynamicConnectionLine` по фактическим пинам,
тексты, `generate.connections`, экспорт PNG.
**Продукт:** EPLAN Electric P8 `2026.0.3`; .NET Framework `4.8.1`; SDK `8.0.423`;
`Eplan.EplApi.*u.dll` из `C:\Program Files\EPLAN\Platform\2026.0.3\Bin`.

## TL;DR / Headline

1. **Добавлена команда `schematic.draw spec=<json>`** (region `schematic`,
   `CommandMode.Offline`). Это и есть «следующий тонкий слой» из open item #1 отчёта
   `wY5_rpc_thin_layers.md`: рисунок теперь целиком внутри exe.
2. **Пины берутся из фактической геометрии символа** — `SymbolVariant.ConnectionPoints`
   (`PinBase.Index` + `PinBase.Location`, `PointD`, локальные смещения). Встроенный
   `symbol_pins.json` оставлен только как fallback.
3. **`Function.ConnectionPointPositions` для этого не годится:** у размещённой функции
   позиции = `0`, а у IEC-символов и имена пустые (подтверждено `pin_offsets.txt` и
   прогоном). Поэтому источник смещений — `SymbolVariant`, а не `Function`.
4. **Сборка:** `net481`, `0 errors, 0 warnings`. Команд стало **47** (+1), регион
   `schematic` — новый.
5. **Прогон offline (IEC_symbol):** 3 placements, 2 провода, 1 текст,
   `connectionsGenerated=true`, `functions=3`, `connections=4`, PNG
   `=T+IEC_1.png` (14 970 B). Визуально: `-X1 → -F1 → -X2` соединены реальными линиями.
6. **Ограничение окружения:** offline-проект из шаблона `IEC_bas001` содержит только
   `IEC_symbol`. `GOST_symbol` — библиотека GUI-проекта, offline не открывается
   (`S063181`), поэтому GOST-схему offline рисуем только в проекте, где GOST
   зарегистрирован (напр. `project1`), либо в online-режиме.

## Основные разделы

### 1. Что добавлено

- `artifacts\wY_rpc\SchematicCommands.cs` — модуль команды:
  `schematic.draw` → `Offline.WithLock(() => Offline.SchematicDraw(a))`.
- `artifacts\wY_rpc\PinMap.cs` — лоадер `symbol_pins.json` (file-or-embedded,
  зеркалит `ApiSurface.Load`); используется как fallback смещений пинов.
- `artifacts\wY_rpc\Offline.cs` — новое:
  - `SchematicDraw(Dictionary)` — парсинг спеки, страница, размещения, провода,
    тексты, generate, export, статистика;
  - `PlaceFunction(... , out PinBase[] connectionPoints)` — единый механизм
    размещения, теперь отдаёт фактическую геометрию пинов; старый
    `function.place` делегирует в него (DRY);
  - `DrawConnectionLine(x1,y1,x2,y2)` — типизированный
    `DynamicConnectionLine.Create(Page)` + `Location` + `SetGraphics(rel)`.
- `artifacts\wY_rpc\Commands.cs` — `registry.Add(new SchematicCommands())`.
- `artifacts\wY_rpc\EplanRpc.csproj` — `symbol_pins.json` как `EmbeddedResource`
  (`LogicalName=symbol_pins.json`).
- `artifacts\wY_rpc\gen_commands.py` — регион `schematic` + рецепты (self-contained
  offline и legacy online).
- `~\.agents\skills\eplan-api\` — `references\*` перегенерированы (47 команд,
  13 регионов), в `SKILL.md` добавлена строка про `schematic.draw`.

### 2. Формат спеки (тот же, что у клиентского генератора)

```json
{
  "page": {"library":"IEC_symbol","plant":"T","location":"IEC","counter":1,"replace":true},
  "placements":[{"id":"X1","symbol":"X","tag":"-X1","x":50,"y":100,"pins":["1","2"]}],
  "wires":[{"from":{"placement":"X1","pin":"2"},"to":{"placement":"F1","pin":"1"}}],
  "texts":[{"text":"...","x":20,"y":120,"height":3.5}],
  "export":{"path":"<dir>","width":1200}
}
```

`wires[].from/to` — либо `{placement,pin}` (резолв в абсолютную точку), либо `[x,y]`.
`library` читается из top-level `library`, затем из `page.library`, затем
`GOST_symbol` (как дефолт клиентского генератора).

### 3. Разрешение пинов — механизм

1. При размещении `PlaceFunction` сохраняет `variant.ConnectionPoints` (`PinBase[]`).
2. Конец провода = `placement.Location + ConnectionPoints[pinIndex].Location`
   (`pinIndex` = позиция имени пина в `placements[].pins`).
3. Если `ConnectionPoints` пуст/короче — fallback на встроенный `symbol_pins.json`
   (`PinMap.Get`), который был получен тем же способом (перечисление
   `SymbolVariant.ConnectionPoints`) и уже проверен на GOST.

Почему не `Function.ConnectionPointPositions`:
`pin_offsets.txt` показывает `px=0, py=0` для всех пинов размещённой функции; прогон
для `IEC_symbol` даёт ещё и пустые имена (`n:""`). Символ-вариант даёт правильные
локальные смещения, поэтому он и стал первичным источником.

### 4. Ограничение библиотек offline (доказательства)

- `symbol.list library=GOST_symbol` (system-scoped `Initialize`) → `opened:true`,
  `matched:290` (символы `S,O,SSV,...`) — сама библиотека на диске есть.
- `schematic.draw` с `GOST_symbol` в свежем offline-проекте →
  `SymbolLibraryOpeningFailedException: S063181 ... 'GOST_symbol' ... нет в
  библиотеках проекта или системы`.
- system-scoped `SymbolLibrary().Initialize()` + `Function.Create` →
  `ObjectCreationException: S063085 ... Невозможно создать функцию` (вариант не
  привязан к проекту).
- Вывод: `Function.Create` требует project-scoped `SymbolLibrary(Project, name)`,
  а offline-проект из `IEC_bas001.zw9` несёт только `IEC_symbol`. Публичного
  метода «добавить библиотеку в проект» нет (`Project.SymbolLibraries` — только get).
- `project1` (`C:\Users\HALLIBURTON\Documents\Eplan_Test\project1.elk`) держит
  `GOST_symbol`, но сейчас открыт в GUI (2 процесса EPLAN) и offline-открывается
  эксклюзивно только после закрытия GUI.

### 5. Прогон (offline, IEC_symbol)

```
ping → pong
project.create link=%TEMP%\iec_draw.elk → ok
schematic.draw spec=%TEMP%\iec_spec.json →
  {"page":"=T+IEC/1","placements":3,"wires":2,"texts":1,
   "connectionsGenerated":true,"functions":3,"connections":4,
   "exportPath":"...\\iec_draw_vbai8yl8"}
connection.read →
  X1:[68/18/1/0]  F1:[68/18/1/0,68/18/2/0]  X2:[68/18/2/0]
```

PNG `=T+IEC_1.png` (14 970 B): `-X1 → -F1(fuse) → -X2` соединены линиями,
текст `IEC offline test`, рамка/штамп EPLAN. Провода легли на фактические пины,
`generate.connections` дал реальные связи.

## Open items

1. **GOST offline** требует проекта с зарегистрированным `GOST_symbol`. Варианты:
   (а) открыть `project1` offline при закрытом GUI; (б) найти/создать шаблон с GOST;
   (в) online-режим (`eplan_schematic.py` + `remoting.script`), как раньше.
2. Публичного API «добавить symbol library в проект» не найдено
   (`Project.SymbolLibraries` read-only). Если нужно — исследовать
   `SymbolLibrary.Initialize(Project, name)` на предмет регистрации.
3. `CommandMode.Online` по-прежнему не используется (`Any` + `RequireClient`).
4. `api.invoke` не поддерживает индексированные свойства / `@handle` / `@variant`.
5. Экспорт: `export.path` трактуется как каталог (как в принятой спеке); путь с
   `.png` трактуется как файл (создаётся родительский каталог).
6. `project1` после прошлых прогонов не сохранялся автоматически (сохранять `Ctrl+S`).

## Воспроизведение

```powershell
# build
dotnet --version   # 8.0.423
dotnet build artifacts\wY_rpc\EplanRpc.csproj -c Release   # 0/0

# offline self-contained draw (IEC)
py artifacts\wY_rpc\test_schematic.py
# или вручную:
#   EplanRpc.exe serve --mode offline --port 45460
#   project.create link=%TEMP%\p.elk
#   schematic.draw spec=<spec.json>

# docs
py artifacts\wY_rpc\gen_commands.py
```

Артефакты:
- `artifacts\wY_rpc\SchematicCommands.cs`, `PinMap.cs`, `Offline.cs`,
  `Commands.cs`, `EplanRpc.csproj`.
- `artifacts\wY_rpc\commands.json` — 47 команд, регион `schematic`.
- `artifacts\wY_rpc\test_schematic.py` — offline smoke-тест.
- PNG-проверка: `%TEMP%\iec_draw_vbai8yl8\=T+IEC_1.png`.
- Скилл: `~\.agents\skills\eplan-api\references\regions\schematic.md`.

## Update 2026-10-28 — RPC-сервер теперь чистый C# (без Python)

По требованию «в RPC-сервере не должно быть Python»: из `artifacts\wY_rpc\`
удалены `client.py`, `eplan_schematic.py`, `gen_commands.py`, `test_schematic.py`,
а также осиротевший `draw_template.cs` (его единственным потребителем был
`eplan_schematic.py`) и `<Compile Remove="draw_template.cs" />` из `.csproj`.

Функциональность перенесена в C#:

- `Docs.cs` + режим `EplanRpc.exe docs` — пишет `commands.json` и
  `commands.md` / `api-map.md` / `recipes.md` / `regions/*.md` (замена
  `gen_commands.py`; источник — та же таблица команд `Cmd.ListCommands()`).
- `Smoke.cs` + режим `EplanRpc.exe smoke` — offline self-test: `project.create`
  + `schematic.draw` на маленькой IEC-странице (замена `test_schematic.py`).
- `Program.cs` — `usage: probe|serve|dump|docs|smoke`.

Проверки:

- `dotnet build -c Release` → **0 errors, 0 warnings**.
- `EplanRpc.exe docs` → `commands: 47, regions: 13` в skill references.
- `EplanRpc.exe smoke` →
  `{"page":"=T+IEC/1","placements":3,"wires":2,"texts":1,
  "connectionsGenerated":true,"functions":3,"connections":4}`.
- `Get-ChildItem -Filter *.py` в `artifacts\wY_rpc` → **пусто**.

`scripts\generated\draw_*.cs` остаются C#-артефактами (excluded from build);
новые не генерируются. Для online GOST доступен `remoting.script` (нативный
C#-скрипт, без Python).

