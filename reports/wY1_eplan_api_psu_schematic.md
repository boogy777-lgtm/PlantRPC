# wY1 — EPLAN API: создание листа и схемы БП 230 В → 24 В

**Дата:** 2026-10-27. **Режим:** READ-ONLY (Program Files не изменялись).
**Цель:** найти рабочий способ использовать EPLAN .NET API, создать новый лист и
начертить на нём электрическую схему блока питания 230 В → 24 В.
**Версии:** EPLAN Electric P8 `2026.0.3`; API `Eplan.EplApi.*u.dll` из
`C:\Program Files\EPLAN\Platform\2026.0.3\Bin`; сборка `net481`, dotnet SDK `8.0.423`.

## TL;DR / Headline

1. **Способ найден и реализован:** консольный инструмент `EplanPsuSchematic`
   (`artifacts\wY_psu_api`) собирает проект, создаёт лист `Circuit`, размещает
   символы БП и вызывает `Generate().Connections()`. **Компилируется под реальный
   API 2026.0.3 с 0 ошибок** — значит вызовы API корректны.
2. **Главный вывод / поправка к предпосылке:** API на этом стенде **сейчас не
   запускается** — падает инициализация подсистемы лицензий
   (`XCreateLicenseAction`), поэтому ни GUI, ни offline-API не стартуют.
   Это НЕ проблема кода/API, а состояние лицензии.
3. `EplApplication.EplanBinFolder` должен указывать на каталог **варианта**
   `…\Electric P8\2026.0.3\Bin`, а не на `…\Platform\2026.0.3\Bin` (иначе
   `ArgumentException: The Variant … is not valid`).
4. API-сборки EPLAN собраны под **.NET Framework 4.8.1** — проект нужно целиться
   в `net481` (net48 → `CS0246`, т.к. ссылки не резолвятся).
5. Имена символов и геометрия — мастер-данные, их надо уточнить через режим
   `list` на лицензированном стенде.

## Основные разделы

### 1. Инструмент и проверка компиляцией
Каталог: `artifacts\wY_psu_api\`
- `EplanPsuSchematic.csproj` — `net481`, `PackageReference Microsoft.NETFramework.ReferenceAssemblies.net481 1.0.3`,
  ссылки на `Eplan.EplApi.{Systemu,Baseu,DataModelu,HEServicesu,MasterDatau}.dll`.
- `Program.cs` — режимы `list` (перечисление символов) и `draw` (построение схемы).
- `bin\Release\EplanPsuSchematic.exe` — собран, `Build succeeded. 0 Warning(s) 0 Error(s)`.

Использованные и компиляционно подтверждённые элементы API (2026.0.3):
`EplApplication.Init(string,bool,bool)`, `EplApplication.EplanBinFolder`,
`ProjectManager.CreateProject/OpenProject/LockProjectByDefault`,
`Page.Create(Project,DocumentType,PagePropertyList)`, `PagePropertyList.PAGE_NAME`,
`DocumentTypeManager.DocumentType.Circuit`, `SymbolLibrary.Initialize(string)`,
`Symbol(SymbolLibrary,string)`, `SymbolVariant.Initialize(Symbol,int)`,
`Function.Create(Page,SymbolVariant)`, `Function.Name/VisibleName/Location/ManualPlacementType`,
`Function.Properties.FUNC_CONNECTIONDESIGNATION[int]`, `Generate.Connections(Project)`,
`Project.Close()`, `PointD(double,double)`.

### 2. Схема БП 230 В → 24 В (что строится)
- `-X1` — входные клеммы L / N / PE (230 В AC).
- `-F1` — предохранитель в линии L (L0 → L1).
- `-G1` — блок питания/трансформатор (L1 / N / PE → +24 В / 0 В).
- `-X2` — выходные клеммы +24 В / 0 В / PE.
- `new Generate().Connections(project)` — генерация соединений.
Координаты (`PointD`) и имена символов вынесены в `PsuSchematicBuilder` для быстрой подстройки.

### 3. Блокер рантайма: лицензия (доказательства)
- Запуск `EplanPsuSchematic.exe list IEC_symbol` → `EplApplication.Init` бросает
  `Eplan.EplApi.Base.BaseException`: «Не удалось инициализировать систему лицензий Eplan…».
- `C:\Users\Public\EPLAN\Electric P8\PerfLogSession.xml` (сессия 17:19:05–17:19:21):
  `ApplicationModifier="Basic"`, затем `XCreateLicenseAction` длительностью 15488 мс,
  затем `SystemEnd` — действие лицензии завершилось неуспешно.
- `EplLog.eldb` — устаревший (сессии до 2026-09-26), SHA-256 `8a3f88e2…`; в нём те же
  сообщения «диалог Активировать лицензию закрыт без выбора».
- Текущие хэши: `EplanCommonCore.dll` `39533EF1…` (140800 B), `AFu.dll` `ACCD7AB0…`,
  `EplanCommon.lit` `1E7A63EA…` — **не совпадают** с известным рабочим набором из
  `HANDOFF.md` (W-N1/W-S2), т.е. состояние лицензии разошлось с задокументированным.
- **`C:\Reverse` отсутствует** — исходники/киты прошлых волн (`lic_tool.py`,
  `kits\demo`) недоступны, восстановление «в один шаг» невозможно.

### 4. Прочие факты окружения
- Мастер-данные символов на месте: `…\Platform Data\2026.0.3\Symbols\EPLAN\IEC_symbol.sdb`
  — это каталог с `Variant.eod` (5 516 245 B) / `Variant.eox` (278 528 B).
- Шаблон проекта: `…\Electric P8 Data\2026.0.3\Templates\EPLAN\IEC_bas001.zw9`.
- .NET Framework runtime 4.8.1 установлен (release 533325); reference assemblies 4.8.1
  подтянуты NuGet-пакетом.

## Open items

1. **Восстановить лицензию API.** Offline-API требует рабочей подсистемы лицензий;
   по HANDOFF §3 для API нужен также опцион **725 / 0x02D5 APIExtension**. Прежний
   механизм жил в `C:\Reverse` (утрачен) — нужно пересобрать/переустановить рабочий
   shim+AFu+`.lit` (или перевыпустить `.lic`).
2. После лицензии — выполнить `list IEC_symbol` и зафиксировать реальные имена
   символов; подставить их в `PsuSchematicBuilder.Candidates`.
3. Подстроить координаты `PointD` под фактические точки подключения символов,
   чтобы автотрассировка дала непрерывные линии.
4. Проверить, что `PagePropertyList.PAGE_NAME="1"` даёт ожидаемое имя листа
   `/1` в выбранном шаблоне структуры; при необходимости — `PAGE_FULLNAME`.

## Воспроизведение

```powershell
cd C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_psu_api
dotnet build -c Release
# после восстановления лицензии:
.\bin\Release\EplanPsuSchematic.exe list IEC_symbol
.\bin\Release\EplanPsuSchematic.exe draw
```

Логи/артефакты:
- `C:\Users\Public\EPLAN\Electric P8\PerfLogSession.xml`
- `C:\Users\Public\EPLAN\Electric P8\EplLog.eldb`
- `artifacts\wY_psu_api\out\` — проект `PSU_230V_24V.elk` и `symbols_IEC_symbol.txt`
  (появятся после успешного запуска).

## Update 2026-10-27 17:48 — лицензия «починилась» и снова сломалась (регрессия)

- Пока стоял ECC-shim `39533EF1…` (140800 B), offline-API **работал**: `Init` успешен,
  `list IEC_symbol` перечислил **908** символов (реальные имена: `X`=30 клемма, `F1`=50
  предохранитель, `V`=55, `GL`=185 выпрямитель, `GNT`=198).
- В **17:45:11** файл `C:\Program Files\EPLAN\Common\EplanCommonCore.dll` заменён на
  `2523C9B8…` (142336 B). После этого `Init` падает:
  `BaseException: Не удалось инициализировать систему лицензий`; PerfLog 17:47:34 →
  `XCreateLicenseAction` 32546 мс → `SystemEnd` (без успеха).
- `AFu.dll` не менялся: `ACCD7AB0…` (2026-07-10). Похоже на **несовместимость нового
  ECC-shim со старым AFu-биндером**.
- Вывод: чтобы вернуть работу API — откатить `EplanCommonCore.dll` к `39533EF1…`
  (140800 B) либо поставить AFu, соответствующий новому ECC.

## Update 2026-10-28 — лицензия работает, схема нарисована, RPC готов

- После восстановления лицензии offline-API стартует; `list IEC_symbol` → **908** символов.
- Реальные символы (проверено через проектную библиотеку): `X`(2), `F1`(2), `GBOX32`(5).
- Поднят **RPC-сервер** `EplanPsuSchematic.exe serve 45455` (JSON-over-TCP, процесс
  живёт между вызовами, пересборка не нужна).
- Через RPC: создан проект `out\PSU_230V_24V.elk`, лист `/1`, размещено **8 функций**,
  нарисованы провода, `generate.connections` → **8 соединений**; экспорт **PDF + PNG**.
- Визуальная проверка PNG (`out\PSU_v3.png\_1.png`): L/N/PE → `F1` → блок `G1` → +24 В / 0 В / PE.
- Ограничение: `Function.ConnectionPointPositions` отдаёт нули → геометрию проводов
  подбираем координатами (можно довести через RPC без пересборки).
