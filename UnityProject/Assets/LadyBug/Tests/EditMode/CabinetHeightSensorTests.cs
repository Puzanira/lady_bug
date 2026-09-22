using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LadyBug.Tests
{
    /// <summary>
    /// What the cabinet's two height sensors mean once they have crossed the arcade facade
    /// into this game's millimetres.
    ///
    /// The game's whole physical vocabulary is built on three states per hand — Down (close
    /// to the sensor), Up (far from it), Neutral (in between, or nothing there at all) — and
    /// the pair of them spells duck, lean and flap. The bridge that turns the hub's 0..1
    /// height into those millimetres (ArcadeControlsReader.HeightToSensorMm) used to have no
    /// way of saying "nothing there at all": an empty sensor came back as the far end of the
    /// span, which is past the "hand up" cutoff. So a cabinet standing alone, with nobody
    /// near it, was reporting two raised hands, and a player with one hand over one sensor
    /// was reporting a lean they were not making.
    ///
    /// These tests fix the meaning of an empty sensor, and — because this is the game's main
    /// mechanic and the whole risk of touching it — they also fix that the three real
    /// gestures still come through the same bridge: ВЗМАХ (flap/jump), ПРИСЕД (duck) and the
    /// lean that steers.
    ///
    /// Everything here is driven through the production path — the launcher facade, then
    /// JoystickSerial, which is what actually publishes HandLeftMm/HandRightMm on the
    /// cabinet — rather than by calling the conversion directly, so a change that moves the
    /// conversion somewhere else still has to keep these answers.
    /// </summary>
    public class CabinetHeightSensorTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private JoystickSerial _serial;

        [SetUp]
        public void SetUp()
        {
            ArcadeLauncherStub.Install();
            ArcadeLauncherStub.ResetControls();
            Assert.IsTrue(ArcadeLauncherStub.GameSeesTheLauncher(),
                "Тест не смог привести игру в состояние «я внутри автомата» — смотри ArcadeLauncherStub.");

            var go = new GameObject("CombinedBoard");
            _spawned.Add(go);
            _serial = go.AddComponent<JoystickSerial>();
            Call(_serial, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            ArcadeLauncherStub.ResetControls();
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        [Test]
        public void AnEmptySensor_ReadsAsNoHand_NotAsARaisedHand()
        {
            ReadSensors(0f, 0f, out int leftMm, out int rightMm);

            Assert.IsFalse(GestureInput.HandIsUp(leftMm) || GestureInput.HandIsUp(rightMm),
                "Пустой датчик высоты снова читается как ПОДНЯТАЯ РУКА. Это значит, что " +
                "автомат, возле которого никого нет, докладывает две поднятые руки, а игрок " +
                "с одной рукой над одним датчиком — наклон, которого он не делает. " +
                "«Цели нет» обязано быть нейтралью: смотри ArcadeControlsReader.HeightToSensorMm " +
                "и GestureInput.HandStateForDistance (mm < 0 -> Neutral).");
            Assert.IsFalse(GestureInput.HandIsDown(leftMm) || GestureInput.HandIsDown(rightMm),
                "пустой датчик — это и не опущенная рука тоже");
        }

        [Test]
        public void ACabinetNobodyIsStandingAt_ReportsNoGestureAtAll()
        {
            ReadSensors(0f, 0f, out int leftMm, out int rightMm);

            Assert.IsFalse(GestureInput.DuckHeldFromDistances(leftMm, rightMm), "пустая стойка — не присед");
            Assert.IsFalse(GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm), "пустая стойка — не наклон влево");
            Assert.IsFalse(GestureInput.LeanRightHeldFromDistances(leftMm, rightMm), "пустая стойка — не наклон вправо");
            Assert.IsFalse(GestureInput.BothHandsUpFromDistances(leftMm, rightMm),
                "Пустая стойка снова докладывает «обе руки подняты».");
        }

        /// <summary>
        /// The pose the arcade hub hands this game over in — game.json declares controls
        /// HeightA/HeightB, so the player launches lady_bug by holding a hand over one
        /// sensor. One hand there and nothing over the other must not be a lean.
        /// </summary>
        [Test]
        public void OneHandOverOneSensor_WithNothingOverTheOther_IsNotALean()
        {
            ReadSensors(1f, 0f, out int leftMm, out int rightMm);

            Assert.IsFalse(GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm),
                "Рука над левым датчиком при пустом правом снова читается как наклон влево, " +
                "зажатый навсегда. Именно в этой позе автомат отдаёт игру игроку (запуск из " +
                "хаба — удержание руки над датчиком), поэтому меню открывалось уже внутри " +
                "фантомного жеста и само перещёлкивало значение строки.");

            ReadSensors(0f, 1f, out leftMm, out rightMm);
            Assert.IsFalse(GestureInput.LeanRightHeldFromDistances(leftMm, rightMm),
                "то же самое зеркально: рука над правым датчиком при пустом левом — не наклон");
        }

        // --- the gestures the game is actually made of -----------------------------

        [Test]
        public void ADuckIsStillADuck_ThroughTheCabinetFacade()
        {
            // ПРИСЕД — обе руки у датчиков.
            ReadSensors(1f, 1f, out int leftMm, out int rightMm);

            Assert.IsTrue(GestureInput.DuckHeldFromDistances(leftMm, rightMm),
                "ПРИСЕД перестал доходить до игры через аркадный фасад — это одна из двух " +
                "основных механик коровки.");
        }

        [Test]
        public void ALeanStillSteers_ThroughTheCabinetFacade()
        {
            // Одна рука у датчика, вторая поднята, но ВНУТРИ полосы — то есть датчик её
            // всё ещё видит. Это и есть наклон.
            ReadSensors(1f, 0.1f, out int leftMm, out int rightMm);

            Assert.IsTrue(GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm),
                "Наклон перестал доходить до игры через аркадный фасад — коровка больше не " +
                "перестраивается по полосам. Поднятая рука, которую датчик ВИДИТ, обязана " +
                "оставаться «рукой вверх»; нейтралью становится только пустой датчик.");

            ReadSensors(0.1f, 1f, out leftMm, out rightMm);
            Assert.IsTrue(GestureInput.LeanRightHeldFromDistances(leftMm, rightMm), "и зеркально");
        }

        [Test]
        public void AFlapIsStillAFlap_ThroughTheCabinetFacade()
        {
            var left = new GestureInput.HandFlapTracker();
            var right = new GestureInput.HandFlapTracker();

            Assert.IsTrue(FlapDetected(left, right, WingBeatInsideTheBand()),
                "ВЗМАХ КРЫЛЬЯМИ перестал доходить до игры через аркадный фасад — это главная " +
                "механика коровки (прыжок). Трекер взмаха смотрит на сырые миллиметры, а не " +
                "на пороги Up/Down, так что ломает его именно подмена значений на входе.");
        }

        /// <summary>
        /// The flap that matters most for this change: a big one, where the hands swing clean
        /// out of the sensors' band at the top of every stroke and come back as "no reading".
        /// Before, those frames arrived as a flat 300mm; now they arrive as -1. The tracker
        /// was written for -1 (it is what the author's own boards send when the beam comes
        /// back empty), but it must still count the swing.
        /// </summary>
        [Test]
        public void ABigFlapThatLeavesTheSensorsBand_IsStillAFlap()
        {
            var left = new GestureInput.HandFlapTracker();
            var right = new GestureInput.HandFlapTracker();

            Assert.IsTrue(FlapDetected(left, right, WingBeatOutOfTheBand()),
                "Широкий взмах, на верхней точке которого руки выходят за полосу датчиков, " +
                "перестал засчитываться. Кадры «цели нет» приходят как -1 — HandFlapTracker " +
                "умеет их пропускать, но размах вокруг них обязан остаться виден.");
        }

        // --- harness ---------------------------------------------------------------

        /// <summary>
        /// Put the two cabinet sensors at a normalized reading and read back the millimetres
        /// the game's own board reader publishes — the exact path the cabinet uses.
        /// </summary>
        private void ReadSensors(float a, float b, out int leftMm, out int rightMm)
        {
            ArcadeLauncherStub.SetHeights(a, b);
            Call(_serial, "Update");
            Assert.IsTrue(_serial.IsConnected && _serial.HasHandSensors,
                "JoystickSerial не поднялся с аркадного фасада — дальше тест проверяет не то.");
            leftMm = GestureInput.SanitizeDistanceMm(_serial.HandLeftMm);
            rightMm = GestureInput.SanitizeDistanceMm(_serial.HandRightMm);
        }

        private bool FlapDetected(GestureInput.HandFlapTracker left, GestureInput.HandFlapTracker right,
            IEnumerable<float> wave)
        {
            bool flapping = false;
            foreach (float normalized in wave)
            {
                ReadSensors(normalized, normalized, out int leftMm, out int rightMm);
                flapping |= GestureInput.BothHandsFlapping(left, right, leftMm, rightMm);
            }
            return flapping;
        }

        /// <summary>Both hands sweeping up and down, staying where the sensors can see them.</summary>
        private static IEnumerable<float> WingBeatInsideTheBand()
        {
            return Repeat(new[] { 1f, 0.75f, 0.5f, 0.25f, 0.1f, 0.25f, 0.5f, 0.75f }, 4);
        }

        /// <summary>The same beat, swung wide enough to leave the band at the top.</summary>
        private static IEnumerable<float> WingBeatOutOfTheBand()
        {
            return Repeat(new[] { 1f, 0.6f, 0.2f, 0f, 0f, 0.2f, 0.6f, 1f }, 6);
        }

        private static IEnumerable<float> Repeat(float[] cycle, int times)
        {
            for (int i = 0; i < times; i++)
                foreach (float v in cycle)
                    yield return v;
        }

        private static object Call(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + method + "() is gone — re-point this test.");
            return info.Invoke(target, args);
        }
    }
}
