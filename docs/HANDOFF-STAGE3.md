# Передача — этап 3b ENet/аккаунты

Репо https://github.com/michaeltaranov2-design/rubezh после `3fcd22f`.

1. Распаковать `rubezh-stage3b-overlay.zip` поверх корня.
2. `dotnet run --project tests/core/Rubezh.Core.Tests.csproj -c Release`
3. `dotnet run --project tests/stage2/Rubezh.Stage2.Tests.csproj -c Release`
4. `dotnet run --project tests/stage3/Rubezh.Stage3.Tests.csproj -c Release`
5. `python tools/verify_stage3.py`
6. Регресс Godot: `python tools/verify_stage1.py --godot "<godot.exe>"`
7. Код ≠ 0 — не коммитить, полный лог. API ENet править минимально (`CreateServer`, `GetPacketPeer`, `PutPacket`).
8. Код 0 — коммит `stage3: dedicated loop, accounts http, godot enet host`, без GLB. В коммит `reports/stage3-verify.json`.
9. Отчёт: коды выхода, json, ссылка на коммит, список файлов.
