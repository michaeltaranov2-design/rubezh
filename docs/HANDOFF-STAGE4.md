# Передача — этап 4 консоль и bhop

Репо https://github.com/michaeltaranov2-design/rubezh после `d211df7`.

1. Распаковать `rubezh-stage4-overlay.zip` поверх корня.
2. `dotnet run --project tests/core/Rubezh.Core.Tests.csproj -c Release`
3. `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj -c Release`
4. `dotnet run --project tests/stage3/Rubezh.Stage3.Tests.csproj -c Release`
5. `dotnet run --project tests/stage4/Rubezh.Stage4.Tests.csproj -c Release`
6. `python tools/verify_stage4.py`
7. Регресс Godot: `python tools/verify_stage1.py --godot "<godot.exe>"`
8. Код ≠ 0 — не коммитить, полный лог. Не ломать stage2 35 / stage3 39. Core может стать 44 из‑за air=2×bhop. Тест air-strafe: лимит в воздухе = 2× bhop.
9. Код 0 — коммит `stage4: match console modules, bhop and air-strafe limits`, без GLB. В коммит `reports/stage4-verify.json`.
10. Отчёт: коды выхода, json, ссылка на коммит, список файлов.
