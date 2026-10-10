# Передача локальному агенту — этап 1

1. Рабочее дерево: https://github.com/michaeltaranov2-design/rubezh (коммит 5406380 или новее).
2. Распаковать оверлей `rubezh-stage1-overlay.zip` **поверх** корня репозитория, не удаляя generated/.
3. .NET SDK 8+, Godot 4.7.2 .NET (Compatibility).
4. `python tools/install_weapons.py generated/refresh-iter5`
5. `python tools/verify_stage1.py --godot <godot.exe> --profile`
6. Если код выхода не 0 — **не коммитить**. Чинить только упавшие шаги, прогнать снова.
7. Если 0 — коммит исходников + `reports/stage1-verify.json` (+ профиль). GLB не коммитить. Затем push.
8. Этап 2 не начинать без зелёной проверки.
