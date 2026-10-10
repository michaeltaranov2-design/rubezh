# Этап 2 — раунды, экономика, гранаты и карты

Ядро без Godot. Этап **не принят**, пока `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj` не вернёт 0.

## Раунды
16 раундов, freeze 15 с, live 120 с, plant 3 с (не позже 85 с), бомба 35 с, дефуз 7 с, смена сторон после 8. Старт 800, потолок 16000. Без овертайма.

## Карты
Клин 40×32 compact, Узел 64×48 medium, Хорда 96×64 large. Автор Рубеж, CC0-1.0. Две точки закладки, несколько маршрутов, проверка стен и связности графа.

## Загрузчик
`MapLoader`: лимит 8 МБ, магия GLB/glTF, запрет внешних URI и data:. extras.rubezhMapId.

## Боты
`BotNavSim.Run(map, 20)` — 20 раундов по графу без падений и stall>40 шагов.

## Освещение / LOD / culling
Greybox: runtime DirectionalLight, без baked GI в этом оверлее. LOD оружия уже 3 файла. Occlusion — Godot OccluderInstance3D на локальном импорте (этап карт в редакторе). Бюджеты в MapDef.TriangleBudget / MemoryBudgetKb.
