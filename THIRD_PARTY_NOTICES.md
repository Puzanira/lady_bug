# Сторонние материалы

Перечень всего, что в этом репозитории **не** принадлежит его автору и на что
не распространяются условия из [`LICENSE`](LICENSE). Каждый пункт — со своей
лицензией и первоисточником.

Составлено 2026-08-04 по результатам аудита: часть источников пришлось
восстанавливать (по транскриптам разработки и сверкой файлов побайтово с
библиотеками), потому что при добавлении их не записали.

⚠️ **При добавлении любого нового стороннего файла сразу дописывайте сюда
строку**: автор, лицензия, URL, нужна ли атрибуция. Именно пропуск этого шага
и создал всю работу выше.

---

## Звук — mixkit.co

**13 из 14 файлов** в `UnityProject/Assets/Audio/lady_bug/` скачаны с
[mixkit.co](https://mixkit.co) под [Mixkit Free License](https://mixkit.co/license/):
свободное использование, коммерческое и личное, **атрибуция не обязательна**.

| Файл | Mixkit id | Название | Как установлено |
|---|---|---|---|
| `Buzz.wav` | 1926 | Bee buzz | из доков |
| `PickupPositive.mp3` | 2069 | — | сверено, MD5 совпал |
| `BadDog.mp3` | 1 | — | из доков |
| `BadCat.mp3` | 93 | — | сверено, MD5 совпал |
| `HitGeneric.mp3` | 757 | — | сверено, MD5 совпал |
| `TrickApplause.mp3` | 482 | — | сверено, MD5 совпал |
| `RunFeet.mp3` | 37 | Cartoon insect running fast | сверено, MD5 совпал |
| `EngineHum.mp3` | 2721 | Motorcycle engine working | сверено, MD5 совпал |
| `BadCrow.mp3` | **316** | Crow shor crowing | **восстановлено** сверкой MD5 |
| `SnakeHiss.mp3` | **1964** | Monster hiss | **восстановлено** сверкой MD5 |
| `StartScreenMusic.mp3` | 506 | Little Bells | из доков |
| `MenuMusic_PopTrack03.mp3` | 729 | — | из доков |
| `MenuMusic_BanjoMan.mp3` | 822 | — | из доков |

Прямые ссылки: `https://assets.mixkit.co/active_storage/sfx/<id>/<id>-preview.mp3`

14-й файл — `GearShift.wav`, он **не с mixkit**, см. ниже.

### ✅ `GearShift.wav` — не сторонний файл, синтезирован с нуля

14-й аудиофайл — **не с mixkit и вообще ниоткуда не скачан**. Он сгенерирован
процедурно генераторами ffmpeg lavfi (`anoisesrc` + `sine`, без единого
входного файла) 2026-07-20 в ходе разработки: два band-pass щелчка белым шумом
1.8–6 кГц со сдвигом 85 мс, тон 95 Гц и 140 Гц («лязг»), затем
`acrossfade` с хвостом из 110/165 Гц с `vibrato` и brown-noise «рёва» под 900 Гц.

**Лицензия — общая лицензия репозитория ([`LICENSE`](LICENSE)), атрибуция не
требуется.** Лицензия самого ffmpeg (LGPL-2.1) распространяется на программу,
а не на сгенерированные ею файлы; генераторы `anoisesrc`/`sine` синтезируют
отсчёты алгоритмически и не содержат встроенных сэмплов; бинарники ffmpeg в
репозиторий не входят.

Числившийся в доках mixkit id 2730 «Motorcycle changing gears» относился к
**удалённому предшественнику `GearShift.mp3`** — документация просто
устарела, а не была неверной изначально.

<details>
<summary>Как это установлено (метод корреляции здесь принципиально не работает)</summary>

Кросс-корреляция по волновой форме дала около 0.1 против всех 457 звуков
mixkit, 183 freesound, 97 pixabay и всех 11 звуков, извлечённых из
`LadybugAdventures.swf`. Причина не в том, что источник не нашли, а в том, что
~80% энергии файла — одна конкретная реализация несеянного генератора шума:
**два запуска одного и того же рецепта коррелируют между собой всего на
0.16–0.29**, так что порог «настоящее совпадение > 0.8» здесь недостижим в
принципе.

Идентификация подтверждена иначе — вычитанием детерминированного слоя.
`amix ... normalize=0` — обычное сложение, поэтому файл это «синусы + шум».
Если отрендерить только синусы и вычесть их из файла, линия 95 Гц гасится на
**66.5 дБ** (у свежих прогонов рецепта 64–75 дБ, у контроля с частотой
97 Гц — 2.0 дБ), фаза сходится до **−0.01°**, амплитуда до **1.000**. Так
не может совпасть ни одна запись.

Повторный прогон записанного рецепта воспроизводит промежуточный файл на
15954 байта и итоговый на 31830 байт, длительность 0.360000 с и **первые
72 байта заголовка байт в байт** (`cmp -n 72` → 0); первое расхождение — на
79-м байте, то есть на первом же отсчёте PCM.

Рецепт: `~/.claude/projects/-Users-antonmikhalev-repos-Y-GameLab/d0583589-7442-487e-8ee0-239d84560b68.jsonl`,
строки 9742 / 9818 / 9824 / 9830.
</details>

### Удалённый `GearShift_test.wav` — для полноты

Тоже расшифрован: первые 0.5 с mixkit id **2856** «Gear lock sound» плюс
наложенный с задержкой 90 мс mixkit id **1131** «mech_click» на громкости 0.7.
Файл удалён 2026-08-04, кодом не использовался.

---

## Спрайты — OpenGameArt

В `RawAssets/CelebrationFX/opengameart/`. Подробности и рекомендации по
импорту — в [`RawAssets/CelebrationFX/README.md`](RawAssets/CelebrationFX/README.md).

### Едут в игру

| Файл в проекте | Автор | Лицензия | Источник |
|---|---|---|---|
| `Assets/Sprites/lady_bug/Celebration/Confetti_spritesheet.png` и разрезанные кадры в `Assets/Resources/Celebration/Confetti/` | **jellyfizh** | **CC0** — атрибуция не требуется | https://opengameart.org/content/confetti-effect-spritesheet |
| `Assets/Sprites/lady_bug/Celebration/Firework_spritesheet.png` и кадры в `Assets/Resources/Celebration/Firework/` | **jellyfizh** | **CC0** — атрибуция не требуется | https://opengameart.org/content/fireworks-effect-spritesheet |

Совпадение с исходниками проверено побайтово (`cmp`).

### Лежат в `RawAssets/`, в игру не едут

| Файл | Автор | Лицензия | Источник |
|---|---|---|---|
| `davididev_confetti_spritesheet.png` | **davididev** | **CC-BY 3.0 — атрибуция ОБЯЗАТЕЛЬНА**: «davididev or davididev.com» | https://opengameart.org/content/confetti-particle |
| `party_confetti_sprite10_strip10.png` | **sketcherskt** | **CC0** («anyone can use my artwork for whatever») | https://opengameart.org/content/party-confetti-sprite-sheet-effect |
| `pixel_fireworks_4colors.zip` | **myriad** | **CC0 1.0** | https://opengameart.org/content/fireworks |

⚠️ Файл davididev — единственный здесь с обязательной атрибуцией. Проверено по
хешам: в `UnityProject/Assets` его копий **нет**, поэтому сейчас обязательств
не возникает. **Если он когда-нибудь попадёт в игру — атрибуцию надо будет
добавить.**

### Удалено 2026-08-04

Kenney Particle Pack (CC0, credit optional) и его форк
[Calinou](https://github.com/Calinou/kenney-particle-pack) лежали в
`RawAssets/CelebrationFX/kenney/` и `github/` — 28.4 МБ, ни один байт в проект
не попал. Удалены; команды восстановления — в
[`RawAssets/CelebrationFX/README.md`](RawAssets/CelebrationFX/README.md).

---

## Шрифт — ComicCAT

`UnityProject/Assets/Resources/lady_bug/Fonts/ComicCAT.otf` — шрифт всего UI
игры (загружается по имени: `Resources.Load<Font>("lady_bug/Fonts/ComicCAT")`).

- **Автор:** Виталий Лазаренко (Vitaly Lazarenko), Нур-Султан
- **Заявление автора**, [Behance gallery 119157709](https://www.behance.net/gallery/119157709/Comic-CAT-Free-Font-Cyrillic-and-Latin),
  дословно: **«COMIC CAT - free for commercial and pesonal use»** [sic].
  Поддержать автора — добровольный донат, указан там же.
- **Скачан с:** https://fonts-online.ru/fonts/comic-cat — «Можно использовать
  в коммерческой и не коммерческой деятельности»
- **Встраивание:** `OS/2 fsType = 0` (Installable Embedding, без ограничений)

Строка `Vitaly Lazarenko© . <2019>. All Rights Reserved` внутри самого файла —
дефолтная болванка редактора FontCreator; она противоречит публичному
заявлению автора и не отражает его намерений.

⚠️ Оговорка: разрешение касается **использования**. Распространение самого
файла `.otf` (re-hosting) автор нигде явно не оговаривал — ни разрешил, ни
запретил. Практически он сам раздаёт шрифт бесплатно и отдаёт агрегаторам.

---

## `RawAssets/swf/LadybugAdventures.swf`

Это **собственная более ранняя Flash-игра автора этого репозитория** —
источник стиля дороги, препятствий и части спрайтов. Сама игра сторонней не
является.

Но **внутри бинарника зашиты чужие материалы**, и они остаются под своими
условиями:

| Что | Условия |
|---|---|
| Шрифт Ray Larabie / Typodermic Fonts (строка «(c) 1996-2010 Ray Larabie … See attached license agreement») | EULA к файлу не приложен; см. https://typodermicfonts.com |
| Музыка **Kevin MacLeod** («At the shore», «Beach Party», incompetech.com) | **CC-BY** — при распространении требуется атрибуция |
| Движок **FlashPunk** | MIT, https://github.com/useflashpunk/FlashPunk |

Кроме того, в constant pool присутствуют маркеры `flashgamelicense.com` и
`flashgamm.com` — площадок лицензирования флеш-игр. Если игра в своё время
продавалась спонсору по эксклюзивной лицензии, у спонсора могли остаться
права, ограничивающие публикацию исходника. Это может проверить только автор.

**Спрайтов, перенесённых из этой игры в новый проект, всё вышесказанное не
касается** — там растровая графика самого автора, не шрифт и не музыка.

---

## Сгенерированные изображения (ИИ)

Часть спрайтов (`Assets/Sprites/lady_bug/`, `Assets/Sprites/loader/`,
`RawAssets/panel/generated/`) сгенерирована моделями `gpt-image-1-mini`,
`gpt-image-1` и `gemini-2.5-flash-image`.

34 PNG несут вшитые подписанные манифесты **C2PA**, удостоверяющие машинную
генерацию и называющие поставщика (OpenAI OpCo LLC / Google LLC). У четырёх
концептов от Google дополнительно есть **SynthID** — водяной знак в самих
пикселях, он не убирается очисткой метаданных.

⚠️ **Правовая оговорка:** в ряде юрисдикций (в частности, в США) у чисто
машинно-сгенерированных изображений автор-человек не признаётся, а значит
авторского права на них может не возникать вовсе — и тогда пункт 2 в
[`LICENSE`](LICENSE) к ним просто неприменим. Условия использования самих
моделей определяются договором с их поставщиком, а не этим репозиторием.

---

## Unity

Проект собирается на Unity 6000.0.78f1 и использует пакет `com.unity.ugui`.
Сам движок и его пакеты в репозиторий не входят (`UnityProject/Library/`
исключён `.gitignore`) и распространяются по
[Unity Terms of Service](https://unity.com/legal/terms-of-service).
