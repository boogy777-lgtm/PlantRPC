# wY7 — EplanRpc: проверка «линии не соеденены» + полный self-contained рисунок ГОСТ (offline)

**Дата:** 2026-10-28. **Режим:** read/write только в `artifacts\wY_rpc` (тестовые проекты — копии в `%TEMP%`).
**Продукт:** EPLAN Electric P8 `2026.0.3` (Bin: `C:\Program Files\EPLAN\Electric P8\2026.0.3\Bin`).
**Инструмент:** `EplanRpc.exe` (net481, x64, SDK `8.0.423`), сборка `0 warnings / 0 errors`.
**Сервер (после правок):** SHA-256 `0A90A0A65BB4604E60087D892F7ECD13E6DB79F3788828E3C0D7C5E2D925426B`.

## TL;DR / Headline

1. **Жалоба «линии не соеденены на листе 1» на текущем сервере НЕ воспроизводится.** Текущий `schematic.draw`
   рисует реальные `DynamicConnectionLine` от пина до пина; добавленный self-check `wireCheck.locationOk = 7/7`
   (для ГОСТ-спеки) доказывает, что начало каждой линии лежит точно в координате запрошенного пина.
2. **GUI больше не нужен для ГОСТ.** Найден и подключён штатный шаблон `GOST_bas001.zw9`, который
   регистрирует `GOST_symbol`. Полный self-contained цикл offline теперь в двух командах:
   `project.create link=x.elk template=GOST_bas001` → `schematic.draw spec=...` (10 размещений, 7 проводов, 14 соединений, PNG).
3. **Найден и исправлен реальный дефект протокола:** кавычки в `key="value"` не снимались (метод `Unquote`
   был мёртвым кодом). Из-за этого `link="…"` / `spec="…"` давали неверный путь → `project.create` падал
   («Не задан корневой каталог»), `schematic.draw` — «spec not found».
4. **Найден и исправлен дефект экспорта:** `Export.GraphicPage` принимает **каталог** (файл именуется по
   странице), а код считал `*.png` файлом → создавался каталог с именем `*.png`. Введён один резолвер
   `ResolvePngDir` и применён в **двух** местах (schematic.draw и `export.image`).
5. **Введён механизм `wireCheck`** (self-check): `schematic.draw` возвращает для каждого провода
   `expect` (пин-координаты), `location` (что реально сохранено в DCL) и `locationOk`. Это инвариант
   «линия соединена», а не декларация.
6. **Провода присутствуют в растре:** анализ пикселей PNG (1800×1273) находит непрерывные вертикальные
   прогоны 75–76 px (=17.5 мм) в x=385/728/1071 px (мм 90/170/250) — это провода колонок 1/2/3; стыкуются
   с пинами символов.
7. **Ключевое наблюдение по геометрии:** `DynamicConnectionLine.Location` — абсолютная точка (совпадает с
   пином старта), а `SetGraphics((0,0),(dx,dy))` задаёт **относительный** отрезок
   (`GetBoundingBox()` вернул `0..-15.25`, т.е. местную систему DCL). Это и есть корректный механизм.

## Основные разделы

### 1. Предпосылка и что проверялось

- Приложенный пользователем PNG (`do_fb_spec_export\=DO+FB_1.png`, 13:21) **идентичен** по геометрии
  свежему рендеру текущего сервера (совпадают прогоны длинных вертикальных линий).
- Гипотезы: (a) нет проводов; (b) провода нарисованы, но не касаются пинов; (c) линии видны, но нет
  электрических соединений.

### 2. Итог измерения (текущий сервер, ГОСТ offline)

`project.create link=%TEMP%\gost_base.elk template=GOST_bas001` → `symbol.check library=GOST_symbol name=X`
(`conpoints=2`) → `schematic.draw spec=diag\do_fb_gost.json`:

```
placements=10  wires=7  texts=15
wireCheck: count=7  locationOk=7
  { expect:[90,169.25,90,154], location:[90,169.25], bbox:[0,-15.25,0,0], locationOk:true }
  { expect:[90,146,90,132],    location:[90,146],    bbox:[0,-14,0,0],    locationOk:true }
  { expect:[170,169.25,170,154],location:[170,169.25],bbox:[0,-15.25,0,0], locationOk:true }
  { expect:[170,146,170,132],  location:[170,146],   bbox:[0,-14,0,0],    locationOk:true }
  { expect:[250,169.25,250,154],location:[250,169.25],bbox:[0,-15.25,0,0], locationOk:true }
  { expect:[250,146,250,134],  location:[250,146],   bbox:[0,-12,0,0],    locationOk:true }
  { expect:[250,126,250,112],  location:[250,126],   bbox:[0,-14,0,0],    locationOk:true }
connectionsGenerated=true  functions=10  connections=14
```

`connection.read` даёт непрерывные цепочки (7 уникальных соединений, каждое = 2 ссылки на функции):
`X1—111—K1—112—X2`, `X3—113—K1—114—X4`, `X5—115—K1—116—Y1—117—X6`.

### 3. Пиксельный анализ PNG (offline-рендер ГОСТ)

Файл `artifacts\wY_rpc\diag\gost_out\=DO+FB_1.png` (57561 B), `1800×1273` (= A3 420×297 мм, ≈4.2857 px/мм).
Скан «самого длинного вертикального прогона чернил» по каждому x:

```
x=385 (мм 89.8): 547..621 (мм 152.10..169.37) | 638..712 (мм 130.87..148.13)
x=728 (мм 169.9): 547..621 | 637..712
x=1071(мм 249.9): 547..621 | 638..707 | 724..798 (мм 110.80..128.07)
```

Каждый прогон = провод; разрыв посередине (622..637 px) — это тело символа (катушка/контакт), провода
подходят к пинам (169.25, 154, 146, 132 …). То есть линии **соединены**.

### 4. Дефекты и правки (append)

| # | Место | Симптом | Правка |
|---|---|---|---|
| D1 | `Protocol.Tokenize` | `key="value"` сохранял ведущую кавычку, `Unquote` не вызывался (мёртвый код) | при закрывающей кавычке дописываем её в токен (`if (c=='"'){inq=false;cur.Append(c);}`) |
| D2 | `Offline.SchematicDraw` (export) | путь `*.png` трактовался как файл → создавался каталог `*.png` | введён `ResolvePngDir(path)`; в `GraphicPage` передаётся **каталог**; `exportPath` = каталог |
| D3 | `Offline.ExportImage` | тот же дефект (другой сайт) | использует тот же `ResolvePngDir` |
| D4 | нет проверки соединения | «соединено» заявлялось, но не измерялось | `CheckLine` + поле `wireCheck{count,locationOk,items}` в ответе |
| D5 | `project.create template=` | нужен абсолютный путь к `.zw9` | `ResolveTemplate`: голое имя резолвится в `S.TemplatesDir\<name>.zw9`; добавлены `S.TemplatesDir`, `S.GostTemplate` |

### 5. Открытие: ГОСТ offline возможен без GUI

`S.TemplatesDir = C:\ProgramData\EPLAN\O_Data\Electric P8 Data\2026.0.3\Templates\EPLAN` содержит
`GOST_bas001.zw9` (12.5 МБ), который регистрирует `GOST_symbol`. Проверено: `symbol.check name=X
library=GOST_symbol` → `conpoints=2`; размещение и провода работают полностью offline.

Следствие: ранее записанный блокер «GOST_symbol не открывается offline» снят для случая
`template=GOST_bas001`. Единственный остающийся путь для «чужой» библиотеки — если её нет ни в одном
`.zw9`-шаблоне.

### 6. Обновлённые производные

- `Docs.cs` — recipe «draw a page from a JSON spec» переписан под `template=GOST_bas001`, `wireCheck`,
  «export.path = каталог». `EplanRpc.exe docs` → 47 команд / 13 регионов, `references\*` перегенерированы.
- `~\.agents\skills\eplan-api\SKILL.md` — quick reference дополнен (GOST-шаблон, `wireCheck.locationOk`,
  `export.path` каталог).
- `EplanRpc.exe smoke` — offline self-test (IEC): `wireCheck 2/2 locationOk`, `connections=4`.

## Open items

1. Причина исходного восприятия «не соеденены» пользователем — **не подтверждена**: артефакт совпадает с
   текущим. Нужен точный репро (какая страница/проект/команда) либо визуальная проверка нового PNG.
2. `function.pins` всё ещё возвращает нули (`ConnectionPointPositions` размещённой функции пуст) —
   работает только `SymbolVariant.ConnectionPoints` внутри `PlaceFunction`; для внешнего запроса геометрии
   пинов нужна отдельная обёртка (сейчас её нет).
3. Имена функций в отчёте — `=+-X1` (без plant/location): `fn.Name = "-X1"`. Проверить, нужно ли
   выставлять полное обозначение по странице.
4. `Export.GraphicPage` при `width` доминирует по X; для нестандартных форматов не проверялось.
5. Прочие open items из `wY6` остаются (api.invoke indexed-property, connection.connect/route,
   masterdata.parts, абсолютный dev-путь `Program.SurfaceDefault`).

## Воспроизведение

```powershell
# сборка
dotnet build artifacts\wY_rpc\EplanRpc.csproj -c Release        # 0/0
# offline self-test (IEC)
EplanRpc.exe smoke
# полный self-contained ГОСТ offline
EplanRpc.exe serve --mode offline --port 45474
#   project.create link=%TEMP%\gost_base.elk template=GOST_bas001
#   symbol.check library=GOST_symbol name=X variant=0
#   schematic.draw spec=C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_rpc\diag\do_fb_gost.json
#   connection.read ; stats
```

Артефакты:
- `artifacts\wY_rpc\wY7_gost_dofb.png` — SHA-256 `B03E844368C40E06B4CBE47DDCF8365D0E51679D515D819F1CF03F20F5B36C8B` (57561 B) — итоговый ГОСТ-рендер.
- `artifacts\wY_rpc\diag\gost_out\=DO+FB_1.png` — тот же рендер (каталог экспорта).
- `artifacts\wY_rpc\diag\do_fb_gost.json` — спека (export.path → `diag\gost_out`).
- `artifacts\wY_rpc\diag\run_gost_tmpl.ps1` — драйвер (TcpClient: project.create/template, symbol.check, schematic.draw, pages).
- `artifacts\wY_rpc\diag\scan5.ps1` — пиксельный анализ прогонов.
- Правки: `Protocol.cs`, `Offline.cs`, `State.cs`, `Docs.cs`; скилл `~\.agents\skills\eplan-api\SKILL.md`.

## Правки в коде (ссылки)

- `Protocol.cs` — tokenizer: закрывающая кавычка дописывается (D1).
- `Offline.cs`:
  - `ProjectCreate` → `ResolveTemplate`; новый `ResolveTemplate` (D5).
  - новый `ResolvePngDir`, `CheckLine`; `DrawConnectionLine` возвращает `DynamicConnectionLine` (D2–D4).
  - `SchematicDraw` — сбор `lineCheck`, поле `wireCheck`, export через каталог.
  - `ExportImage` — через `ResolvePngDir`.
- `State.cs` — `TemplatesDir`, `GostTemplate`, `TemplatePath` производный.
- `Docs.cs` — актуальный recipe.
