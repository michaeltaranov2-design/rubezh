# Передача локальному агенту — этап 2 + карты

1. Репо https://github.com/michaeltaranov2-design/rubezh (после 946b38e).
2. Распаковать `rubezh-stage2-overlay.zip` поверх корня.
3. `dotnet run --project tests/core/Rubezh.Core.Tests.csproj -c Release`
4. `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj -c Release`
5. `python tools/verify_stage1.py --godot <godot.exe>` (регресс этапа 1).
6. Код ≠ 0 — не коммитить. Код 0 — коммит исходников, без GLB.
7. Этап 3 (сеть) не начинать без зелёных тестов этапа 2.
