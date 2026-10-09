# Кандидат нативной зависимости
Godot .NET 4.7.2 сохраняется. godot-cpp кандидат: commit 272e7f4a5fde342ea20983371fffafdccea07f20. Его README допускает custom_api_file из целевого движка. Это документированный путь сборки, НЕ результат теста ABI или загрузки.

Порядок локальной проверки:
1. Скачать точный Godot 4.7.2 .NET и проверить официальный SHA256 архива.
2. В пустом каталоге выполнить путь/к/godot --headless --dump-extension-api. Сохранить extension_api.json и его SHA256.
3. git clone https://github.com/godotengine/godot-cpp.git
4. В репозитории: git checkout --detach 272e7f4a5fde342ea20983371fffafdccea07f20
5. git submodule update --init --recursive
6. Установить совместимые C++ toolchain, Python и SCons; точные версии ещё не проверены, поэтому не объявлены закреплёнными.
7. Пример для Linux x64 из каталога godot-cpp: scons platform=linux arch=x86_64 target=template_debug custom_api_file=/полный/путь/extension_api.json
8. Собрать минимальное расширение по примеру Godot, проверить загрузку и C# → ClassDB → native вызов. Повторить для Windows x64. Нужны отдельные .dll/.so, выбранные через .gdextension.
9. Зафиксировать версии компилятора/SCons, API hash, флаги, логи debug/release и CI. Только после этого повысить candidate до verified.

Сборка одной godot-cpp библиотеки НЕ является готовым игровым GDExtension. C++ движение, хитскан, лаг-компенсация ещё отсутствуют. Нельзя вызывать SceneTree или физические API с произвольного сетевого потока. Этап 1 заблокирован до запуска исходного стартового проекта и проверки нативного стека.
