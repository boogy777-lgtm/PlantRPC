# wY3 — RPC-сервер EPLAN API (постоянный процесс)

**Дата:** 2026-10-28. **Режим:** READ-ONLY (EPLAN/Program Files не изменялись).
**Цель:** инструмент-хост, чтобы слать команды в EPLAN и выполнять их без
пересборки `.cs`.

## TL;DR
- Готов `EplanPsuSchematic.exe serve <port>` — EPLAN инициализируется один раз,
  далее JSON-строки по TCP `127.0.0.1:<port>` (по умолчанию 45455).
- Процесс переживает вызовы инструмента; состояние (project/page) сохраняется.
- Команды: `ping/state/stats/shutdown`, `project.create|open|close`,
  `page.create|use|clear`, `symbol.list|check`, `function.place`, `line.draw`,
  `generate.connections`, `export.pdf`, `export.image`, `function.points`.
- Проверено end-to-end: проект → лист `/1` → 8 функций → провода →
  `generate.connections` → 8 соединений → PDF + PNG.

## Реализация
- `IpcServer.cs` (JSON через `JavaScriptSerializer`, ссылка `System.Web.Extensions`).
- Диспетчер команд + `LockingStep` на каждую запись (`WithLock`).
- Символы размещаются из **проектной** библиотеки (`new SymbolLibrary(_project, lib)`).
- `export.image` → `Export.GraphicPage(..., Format.PNG, ...)`.

## Артефакты
- `bin\Release\EplanPsuSchematic.exe` (режимы `list`, `draw`, `serve`, `apidump`).
- `bin\Release\out\PSU_230V_24V.elk` — проект.
- `bin\Release\out\PSU_v3.png\_1.png` — схема (PNG).
- `bin\Release\out\PSU_230V_24V.pdf` — PDF.

## Ограничения/заметки
- GUI EPLAN и RPC — разные процессы; один проект нельзя держать в обоих (`*.lck`).
  Перед работой в GUI: `project.close` + `shutdown`.
- `Function.ConnectionPointPositions` возвращает нули → провода позиционируются
  координатами (`line.draw`), доводка — через RPC без пересборки.
- Порт занят, пока сервер жив; пересборка требует `shutdown` (файл `.exe` залочен).

## Воспроизведение
```powershell
cd C:\Users\HALLIBURTON\Desktop\EPLAN_RESEARCH\artifacts\wY_psu_api
dotnet build -c Release
Start-Process .\bin\Release\EplanPsuSchematic.exe -ArgumentList 'serve','45455' -WindowStyle Hidden
# далее — JSON-команды по TCP (см. README.md, раздел «RPC-сервер»)
```
