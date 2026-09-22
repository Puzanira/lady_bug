using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LadyBug.Tests
{
    /// <summary>
    /// The cabinet stick has to walk the pre-game menu. On the cabinet the station is
    /// ONE player (StartScreenController.ApplyArcadeDefaults), and in 1-player mode
    /// UpdateVisuals deactivates PlayerRight — so the JoystickInput that lives on
    /// PlayerRight never runs Update, and every edge it publishes (LeftDown/RightDown/
    /// UpHeld/DownHeld) is frozen false no matter how "enabled" the component is. The
    /// menu reads those edges. That is how the stick once stopped listing the menu
    /// while the side HUD indicator — which reads JoystickSerial directly — kept
    /// moving, and it was repaired by a single line in RestoreMenuGestureMode
    /// (90b3913). One line is not a guard: the author's repository is repacked into
    /// this one by hand on every update, and a line like that goes silently.
    ///
    /// These tests are the guard. They drive the real chain the cabinet drives —
    /// ArcadeInput facade -> ArcadeControlsReader -> JoystickSerial -> JoystickInput
    /// -> the menu's own nav methods — and they tick each JoystickInput ONLY when
    /// Unity itself would (isActiveAndEnabled), which is precisely the rule the
    /// regression fell foul of. If the stick stops listing the menu in 1-player mode,
    /// they go red.
    /// </summary>
    public class MenuJoystickNavigationTests
    {
        private const int PlayersRow = 0;
        private const int LanesRow = 1;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private StartScreenController _menu;
        private JoystickSerial _serial;
        private JoystickInput _stickOnPlayerLeft;   // PlayerLeft — always active in the menu
        private JoystickInput _stickOnPlayerRight;  // PlayerRight — deactivated in 1-player mode

        [SetUp]
        public void SetUp()
        {
            // Make the game believe it is running inside the cabinet launcher, the same
            // way the launcher makes it believe that — through its own reflection probe.
            ArcadeLauncherStub.Install();
            ArcadeLauncherStub.ResetControls();
            Assert.IsTrue(ArcadeLauncherStub.GameSeesTheLauncher(),
                "Тест не смог привести игру в состояние «я внутри автомата» — без этого он " +
                "проверяет не ту сборку. Смотри ArcadeLauncherStub.");

            _serial = NewObject("CombinedBoard").AddComponent<JoystickSerial>();
            Call(_serial, "Awake"); // sets JoystickSerial.Instance, as it does at runtime

            GameObject playerLeft = NewObject("PlayerLeft");
            GameObject playerRight = NewObject("PlayerRight");
            _stickOnPlayerLeft = playerLeft.AddComponent<JoystickInput>();
            _stickOnPlayerRight = playerRight.AddComponent<JoystickInput>();

            // 1 player: exactly what UpdateVisuals does to PlayerRight, and what the
            // cabinet preselects. See TheOnePlayerMenu_DeactivatesPlayerRight below.
            playerRight.SetActive(false);

            _menu = NewObject("StartScreen").AddComponent<StartScreenController>();
            Set(_menu, "joystickLeft", _stickOnPlayerLeft);
            Set(_menu, "joystickRight", _stickOnPlayerRight);
            Set(_menu, "_useHardwareInput", true);
            Set(_menu, "_selectedPlayers", 1);
            Set(_menu, "_selectedLanes", 3);
            Set(_menu, "_row", PlayersRow);
            ArmRoadPreview();

            // The board has to be reporting itself connected before the menu decides
            // anything about the wrappers — RestoreMenuGestureMode enables them only for a
            // connected stick, and at runtime it is re-run twice a second by
            // RefreshControllerDetection for exactly that reason. One serial frame here
            // stands in for that: it is the reader picking the cabinet up off the facade.
            Call(_serial, "Update");
            Assert.IsTrue(_serial.IsConnected,
                "JoystickSerial не поднялся с аркадного фасада — дальше тест проверяет не то.");

            // The menu's own decision about which wrappers are live. This is the line
            // the regression lived in; the tests below never bypass it.
            Call(_menu, "RestoreMenuGestureMode");
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
        public void CabinetStickDown_WalksTheMenuCursorDown_InOnePlayerMode()
        {
            Assert.AreEqual(PlayersRow, Row(), "cursor starts on the first row");

            PushStick(Vector2.down);
            MenuFrame();

            Assert.AreEqual(LanesRow, Row(),
                "Джойстик перестал листать меню вниз в режиме одного игрока — на стойке это " +
                "значит, что из меню нельзя дойти до СТАРТ вообще ничем, кроме клавиатуры, " +
                "которой у игрока нет.\n" +
                "Меню берёт фронты (DownHeld) с компонентов JoystickInput, а Update не идёт " +
                "на выключенном компоненте и на выключенном объекте. PlayerRight в режиме " +
                "одного игрока выключен, значит джойстик меню обязан ехать и на joystickLeft " +
                "(PlayerLeft всегда активен) — смотри RestoreMenuGestureMode и коммит 90b3913.");
        }

        [Test]
        public void CabinetStickRight_SwitchesTheFocusedRowValue_InOnePlayerMode()
        {
            Set(_menu, "_row", LanesRow);
            int lanesBefore = (int)Get(_menu, "_selectedLanes");

            PushStick(Vector2.right);
            MenuFrame();

            Assert.AreNotEqual(lanesBefore, (int)Get(_menu, "_selectedLanes"),
                "Джойстик перестал переключать значение строки меню (вправо) в режиме одного " +
                "игрока: на стойке нельзя выбрать ни число полос, ни СТАРТ/ТРЕНИРОВКА. " +
                "Причина та же, что у «вниз» — смотри соседний тест и RestoreMenuGestureMode.");
        }

        [Test]
        public void CabinetStickUpTap_WalksTheMenuCursorBack_InOnePlayerMode()
        {
            Set(_menu, "_row", LanesRow);

            PushStick(Vector2.up);
            MenuFrame();          // held
            PushStick(Vector2.zero);
            MenuFrame();          // released inside the tap window -> one row up

            Assert.AreEqual(PlayersRow, Row(),
                "Короткий рывок джойстика вверх перестал поднимать курсор меню в режиме " +
                "одного игрока: пройдя строку мимо, игрок на стойке уже не вернётся назад. " +
                "Причина та же, что у «вниз» — смотри соседний тест и RestoreMenuGestureMode.");
        }

        /// <summary>
        /// The hardening, stated as behaviour: the menu must keep seeing the stick even
        /// if BOTH JoystickInput wrappers are dead — which is exactly the state the game
        /// shipped in before 90b3913 (joystickLeft switched off, joystickRight sitting on
        /// a deactivated PlayerRight). The menu owns its own edge tracker off
        /// JoystickSerial for this reason; the wrappers are an addition, not the floor.
        /// </summary>
        [Test]
        public void CabinetStickDown_StillWalksTheMenu_WhenBothWrappersAreDead()
        {
            _stickOnPlayerLeft.enabled = false;   // the pre-90b3913 line, put back by hand
            Assert.IsFalse(_stickOnPlayerRight.isActiveAndEnabled, "PlayerRight is inactive in 1-player mode");

            PushStick(Vector2.down);
            MenuFrame();

            Assert.AreEqual(LanesRow, Row(),
                "Листание меню джойстиком снова держится на одном-единственном месте — на том, " +
                "что RestoreMenuGestureMode включит нужный JoystickInput. Ровно это молча " +
                "сломалось при прошлом слиянии (90b3913). Меню обязано снимать фронты стика " +
                "само (MenuStickEdges), не завися от того, на каком объекте живёт обёртка и " +
                "активен ли он.");
        }

        /// <summary>
        /// Pins the premise the tests above are built on: in 1-player mode the menu really
        /// does deactivate PlayerRight, so a JoystickInput living there really does stop
        /// ticking. If the author ever stops deactivating it, this test says so out loud
        /// rather than letting the guards above quietly test nothing.
        /// </summary>
        [Test]
        public void TheOnePlayerMenu_DeactivatesPlayerRight()
        {
            GameObject left = NewObject("RoadPlayerLeft");
            GameObject right = NewObject("RoadPlayerRight");
            Set(_menu, "playerLeft", left);
            Set(_menu, "playerRight", right);

            Call(_menu, "UpdateVisuals");

            Assert.IsFalse(right.activeSelf,
                "В режиме одного игрока PlayerRight больше не выключается. Это меняет посылку " +
                "тестов про джойстик в меню (обёртка на выключенном объекте не тикает) — " +
                "перечитай их и RestoreMenuGestureMode.");
            Assert.IsTrue(left.activeSelf, "PlayerLeft stays on the road in 1-player mode");
        }

        /// <summary>
        /// The tests above replay one menu frame in the order StartScreenController.Update
        /// runs it. That is their one assumption, so it is checked rather than assumed: if a
        /// merge drops the joystick nav out of Update itself, nothing above would notice.
        /// </summary>
        [Test]
        public void MenuUpdate_StillRunsTheJoystickNavPath()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "LadyBug/Scripts/lady_bug/StartScreenController.cs"));
            int updateAt = source.IndexOf("private void Update()", System.StringComparison.Ordinal);
            Assert.Greater(updateAt, 0, "StartScreenController.Update() not found");
            string update = source.Substring(updateAt, System.Math.Min(2500, source.Length - updateAt));

            foreach (string call in new[]
                     {
                         "AppendMenuJoystickNav(ref left, ref right)",
                         "UpdateMenuJoystickUp(ref up)",
                         "UpdateMenuDownHold(ref down)"
                     })
            {
                StringAssert.Contains(call, update,
                    "StartScreenController.Update больше не вызывает " + call + " — джойстик " +
                    "выпал из обработки меню целиком, и тесты рядом этого не увидят: они " +
                    "повторяют порядок вызовов Update вручную. Верни вызов или перепиши тесты.");
            }
        }

        // --- harness -------------------------------------------------------------

        /// <summary>
        /// One frame of the menu, in the order the real Update runs it: the serial reader
        /// takes the cabinet's stick off the facade, every JoystickInput Unity WOULD tick
        /// ticks (and only those), then the menu's own nav methods run and the cursor moves.
        /// </summary>
        private void MenuFrame()
        {
            Call(_serial, "Update");
            foreach (JoystickInput stick in new[] { _stickOnPlayerLeft, _stickOnPlayerRight })
                if (stick != null && stick.isActiveAndEnabled)
                    Call(stick, "Update");

            var nav = new object[] { false, false };
            typeof(StartScreenController)
                .GetMethod("AppendMenuJoystickNav", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_menu, nav);
            bool left = (bool)nav[0];
            bool right = (bool)nav[1];

            var upArgs = new object[] { false };
            typeof(StartScreenController)
                .GetMethod("UpdateMenuJoystickUp", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_menu, upArgs);
            bool up = (bool)upArgs[0];

            var downArgs = new object[] { false };
            typeof(StartScreenController)
                .GetMethod("UpdateMenuDownHold", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_menu, downArgs);
            bool down = (bool)downArgs[0];

            if (left || right)
                ApplyHorizontal(right);

            if (down)
                Call(_menu, "MoveRow", 1);
            else if (up)
                Call(_menu, "MoveRow", -1);
        }

        // Same row-value edit Update() does; kept here (rather than calling Update) because
        // the menu's Update also drives carousels, detection timers and Time.deltaTime, none
        // of which exist in an edit-mode frame.
        private void ApplyHorizontal(bool right)
        {
            int row = Row();
            if (row == PlayersRow)
            {
                Set(_menu, "_selectedPlayers", (int)Get(_menu, "_selectedPlayers") == 1 ? 2 : 1);
            }
            else if (row == LanesRow)
            {
                int lanes = (int)Get(_menu, "_selectedLanes");
                int min = (int)Call(_menu, "MinSelectableLaneIndex");
                Set(_menu, "_selectedLanes", Mathf.Clamp(lanes + (right ? 1 : -1), min, RoadLayout.MaxLaneCount - 1));
            }
            else
            {
                Set(_menu, "_selectedStartOption", (int)Get(_menu, "_selectedStartOption") == 0 ? 1 : 0);
            }
        }

        private static void PushStick(Vector2 deflection)
        {
            ArcadeLauncherStub.SetJoystick(deflection);
        }

        private int Row()
        {
            return (int)Get(_menu, "_row");
        }

        // Keeps UpdateVisuals from rebuilding the road geometry in an edit-mode frame.
        private void ArmRoadPreview()
        {
            Set(_menu, "_roadPreviewApplied", true);
            Set(_menu, "_appliedPreviewLaneCount", (int)Call(_menu, "EffectiveLaneCount"));
            Set(_menu, "_appliedPreviewPlayers", (int)Get(_menu, "_selectedPlayers"));
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + field + " is gone — re-point this test at what replaced it.");
            info.SetValue(target, value);
        }

        private static object Get(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + field + " is gone — re-point this test at what replaced it.");
            return info.GetValue(target);
        }

        private static object Call(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + "." + method + "() is gone — re-point this test at what replaced it.");
            return info.Invoke(target, args);
        }
    }
}
