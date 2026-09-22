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
        private const int StartRow = 2;

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

        // --- the right-hand option of every row -----------------------------------
        //
        // Founder's live report from the cabinet: «у ледибаги нельзя выбрать режим с 2
        // игроками сейчас и тренировку». Both are the RIGHT option of their row, and both
        // rows are plain toggles — so the report is not "right is dead", it is "the whole
        // horizontal axis is dead". Two separate things pinned it, and the tests below
        // keep each of them fixed:
        //
        //   1. Choosing 1 ИГРОК deactivates PlayerRight, and it is chosen BY PUSHING THE
        //      STICK. The JoystickInput living there froze mid-deflection with RightHeld
        //      still true, and the nav latch releases only on "nothing held" — so after
        //      one trip through the players row the axis never unlatched again.
        //   2. The cabinet launches this game by holding a hand over a height sensor
        //      (game.json controls = HeightA/HeightB). One hand over one sensor and
        //      nothing over the other reads as a lean held forever, and while the sensors
        //      and the stick shared a single latch that lean swallowed the stick too.

        /// <summary>
        /// Walk the players row the way a player does — 1 → 2 → 1 → 2 — instead of once.
        /// The freeze only appears on the trip that switches PlayerRight OFF, so a test
        /// that pushes the stick a single time never meets it.
        /// </summary>
        [Test]
        public void CabinetStick_StillSwitchesPlayers_AfterPlayerRightWasDeactivatedMidPush()
        {
            Assert.AreEqual(1, Players(), "the cabinet preselects 1 player");

            TapStickRight();
            Assert.AreEqual(2, Players(), "first push: 1 ИГРОК -> 2 ИГРОКА");

            TapStickRight();
            Assert.AreEqual(1, Players(), "second push: back to 1 ИГРОК, PlayerRight switched off");
            Assert.IsFalse(_stickOnPlayerRight.isActiveAndEnabled,
                "PlayerRight is off again — that is the state the freeze lives in");

            TapStickRight();
            Assert.AreEqual(2, Players(),
                "Джойстик перестал возвращать «2 ИГРОКА» после того, как строка один раз " +
                "сходила обратно на «1 ИГРОК». Ровно это увидела основательница на стойке.\n" +
                "Выбор «1 ИГРОК» ВЫКЛЮЧАЕТ PlayerRight, а делается он движением джойстика вбок " +
                "— обёртка JoystickInput на PlayerRight замирает прямо в отклонённом " +
                "положении и навсегда продолжает докладывать RightHeld. Замок горизонтали " +
                "снимается только когда не зажато НИЧЕГО, поэтому ось умирает насовсем. " +
                "Смотри IsJoystick*Held (isActiveAndEnabled, не enabled) и JoystickInput.OnDisable.");
        }

        /// <summary>
        /// ТРЕНИРОВКА, reached with a hand sitting over a height sensor — which is not an
        /// exotic pose: it is how the hub hands this game to the player.
        /// </summary>
        [Test]
        public void CabinetStick_ReachesTraining_WhileAHandRestsOverAHeightSensor()
        {
            Set(_menu, "_row", StartRow);
            RestAHandOverTheLeftSensor();

            MenuFrame();  // the lean the cabinet starts us in, seen for the first time
            Assert.IsTrue((bool)Call(_menu, "MenuSensorLeanLeftHeld"),
                "предпосылка теста: рука над одним датчиком и пустота над другим читаются " +
                "как наклон — если это больше не так, тест проверяет не тот случай");

            // That first lean also counts as one step, so put the row back on СТАРТ and
            // ask the stick — with the lean still held — to carry it across to ТРЕНИРОВКА.
            Set(_menu, "_selectedStartOption", 0);
            TapStickRight();

            Assert.AreEqual(1, (int)Get(_menu, "_selectedStartOption"),
                "Джойстиком нельзя выбрать ТРЕНИРОВКУ, пока рука лежит над датчиком высоты.\n" +
                "Именно так автомат и отдаёт игру игроку: game.json объявляет controls " +
                "HeightA/HeightB, то есть запуск из хаба — это удержание руки над датчиком. " +
                "Рука над одним датчиком при пустом втором = наклон, зажатый навсегда; пока " +
                "у датчиков и джойстика был ОДИН замок горизонтали, этот наклон глушил стик. " +
                "Смотри ApplyMenuStickHorizontalNavLock — у стика свой замок.");
        }

        /// <summary>
        /// The same thing again, but driven by StartScreenController.Update ITSELF rather
        /// than by this file's replay of it. The replay fixes an order; Update is the order.
        /// The deadlock was made OF that order — one latch applied after both families had
        /// already been merged into the same two booleans — so a replay cannot be the only
        /// witness to it. The start row is chosen because it is the one row whose value can
        /// change without UpdateVisuals rebuilding the road preview, which edit mode cannot
        /// do (Destroy vs DestroyImmediate).
        /// </summary>
        [Test]
        public void CabinetStick_ReachesTraining_ThroughTheMenusOwnUpdate_WhileAHandRestsOverAHeightSensor()
        {
            Set(_menu, "_row", StartRow);
            Set(_menu, "_controllerDetectionSettled", true);
            RestAHandOverTheLeftSensor();

            RealMenuFrame();  // the opening lean: seen once, counted once
            Assert.IsTrue((bool)Call(_menu, "MenuSensorLeanLeftHeld"),
                "предпосылка теста: рука над одним датчиком и пустота над другим = наклон");

            Set(_menu, "_selectedStartOption", 0);

            PushStick(Vector2.right);
            RealMenuFrame();

            Assert.AreEqual(1, (int)Get(_menu, "_selectedStartOption"),
                "Через настоящий StartScreenController.Update джойстиком по-прежнему нельзя " +
                "выбрать ТРЕНИРОВКУ, пока рука лежит над датчиком высоты. Это и есть живой " +
                "баг основательницы: наклон, зажатый навсегда, держит общий замок " +
                "горизонтали, и стик до значения строки не доходит. У датчиков и у стика " +
                "должны быть РАЗНЫЕ замки — смотри ApplyMenuStickHorizontalNavLock.");
        }

        /// <summary>The same pinned axis, on the row the founder named first.</summary>
        [Test]
        public void CabinetStick_ReachesTwoPlayers_WhileAHandRestsOverAHeightSensor()
        {
            Set(_menu, "_row", PlayersRow);
            RestAHandOverTheLeftSensor();

            MenuFrame();

            // As above: the opening lean spends one step of its own. Put the row back on
            // 1 ИГРОК and ask the stick for 2 ИГРОКА with the lean still held.
            Set(_menu, "_selectedPlayers", 1);
            MirrorUpdateVisualsPlayerRight();
            TapStickRight();

            Assert.AreEqual(2, Players(),
                "Джойстик не переключает строку ИГРОКИ, пока рука лежит над датчиком высоты — " +
                "«2 ИГРОКА» недостижимы. Причина та же, что у соседнего теста про ТРЕНИРОВКУ.");
        }

        /// <summary>
        /// The lean the cabinet leaves us in must cost exactly one step, not a step every
        /// frame: the de-bounce has to survive being split in two.
        /// </summary>
        [Test]
        public void AHeldSensorLean_StillCountsOnce_NotEveryFrame()
        {
            Set(_menu, "_row", LanesRow);
            RestAHandOverTheLeftSensor();

            MenuFrame();
            int afterFirstFrame = (int)Get(_menu, "_selectedLanes");

            for (int i = 0; i < 5; i++)
                MenuFrame();

            Assert.AreEqual(afterFirstFrame, (int)Get(_menu, "_selectedLanes"),
                "Зажатый наклон рук снова листает значение строки каждый кадр. Замок " +
                "ApplyMenuHorizontalNavLock существует ровно против этого — разделяя его " +
                "надвое, не потеряй сам дребезг.");
        }

        /// <summary>
        /// Same for the stick: one push, one step, however long it is held.
        /// </summary>
        [Test]
        public void AHeldStick_StillCountsOnce_NotEveryFrame()
        {
            Set(_menu, "_row", LanesRow);
            int before = (int)Get(_menu, "_selectedLanes");

            PushStick(Vector2.right);
            for (int i = 0; i < 6; i++)
                MenuFrame();

            Assert.AreEqual(before + 1, (int)Get(_menu, "_selectedLanes"),
                "Зажатый вбок джойстик снова листает полосы каждый кадр вместо одного шага — " +
                "собственный замок стика (ApplyMenuStickHorizontalNavLock) потерян.");
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
            string update = MenuUpdateBody();

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

            // The order is load-bearing and not self-evident from the call site:
            // AppendMenuJoystickNav is where the menu's own stick tracker is polled, and
            // the other two only READ the edges that poll refreshes. Swapped, the cursor
            // answers the stick a frame late on every row — and nothing else here would
            // notice, because the tests above replay this order by hand rather than
            // reading it off Update.
            const string orderBroke =
                "Порядок вызовов в StartScreenController.Update разъехался: " +
                "AppendMenuJoystickNav обязан идти ПЕРВЫМ — именно он опрашивает стик " +
                "(_menuStick.Poll), а UpdateMenuJoystickUp и UpdateMenuDownHold только " +
                "читают снятые им фронты. Иначе меню отвечает на джойстик с опозданием " +
                "на кадр, и тесты рядом этого не покажут: они воспроизводят порядок " +
                "Update руками.";
            // Full call text, not the bare method name: Update's own comments mention
            // these methods by name, and matching prose would order the comments.
            int nav = update.IndexOf("AppendMenuJoystickNav(ref left, ref right)", System.StringComparison.Ordinal);
            Assert.Less(nav, update.IndexOf("UpdateMenuJoystickUp(ref up)", System.StringComparison.Ordinal), orderBroke);
            Assert.Less(nav, update.IndexOf("UpdateMenuDownHold(ref down)", System.StringComparison.Ordinal), orderBroke);

            // And the other half of the order, which is what the founder's bug was made of:
            // the sensor/key family is de-bounced BEFORE the stick joins the same pair of
            // booleans. Merge first and de-bounce once afterwards — the shape this code had
            // — and a lean that is never released (one hand over a sensor, nothing over the
            // other: how the hub launches this game) swallows every push of the stick.
            int sensorLock = update.IndexOf("ApplyMenuHorizontalNavLock(ref left, ref right)", System.StringComparison.Ordinal);
            Assert.Greater(sensorLock, 0,
                "StartScreenController.Update больше не вызывает ApplyMenuHorizontalNavLock — " +
                "дребезг горизонтали в меню пропал: зажатый наклон рук снова будет листать " +
                "значение строки каждый кадр.");
            Assert.Less(sensorLock, nav,
                "ApplyMenuHorizontalNavLock снова стоит ПОСЛЕ AppendMenuJoystickNav, то есть " +
                "один замок опять накрывает и датчики, и джойстик. Именно так «2 ИГРОКА» и " +
                "«ТРЕНИРОВКА» стали недостижимы на стойке: рука, лежащая над датчиком " +
                "высоты, — это наклон, зажатый навсегда, и он глушил стик. У стика свой " +
                "замок внутри AppendMenuJoystickNav (ApplyMenuStickHorizontalNavLock).");
        }

        /// <summary>
        /// StartScreenController.Update()'s body, brace-matched rather than a fixed window
        /// of characters: the method grows with every menu feature, and a window that
        /// quietly stops reaching the joystick calls turns the guard above into a no-op.
        /// </summary>
        private static string MenuUpdateBody()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "LadyBug/Scripts/lady_bug/StartScreenController.cs"));
            int updateAt = source.IndexOf("private void Update()", System.StringComparison.Ordinal);
            Assert.Greater(updateAt, 0,
                "StartScreenController.Update() не найден — меню перестало обновляться " +
                "из Update, перенацель этот сторож на то, что пришло на смену.");

            int open = source.IndexOf('{', updateAt);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(open, i - open + 1);
            }

            Assert.Fail("StartScreenController.Update() не закрывается — разбор исходника сломан.");
            return null;
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

            // Update's own order, in full. The two de-bounce latches used to be one, and
            // replaying the frame WITHOUT them is what let this file stay green while the
            // menu's horizontal axis was dead on the cabinet: every deadlock in this file
            // lives inside ApplyMenuHorizontalNavLock / ApplyMenuStickHorizontalNavLock,
            // so a frame that skips them proves nothing about the screen the player sees.
            Call(_menu, "UpdateMenuSensorFlapState");

            var board = new object[] { false, false, false };
            typeof(StartScreenController)
                .GetMethod("AppendMenuCombinedBoardNav", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_menu, board);
            var nav = new object[] { board[0], board[1] };

            typeof(StartScreenController)
                .GetMethod("ApplyMenuHorizontalNavLock", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_menu, nav);

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
                Call(_menu, "RestoreMenuGestureMode");
                MirrorUpdateVisualsPlayerRight();
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

        /// <summary>
        /// The one thing UpdateVisuals does that the stick can feel: PlayerRight is on the
        /// road only in 2-player mode (pinned by TheOnePlayerMenu_DeactivatesPlayerRight).
        /// It matters here because the player flips that switch WITH THE STICK DEFLECTED —
        /// choosing 1 ИГРОК switches PlayerRight off in the same frame the stick is pushed
        /// sideways — and a JoystickInput switched off mid-deflection stops ticking with
        /// "held" still set. Leaving this out of the replay hides exactly that.
        /// </summary>
        private void MirrorUpdateVisualsPlayerRight()
        {
            if (_stickOnPlayerRight != null)
                _stickOnPlayerRight.gameObject.SetActive((int)Get(_menu, "_selectedPlayers") == 2);
        }

        private static void PushStick(Vector2 deflection)
        {
            ArcadeLauncherStub.SetJoystick(deflection);
        }

        /// <summary>
        /// One frame driven by the menu's own Update, with only the readers Unity itself
        /// would have ticked first. No replay, no hand-picked call order.
        /// </summary>
        private void RealMenuFrame()
        {
            Call(_serial, "Update");
            foreach (JoystickInput stick in new[] { _stickOnPlayerLeft, _stickOnPlayerRight })
                if (stick != null && stick.isActiveAndEnabled)
                    Call(stick, "Update");
            Call(_menu, "Update");
        }

        /// <summary>One deliberate sideways push and release — what a player actually does.</summary>
        private void TapStickRight()
        {
            PushStick(Vector2.right);
            MenuFrame();
            PushStick(Vector2.zero);
            MenuFrame();
        }

        /// <summary>
        /// The pose the arcade hub hands this game over in: one hand over the left height
        /// sensor (normalized 1 = hand right at it), nothing over the right one.
        /// </summary>
        private static void RestAHandOverTheLeftSensor()
        {
            ArcadeLauncherStub.SetHeights(1f, 0f);
        }

        private int Row()
        {
            return (int)Get(_menu, "_row");
        }

        private int Players()
        {
            return (int)Get(_menu, "_selectedPlayers");
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
