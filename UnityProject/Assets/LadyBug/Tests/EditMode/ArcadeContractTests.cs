using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace LadyBug.Tests
{
    /// <summary>
    /// Guards the arcade cabinet contract (ARCADE_INTEGRATION.md) from the inside.
    ///
    /// «Коровка» does not run as its own application on the cabinet: the hub loads it
    /// into the launcher's process, alongside the other six games, on one screen and
    /// one serial line. Everything below is a way the game can take something that
    /// belongs to the whole machine — the process, the port, the display — and each of
    /// them has already arrived in some game's upstream update at least once. The
    /// author's repository is repacked into this one by hand on every update, so these
    /// are tripwires, not documentation.
    ///
    /// The Unity-free contract_check.py at the repo root covers the packaging side
    /// (package.json/game.json/asmdefs/raw input); this file covers what only running
    /// code can show.
    /// </summary>
    public class ArcadeContractTests
    {
        private static string GameRoot => Path.Combine(Application.dataPath, "LadyBug");

        /// <summary>Shipped game code: everything that runs on the cabinet.</summary>
        private static string ScriptsRoot => Path.Combine(GameRoot, "Scripts");

        /// <summary>
        /// Source with comments stripped, so prose ABOUT a forbidden call is not mistaken
        /// for the call — and, just as important, so the scan sees code the editor never
        /// compiles. A quit inside an #if UNITY_STANDALONE branch is invisible to a
        /// compile-time check and very visible to a player at the cabinet; that is exactly
        /// how one arrived in another game of the studio.
        /// </summary>
        private static string CodeOf(string file)
        {
            string text = File.ReadAllText(file);
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*?$", "", RegexOptions.Multiline);
        }

        private static IEnumerable<string> GameScripts()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot),
                "Игровые скрипты должны лежать в Assets/LadyBug/Scripts — иначе этот сторож " +
                "проверяет пустоту. Смотри contract_check.py и структуру пакета.");
            return Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
        }

        // ---------------------------------------------------------------- the process

        [Test]
        public void GameCode_NeverEndsTheProcess()
        {
            var offenders = GameScripts()
                .Where(f => CodeOf(f).Contains("Application.Quit("))
                .Select(Path.GetFileName)
                .ToList();

            Assert.IsEmpty(offenders,
                "Application.Quit() в игровом коде: " + string.Join(", ", offenders) + ".\n" +
                "На стойке игра НЕ владеет процессом — её запускает хаб внутри процесса " +
                "лаунчера, вместе с остальными шестью играми. Этот вызов гасит весь автомат: " +
                "и лаунчер, и аттракт-режим, и возврат в меню. Перехватить его хаб не может.\n" +
                "Выход из игры один — сенсорная кнопка «меню» автомата, её обрабатывает сам " +
                "хаб; игра про неё не знает и знать не должна. Уберите вызов.");
        }

        // ------------------------------------------------------------------- the port

        /// <summary>
        /// The cabinet has ONE combo board on ONE serial line, and it belongs to the hub's
        /// arcade-controls package. Two processes on the same tty is how you get a game that
        /// reads nothing — and since the board sends the joystick and both height sensors as
        /// a single "G,…,J,…" frame, a second reader also tears that frame in half for the
        /// launcher. So the game's own two readers must stay off the port whenever the
        /// arcade facade is present, and take their values from it instead.
        ///
        /// This is the behavioural half: the game is put inside the launcher (the same
        /// probe the real cabinet satisfies) and each reader is started for real. If the
        /// UseArcadeFacade guard in OnEnable ever disappears, a port-scanning thread starts
        /// here and the test says so.
        /// </summary>
        [Test]
        public void BoardReaders_StayOffThePort_WhenTheCabinetFacadeIsPresent()
        {
            ArcadeLauncherStub.Install();
            Assert.IsTrue(ArcadeLauncherStub.GameSeesTheLauncher(),
                "Тест не смог привести игру в состояние «я внутри автомата» — без этого он " +
                "ничего не проверяет. Смотри ArcadeLauncherStub.");

            AssertReaderStaysOffThePort<JoystickSerial>("JoystickSerial");
            AssertReaderStaysOffThePort<GestureSensorSerial>("GestureSensorSerial");
        }

        private static void AssertReaderStaysOffThePort<T>(string readerName) where T : MonoBehaviour
        {
            var go = new GameObject(readerName + "_underTest");
            try
            {
                T reader = go.AddComponent<T>();
                Invoke(reader, "OnEnable");

                FieldInfo thread = typeof(T).GetField("_thread", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(thread,
                    readerName + "._thread пропал — этот сторож ищет по нему поток, который " +
                    "лезет в порт. Перенацельте тест на то, чем читается порт теперь.");

                Assert.IsNull(thread.GetValue(reader),
                    readerName + " открывает последовательный порт, когда активен аркадный " +
                    "фасад.\n" +
                    "На стойке одна плата на одной линии, и ею владеет пакет arcade-controls " +
                    "хаба. Второй читатель на том же tty рвёт строки лаунчеру (плата шлёт " +
                    "джойстик и оба датчика ОДНОЙ строкой «G,…,J,…»), и в итоге не читает " +
                    "никто.\n" +
                    "Верните ранний выход по ArcadeControlsReader.Available в OnEnable: " +
                    "значения берутся с фасада (ArcadeInput), поток не стартует вовсе.");
            }
            finally
            {
                // If the guard IS gone, a real port-scanning thread is now running: stop it.
                T live = go.GetComponent<T>();
                if (live != null)
                    Invoke(live, "OnDisable");
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// The textual half of the same rule: only the two sanctioned readers (and the
        /// shared macOS helper) are allowed to speak to a serial device at all. A third
        /// reader arriving from upstream would have its own OnEnable, its own thread and
        /// no facade guard — the test above would never see it.
        /// </summary>
        [Test]
        public void OnlyTheTwoBoardReaders_SpeakToASerialPort()
        {
            var allowed = new HashSet<string>
            {
                "JoystickSerial.cs",      // combo board: stick + both height sensors
                "GestureSensorSerial.cs", // legacy separate hand-sensor board
                "MacSerialPort.cs",       // shared probe lock / DTR helper
            };

            var portVocabulary = new Dictionary<string, string>
            {
                { @"System\.IO\.Ports", "System.IO.Ports" },
                { @"\bSerialPort\b", "SerialPort" },
                // Both readers reach the tty through libSystem's open()/read() rather than
                // any managed serial class, and find the board by scanning /dev — so the
                // literals themselves are the signature, whether spelled inline or hidden
                // behind a const.
                { @"""libSystem", "raw open()/read() on a tty via libSystem" },
                { @"""/dev""", "scanning /dev for a board" },
            };

            var offenders = new List<string>();
            foreach (string file in GameScripts())
            {
                if (allowed.Contains(Path.GetFileName(file)))
                    continue;
                string code = CodeOf(file);
                foreach (var rule in portVocabulary)
                    if (Regex.IsMatch(code, rule.Key))
                        offenders.Add(Path.GetFileName(file) + " → " + rule.Value);
            }

            Assert.IsEmpty(offenders,
                "Новый читатель последовательного порта в игре: " + string.Join(", ", offenders) + ".\n" +
                "Порт на стойке принадлежит пакету arcade-controls: игра, которая лезет в него " +
                "сама, рвёт строки лаунчеру и остаётся без данных вместе с ним.\n" +
                "Ввод стойки приходит через ArcadeControlsReader (фасад ArcadeInput). Если " +
                "читатель действительно нужен вне автомата — он обязан молчать при " +
                "ArcadeControlsReader.Available, как JoystickSerial/GestureSensorSerial, и " +
                "быть внесён в белый список этого теста осознанно.");
        }

        /// <summary>
        /// The other half of "stay off the port": having stayed off it, the reader must
        /// still deliver the cabinet's buttons.
        ///
        /// The author's 29b1e3f taught JoystickSerial to parse the panel's "C,…" line and
        /// hung four screens off the result — the quit dialog's ДА/НЕТ, the green confirm
        /// on the start row, the 10 km continue prompt, skipping a recap page. He wrote
        /// that parser without ever having the board: it is ours. Inside the hub the port
        /// is never opened, so no "C,…" line ever arrives and all four would sit dead with
        /// the buttons physically under the player's hand — a failure no compile and no
        /// port-guard would show. The values come off the facade instead
        /// (ApplyArcadePanelButtons), and this is the test that says so.
        ///
        /// The test above proves the thread never starts; this one proves that staying off
        /// the port did not cost the game its buttons.
        /// </summary>
        [Test]
        public void CabinetPanelButtons_ArriveThroughTheFacade_NotThroughThePort()
        {
            ArcadeLauncherStub.Install();
            ArcadeLauncherStub.ResetControls();
            Assert.IsTrue(ArcadeLauncherStub.GameSeesTheLauncher(),
                "Тест не смог привести игру в состояние «я внутри автомата» — без этого он " +
                "ничего не проверяет. Смотри ArcadeLauncherStub.");

            var go = new GameObject("JoystickSerial_panelButtons");
            try
            {
                JoystickSerial reader = go.AddComponent<JoystickSerial>();
                Invoke(reader, "Awake");
                Invoke(reader, "OnEnable");

                FieldInfo thread = typeof(JoystickSerial).GetField(
                    "_thread", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNull(thread.GetValue(reader),
                    "JoystickSerial полез в порт — смотри соседний тест про порт.");

                ArcadeLauncherStub.SetGreenButton(true);
                Invoke(reader, "Update");

                Assert.IsTrue(reader.GreenButton,
                    "Зелёная кнопка стойки не доходит до игры.\n" +
                    "Внутри хаба порт не открывается вовсе, значит строка «C,…» не приходит " +
                    "никогда и парсер панели (его код, написанный без платы на руках) не " +
                    "исполняется ни разу. Кнопки обязаны приходить с фасада " +
                    "(ArcadeControlsReader.GreenHeld) — смотри JoystickSerial." +
                    "ApplyArcadePanelButtons. Иначе на стойке не работают: ДА/НЕТ в диалоге " +
                    "выхода, подтверждение строки СТАРТ, вопрос «продолжить» на 10 км и " +
                    "перелистывание итогов.");
                Assert.IsTrue(reader.GreenButtonDown,
                    "Фронт зелёной кнопки не снимается на аркадном пути. Все потребители " +
                    "читают именно фронт (GreenButtonDown), а не удержание, так что без него " +
                    "кнопка «нажата» и не делает ничего. Фронты считает " +
                    "UpdatePanelButtonEdges(), и её обязаны звать ОБЕ ветки Update.");
                Assert.IsTrue(reader.HasPanelButtons,
                    "HasPanelButtons остаётся false на стойке — а это ровно тот флаг, по " +
                    "которому потребители отличают «панели нет» от «ничего не нажато».");

                Invoke(reader, "Update");
                Assert.IsFalse(reader.GreenButtonDown,
                    "Зажатая зелёная кнопка даёт фронт каждый кадр — это автоповтор, " +
                    "которого ни один потребитель не ждёт: каждый из них меняет экран. " +
                    "Фронт обязан быть ровно один на нажатие.");

                ArcadeLauncherStub.SetGreenButton(false);
                ArcadeLauncherStub.SetRedButton(true);
                Invoke(reader, "Update");
                Assert.IsTrue(reader.RedButtonDown, "Красная кнопка стойки не доходит до игры.");
                Assert.IsFalse(reader.GreenButton, "Отпущенная зелёная осталась зажатой.");

                Assert.IsFalse(reader.EscapeButton,
                    "Игра видит системную кнопку стойки. Выход из игры на стойке — сенсорная " +
                    "кнопка «меню», и её обрабатывает ХАБ; игра про неё знать не должна. " +
                    "Фасад её не публикует, и подставлять её сюда нельзя: иначе игра начнёт " +
                    "сама открывать свой диалог выхода и уводить экран на свою заставку " +
                    "поверх лаунчера.");
            }
            finally
            {
                var live = go.GetComponent<JoystickSerial>();
                if (live != null)
                    Invoke(live, "OnDisable");
                ArcadeLauncherStub.ResetControls();
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------- the attract screen

        /// <summary>
        /// Inside the launcher lady_bug's own attract screen must not be shown for a single
        /// frame (190bdc0) — the launcher IS the attract screen, and the game is one window
        /// inside its session.
        ///
        /// 29b1e3f gave that screen a 10-track playlist and a new public way back TO it
        /// (ReturnToLoader, from the menu's SYSTEM/Esc handling). Both are places the
        /// stand-down in Awake does not reach by itself: music starts from OnEnable, and
        /// ReturnToLoader exists precisely to undo a hidden canvas. On the cabinet that
        /// would be lady_bug's attract screen drawn over the launcher's, with a second
        /// soundtrack over the launcher's own — and nothing about it is visible in a
        /// compile, in the port guards above, or in a screenshot of the menu.
        /// </summary>
        [Test]
        public void TheGamesAttractScreen_AndItsPlaylist_StayDown_InsideTheCabinet()
        {
            ArcadeLauncherStub.Install();
            Assert.IsTrue(ArcadeLauncherStub.GameSeesTheLauncher(),
                "Тест не смог привести игру в состояние «я внутри автомата».");

            var canvasRoot = new GameObject("LoaderCanvas");
            var musicGo = new GameObject("LoaderMusic");
            var loaderGo = new GameObject("LoaderScreenManager");
            try
            {
                musicGo.transform.SetParent(canvasRoot.transform, false);
                AudioSource source = musicGo.AddComponent<AudioSource>();
                source.playOnAwake = false;
                MenuMusicRotator music = musicGo.AddComponent<MenuMusicRotator>();
                SetPrivate(music, "source", source);
                SetPrivate(music, "clips", new[] { AudioClip.Create("stub", 64, 1, 8000, false) });

                LoaderScreenController loader = loaderGo.AddComponent<LoaderScreenController>();
                SetPrivate(loader, "canvasRoot", canvasRoot);
                SetPrivate(loader, "music", music);

                Invoke(loader, "Awake");

                Assert.IsFalse(canvasRoot.activeSelf,
                    "Заставка игры остаётся на экране внутри лаунчера. Смотри " +
                    "LoaderScreenController.Awake и ArcadeControlsReader.InsideArcadeLauncher " +
                    "(коммит 190bdc0).");
                Assert.IsFalse(loader.enabled,
                    "LoaderScreenController не выключает себя внутри лаунчера. Именно " +
                    "выключение ДО OnEnable не даёт стартовать ни карусели сообщений, ни " +
                    "плейлисту заставки: Unity не зовёт OnEnable у выключенного компонента. " +
                    "Если стоп-кран уехал в другое место — перенацель этот сторож туда, " +
                    "иначе музыка заставки заиграет на стойке поверх лаунчера.");
                Assert.IsFalse(IsRotating(music),
                    "Плейлист заставки запустился внутри лаунчера — на стойке это вторая " +
                    "музыка поверх музыки лаунчера.");

                // The new door: the menu's SYSTEM/Esc handling calls this to go back to
                // attract mode. In the hub it must refuse.
                loader.ReturnToLoader();

                Assert.IsFalse(canvasRoot.activeSelf,
                    "ReturnToLoader() поднимает заставку игры внутри лаунчера. Этот путь " +
                    "пришёл с обновлением автора (меню уходит назад на заставку по SYSTEM/Esc) " +
                    "и он ровно для того и написан, чтобы вернуть спрятанный канвас — стоп-кран " +
                    "в Awake его не закрывает. Нужен явный ранний выход по " +
                    "ArcadeControlsReader.InsideArcadeLauncher.");
                Assert.IsFalse(IsRotating(music),
                    "ReturnToLoader() запустил плейлист заставки внутри лаунчера.");

                // The rotator restarts itself from its own Update while it believes it is
                // playing; if anything above had flipped that flag, this is where it would
                // keep coming back.
                Invoke(music, "Update");
                Assert.IsFalse(IsRotating(music),
                    "MenuMusicRotator продолжает крутить плейлист внутри лаунчера.");
            }
            finally
            {
                Object.DestroyImmediate(loaderGo);
                Object.DestroyImmediate(canvasRoot);
            }
        }

        private static bool IsRotating(MenuMusicRotator music)
        {
            FieldInfo playing = typeof(MenuMusicRotator).GetField(
                "_playing", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(playing,
                "MenuMusicRotator._playing пропал — этот сторож смотрит по нему, крутится " +
                "ли плейлист. Перенацельте тест на то, чем это выражено теперь.");
            return (bool)playing.GetValue(music);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info,
                target.GetType().Name + "." + field + " is gone — re-point this guard at what replaced it.");
            info.SetValue(target, value);
        }

        // ----------------------------------------------------------------- the display

        /// <summary>
        /// Same class of offence as quitting: the game is one window inside the cabinet's
        /// session and does not get to re-mode the display the launcher set up (1920×1080).
        /// Reading Screen.width/height/fullScreen is fine — the HUD has to scale to it;
        /// only writing is the offence.
        /// </summary>
        [Test]
        public void GameCode_DoesNotOwnTheDisplay()
        {
            var forbidden = new Dictionary<string, string>
            {
                { @"Screen\.SetResolution\s*\(", "Screen.SetResolution(...)" },
                { @"Screen\.fullScreen(Mode)?\s*=(?!=)", "присваивание Screen.fullScreen" },
            };

            var offenders = new List<string>();
            foreach (string file in GameScripts())
            {
                string code = CodeOf(file);
                foreach (var rule in forbidden)
                    if (Regex.IsMatch(code, rule.Key))
                        offenders.Add(Path.GetFileName(file) + " → " + rule.Value);
            }

            Assert.IsEmpty(offenders,
                "Игра сама задаёт режим экрана: " + string.Join(", ", offenders) + ".\n" +
                "Окно и разрешение на стойке (1920×1080) выставляет лаунчер — игра живёт " +
                "гостем в его процессе и делит с ним экран. Смена режима из игры меняет его " +
                "для всего автомата, включая лаунчер и остальные игры.\n" +
                "Читать Screen.width/height можно сколько угодно, писать — нельзя.");
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + method + "() is gone — re-point this guard.");
            info.Invoke(target, null);
        }
    }
}
