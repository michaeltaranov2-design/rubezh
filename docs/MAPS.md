# Карты «Рубеж»

Три оригинальные greybox-карты, CC0-1.0, автор Рубеж.

| id | имя | класс | размер | бюджет треугольников | память |
|---|---|---|---|---|---|
| klin_greybox | Клин | compact | 40×32 м | 5000 | 16 МБ |
| uzel_greybox | Узел | medium | 64×48 м | 8000 | 24 МБ |
| horda_greybox | Хорда | large | 96×64 м | 14000 | 48 МБ |

На каждой: 5+5 спавна, сайты A/B, несколько маршрутов, проверка стен и связности графа.

## Освещение, culling, LOD
- Compatibility / OpenGL. SDFGI и VoxelGI не используются.
- DirectionalLight3D «Sun» — рабочий свет на слабом ПК (UHD 620, 720p).
- Узел LightmapGI (BakeQuality.Low, 1024, без denoiser). Запекание в редакторе: выбрать узел и Bake. В рантайме не печётся, чтобы не вешать headless.
- Меши GIMode=Static, UV2 генерируется через ArrayMesh.LightmapUnwrap и готова к LightmapGI.
- OccluderInstance3D + BoxOccluder3D на стенах, внешних стенах и ящиках.
- LOD: ящики VisibilityRangeEnd=40 м, стены 90 м. Пол без отсечения.

## Навмеш
NavigationRegion3D «Nav», агент r=0.4, h=1.8. BakeNavigationMesh при сборке. Боты ходят по графу Waypoints текущей карты (MapRuntime.Current).

## Пользовательские карты
`MapLoader` + `--user-map=path.glb`: лимит 8 МБ, магия GLB/glTF, запрет внешних URI. Каталог: `maps/catalog.json`.

## Запуск
`--map=klin_greybox` / `uzel_greybox` / `horda_greybox`. Автотест: `--maps-test` → маркер `MAPS_SELFTEST_OK` и 20 раундов BotNavSim на каждой карте.
