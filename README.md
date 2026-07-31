# LadyBugHitTheRoad

Локальный кооперативный (1–2 игрока) endless-runner про божью коровку на
трёхполосной дороге.

## Требования
- Unity **6000.5.3f1**.
- Рендер: **URP (2D Renderer)** — пайплайн настроен в проекте
  (`Assets/LadyBug/Rendering/LadyBug_URP.asset` назначен в Graphics и во всех
  Quality-уровнях); Built-in больше не используется.

## Как запустить
1. Открыть `UnityProject/` в Unity Hub (Unity **6000.5.3f1**), именно эту
   папку, не корень репозитория.
2. Entry-сцена — `Assets/LadyBug/Scenes/Main.unity` (уже в Build Settings).
   Весь контент игры собран в одну папку `Assets/LadyBug/` (Scripts со своим
   asmdef `LadyBug.Runtime`, Scenes, Prefabs, Materials, Audio, Sprites,
   Resources, Editor, Rendering).
3. Play. При необходимости пересобрать сцену из кода — **Tools → Rebuild
   Scene** (генератор `Assets/LadyBug/Editor/SceneSetup.cs`).

## Аркадный автомат
Игра едет в общий проект автомата как пакет `com.aigamestudio.game-lady-bug`.
Границу интеграции описывает [ARCADE_INTEGRATION.md](ARCADE_INTEGRATION.md), а
проверяет `python3 contract_check.py` (без Unity-лицензии, гоняется в CI).

## Документация
- [docs/technical-details.md](docs/technical-details.md) — технические детали для
  ИИ-агентов: **как подключиться к проекту агентом** (MCP и запасной вариант
  через файловую систему), как устроена разработка (инструменты, ограничения,
  почему сцена собирается кодом через `Tools → Rebuild Scene`, а не руками),
  и как устроена сама игра (механики, архитектура, файл за файлом).
- [docs/game-brief.md](docs/game-brief.md) — бриф/анкета курса по игре.

## Структура
- `UnityProject/` — чистый Unity-проект, открывать через Unity Hub именно эту
  папку (скрипты в `UnityProject/Assets/LadyBug/Scripts`, генератор сцены в
  `UnityProject/Assets/LadyBug/Editor/SceneSetup.cs`).
- `RawAssets/` — сырые исходники арта (кадры из `.swf`, скачанные картинки) до
  конвертации в спрайты в `UnityProject/Assets/LadyBug/Sprites`.
- `docs/` — вся документация проекта.

## Быстрый старт
Открыть `UnityProject/` в Unity Hub (Unity 6000.5.3f1), затем **Tools →
Rebuild Scene** — соберёт `Assets/LadyBug/Scenes/Main.unity` с нуля из кода (сцена
не хранится вручную). После изменений в `SceneSetup.cs` — повторить Rebuild
Scene; после изменений только в `Assets/LadyBug/Scripts/*.cs` — Unity подхватывает
сама, пересборка сцены не нужна.
