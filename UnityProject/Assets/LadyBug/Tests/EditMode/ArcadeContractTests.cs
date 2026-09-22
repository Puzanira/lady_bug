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
