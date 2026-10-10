# Передача — этап 3 сеть

Репо https://github.com/michaeltaranov2-design/rubezh после `1f3380b`.

1. Распаковать `rubezh-stage3-overlay.zip` поверх корня.
2. `dotnet run --project tests/core/Rubezh.Core.Tests.csproj -c Release`
3. `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj -c Release`
4. `dotnet run --project tests/stage3/Rubezh.Stage3.Tests.csproj -c Release`
5. `python tools/verify_stage3.py`
6. Код ≠ 0 — не коммитить, полный лог.
7. Код 0 — коммит `stage3: net protocol, sim server 64hz, predict, console quotas`, без GLB.
8. Отчёт: коды выхода, reports/stage3-verify.json, ссылка на коммит, список файлов.
