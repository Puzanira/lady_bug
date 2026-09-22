using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace LadyBug.Tests
{
    /// <summary>
    /// Guards the cabinet's single control vocabulary (founder decision after the
    /// 2026-09 live playtest, same guard the factory already carries as
    /// LocVocabularyTests).
    ///
    /// Every game on the machine must name a control with the SAME word, because
    /// the controls are physically labelled with those words — the stickers are
    /// already ordered. The canon:
    ///   крутилка · жёлтая кнопка · зелёная кнопка · красная кнопка ·
    ///   датчики высоты · джойстик · кнопка меню
    /// («датчики высоты» is the group name on the sticker; a hint about one
    /// sensor says «датчик высоты» — the number follows the fact.)
    ///
    /// «Коровка» arrived from its author saying «ПОКРУТИТЕ ЛЮБУЮ РУКОЯТКУ»,
    /// «ДАТЧИКИ РАССТОЯНИЯ» and «ИГРОК 1: ДАТЧИКИ РУК» — three names for two
    /// organs, none of them printed on the panel. The author's repository is
    /// repacked into this one by hand on every update, so this is a tripwire
    /// against that wording coming back, not documentation.
    ///
    /// Keyboard hints (A/D, W/S, WASD/IJKL, F1/Q) are deliberately NOT caught:
    /// they exist for developing without the panel and the cabinet never shows
    /// them. What the cabinet shows must speak the vocabulary.
    /// </summary>
    public class ControlVocabularyTests
    {
        private static string GameRoot => Path.Combine(Application.dataPath, "LadyBug");

        /// <summary>
        /// Every file that can put Russian text on the screen: the shipped runtime
        /// scripts, plus SceneSetup — Main.unity is GENERATED (Tools ▸ Rebuild
        /// Scene), so the menu's own copy lives in the editor script, not in the
        /// scene asset. Scanning the scene instead would check a build artifact
        /// and miss the source it is rebuilt from.
        /// </summary>
        private static IEnumerable<string> ScreenTextSources()
        {
            string scripts = Path.Combine(GameRoot, "Scripts");
            Assert.IsTrue(Directory.Exists(scripts),
                "Игровые скрипты должны лежать в Assets/LadyBug/Scripts — иначе этот сторож " +
                "проверяет пустоту.");

            string sceneSetup = Path.Combine(GameRoot, "Editor", "SceneSetup.cs");
            Assert.IsTrue(File.Exists(sceneSetup),
                "Assets/LadyBug/Editor/SceneSetup.cs пропал — а в нём лежат ВСЕ строки меню, " +
                "карусели и диалогов: сцена генерируется из него. Без этого файла сторож " +
                "словаря не видит главный экран игры. Перенацельте его туда, где копия " +
                "живёт теперь.");

            return Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Concat(new[] { sceneSetup });
        }

        /// <summary>Source with comments stripped: prose ABOUT a banned word is not the word.</summary>
        private static string CodeOf(string file)
        {
            string text = File.ReadAllText(file);
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*?$", "", RegexOptions.Multiline);
        }

        private static readonly Regex StringLiteral = new Regex(@"""(?:[^""\\\n]|\\.)*""");
        private static readonly Regex Cyrillic = new Regex(@"\p{IsCyrillic}");

        /// <summary>
        /// Every Russian string literal the game can draw, as «file:literal».
        /// Latin-only literals are asset paths, object names and log tags — never
        /// what a player reads.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> ScreenStrings()
        {
            foreach (string file in ScreenTextSources())
            {
                string name = Path.GetFileName(file);
                foreach (Match m in StringLiteral.Matches(CodeOf(file)))
                {
                    string raw = m.Value.Substring(1, m.Value.Length - 2);
                    if (!Cyrillic.IsMatch(raw)) continue;
                    // A literal is one visual line of copy; the escapes inside it are
                    // layout, not letters, and would otherwise glue two words together
                    // ("ДАТЧИКИ ВЫСОТЫ\n" + "← →  ИЗМЕНИТЬ…").
                    string value = raw.Replace(@"\n", " ").Replace(@"\t", " ")
                                      .Replace(@"\""", "\"").Replace(@"\\", "\\");
                    yield return new KeyValuePair<string, string>(name, value);
                }
            }
        }

        private static string[] Words(string value) =>
            Regex.Matches(value, @"\p{L}+").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToArray();

        private const string Canon =
            "\nСловарь (наклейки на стойке): крутилка · жёлтая кнопка · зелёная кнопка · " +
            "красная кнопка · датчики высоты · джойстик · кнопка меню.";

        /// <summary>
        /// This test is the point of the file — it must be able to FAIL. Sanity-check
        /// it by pasting «ПОКРУТИТЕ ЛЮБУЮ РУКОЯТКУ» back into
        /// LoaderScreenController.Messages: this is the test that goes red.
        /// </summary>
        [Test]
        public void NoScreenStringNamesALegacyOrKeyboardControl()
        {
            // \b works on Cyrillic in .NET, so «СТРЕЛКИ» trips and «ПЕРЕСТРЕЛКА» would not.
            var forbidden = new Dictionary<string, string>
            {
                { @"\bРУКОЯТ", "«рукоятка» — орган называется КРУТИЛКА" },
                { @"\bРУЧК", "«ручка» — орган называется КРУТИЛКА" },
                { @"\bДИНАМО", "«динамо-машина» — орган называется КРУТИЛКА" },
                { @"ДАТЧИК\p{L}*\s+РАССТОЯН", "«датчики расстояния» — орган называется ДАТЧИКИ ВЫСОТЫ" },
                { @"ДАТЧИК\p{L}*\s+РУК", "«датчики рук» — орган называется ДАТЧИКИ ВЫСОТЫ" },
                { @"\bСТРЕЛК", "«СТРЕЛКИ» — орган называется ДЖОЙСТИК" },
                { @"\bENTER\b", "«ENTER» — на стойке клавиш нет; это ЗЕЛЁНАЯ или КРАСНАЯ КНОПКА" },
                { @"\bESC\b", "«ESC» — выход со стойки это КНОПКА МЕНЮ, и её обрабатывает хаб" },
                { @"\bКЛАВИ", "клавиатуры на стойке нет" },
                { @"\bПОТЕНЦИОМЕТР", "потенциометра на стойке нет" },
                { @"КНОПКА НА ДЖОЙСТИКЕ", "такой кнопки нет; на стойке это КРАСНАЯ КНОПКА" },
            };

            var offenders = new List<string>();
            foreach (var s in ScreenStrings())
                foreach (var rule in forbidden)
                    if (Regex.IsMatch(s.Value, rule.Key, RegexOptions.IgnoreCase))
                        offenders.Add(s.Key + ": «" + s.Value + "» — " + rule.Value);

            Assert.IsEmpty(offenders,
                "Экранные строки называют орган не по словарю автомата:\n  " +
                string.Join("\n  ", offenders) + Canon);
        }

        [Test]
        public void EveryButtonIsNamedByItsColourOrIsTheMenuButton()
        {
            var qualifiers = new[] { "красн", "жёлт", "желт", "зелён", "зелен" };
            var offenders = new List<string>();

            foreach (var s in ScreenStrings())
            {
                string[] w = Words(s.Value);
                for (int i = 0; i < w.Length; i++)
                {
                    if (!w[i].StartsWith("кнопк")) continue;
                    bool colour = i > 0 && qualifiers.Any(q => w[i - 1].StartsWith(q));
                    bool menu = i + 1 < w.Length && w[i + 1].StartsWith("меню");
                    if (!colour && !menu) offenders.Add(s.Key + ": «" + s.Value + "»");
                }
            }

            Assert.IsEmpty(offenders,
                "Кнопка названа без цвета:\n  " + string.Join("\n  ", offenders) +
                "\nИгрок у стойки выбирает кнопку глазами: «красная кнопка», «жёлтая кнопка», " +
                "«зелёная кнопка» — или «кнопка меню». Просто «кнопка» не говорит, какая." + Canon);
        }

        [Test]
        public void JoystickAndSensorsUseTheirCanonNames()
        {
            var offenders = new List<string>();
            foreach (var s in ScreenStrings())
            {
                string[] w = Words(s.Value);
                for (int i = 0; i < w.Length; i++)
                {
                    // «стик» alone is the old name; the canon word is «джойстик».
                    if (w[i].Contains("стик") && !w[i].StartsWith("джойстик"))
                        offenders.Add(s.Key + ": «" + w[i] + "» — орган называется ДЖОЙСТИК");

                    // Sensors are «датчик(и) высоты»: the group name on the sticker is
                    // plural, a hint about one sensor is singular — but «высоты» always.
                    if (w[i].StartsWith("датчик") &&
                        !(i + 1 < w.Length && w[i + 1].StartsWith("высот")))
                        offenders.Add(s.Key + ": «" + s.Value + "» — «" + w[i] +
                                      "» без «высоты»; орган называется ДАТЧИК(И) ВЫСОТЫ");
                }
            }

            Assert.IsEmpty(offenders, "Органы названы не по словарю:\n  " +
                string.Join("\n  ", offenders) + Canon);
        }

        /// <summary>
        /// The controls this game actually reads must stay named somewhere on screen —
        /// a screen that names nothing is how the founder got stuck at the cabinet in
        /// the factory. Coverage, not wording: the tests above police the wording.
        /// </summary>
        [Test]
        public void TheControlsThisGameUsesAreNamedOnScreen()
        {
            var all = ScreenStrings().Select(s => s.Value).ToList();

            void Require(string pattern, string why)
            {
                Assert.IsTrue(all.Any(v => Regex.IsMatch(v, pattern, RegexOptions.IgnoreCase)),
                    "Ни одна экранная строка не называет " + why + Canon);
            }

            Require(@"ДАТЧИК\p{L}*\s+ВЫСОТЫ", "датчики высоты — а ими играет ИГРОК 1 и ими же подтверждается СТАРТ");
            Require(@"ДЖОЙСТИК", "джойстик — а им играет ИГРОК 2 и листается меню");
            Require(@"ЗЕЛ[ЁЕ]Н\p{L}*\s+КНОПК", "зелёную кнопку — а ею подтверждается выбор в меню");
            Require(@"КРАСН\p{L}*\s+КНОПК", "красную кнопку — а ею открывается и отменяется выход из игры");
            Require(@"КРУТИЛК", "крутилку — заставка приглашает потрогать каждый орган панели");
        }
    }
}
