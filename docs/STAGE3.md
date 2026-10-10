# Этап 3 — сеть

Сервер авторитетен, тик 64 Гц. Клиент шлёт только `InputCommand`. Скрытые противники не входят в payload.

## Ядро
RZH1, MTU 1200. `SimServer`, `ClientPredict`, `ConsoleState` (квота 3, nonce, Competitive без wallhack).

## Dedicated и аккаунты
`DedicatedLoop`: login → join-слот, отказ подмены slot, accumulator 64 Гц.
`AccountServer`: in-memory + HTTP `/v1/login` `/v1/join` `/v1/catalog` на 127.0.0.1. Join всегда `"admin":false`. Lobby-токен не даёт игрового admin.

## Godot ENet
`NetHost`: `ENetMultiplayerPeer.CreateServer/CreateClient`, пакеты RZH1. `--dedicated` слушает UDP 24567. `--connect=host[:port]` клиент.
Самотест: `--dedicated --net-test` → `STAGE3_NET_OK`.

Этап не принят, пока `dotnet run --project tests/stage3/Rubezh.Stage3.Tests.csproj -c Release` не вернёт 0.
