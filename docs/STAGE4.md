# Этап 4 — консоль в матче и лимиты bhop

Ядро без Godot. 15 модулей, квота 3 на аккаунт/модуль/матч, nonce, Competitive-запреты.

## Скорость
Без модуля: bhop 25 м/с, air-strafe ×2 = 50.
С модулем bhop: Competitive 10/20, Sandbox 15/30, Community custom 15–25 (air ×2).
Консольный `maxSpeed` только 15–25. Жёсткий потолок режима не поднимается параметром.

## Competitive нельзя
wallhack, rage, noclip, rethrow, maxmoney, startmoney.

Этап не принят, пока `dotnet run --project tests/stage4/Rubezh.Stage4.Tests.csproj -c Release` не вернёт 0.
