# Celebration FX — сырые ассеты (конфетти / салют)

Скачано автоматически из бесплатных источников с прямой загрузкой.
Папка для выбора и импорта в Unity (`WinSequence`, Particle System и т.д.).

## Что реально едет в игру

Из всей папки в проект попали **ровно два файла**, оба CC0, оба без
обязательной атрибуции (проверено побайтово — `cmp` совпадает):

| Исходник здесь | Копия в проекте |
|---|---|
| `opengameart/jellyfizh_Confetti.png` | `UnityProject/Assets/Sprites/lady_bug/Celebration/Confetti_spritesheet.png` |
| `opengameart/Firework_spritesheet.png` | `UnityProject/Assets/Sprites/lady_bug/Celebration/Firework_spritesheet.png` |

Разрезанные кадры (`Confetti/frame_00…58.png`, `Firework/frame_00…29.png`)
лежат в `UnityProject/Assets/Resources/Celebration/` — именно оттуда их грузит
`WinCelebrationFx.cs` через `Resources.LoadAll<Texture2D>`. Дубликаты этих же
89 кадров в `Assets/Sprites/lady_bug/Celebration/` удалены (2026-08-04) —
игра их не использовала.

## Скачано ✅

### OpenGameArt (`opengameart/`)

| Файл | Автор | Лицензия | Источник | Атрибуция | Описание |
|------|-------|----------|----------|-----------|----------|
| `jellyfizh_Confetti.png` | jellyfizh | **CC0** | https://opengameart.org/content/confetti-effect-spritesheet | не требуется | Анимированный spritesheet конфетти 4096×4096, **альфа 0–255**. **Едет в игру** |
| `Firework_spritesheet.png` | jellyfizh | **CC0** | https://opengameart.org/content/fireworks-effect-spritesheet | не требуется | Большой spritesheet салюта 1536×1280, **альфа**. **Едет в игру** |
| `davididev_confetti_spritesheet.png` | davididev | **CC-BY 3.0** | https://opengameart.org/content/confetti-particle | **обязательна**: «davididev or davididev.com» | Белые частицы конфетти 768×768, перекрашиваются в Unity. **В игру НЕ едет** — если когда-нибудь поедет, атрибуцию придётся добавить |
| `party_confetti_sprite10_strip10.png` | sketcherskt | **CC0** | https://opengameart.org/content/party-confetti-sprite-sheet-effect | не требуется («anyone can use my artwork for whatever») | 10 кадров, полоска 640×64, pixel confetti |
| `pixel_fireworks_4colors.zip` | myriad | **CC0 1.0** | https://opengameart.org/content/fireworks | не требуется | 4 цвета: red/blue/yellow/violet, кадры `firework_red0..7.png`. Скачан как `Fireworks.zip`, переименован здесь |

Распаковано: `pixel_fireworks_4colors_extracted/`

### Удалено 2026-08-04 — паки Kenney (`kenney/`, `github/`)

~28.4 МБ (200 файлов) вычищено: ни один байт из этих паков в проект не попал
(проверено семью способами: хеши, попиксельное сравнение, совпадения имён,
GUID из `.unitypackage`, грепы по всем `*.cs`). Обе распакованные копии были
побайтовым дубликатом своих же zip. Обе лицензии — CC0, credit optional,
поэтому удаление не создаёт никаких обязательств.

Восстанавливается одной командой, если понадобится:

```bash
# Kenney Particle Pack (CC0). Внимание: прямой URL kenney.nl/media/... сейчас
# отдаёт 404 — рабочее зеркало на OpenGameArt, проверено побайтово:
curl -L -o kenney_particlePack.zip \
  https://opengameart.org/sites/default/files/kenney_particlePack.zip

# Форк Calinou (тот же пак, пересобран для Godot, с корректной прямой альфой)
curl -L -o calinou_kenney-particle-pack.zip \
  https://github.com/Calinou/kenney-particle-pack/archive/refs/heads/master.zip
```

Страница-первоисточник Kenney: https://kenney.nl/assets/particle-pack

**Примечание про альфу:** у оригинального пака Kenney альфа везде 255 (частицы
на чёрном фоне) — в Unity нужен **Additive** / **Alpha Blend** материал. Форк
Calinou — как раз исправление этого: у него настоящая прямая альфа.

---

## Не удалось скачать автоматически ⚠️ (только itch.io, нужен браузер)

Itch.io отдаёт файлы только после «покупки» за $0 — curl/API не работает без сессии.

| Пак | Ссылка | Размер | Лицензия |
|-----|--------|--------|----------|
| **Stealthix Animated Fireworks** (оригинал 164 KB) | https://stealthix.itch.io/animated-fireworks | 164 KB | CC0 |
| **Kronbits 1000 Particles** | https://kronbits.itch.io/particle-pack | 92 MB | CC0 |
| **Mochi Lab Game FX Starter Vol.1** | https://mochilab-studio.itch.io/game-fx-starter-vol1 | 94 MB | Royalty-free |
| **CodeManu VFX Free Pack** | https://codemanu.itch.io/vfx-free-pack | 90 MB | Public domain |
| **Pixogen Pixel RPG VFX Lite** (есть fireworks 64×64) | https://pixogenassets.itch.io/pixel-art-rpg-vfx-lite | ~150 KB | Free |
| **pewas Pixel RPG VFX Pack** | https://pewas.itch.io/pixel-rpg-vfx-pack-free-animated-effects | 23 MB | Royalty-free |

Положить сюда: `RawAssets/CelebrationFX/itchio/` после ручной загрузки.

---

## Рекомендации для экрана победы

1. **Дождь конфетти:** `jellyfizh_Confetti.png` (уже используется).
2. **Вспышки салюта:** `Firework_spritesheet.png` (уже используется) или
   `pixel_fireworks_4colors_extracted/`.
3. **UI-эффект «milestone»:** после ручной загрузки — Mochi Lab Starter.

## Импорт в Unity

1. Скопировать выбранные PNG в `UnityProject/Assets/Sprites/lady_bug/` (кадры,
   которые грузятся по имени в рантайме — в `Assets/Resources/`).
2. Texture Type: **Sprite (2D and UI)** или **Default** для Particle System.
3. Spritesheet → **Sprite Editor** → Slice (Grid By Cell / Automatic).
4. Particle System → **Texture Sheet Animation** + материал с прозрачностью.

⚠️ Если добавляете сюда что-то новое — **сразу записывайте автора, лицензию,
URL и нужна ли атрибуция**. Именно из-за пропуска этого шага (`Firework` был
записан как «см. страницу») происхождение пришлось потом восстанавливать по
транскриптам.
