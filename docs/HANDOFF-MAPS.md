# Передача — карты Godot

Репо https://github.com/michaeltaranov2-design/rubezh после `3f42fbf`.

1. Распаковать `rubezh-maps-overlay.zip` поверх корня.
2. `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj -c Release`
3. `python tools/verify_stage1.py --godot "C:\Users\g3raa\AppData\Local\Programs\Godot\4.7.2-mono\Godot_v4.7.2-stable_mono_win64.exe"`
4. `python tools/verify_maps.py --godot "C:\Users\g3raa\AppData\Local\Programs\Godot\4.7.2-mono\Godot_v4.7.2-stable_mono_win64.exe"`
5. Код ≠ 0 — не коммитить, вернуть полный лог.
6. Код 0 — один коммит `maps: godot builder GI occluders LOD navmesh maps-test`, без GLB.
7. Отчёт: коды выхода, reports/maps-verify.json, ссылка на коммит, список файлов.
