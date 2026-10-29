# wY2 — Полная карта API EPLAN для LLM (100% публичной поверхности)

**Дата:** 2026-10-27. **Режим:** READ-ONLY (`Eplan.EplApi.*u.dll` не изменялись).
**Цель:** дать агенту-LLM файл-карту всего EPLAN API и покрыть 100% публичной поверхности.
**Версии:** EPLAN `2026.0.3`; сборки `Eplan.EplApi.*u.dll` из
`C:\Program Files\EPLAN\Platform\2026.0.3\Bin`; инструмент `net481`, SDK `8.0.423`.

## TL;DR / Headline

1. **100% публичного API покрыто механически** — рефлексией по 14 сборкам:
   **7388 публичных типов, 43 899 публичных членов**.
2. Источник истины — сами DLL (не пересказ), воспроизводимо: `EplanPsuSchematic.exe apidump`.
3. Артефакты для LLM: `EPLAN_API_FOR_LLM.md` (гайд+рецепты), `EPLAN_API_INDEX.md`
   (навигация), `ns/*.md` (47 файлов с сигнатурами), `EPLAN_API_SURFACE.json`
   (машиночитаемо), `EPLAN_API_ALL.md` (~3.6 МБ, единый файл).
4. Дополнение: в `C:\EPLAN_Reverse\tools\fulldump` лежит декомпиляция managed/native —
   для внутренних (private) и native-деталей (DISAM/AFu/ECC), которые рефлексия не видит.
5. Публичный API — это **фасад**; ядро EPLAN (DISAM, лицензия, свойства) — native.

## Основные разделы

### 1. Как получено (воспроизводимо)
- `artifacts\wY_psu_api\ApiDump.cs` — `Assembly.ReflectionOnlyLoadFrom` по
  `Eplan.EplApi*.dll`, резолвер зависимостей (Bin + GAC), `GetTypes()`,
  публичные члены `DeclaredOnly`.
- Запуск: `.\bin\Release\EplanPsuSchematic.exe apidump` — **без Init, без лицензии**.
- Выход: `…\bin\Release\out\api\` → скопировано в `artifacts\wY_api_surface\`.

### 2. Состав дампа
- 47 namespace-файлов; ключевые: `Eplan.EplApi.DataModel` (339 типов),
  `HEServices` (108), `MasterData` (127), `ApplicationFramework` (37),
  `Base` (40), `DataModel.Graphics` (87), `DataModel.E3D` (80), `DataModel` (339).
- Единый файл `EPLAN_API_ALL.md` = индекс + все namespace (~3.68 МБ).
- JSON `EPLAN_API_SURFACE.json` (~3.84 МБ): `namespaces → [{name,kind,base,members[]}]`.

### 3. Курируемый гайд `EPLAN_API_FOR_LLM.md`
Содержит: два режима (offline/Remoting), карту namespace с ролями, объектную модель,
проверенные рецепты (init → project → page → symbol/function → connections → PDF),
правила/подводные камни (net481, `EplanBinFolder`=вариант, мм, `[STAThread]`,
`LockingStep`, `BaseException`), и как искать типы/члены.

### 4. Ограничения покрытия
- Отражена **публичная** поверхность; `internal`/`private` и native-ядро не входят
  (см. `fulldump`).
- Пустые/degenerate типы компилятора могут давать `<err>` при индексации — учтено.
- Имена символов/изделий — мастер-данные (не API), перечисляются рантайм-режимом `list`.

## Open items
1. При изменении версии EPLAN перегенерировать: `apidump` → повторно скопировать.
2. Для native-деталей — сверить с `C:\EPLAN_Reverse\tools\fulldump` (не дублировать).
3. Подключить дамп как постоянный контекст агента (индекс + при необходимости `ns/*`).

## Воспроизведение
```powershell
cd C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_psu_api
dotnet build -c Release
.\bin\Release\EplanPsuSchematic.exe apidump
# результат:
#   bin\Release\out\api\EPLAN_API_INDEX.md, EPLAN_API_SURFACE.json, ns\*.md
# копия для проекта:
#   artifacts\wY_api_surface\ (EPLAN_API_FOR_LLM.md, EPLAN_API_ALL.md, ...)
```
