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
| `WinApplause.wav` | **513** | Applause ambience loop | скачано под эту задачу |

Прямые ссылки: `https://assets.mixkit.co/active_storage/sfx/<id>/<id>-preview.mp3`

14-й файл — `GearShift.wav`, он **не с mixkit**, см. ниже.

### Звуки заполнения экранов загрузчика — `Assets/Audio/loader/`

Тоже mixkit, та же лицензия. По одному на игровой слот автомата (какой слот
что — см. `SceneSetup.GameIntroThemes`):

| Файл | Mixkit id | Название | Слот |
|---|---|---|---|
| `IntroClock.wav` | 1063 | Fast wall clock ticking | 2 — Викторина про жизнь |
| `IntroIndianFlute.wav` | 2312 | Possitive indian flute [sic] | 3 — Медитация в спешке |
| `IntroStones.wav` | 388 | Falling bricks | 4 — Бесконечный Сизиф |
| `IntroFactoryHum.wav` | **828** | Loud construction machine | 5 — Завод |
| `IntroSpaceDrone.wav` | 2510 | Cinematic suspense ambience | 6 — Таблетка в космосе |

Слот 1 (БК) использует `Buzz.wav`, слот 7 (Игра про кота) — уже имеющийся
`BadCat.mp3`; новых файлов под них не заводилось.

**Все пять переработаны в бесшовные петли** (исходники — превью с mixkit):
срезана тишина по краям, хвост подмешан в начало кроссфейдом, результат
сохранён в **WAV, а не MP3** — MP3 добавляет собственный отступ от кодека,
слышимый как заминка на каждом обороте. У `IntroClock` длина подобрана кратно
интервалу тиков (0.294 с), чтобы ритм не сбивался на стыке. `IntroSpaceDrone`
вырезан из плотного участка 12–20 с и нормализован — исходник тихий.

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

## Музыка экрана лаунчера — `Assets/Audio/loader/LoaderMusic_*.mp3`

Плейлист attract-режима: 10 треков, все стилизованы под 8-битный звук старых
приставок, разных музыкальных жанров. Крутятся случайно и непрерывно, пока не
запущена игра (`LoaderScreenController` + `MenuMusicRotator`).

⚠️ **Ни один из них НЕ с mixkit, и это не случайность.** У mixkit две разные
лицензии, и они расходятся ровно в нашем случае: Sound Effects Free License
видеоигры разрешает явно, а **Stock Music Free License держит Video Games в
списке Not Allowed** вместе с CD/DVD и ТВ-радио (проверено 2026-09-22 на
`mixkit.co/license/modal/musicFree` и `/sfxFree`). Поэтому звуковые эффекты
выше — с mixkit, а музыка здесь — нет. Если когда-нибудь будете добавлять
трек в этот плейлист, mixkit не подходит в принципе.

| Файл | Трек | Автор | Лицензия | Атрибуция |
|---|---|---|---|---|
| `LoaderMusic_01_Bebop.mp3` | Bebop (Chiptune) — джаз/бибоп | Pro Sensory (Alex McCulloch) | CC0 1.0 | не требуется |
| `LoaderMusic_02_Polka.mp3` | Spazzmatica Polka — полька | Kevin MacLeod | CC BY 4.0 | **обязательна** |
| `LoaderMusic_03_Waltz.mp3` | 8-bit Quirky Waltz — вальс 3/4 | Ted Kerr (Wolfgang_) | CC BY 4.0 | **обязательна** |
| `LoaderMusic_04_Funk.mp3` | NES chiptune «Slam-Funk» — фанк | Haley Halcyon | CC BY 4.0 | **обязательна** |
| `LoaderMusic_05_Oriental.mp3` | Oriental Music C64 Style — восточный мотив на SID | skrjablin | CC BY 3.0 **или** CC0 1.0 (двойная, на выбор) | по выбранной лицензии |
| `LoaderMusic_06_Shanty.mp3` | Salty Ditty — пиратская шанти | Kevin MacLeod | CC BY 4.0 | **обязательна** |
| `LoaderMusic_07_Balalaika.mp3` | Padanaya Blokov — балалайка, «русская» тема | Kevin MacLeod | CC BY 4.0 | **обязательна** |
| `LoaderMusic_08_March.mp3` | Desert March — марш | tcarisland | CC BY 4.0 | **обязательна** |
| `LoaderMusic_09_Medieval.mp3` | Medieval: The Old Tower Inn — средневековый фолк | RandomMind | CC0 1.0 | не требуется |
| `LoaderMusic_10_Ambient.mp3` | Prairie Nights (JRPG Pack #1) — спокойный ночной эмбиент | Juhani Junkala (SubspaceAudio) | CC0 1.0 | не требуется |

Страницы источников: [Bebop](https://opengameart.org/content/bebop-chiptune) ·
[Spazzmatica Polka](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100721) ·
[Quirky Waltz](https://opengameart.org/content/8-bit-quirky-waltz) ·
[Slam-Funk](https://opengameart.org/content/nes-chiptune-slam-funk) ·
[Oriental C64](https://opengameart.org/content/oriental-music-c64-style) ·
[Salty Ditty](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1600053) ·
[Padanaya Blokov](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100606) ·
[Desert March](https://opengameart.org/content/desert-march) ·
[The Old Tower Inn](https://opengameart.org/content/chiptune-medieval-the-old-tower-inn) ·
[JRPG Pack #1](https://opengameart.org/content/jrpg-pack-1-exploration)

### Готовый блок атрибуции

CC BY требует три вещи: указать автора, дать ссылку на лицензию и **отметить,
что файл менялся**. Менялся он у всех десяти: перекодирован в MP3 48 kbps при
22050 Гц (`asset_gen/fetch_loader_music.sh`) — ради размера репозитория и
приставочного тембра. Музыка не редактировалась, только сжата.

```
Музыка на экране загрузчика (все треки перекодированы в MP3 48 kbps / 22050 Гц,
сама музыка не изменена):

"Spazzmatica Polka", "Salty Ditty", "Padanaya Blokov" — Kevin MacLeod
(incompetech.com), Creative Commons By Attribution 4.0,
https://creativecommons.org/licenses/by/4.0/

"8-bit Quirky Waltz" — Ted Kerr (Wolfgang_), CC BY 4.0
"NES chiptune Slam-Funk" — Haley Halcyon, CC BY 4.0
"Desert March" — tcarisland, CC BY 4.0
"Oriental Music C64 Style" — skrjablin, CC BY 3.0
https://creativecommons.org/licenses/by/4.0/ · https://creativecommons.org/licenses/by/3.0/

"Bebop (Chiptune)" — Alex McCulloch (Pro Sensory), CC0 1.0
"Medieval: The Old Tower Inn" — RandomMind, CC0 1.0
"Prairie Nights" — Juhani Junkala (SubspaceAudio), CC0 1.0
```

Три последних под CC0 — атрибуция юридически не нужна, авторы её только
приветствуют; оставлена по-человечески. У `05_Oriental` автор дал выбор между
CC BY 3.0 и CC0: указан по CC BY, потому что это строже и покрывает оба
варианта.

**Отбраковано при отборе** (чтобы не возвращались к этим кандидатам): трек под
**CC-BY-SA 3.0** без CC-BY-альтернативы — share-alike способен задеть саму
игру, а не только трек; трек с рабочей лицензией, но нерабочей прямой ссылкой;
и «8bit Bossa» под CC0 — длительность 59.6 с, не дотянул до нижней границы 60.

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

Проект собирается на Unity 6000.5.3f1 и использует пакет `com.unity.ugui`.
Сам движок и его пакеты в репозиторий не входят (`UnityProject/Library/`
исключён `.gitignore`) и распространяются по
[Unity Terms of Service](https://unity.com/legal/terms-of-service).
