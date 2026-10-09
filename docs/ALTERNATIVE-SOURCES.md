# Альтернативные источники
Проверено 2026-10-08 через браузер. Внешние страницы могут измениться.

## CS2
Независимый справочник Total CS (не Valve):
- https://totalcsgo.com/commands/svrethrowlastgrenade — повтор последней гранаты на сервере, требует sv_cheats.
- https://totalcsgo.com/commands/svshowimpacts — 0 выключено, 1 оба источника, 2 клиент, 3 сервер.
- https://totalcsgo.com/commands/mpmaxmoney — максимальный баланс.
- https://totalcsgo.com/commands/mpstartmoney — начальный баланс.

Дампы игры от независимого SteamDatabase:
- https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/DumpSource2/commands.txt — сверен sv_rethrow_last_grenade и noclip.
- https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/DumpSource2/convars.txt — сверены sv_showimpacts, sv_cheats, mp_maxmoney, mp_startmoney.
Это не гарантия поведения каждой будущей версии CS2. Valve Developer Community ранее потребовал антибот-проверку; она не обходилась. Копирование названий служебных команд не означает использования игровых ассетов.

## Godot C++
Первичный источник — README конкретного коммита авторов библиотеки:
https://github.com/godotengine/godot-cpp/blob/272e7f4a5fde342ea20983371fffafdccea07f20/README.md
README описывает независимую нумерацию 10.x, api_version и custom_api_file. Поиск только тега godot-4.7.2-stable был недостаточным: отсутствие такого тега само по себе не доказывает несовместимость.
Скрипт сборки того же коммита прочитан:
https://github.com/godotengine/godot-cpp/blob/272e7f4a5fde342ea20983371fffafdccea07f20/SConstruct
Исходная документация, доступная без сайта ReadTheDocs:
https://github.com/godotengine/godot-docs/blob/master/tutorials/scripting/cpp/gdextension_cpp_example.rst
Теги библиотеки: https://api.github.com/repos/godotengine/godot-cpp/tags?per_page=12 — найден godot-4.5-stable, SHA e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77. Это запасная пара с движком 4.5, не автоматическое переключение текущего проекта.

## Что создано самостоятельно
Весь каталог 15 модулей, диапазоны, права, лимиты, протокол ошибок и игровые отличия — оригинальные проектные решения. Они не выдаются за документацию Valve.
