# Этап 3 — сеть (ядро без Godot)

Сервер авторитетен, тик 64 Гц. Клиент шлёт только `InputCommand`. Скрытые противники не входят в payload снимка.

## Протокол RZH1
Magic `RZH1`, MTU 1200. Input 28 байт. Snapshot: заголовок 22 + 20 байт на видимого игрока (10 игроков = 222 байта).

## Сервер
`SimServer`: очередь ввода, latest-wins, дубли seq отбрасываются, MovementCore + hitscan, MatchState, персональный Snapshot.

## Клиент
`ClientPredict`: история 64, откат к ack, повтор неподтверждённого. `NetChannel` — эмулятор loss/dup/reorder для тестов.

## Консоль
3 активации модуля на аккаунт за матч, nonce идемпотентен, wallhack запрещён в Competitive. Не читы для чужих игр.

## Не в этом оверлее
Godot ENet peer, dedicated process, аккаунты HTTPS — следующий инкремент после зелёных тестов ядра.

Этап не принят, пока `dotnet run --project tests/stage3/Rubezh.Stage3.Tests.csproj -c Release` не вернёт 0.
