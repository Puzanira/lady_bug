# LadyBugHitTheRoad

Локальный кооперативный (1–2 игрока) endless-runner про божью коровку на
дороге; число полос выбирается в меню (1–7, по умолчанию 3).

Это **игра 1 из 7** в мега-проекте аркадного автомата — остальные 6 слотов
пока пустые заглушки (см. раздел 3.0 в `docs/technical-details.md`). Отсюда
раскладка ассетов по папке на игру: `Assets/LadyBug/Scripts/lady_bug/`,
`Assets/LadyBug/Sprites/lady_bug/` и т.д.

## Лицензия
Код (`Assets/LadyBug/Scripts`, `Assets/LadyBug/Editor`, `Assets/LadyBug/Shaders`, `ArduinoFirmware`) —
**MIT**. Арт, звук и документация — **CC BY-NC 4.0**. Сторонние материалы
(mixkit, OpenGameArt, шрифт ComicCAT) — под своими лицензиями.
Подробности: [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

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
  ИИ-агентов: подключение к проекту, разработка (`Tools → Rebuild Scene`),
  механики, архитектура кода.
- [docs/game-brief.md](docs/game-brief.md) — бриф/анкета по игре.
- [docs/hardware-wiring.md](docs/hardware-wiring.md) — разводка Arduino,
  прошивки, Serial-протокол, меню и отладка железа.

## Структура
- `UnityProject/` — чистый Unity-проект, открывать через Unity Hub именно эту
  папку. Весь игровой контент лежит под `Assets/LadyBug/` (рантайм-код в
  `Assets/LadyBug/Scripts/lady_bug/`, экран автомата в
  `Assets/LadyBug/Scripts/loader/`, генератор сцены в
  `Assets/LadyBug/Editor/SceneSetup.cs`).
- `ArduinoFirmware/` — прошивки CombinedBoard / GestureSensors / Joystick /
  SingleSensorTest.
- `RawAssets/` — сырые исходники арта (кадры из `.swf`, скачанные картинки) до
  конвертации в спрайты в `UnityProject/Assets/LadyBug/Sprites/lady_bug/`.
- `docs/` — вся документация проекта.

## Быстрый старт
Открыть `UnityProject/` в Unity Hub (Unity 6000.5.3f1), затем **Tools →
Rebuild Scene** — соберёт `Assets/LadyBug/Scenes/Main.unity` с нуля из кода (сцена
не хранится вручную). После изменений в `SceneSetup.cs` — повторить Rebuild
Scene; после изменений только в `Assets/LadyBug/Scripts/*.cs` — Unity подхватывает
сама, пересборка сцены не нужна.

**Цель забега:** **10 км** (`WinSequence.WinSegmentDistanceKm`) — столько же
добавляет каждое «продолжение» после финиша. Для отладки кат-сцены победы
ставится 1.
