# Железо: разводка, прошивки, Unity

Справка по физическому контроллеру игры **LadyBug — Hit the Road!**  
Актуально для сборки на **Arduino Nano** (CH340/WCH, macOS, Serial **115200**).

---

## Содержание

1. [Какая прошивка когда](#1-какая-прошивка-когда)
2. [CombinedBoard — одна плата (основная)](#2-combinedboard--одна-плата-основная)
3. [GestureSensors — только датчики (2 руки, игрок 1)](#3-gesturesensors--только-датчики-2-руки-игрок-1)
4. [Joystick — только джойстик](#4-joystick--только-джойстик)
5. [Общая земля (GND)](#5-общая-земля-gnd)
6. [Serial-протокол](#6-serial-протокол)
7. [Unity: кто что читает](#7-unity-кто-что-читает)
8. [Меню и игроки](#8-меню-и-игроки)
9. [Сборка и отладка](#9-сборка-и-отладка)
10. [Устранение неполадок](#10-устранение-неполадок)

---

## 1. Какая прошивка когда

| Прошивка | Файл | Когда использовать |
|----------|------|-------------------|
| **CombinedBoard** | `ArduinoFirmware/CombinedBoard/CombinedBoard.ino` | **Одна Nano**: 2 датчика (игрок 1) + джойстик (игрок 2) |
| GestureSensors | `ArduinoFirmware/GestureSensors/GestureSensors.ino` | Одна Nano: 2 датчика высоты (левая/правая рука игрока 1) |
| Joystick | `ArduinoFirmware/Joystick/Joystick.ino` | Отдельная Nano: только джойстик игрока 2 |

На `?` по Serial:

| Прошивка | Ответ |
|----------|--------|
| CombinedBoard, Joystick | `BOARD,JOYSTICK` |
| GestureSensors | `BOARD,GESTURE_SENSORS` |

**Библиотека датчиков:** только **Pololu VL53L0X**  
(`Sketch → Include Library → …` или `github.com/pololu/vl53l0x-arduino`).  
**Не** Adafruit VL53L0X — на Nano не хватает RAM для нескольких экземпляров.

---

## 2. CombinedBoard — одна плата (основная)

### Назначение

- **Игрок 1 (слева):** 2× VL53L0X над руками — жесты (наклон, прыжок, присед).
- **Игрок 2 (справа):** аналоговый джойстик KY-023 (или аналог) — те же направления, что на клавиатуре.

Unity определяет плату как **`BOARD,JOYSTICK`** и читает её через **`JoystickSerial.cs`** (одна строка `G,...,J,...` за цикл).

### Таблица разводки

| Комponent | Контакт | Arduino Nano | Примечание |
|-----------|---------|--------------|------------|
| **Джойстик** | GND | GND | общая земля |
| | +5V | 5V | |
| | VRx | **A0** | влево / вправо |
| | VRy | **A1** | вверх / вниз |
| | SW | — | **не подключать** |
| **VL53L0X #1** (левая рука) | GND | GND | |
| | SCL | **A5** | общая I2C |
| | SDA | **A4** | общая I2C |
| | VIN | **D2** | ⚠️ **не** на 5V |
| **VL53L0X #2** (правая рука) | GND | GND | |
| | SCL | **A5** | |
| | SDA | **A4** | |
| | VIN | **D3** | ⚠️ **не** на 5V |

### Схема

```
                    Arduino Nano
                 ┌─────────────────┐
     GND ────────┤ GND             │  ← все GND на одну землю (см. §5)
     5V  ────────┤ 5V              │
                 │                 │
  Joystick GND ──┤ GND             │
  Joystick +5V ──┤ 5V              │
  Joystick VRx ──┤ A0              │
  Joystick VRy ──┤ A1              │
                 │                 │
  VL53 #1 GND ───┤ GND             │
  VL53 #1 SCL ───┤ A5 (SCL)        ├─── общая шина I2C
  VL53 #1 SDA ───┤ A4 (SDA)        │
  VL53 #1 VIN ───┤ D2  ← левая рука│
                 │                 │
  VL53 #2 GND ───┤ GND             │
  VL53 #2 SCL ───┤ A5              │
  VL53 #2 SDA ───┤ A4              │
  VL53 #2 VIN ───┤ D3  ← правая рука│
                 │                 │
         USB ────┤                 │──→ Mac (/dev/cu.wchusbserial…)
                 └─────────────────┘
```

### Почему VIN на D2/D3, а не на 5V

На купленных модулях VL53L0X часто **нет вывода XSHUT** на разъёме (только `VIN/GND/SCL/SDA`).  
Все датчики по умолчанию на I2C-адресе **0x29**.

Прошивка при старте:

1. Включает датчики **по одному** через `digitalWrite(VIN, HIGH)`.
2. Задаёт адреса **0x30** и **0x31**.
3. Держит D2 и D3 в HIGH до перезагрузки.

~20 мА на датчик — в пределах возможностей цифрового пина ATmega328P.

### Частота опроса

~**15 Гц** (медленнее, чем отдельный Joystick: VL53L0X тратит время на измерение).

---

## 3. GestureSensors — только датчики (2 руки, игрок 1)

Прошивка: `ArduinoFirmware/GestureSensors/GestureSensors.ino`  
Unity: **`GestureSensorSerial.cs`**, ответ на `?` → **`BOARD,GESTURE_SENSORS`**.

Отдельная плата только для жестов игрока 1 (без джойстика). Разводка совпадает с блоком датчиков в CombinedBoard:

| Датчик | Пин Nano |
|--------|----------|
| Левая рука VIN | D2 |
| Правая рука VIN | D3 |
| SCL / SDA (общие) | A5 / A4 |

I2C-адреса после инициализации: **0x30**, **0x31**.  
Serial: **`G,<left_mm>,<right_mm>`** — только два датчика высоты, без тормоза и без каналов второго игрока.

~**33 Гц** (2 датчика подряд, см. `MEASUREMENT_BUDGET_US` в `.ino`).

---

## 4. Joystick — только джойстик

Прошивка: `ArduinoFirmware/Joystick/Joystick.ino`  
Unity: **`JoystickSerial.cs`**.

| Джойстик | Nano |
|----------|------|
| GND | GND |
| +5V | 5V |
| VRx | A0 |
| VRy | A1 |
| SW | — (не используется) |

~**33 Гц**, одна строка `J,<up>,<down>,<left>,<right>`.

Если направления перепутаны — менять пороги `LOW_THRESHOLD` / `HIGH_THRESHOLD` в `.ino`, не провода.

---

## 5. Общая земля (GND)

На **Arduino Nano** обычно **2–3 контакта GND** — это **одна и та же земля**, просто несколько точек для удобства.

**Все** GND должны быть общими:

- Nano (любой контакт GND)
- джойстик
- оба (или все четыре) VL53L0X

### Как развести

**Через макетную плату (рекомендуется):** один провод Nano GND → минусовая шина; все остальные GND на эту шину.

**Проводами:** один GND Nano → GND джойстика; второй GND Nano → GND датчиков (или «гребёнка»/скрутка).

Сколько физических пинов GND на Nano задействовано — **не важно**, важна **общая земля**.

---

## 6. Serial-протокол

**Скорость:** 115200 бод, 8N1.

**Идентификация:** Unity шлёт **`?`**, плата отвечает строкой `BOARD,...`.

### CombinedBoard (каждый цикл — одна строка)

```
G,<left_mm>,<right_mm>,J,<up>,<down>,<left>,<right>
```

Сначала блок **G** (2 датчика высоты), затем **J** (4 направления джойстика).

| Поле | Значение |
|------|----------|
| `G,*` мм (поля 2–3) | расстояние в миллиметрах; **-1** = нет цели (таймаут или ≥ **2000** мм — датчик без близкой поверхности отдаёт ~8190) |
| `J,*` (поля 5–8) | 0 или 1 — пороги аналога джойстика |

### GestureSensors (одна строка)

```
G,<left_mm>,<right_mm>
```

Поля расстояния: **-1** = нет цели (таймаут или ≥ **2000** мм).

### Joystick (одна строка)

```
J,<up>,<down>,<left>,<right>
```

---

## 7. Unity: кто что читает

| Скрипт | Плата | macOS-порт |
|--------|-------|------------|
| `JoystickSerial.cs` | CombinedBoard, Joystick | `/dev/cu.*usbserial*`, `*wchusbserial*`, `*usbmodem*` |
| `GestureSensorSerial.cs` | GestureSensors | те же маски |

Оба ищут порт в фоновом потоке, шлют `?`, сверяют ответ.  
**macOS only** (termios через P/Invoke); сборка проекта — `StandaloneOSX`.

`GestureInput.cs`:

- Обычно читает `GestureSensorSerial` (**только игрок 1** — два датчика высоты).
- Если это **игрок 1** и подключён **CombinedBoard** (`JoystickSerial.IsConnected`, `GestureSensorSerial` — нет), расстояния рук берутся из **`JoystickSerial.HandLeftMm` / `HandRightMm`**.
- Игрок 2 в режиме 2P — **`JoystickInput`** / джойстик, не `GestureSensorSerial`.

`JoystickInput.cs` — дискретные направления джойстика для игрока 2.

---

## 8. Меню и игроки

Стартовый экран (`StartScreenController.cs`):

- Контроллер определяется **автоматически** (без ручного выбора).
- Если **нет** железа — внизу справа: «КОНТРОЛЛЕР НЕ ОБНАРУЖЕН», управление с клавиатуры.

При подключённом **CombinedBoard**:

| Действие в меню | Источник |
|-----------------|----------|
| Влево / вправо / вверх | **Жесты датчиков** и **джойстик** (оба) |
| Вниз на строках «1/2 игрока» и «полосы» | **Короткий жест/джойстик «вниз»** — сразу переход |
| Подтверждение СТАРТ / ТРЕНИРОВКА | **Удержание «вниз» 5 сек** на этой строке (отсчёт на кнопке) |
| Выход из **тренировки** (карусель) | Все **активные** игроки держат присед **3 с молча + 5 с отсчёта** |

В игре (2 игрока, CombinedBoard):

| Игрок | Управление |
|-------|------------|
| 1 (слева) | Датчики над руками |
| 2 (справа) | Джойстик |

---

## 9. Сборка и отладка

1. Arduino IDE → плата **Arduino Nano** → процессор **ATmega328P (Old Bootloader)** при необходимости.
2. Установить библиотеку **Pololu VL53L0X** (для CombinedBoard / GestureSensors).
3. Залить **`CombinedBoard.ino`** (или нужный скетч).
4. Serial Monitor: **115200** — должна идти строка `G,...,J,...`.
5. **Закрыть** Serial Monitor / Plotter перед запуском Unity (порт один на всех).
6. Запустить игру — в Console: `[JoystickSerial] Connected: /dev/cu.wchusbserial…`

### Проверенное железо (сессия 2026)

- Клон Arduino Nano, USB-C, **ATmega328P**, мост **CH340** (`/dev/cu.wchusbserial10`).
- CombinedBoard: джойстик + 2× VL53L0X по схеме выше.

---

## 10. Устранение неполадок

| Симптом | Что проверить |
|---------|----------------|
| В IDE данные есть, в игре «не обнаружен» | **Закрыт ли Serial Monitor?** Порт занят только одной программой. |
| «Не обнаружен», в Console нет Connected | USB-кабель (данные, не только зарядка); драйвер CH340; переподключить Nano. |
| Датчики всегда -1 | VIN на **D2/D3**, не на 5V; SDA/SCL не перепутаны; датчик смотрит вниз на руку. |
| Джойстик «сам дёргается» | Мёртвая зона в прошивке; центр стика ~512, пороги ~307 / ~716. |
| Направления джойстика наоборот | Поменять сравнения порогов в `.ino`, не провода. |
| Два скрипта не находят плату | Обновлённый код: `MacSerialPort.cs` (DTR, mutex), пассивное опознавание по `G,...,J,...` / `J,` / `G,`. |

---

## Связанные файлы в репозитории

| Путь | Назначение |
|------|------------|
| `ArduinoFirmware/CombinedBoard/CombinedBoard.ino` | Прошивка combined |
| `ArduinoFirmware/GestureSensors/GestureSensors.ino` | 2 датчика (игрок 1) |
| `ArduinoFirmware/Joystick/Joystick.ino` | Только джойстик |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/JoystickSerial.cs` | Приём combined / joystick |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/GestureSensorSerial.cs` | Приём gesture board |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/GestureInput.cs` | Жесты → игровые сигналы |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/JoystickInput.cs` | Джойстик → сигналы |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/StartScreenController.cs` | Меню, авто-определение |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/MacSerialPort.cs` | DTR, блокировка порта |
| `UnityProject/Assets/LadyBug/Scripts/lady_bug/PlayerBugVisuals.cs` | Спрайты P1/P2 (светлый/тёмный) |

Подробнее об архитектуре Unity — [`technical-details.md`](technical-details.md), §4 и §5.7.
