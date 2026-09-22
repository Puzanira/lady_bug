using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// Pre-game menu: pick 1 or 2 players, lane count, then confirm
// СТАРТ/ТРЕНИРОВКА with a 5-second hold-down. Help text and the
// bottom-right status line follow auto-detected hardware (gesture board /
// joystick) after a short probe window.
public class StartScreenController : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private GameObject[] carouselPages;
    [SerializeField] private GameObject carouselBackground;

    [SerializeField] private Image option1Bg;
    [SerializeField] private Image option2Bg;
    [SerializeField] private Text option1Text;
    [SerializeField] private Text option2Text;
    [SerializeField] private Outline optionsRowOutline;
    [SerializeField] private Image optionsRowBg;

    [SerializeField] private Image[] laneOptionBgs;
    [SerializeField] private Text[] laneOptionTexts;
    [SerializeField] private Outline lanesRowOutline;
    [SerializeField] private Image lanesRowBg;

    // Legacy serialized refs from an old controller-selection row — hidden
    // at runtime; kept so existing scenes deserialize until the next rebuild.
    [SerializeField] private Image controller1Bg;
    [SerializeField] private Image controller2Bg;
    [SerializeField] private Text controller1Text;
    [SerializeField] private Text controller2Text;
    [SerializeField] private Outline controllerRowOutline;
    [SerializeField] private Image controllerRowBg;

    [SerializeField] private Text controllerStatusText;
    // One hint per menu row, shown only while that row is selected — replaces
    // the single bottom-right block that used to describe the whole menu.
    [SerializeField] private Text rowHintPlayers;
    [SerializeField] private Text rowHintLanes;
    [SerializeField] private Text rowHintStart;

    // The two per-player rows on the ВЫБОР В МЕНЮ carousel page. Baked with
    // the keyboard mapping by SceneSetup (that page is built long before any
    // board is polled) and rewritten here once a controller is detected —
    // otherwise the upfront instructions tell a player sitting at the real
    // cabinet to press keys that are not in front of them.
    [SerializeField] private Text menuSelectionPlayer1Row;
    [SerializeField] private Text menuSelectionPlayer2Row;
    [SerializeField] private Text menuConfirmCountdownText;

    [SerializeField] private Image startBg;
    [SerializeField] private Text startText;
    [SerializeField] private Image trainingBg;
    [SerializeField] private Text trainingText;
    [SerializeField] private Outline startOutline;
    [SerializeField] private Image startRowBg;

    // Placeholder screen for ТРЕНИРОВКА (genuinely empty for now, see
    // SceneSetup.CreateStartScreen) — this menu just switches to it and
    // watches for the hold-to-exit gesture back, same as DuckToExitController
    // does for quitting a real run, but simpler (no confirm dialog, just
    // straight back to this menu).
    [SerializeField] private GameObject trainingCanvasRoot;
    [SerializeField] private Text trainingExitCountdownText;
    // Same 2-phase feel as DuckToExitController's own real-game exit —
    // first TrainingExitSilentPhase seconds show nothing at all, then a
    // visible 5,4,3,2,1 countdown for TrainingExitCountdownPhase more.
    private const float TrainingExitSilentPhase = 3f;
    private const float TrainingExitCountdownPhase = 5f;
    private float _trainingHoldTimer;

    // ТРЕНИРОВКА is this carousel only — the same trick-instruction pages
    // that used to be part of the general upfront carousel (everyone saw
    // them whether they cared or not), moved so only someone who actually
    // picked training sees them. Flapping must not advance anywhere (it's
    // what you're practicing); exit is hold-down 3 s silent + 5 s countdown
    // back to the main menu.
    [SerializeField] private GameObject trickCarouselCanvasRoot;
    [SerializeField] private GameObject[] trickCarouselPages;
    [SerializeField] private GameObject trickCarouselBackground;
    [SerializeField] private Text trickCarouselExitCountdownText;
    private int _lastTrickPage = -1;
    private float _trickPageDwellElapsed;
    private float _trickExitHoldTimer;

    // "ВАШИ ДЕЙСТВИЯ" live-reaction bugs, one pair built per gesture/trick
    // page (see SceneSetup.CreateLiveBugPreview) — each already
    // activates/deactivates along with its own page, no separate stage
    // toggle needed. The player-2 one on every page still follows the
    // 1-player/2-player choice, same as the real playerLeft.
    [SerializeField] private GameObject[] trainingPreviewLeftBugs;

    [SerializeField] private Text notImplementedText;

    [SerializeField] private GameObject playerRight;
    [SerializeField] private GameObject playerLeft;
    [SerializeField] private GestureInput gestureRight;
    [SerializeField] private GestureInput gestureLeft;

    // "Датчики" means an asymmetric pair, not two sets of hand sensors:
    // player 1 (left) reads real hand-distance sensors, player 2 (right)
    // reads their own physical joystick board instead — see
    // JoystickInput/JoystickSerial and ArduinoFirmware/Joystick. Was the
    // other way around (sensors on the right, joystick on the left) — per
    // feedback, swapped to match the actual physical rig layout being
    // built (sensors on the left, joystick on the right).
    [SerializeField] private JoystickInput joystickLeft;
    [SerializeField] private JoystickInput joystickRight;

    // Per-player КЛАВИШИ/ЖЕСТЫ gameplay HUD (CreateGesturePanel) — hidden
    // while this menu is up (they're feedback for actual play, not menu
    // chrome) and revealed once BeginGame fires.
    [SerializeField] private GameObject gestureCanvasRight;
    [SerializeField] private GameObject gestureCanvasLeft;

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private MenuMusicRotator menuMusic;
    [SerializeField] private float musicFadeOutDuration = 2.5f;

    private static readonly Color SelectedColor = new Color(0.2f, 0.75f, 0.25f, 0.9f);
    private static readonly Color UnselectedColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    private static readonly Color FocusOutline = new Color(1f, 0.85f, 0.15f);
    private static readonly Color IdleOutline = Color.gray;
    // Row background actually tints yellow while that row has focus now
    // (previously only the outline did — the fill stayed a near-invisible
    // 0.05-alpha white regardless of focus, which read as "no highlight
    // at all"). Start used to have its own separate, dim brownish
    // "StartFocusColor" for this — now it's just another row, so its
    // background follows the same yellow-when-focused rule as the other
    // two, and the button INSIDE it turns SelectedColor green instead.
    private static readonly Color RowFocusColor = new Color(1f, 0.85f, 0.15f, 0.22f);
    private static readonly Color RowIdleColor = new Color(1f, 1f, 1f, 0.05f);

    private static readonly Color DisabledColor = new Color(0.1f, 0.1f, 0.1f, 0.55f);
    private static readonly Color DisabledTextColor = new Color(0.45f, 0.45f, 0.45f);

    private const int RowCount = 3; // 0 = players, 1 = lanes, 2 = start
    private const int PlayersRowIndex = 0;
    private const int LanesRowIndex = 1;
    private const int StartRowIndex = 2;
    private const int LaneOptionCount = RoadLayout.MaxLaneCount;

    private int _selectedPlayers = 2;
    private int _selectedLanes; // 0-based index → lane count = index + 1
    private int _selectedStartOption; // 0 = СТАРТ, 1 = ТРЕНИРОВКА
    private int _row;
    private bool _useHardwareInput;
    private float _controllerPollTimer;
    private float _controllerDetectElapsed;
    private bool _controllerDetectionSettled;
    private const float ControllerDetectDuration = 4f;
    private const string ControllerDetectBase = "КОНТРОЛЛЕР";
    private const int ControllerDetectDotMin = 3;
    private const int ControllerDetectDotMax = 20;
    private const float ControllerDetectDotInterval = 0.45f;
    private int _controllerDetectDotCount = ControllerDetectDotMin;
    private float _controllerDetectDotTimer;
    private int _appliedPreviewLaneCount;
    private int _appliedPreviewPlayers;
    private bool _roadPreviewApplied;

    // Joystick/menu: short up = row up; on upper rows down moves immediately;
    // on the start row hold down 5s = confirm.
    private const float MenuConfirmHold = 5f;
    private const float MenuJoystickUpTapMax = 0.35f;
    // Floor for every page's dwell time — animated pages (arch/ring trick,
    // gesture diagrams) can ask for longer via GetPageDwellDuration so a
    // slower carousel here doesn't also stretch their fixed-length loops
    // out further than they already run.
    private const int NoCarouselPage = -2; // sentinel distinct from _lastCarouselPage's initial -1

    // Per-row hints. Arrows are U+2190…U+2193 (← → ↑ ↓) on purpose: ComicCAT,
    // the font every UI element here uses, carries those four but NOT the
    // solid triangles ◀ ▶ ▲ ▼ — checked against the font's own cmap. Those
    // would come out as blank boxes on the cabinet, invisibly to anyone
    // reading this code.
    //
    // Both value rows (players, lanes) carry the SAME two lines: what changes
    // the value, and what moves off to another row. Nothing about "which way
    // do I press" is obvious on a cabinet, and it is the same answer on both
    // rows, so saying it twice beats making the player remember it.
    //
    // The hardware wording names the CONTROLS, not keyboard letters — on the
    // cabinet there is no keyboard to look at. Up is a flap of both hands and
    // down is a duck (GestureInput), which is why those two aren't spelled as
    // "↑ ↓ датчики": the sensor gesture for each direction is a different
    // motion and has to be named separately.
    private const string RowHintValueKeyboard =
        "← →  ИЗМЕНИТЬ — A D · J L\n"
        + "↑ ↓  НА ДРУГУЮ СТРОКУ — W S · I K";

    private const string RowHintValueHardware =
        "← →  ИЗМЕНИТЬ — НАКЛОН РУК\n"
        + "↑ ↓  НА ДРУГУЮ СТРОКУ — ВЗМАХ · ПРИСЕД\n"
        + "ИЛИ ДЖОЙСТИК В ЭТИ ЖЕ СТОРОНЫ";

    // Начинается с «ВЫБОР» — эта строка не меняет значение, а подтверждает
    // выбранное, и без слова подсказка читалась как ещё одна пара «чем
    // переключать».
    private const string RowHintStartHardware =
        "ВЫБОР — ЗЕЛЁНАЯ КНОПКА\n"
        + "ИЛИ ДАТЧИКИ ВЫСОТЫ ЗАЖАТЬ ВНИЗ НА 5 СЕК";

    private const string RowHintStartKeyboard =
        "ВЫБОР — ЗАЖАТЬ ВНИЗ НА 5 СЕК";

    // Rows on the ВЫБОР В МЕНЮ carousel page. Hardware wording matches the
    // УПРАВЛЕНИЕ page built by SceneSetup (ИГРОК 1 — ДАТЧИКИ, ИГРОК 2 —
    // ДЖОЙСТИК), so the two instruction screens agree with each other.
    private const string MenuSelectionPlayer1Keyboard = "ИГРОК 1: WASD";
    private const string MenuSelectionPlayer2Keyboard = "ИГРОК 2: IJKL";
    private const string MenuSelectionPlayer1Hardware = "ИГРОК 1: ДАТЧИКИ РУК";
    private const string MenuSelectionPlayer2Hardware = "ИГРОК 2: ДЖОЙСТИК";

    private int _lastCarouselPage = -1;
    private float _pageDwellElapsed;

    private PlayerController _rightController;

    // Edge-detect state for menu-only gestures that GestureInput doesn't
    // expose as a "just happened" signal (LeanLeft/RightDown already are).
    private bool _prevDuckRight, _prevDuckLeft;

    private float _menuDownHoldTimer;
    private bool _menuDownConfirmTriggered;
    private bool _revealed;
    private bool _prevMenuDownHeld;
    private bool _menuHorizontalNavLocked;
    private float _joystickUpHoldTimer;
    // Reset alongside the other joystick-hold state but never read back — kept as
    // the author left it so the next upstream merge has nothing to reconcile.
#pragma warning disable CS0414 // assigned only; see above
    private bool _joystickUpConfirmTriggered;
#pragma warning restore CS0414
    private bool _prevJoystickUpHeld;

    // CombinedBoard menu nav — lean edges as fallback when GestureInput hasn't
    // picked up yet (flap uses JumpDown via gestureLeft once enabled).
    private bool _menuCombinedPrevLeanLeftHeld;
    private bool _menuCombinedPrevLeanRightHeld;
    private float _menuSuppressFlapUntil;

    // In the cabinet the sensible default is not the author's dev default: the
    // station is ONE player, with a single HeightA/HeightB sensor pair on one combo
    // board. Preselect 1 player so someone who just walks up and holds the stick
    // down gets a playable run, instead of a 2-player game with a dead second slot.
    //
    // Nothing else needs forcing here anymore: since the author replaced the manual
    // КЛАВИАТУРА/ДАТЧИКИ row with auto-detection (IsHardwareConnected), the cabinet
    // selects hardware input by itself — both serial readers report IsConnected off
    // the ArcadeInput facade (see their UseArcadeFacade seams), so the stick and the
    // hand sensors drive this menu through the author's own joystick/gesture paths.
    // Standalone, where the facade is absent, this is a no-op.
    private void ApplyArcadeDefaults()
    {
        if (!ArcadeControlsReader.Available)
            return;

        _selectedPlayers = 1;
    }

    // The cabinet's action buttons confirm. The menu's confirm gesture is
    // "hold DOWN" (instant row-advance on the upper rows, 5-second hold on the
    // start row — see UpdateMenuDownHold), so the buttons feed that same channel:
    // tap to walk down the rows, hold on СТАРТ/ТРЕНИРОВКА to commit. Inert
    // standalone.
    private static bool ArcadeConfirmHeld()
        => ArcadeControlsReader.GreenHeld || ArcadeControlsReader.RedHeld;

    private void Awake()
    {
        ApplyArcadeDefaults();

        // Gameplay stays inert (players unresponsive, road stopped, nothing
        // spawns — SpeedController holds at 0) until a mode is confirmed.
        SetPlayerControlEnabled(playerRight, false);
        SetPlayerControlEnabled(playerLeft, false);

        if (playerRight != null)
            _rightController = playerRight.GetComponent<PlayerController>();

        // Score/tricks HUD off on the menu; gesture debug readout stays on
        // when hardware is connected so sensor mm/actions are visible while
        // navigating the pre-game screen.
        HideScoreHudShowGestureDebug();

        HideLegacyControllerRow();
        EnsureLaneRowUI();
        EnsureControllerStatusText();
        EnsureMenuConfirmCountdownText();

        if (_rightController != null)
            _selectedLanes = Mathf.Clamp(_rightController.LaneCount - 1, 0, LaneOptionCount - 1);
        if (_selectedPlayers == 2 && _selectedLanes == 0)
            _selectedLanes = 1;

        _controllerDetectElapsed = 0f;
        _controllerDetectionSettled = false;
        _controllerDetectDotCount = ControllerDetectDotMin;
        _controllerDetectDotTimer = 0f;
        RefreshControllerDetection();
        RestoreMenuGestureMode();
        UpdateVisuals();
        UpdateCarousel();

        // Normally IntroSequence.Finish() uncovers this menu and calls
        // OnRevealed() (plus PlayMusic() for slot 0). But the loader/intro
        // pair can be switched off in SceneSetup for quicker test runs, and
        // then nobody would ever make those calls — the menu would sit
        // revealed, silent, on whatever carousel page happened to be up.
        //
        // So: only self-start when no IntroSequence exists to do it for us.
        // Inactive ones count — the intro canvases are built deactivated and
        // only wake up once a game control is held, so a search that skipped
        // inactive objects would find nothing and start the music underneath
        // the loader.
        //
        // Inside the shared arcade-cabinet launcher the intros are in the scene
        // but nothing will ever run them: the launcher already showed the player
        // its own attract screen and hold-to-launch animation, so
        // LoaderScreenController.Awake stands down and never calls
        // BeginConfirmHold — leaving this menu waiting forever for a Finish()
        // that isn't coming. Counting the intros alone would get that case
        // wrong (music would never start, and the carousel would come up on
        // whatever page it had silently cycled to), so ask the same question
        // the loader asked and self-start here as well.
        if (ArcadeControlsReader.InsideArcadeLauncher
            || FindObjectsByType<IntroSequence>(FindObjectsInactive.Include).Length == 0)
        {
            PlayMusic();
            OnRevealed();
        }
    }

    // Called by IntroSequence once the flower-fill/countdown screen
    // finishes and this menu is actually revealed — see the Awake comment
    // above for why it doesn't just start here.
    public void PlayMusic()
    {
        if (menuMusic != null)
            menuMusic.Play();
        else if (musicSource != null)
            musicSource.Play();
    }

    // Also called by IntroSequence.Finish(), for every one of the loader's
    // 7 game slots (not just БК's own PlayMusic) — the carousel's own
    // PreGameScreenTiming.PageDwellSeconds pause/dwell timing runs on Time.time from scene load,
    // but this menu can sit hidden behind the loader + a full intro
    // sequence (~15-20s) before a player ever actually sees it. Without
    // this reset, the carousel silently cycles the whole time it's hidden,
    // so by the time it's finally revealed it can already be several pages
    // past ЦЕЛЬ (e.g. showing a leaderboard first) instead of starting
    // fresh. Resetting right at reveal time guarantees page 0 (ЦЕЛЬ) is
    // what's actually on screen the instant it becomes visible.
    public void OnRevealed()
    {
        // The one reliable "the menu is the screen now" signal: this canvas is
        // active the whole time the loader and intro are up (they simply cover
        // it, see SceneSetup's comment at CreateAllIntroScreens), so Update
        // runs behind them and nothing else here distinguishes the two states.
        // The exit button needs that distinction — on the attract screen it
        // must not quit the cabinet's app.
        _revealed = true;

        // Shows page 0 synchronously, right here, instead of just resetting
        // _lastCarouselPage and waiting for the next Update — Finish() (the
        // caller) makes this menu's own canvas visible immediately, in the
        // same breath, so whatever ShowCarouselPageGeneric last left active
        // (some page well past ЦЕЛЬ, from cycling silently in the
        // background) would otherwise render for a stray frame or more
        // before Update ever got a chance to notice and correct it —
        // exactly the brief "wrong page flashes first" glitch this was
        // meant to fix in the first place.
        if (carouselPages != null && carouselPages.Length > 0)
            ShowCarouselPageGeneric(carouselPages, carouselBackground, ref _lastCarouselPage, ref _pageDwellElapsed, 0);
    }

    private void Update()
    {
        if (trickCarouselCanvasRoot != null && trickCarouselCanvasRoot.activeSelf)
        {
            UpdateTrickCarousel();
            return;
        }

        if (trainingCanvasRoot != null && trainingCanvasRoot.activeSelf)
        {
            UpdateTrainingScreen();
            return;
        }

        // SYSTEM button on the menu — or Esc, when no board is connected —
        // backs out to the attract screen. It used to quit the application,
        // on the reasoning that nothing sat above the menu; the loader lives
        // in this same project, so there is somewhere to go back TO.
        //
        // Two gates, and both are load-bearing. _revealed keeps the attract
        // screen safe: the loader and the intro merely COVER this canvas, so
        // Update is already running behind them, and without it this would
        // fire while the loader is the screen on show. IsRunning keeps the run
        // safe: this script also keeps running with just its Canvas component
        // disabled once the game starts (see BeginTraining's comment), where
        // the button must open the quit dialog instead.
        if (JoystickSerial.SystemMenuDown
            && _revealed
            && (SpeedController.Instance == null || !SpeedController.Instance.IsRunning))
        {
            ReturnToLoaderScreen();
            return;
        }

        UpdateCarousel();

        if (!_controllerDetectionSettled)
        {
            _controllerDetectElapsed += Time.deltaTime;
            if (_controllerDetectElapsed >= ControllerDetectDuration)
            {
                _controllerDetectionSettled = true;
                RefreshControllerDetection();
            }
            else if (!_useHardwareInput)
            {
                _controllerDetectDotTimer += Time.deltaTime;
                if (_controllerDetectDotTimer >= ControllerDetectDotInterval)
                {
                    _controllerDetectDotTimer = 0f;
                    _controllerDetectDotCount++;
                    if (_controllerDetectDotCount > ControllerDetectDotMax)
                        _controllerDetectDotCount = ControllerDetectDotMin;
                    UpdateControllerStatusText();
                }
            }
        }

        _controllerPollTimer += Time.deltaTime;
        if (_controllerPollTimer >= 0.5f)
        {
            _controllerPollTimer = 0f;
            RefreshControllerDetection();
        }

        // Must precede every MenuSensor*Held read this frame — see its own comment.
        UpdateMenuSensorFlapState();

        bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.J);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.L);
        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.I);
        bool down = false; // S/K handled by UpdateMenuDownHold — edge on release only, not press

        // Gesture nav: lean L/R, flap up (not "hands held up"), duck down.
        left |= IsLeanLeftDown(gestureRight) || IsLeanLeftDown(gestureLeft);
        right |= IsLeanRightDown(gestureRight) || IsLeanRightDown(gestureLeft);
        up |= IsFlapDown(gestureRight) || IsFlapDown(gestureLeft);
        AppendMenuCombinedBoardNav(ref left, ref right, ref up);
        AppendMenuJoystickNav(ref left, ref right);
        ApplyMenuHorizontalNavLock(ref left, ref right);
        UpdateMenuJoystickUp(ref up);
        UpdateMenuDownHold(ref down);

        if (MenuSensorDuckHeld() || Time.time < _menuSuppressFlapUntil)
            up = false;

        if (left || right)
        {
            if (_row == PlayersRowIndex)
            {
                _selectedPlayers = _selectedPlayers == 1 ? 2 : 1;
                if (_selectedPlayers == 2 && _selectedLanes == 0)
                    _selectedLanes = 1;
                RestoreMenuGestureMode();
            }
            else if (_row == LanesRowIndex)
            {
                int delta = right ? 1 : -1;
                int minLane = MinSelectableLaneIndex();
                _selectedLanes = Mathf.Clamp(_selectedLanes + delta, minLane, LaneOptionCount - 1);
            }
            else if (_row == StartRowIndex)
            {
                _selectedStartOption = _selectedStartOption == 0 ? 1 : 0;
            }

            UpdateVisuals();
        }

        if (down)
            MoveRow(1);
        else if (up)
            MoveRow(-1);
    }

    // Duck-held (real gesture or the same Down/S keys the menu itself
    // reads) for TrainingExitSilentPhase + TrainingExitCountdownPhase —
    // mirrors DuckToExitController's own 2-phase hold-to-confirm feel
    // exactly (silent first, then a visible countdown), just without its
    // confirm dialog: this screen has nothing on it to lose, so holding
    // down all the way just takes you straight back to the menu.
    private void UpdateTrainingScreen()
    {
        if (JoystickSerial.SystemMenuDown)
        {
            _trainingHoldTimer = 0f;
            if (trainingExitCountdownText != null)
                trainingExitCountdownText.gameObject.SetActive(false);
            ExitTraining();
            return;
        }

        bool holding = AreAllActivePlayersHoldingTrainingExit();

        if (!holding)
        {
            _trainingHoldTimer = 0f;
            if (trainingExitCountdownText != null)
                trainingExitCountdownText.gameObject.SetActive(false);
            return;
        }

        _trainingHoldTimer += Time.deltaTime;

        if (_trainingHoldTimer < TrainingExitSilentPhase)
        {
            if (trainingExitCountdownText != null)
                trainingExitCountdownText.gameObject.SetActive(false);
        }
        else if (_trainingHoldTimer < TrainingExitSilentPhase + TrainingExitCountdownPhase)
        {
            if (trainingExitCountdownText != null)
            {
                trainingExitCountdownText.gameObject.SetActive(true);
                int secondsLeft = Mathf.CeilToInt(TrainingExitSilentPhase + TrainingExitCountdownPhase - _trainingHoldTimer);
                trainingExitCountdownText.text = secondsLeft.ToString();
            }
        }

        if (_trainingHoldTimer >= TrainingExitSilentPhase + TrainingExitCountdownPhase)
        {
            _trainingHoldTimer = 0f;
            ExitTraining();
        }
    }

    private void BeginTraining()
    {
        HideScoreHudShowGestureDebug();

        // Not canvasRoot.SetActive(false) — this whole script lives on
        // canvasRoot itself (see SceneSetup.CreateStartScreen), so that
        // would disable this component along with it, and Update (which is
        // what's supposed to detect the hold-to-exit on the OTHER screen)
        // would simply stop being called at all. Same trick BeginGame
        // already uses for the same reason: hide the visuals via the
        // Canvas component, keep the GameObject (and this script) alive.
        Canvas startCanvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (startCanvas != null)
            startCanvas.enabled = false;

        ApplyInputScheme();
        ApplyTrainingVisuals();

        // Trick-instruction carousel first, not the live screen directly —
        // see trickCarouselCanvasRoot's own comment.
        if (trickCarouselCanvasRoot != null)
            trickCarouselCanvasRoot.SetActive(true);
        _lastTrickPage = -1;
        _trickPageDwellElapsed = 0f;
        _trickExitHoldTimer = 0f;
    }

    // Hold-down exit backs all the way out to the main menu — see
    // ExitTrickCarouselToMenu. No confirm/advance gesture here: training IS
    // this carousel (flap practice must not trigger a screen change).
    private void UpdateTrickCarousel()
    {
        UpdateCarouselGeneric(trickCarouselPages, trickCarouselBackground, ref _lastTrickPage, ref _trickPageDwellElapsed);

        if (JoystickSerial.SystemMenuDown)
        {
            _trickExitHoldTimer = 0f;
            if (trickCarouselExitCountdownText != null)
                trickCarouselExitCountdownText.gameObject.SetActive(false);
            ExitTrickCarouselToMenu();
            return;
        }

        bool holding = AreAllActivePlayersHoldingTrainingExit();

        // Same 2-phase feel as the live screen's own UpdateTrainingScreen —
        // silent first, then a visible countdown — per feedback there was
        // no on-screen indication at all that the hold-to-exit even existed.
        if (!holding)
        {
            _trickExitHoldTimer = 0f;
            if (trickCarouselExitCountdownText != null)
                trickCarouselExitCountdownText.gameObject.SetActive(false);
            return;
        }

        _trickExitHoldTimer += Time.deltaTime;

        if (_trickExitHoldTimer < TrainingExitSilentPhase)
        {
            if (trickCarouselExitCountdownText != null)
                trickCarouselExitCountdownText.gameObject.SetActive(false);
        }
        else if (_trickExitHoldTimer < TrainingExitSilentPhase + TrainingExitCountdownPhase)
        {
            if (trickCarouselExitCountdownText != null)
            {
                trickCarouselExitCountdownText.gameObject.SetActive(true);
                int secondsLeft = Mathf.CeilToInt(TrainingExitSilentPhase + TrainingExitCountdownPhase - _trickExitHoldTimer);
                trickCarouselExitCountdownText.text = secondsLeft.ToString();
            }
        }

        if (_trickExitHoldTimer >= TrainingExitSilentPhase + TrainingExitCountdownPhase)
        {
            _trickExitHoldTimer = 0f;
            if (trickCarouselExitCountdownText != null)
                trickCarouselExitCountdownText.gameObject.SetActive(false);
            ExitTrickCarouselToMenu();
        }
    }

    private void ExitTrickCarouselToMenu()
    {
        if (trickCarouselCanvasRoot != null)
            trickCarouselCanvasRoot.SetActive(false);
        RestoreMenuGestureMode();
        UpdateMenuGestureDebugHud();
        ResetMenuHorizontalNavLock();
        Canvas startCanvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (startCanvas != null)
            startCanvas.enabled = true;
        UpdateVisuals();
    }

    private void ExitTraining()
    {
        if (trainingCanvasRoot != null)
            trainingCanvasRoot.SetActive(false);
        if (trainingExitCountdownText != null)
            trainingExitCountdownText.gameObject.SetActive(false);
        RestoreMenuGestureMode();
        UpdateMenuGestureDebugHud();
        ResetMenuHorizontalNavLock();
        Canvas startCanvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (startCanvas != null)
            startCanvas.enabled = true;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        UpdateRowHints();

        bool oneSelected = _selectedPlayers == 1;

        // P1 = light ladybug on the left (sensors); P2 = dark on the right
        // (joystick). In 1-player mode only P1 is on the road.
        if (playerLeft != null)
            playerLeft.SetActive(true);
        if (playerRight != null)
            playerRight.SetActive(!oneSelected);

        UpdateTrainingPreviewBugs(!oneSelected);
        UpdateTrainingLiveBugLooks(!oneSelected);
        ApplyPlayerBugVisuals();

        ApplyRoadPreview();

        if (option1Bg != null)
            option1Bg.color = oneSelected ? SelectedColor : UnselectedColor;
        if (option2Bg != null)
            option2Bg.color = oneSelected ? UnselectedColor : SelectedColor;
        if (option1Text != null)
            option1Text.text = (oneSelected ? "[X] " : "[ ] ") + "1 ИГРОК";
        if (option2Text != null)
            option2Text.text = (oneSelected ? "[ ] " : "[X] ") + "2 ИГРОКА";

        UpdateLaneOptionVisuals();

        if (optionsRowOutline != null)
            optionsRowOutline.effectColor = _row == PlayersRowIndex ? FocusOutline : IdleOutline;
        if (optionsRowBg != null)
            optionsRowBg.color = _row == PlayersRowIndex ? RowFocusColor : RowIdleColor;

        if (lanesRowOutline != null)
            lanesRowOutline.effectColor = _row == LanesRowIndex ? FocusOutline : IdleOutline;
        if (lanesRowBg != null)
            lanesRowBg.color = _row == LanesRowIndex ? RowFocusColor : RowIdleColor;
        if (startOutline != null)
            startOutline.effectColor = _row == StartRowIndex ? FocusOutline : IdleOutline;
        if (startRowBg != null)
            startRowBg.color = _row == StartRowIndex ? RowFocusColor : RowIdleColor;

        bool startSelected = _selectedStartOption == 0;
        if (startBg != null)
            startBg.color = startSelected ? SelectedColor : UnselectedColor;
        if (trainingBg != null)
            trainingBg.color = startSelected ? UnselectedColor : SelectedColor;
        if (startText != null)
            startText.text = (startSelected ? "[X] " : "[ ] ") + "СТАРТ";
        if (trainingText != null)
            trainingText.text = (startSelected ? "[ ] " : "[X] ") + "ТРЕНИРОВКА";

        // Any navigation clears the "not implemented" message from a
        // previous attempt to start with sensors selected.
        if (notImplementedText != null)
            notImplementedText.gameObject.SetActive(false);
    }

    // P1 = light left (sensors); P2 = dark right (joystick). Same assignment
    // as the menu and gameplay — hide P2's road bug and carousel LiveBug when
    // only one player is selected.
    private void ApplyTrainingVisuals()
    {
        bool twoPlayers = _selectedPlayers == 2;
        if (playerLeft != null)
            playerLeft.SetActive(true);
        if (playerRight != null)
            playerRight.SetActive(twoPlayers);
        UpdateTrainingPreviewBugs(twoPlayers);
        UpdateTrainingLiveBugLooks(twoPlayers);
        ApplyPlayerBugVisuals();
        UpdateMenuGestureDebugHud();
    }

    private void ApplyPlayerBugVisuals()
    {
        PlayerBugVisuals.ApplyForPlayerCount(_selectedPlayers, playerLeft, playerRight);
    }

    private static void ApplyLiveBugPreviewLook(GameObject bug, string baseName, Color tint)
    {
        if (bug == null)
            return;

        if (!PlayerBugVisuals.TryGetBugTextures(baseName, out Texture2D normal, out Texture2D air1, out Texture2D air2))
            return;

        LiveBugReactionAnimator animator = bug.GetComponent<LiveBugReactionAnimator>();
        if (animator != null)
        {
            animator.ApplyBugLook(normal, air1, air2, tint);
            return;
        }

        RawImage image = bug.GetComponent<RawImage>();
        if (image == null)
            return;

        if (normal != null)
            image.texture = normal;
        image.color = tint;
    }

    // Match road players: P1 always light Bug1; P2 (2P only) dark Bug2.
    private void UpdateTrainingLiveBugLooks(bool twoPlayers)
    {
        if (trainingPreviewLeftBugs == null)
            return;

        foreach (GameObject p1Bug in trainingPreviewLeftBugs)
        {
            if (p1Bug == null)
                continue;

            ApplyLiveBugPreviewLook(p1Bug, "LadyBug1", Color.white);

            if (!twoPlayers)
                continue;

            Transform column = p1Bug.transform.parent;
            if (column == null)
                continue;

            for (int i = 0; i < column.childCount; i++)
            {
                Transform child = column.GetChild(i);
                if (child.gameObject == p1Bug || child.name != "LiveBug")
                    continue;

                ApplyLiveBugPreviewLook(child.gameObject, "LadyBug2", PlayerBugVisuals.PlayerTwoDarkTint);
            }
        }
    }

    // trainingPreviewLeftBugs = P1 (light, sensors) previews; hide P2's dark
    // LiveBug sibling on each training page when only one player is selected.
    private void UpdateTrainingPreviewBugs(bool showPlayerTwo)
    {
        if (trainingPreviewLeftBugs == null)
            return;

        foreach (GameObject p1Bug in trainingPreviewLeftBugs)
        {
            if (p1Bug == null)
                continue;

            p1Bug.SetActive(true);

            Transform column = p1Bug.transform.parent;
            if (column == null)
                continue;

            for (int i = 0; i < column.childCount; i++)
            {
                Transform child = column.GetChild(i);
                if (child.gameObject == p1Bug || child.name != "LiveBug")
                    continue;
                child.gameObject.SetActive(showPlayerTwo);
            }
        }
    }

    private void BeginGame()
    {
        ApplyInputScheme();
        ApplyPlayerBugVisuals();
        RoadGeometryRuntime.Apply(EffectiveLaneCount(), _selectedPlayers);
        _roadPreviewApplied = true;
        _appliedPreviewLaneCount = EffectiveLaneCount();
        _appliedPreviewPlayers = _selectedPlayers;

        SetPlayerControlEnabled(playerLeft, true);
        SetPlayerControlEnabled(playerRight, _selectedPlayers == 2);

        // Gameplay HUD back on for the actual run.
        GameplayHudVisibility.SetGameplayHudVisible(true);

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginGame();

        if (GameTimer.Instance != null)
            GameTimer.Instance.BeginTiming();

        // Menu music had a strong, deliberate opening (per its own asset) so
        // it's fine to start abruptly, but cutting it dead at BeginGame felt
        // jarring — fades to silence over musicFadeOutDuration instead. The
        // canvas can't just SetActive(false) right away like before, though:
        // that would stop this coroutine (and the AudioSource) instantly,
        // same as it does today — so the menu's *visuals* are hidden
        // immediately via the Canvas component instead (a GameObject-active
        // trick doesn't apply to just the renderer), while the GameObject
        // itself — and the fade running on it — stays alive until the fade
        // finishes and deactivates it for real.
        Canvas canvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (canvas != null)
            canvas.enabled = false;
        StartCoroutine(FadeOutMusicThenHide(musicFadeOutDuration));

        enabled = false;
    }

    private IEnumerator FadeOutMusicThenHide(float duration)
    {
        if (menuMusic != null)
            menuMusic.StopRotating();

        if (musicSource != null)
        {
            float startVolume = musicSource.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }
            musicSource.Stop();
            musicSource.volume = startVolume; // restored in case this source ever plays again
        }

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    // Cycles the middle info panel between pages (rules/controls/trick
    // diagrams/gesture diagrams), looping — driven by an elapsed-time
    // counter per page rather than a fixed PreGameScreenTiming.PageDwellSeconds for every page
    // (see GetPageDwellDuration), so an animated page's own loop doesn't
    // get cut off mid-cycle by a shorter global timer. Pages are whole
    // pre-built GameObjects (not just swapped text) so some can be visual
    // diagrams instead of plain text.
    private void UpdateCarousel()
    {
        if (carouselPages == null || carouselPages.Length == 0)
            return;

        if (Time.time < PreGameScreenTiming.PageDwellSeconds)
        {
            if (_lastCarouselPage != NoCarouselPage)
            {
                for (int i = 0; i < carouselPages.Length; i++)
                    if (carouselPages[i] != null)
                        carouselPages[i].SetActive(false);
                if (carouselBackground != null)
                    carouselBackground.SetActive(false);
                _lastCarouselPage = NoCarouselPage;
            }
            return;
        }

        UpdateCarouselGeneric(carouselPages, carouselBackground, ref _lastCarouselPage, ref _pageDwellElapsed);
    }

    // Shared by the main info carousel and the ТРЕНИРОВКА trick-instruction
    // carousel (UpdateTrickCarousel) — advances pages[lastPage] to the next
    // one once its own dwell duration elapses, looping forever. The main
    // carousel's initial PageDwellSeconds pause is handled by its own caller
    // above; the trick carousel has no equivalent (it's already the result
    // of an explicit player action, not the first thing shown at boot).
    private void UpdateCarouselGeneric(GameObject[] pages, GameObject background, ref int lastPage, ref float dwellElapsed)
    {
        if (pages == null || pages.Length == 0)
            return;

        if (lastPage < 0)
        {
            ShowCarouselPageGeneric(pages, background, ref lastPage, ref dwellElapsed, 0);
            return;
        }

        dwellElapsed += Time.deltaTime;
        if (dwellElapsed >= GetPageDwellDuration(pages, lastPage))
            ShowCarouselPageGeneric(pages, background, ref lastPage, ref dwellElapsed, (lastPage + 1) % pages.Length);
    }

    // Always shows every page in order, including a TopResultsPage with
    // nothing on it yet (3 rows of "--", same placeholder any other empty
    // table shows) — previously skipped those entirely, which meant a save
    // with only 2 of the 4 categories played only ever cycled through those
    // 2, with the other 2 never appearing until played at least once.
    private void ShowCarouselPageGeneric(GameObject[] pages, GameObject background, ref int lastPage, ref float dwellElapsed, int rawIndex)
    {
        if (background != null)
            background.SetActive(true);
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null)
                pages[i].SetActive(i == rawIndex);

        lastPage = rawIndex;
        dwellElapsed = 0f;
    }

    // Animated pages report their own natural loop length (see each
    // component's own CycleDuration) so this carousel doesn't cut them off
    // partway through — gesture pages specifically ask for
    // GestureDiagramAnimation.RepeatCount full loops before advancing.
    // Falls back to PreGameScreenTiming.PageDwellSeconds for static pages (checklists,
    // object grids, plain diagrams) that don't have any of these.
    private float GetPageDwellDuration(GameObject[] pages, int pageIndex)
    {
        GameObject page = pages != null && pageIndex >= 0 && pageIndex < pages.Length
            ? pages[pageIndex]
            : null;
        if (page == null)
            return PreGameScreenTiming.PageDwellSeconds;

        ArchTrickAnimation arch = page.GetComponent<ArchTrickAnimation>();
        if (arch != null)
            return Mathf.Max(PreGameScreenTiming.PageDwellSeconds, arch.TotalDisplayDuration);

        RingTrickAnimation ring = page.GetComponent<RingTrickAnimation>();
        if (ring != null)
            return Mathf.Max(PreGameScreenTiming.PageDwellSeconds, ring.TotalDisplayDuration);

        GestureDiagramAnimation gesture = page.GetComponent<GestureDiagramAnimation>();
        if (gesture != null)
            return Mathf.Max(PreGameScreenTiming.PageDwellSeconds, gesture.TotalDisplayDuration);

        TrickDiagramAnimation trick = page.GetComponent<TrickDiagramAnimation>();
        if (trick != null)
            return Mathf.Max(PreGameScreenTiming.PageDwellSeconds, trick.TotalDisplayDuration);

        return PreGameScreenTiming.PageDwellSeconds;
    }

    private static void SetPlayerControlEnabled(GameObject player, bool value)
    {
        if (player == null)
            return;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = value;
    }

    private static void SetGestureEnabled(GestureInput gesture, bool enabled, bool useRealSensors)
    {
        if (gesture == null)
            return;

        gesture.enabled = enabled;
        gesture.UseRealSensors = useRealSensors;
    }

    private static void SetJoystickEnabled(JoystickInput joystick, bool enabled)
    {
        if (joystick == null)
            return;

        joystick.enabled = enabled;
    }

    // Menu: P1 (left, sensors) always on gestureLeft; joystick stays active
    // for menu navigation whenever plugged in. Gameplay uses ApplyInputScheme.
    private void RestoreMenuGestureMode()
    {
        if (!_useHardwareInput)
        {
            SetGestureEnabled(gestureLeft, false, false);
            SetGestureEnabled(gestureRight, false, false);
            SetJoystickEnabled(joystickLeft, false);
            SetJoystickEnabled(joystickRight, false);
            return;
        }

        bool joystickConnected = JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected;
        // Menu navigation has to ride joystickLeft as well, not joystickRight
        // alone: UpdateVisuals deactivates PlayerRight whenever 1 player is
        // selected, and a JoystickInput on an inactive GameObject never runs
        // Update — so its LeftDown/RightDown/UpHeld edges stay false forever no
        // matter how enabled the component is. Symptom: on a 1-player cabinet
        // the stick moved the side HUD indicator (that reads JoystickSerial
        // directly) while the menu cursor and the row values sat still.
        // PlayerLeft is always active here. This is menu-only — both BeginGame
        // and BeginTraining run ApplyInputScheme, which turns joystickLeft back
        // off before anyone can drive P1's bug with it.
        SetJoystickEnabled(joystickLeft, joystickConnected);
        SetJoystickEnabled(joystickRight, joystickConnected);

        if (_selectedPlayers == 2)
        {
            SetGestureEnabled(gestureLeft, true, true);
            if (joystickConnected)
                SetGestureEnabled(gestureRight, false, false);
            else
                SetGestureEnabled(gestureRight, true, true);
        }
        else
        {
            SetGestureEnabled(gestureLeft, true, true);
            SetGestureEnabled(gestureRight, false, false);
        }
    }

    private static bool IsLeanLeftDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanLeftDown;
    private static bool IsLeanRightDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanRightDown;
    private static bool IsLeanLeftHeld(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanLeftHeld;
    private static bool IsLeanRightHeld(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanRightHeld;
    private static bool IsDuckHeld(GestureInput gesture) => gesture != null && gesture.enabled && gesture.DuckHeld;
    // A real joystick (player-right's own hardware now, see joystickRight)
    // needs the same hold-to-exit path gesture already has — without this a
    // joystick-only player (no keyboard fallback in hand) would have no way
    // to back out of training at all.
    private static bool IsDuckHeld(JoystickInput joystick) => joystick != null && joystick.enabled && joystick.DownHeld;

    // Training exit: every active player must hold down together (same rule
    // as DuckToExitController during a real run). Reads hardware directly —
    // PlayerController is disabled during training; P1 duck reads gestureLeft
    // or CombinedBoard mm directly (see IsPlayerOneTrainingExitHeld).
    private const int TrainingSensorDownThresholdMm = 100; // GestureInput.DownThresholdMm

    // Hands the screen back to attract mode. Stops the menu's own music
    // first: the loader starts its playlist as it comes back up, and two
    // rotators playing at once is just noise.
    private void ReturnToLoaderScreen()
    {
        if (menuMusic != null)
            menuMusic.StopRotating();

        // Repack: FindAnyObjectByType, not the obsolete FindObjectOfType the author
        // wrote — and not FindFirstObjectByType either, which is deprecated in its
        // own turn on this editor (both are CS0618, and the cabinet build is kept
        // warning-clean). There is exactly one loader in the scene, so "any" and
        // "first" are the same object. Include inactive: the loader's controller
        // lives on its own object, but the search must not depend on that.
        LoaderScreenController loader =
            FindAnyObjectByType<LoaderScreenController>(FindObjectsInactive.Include);
        if (loader != null)
            loader.ReturnToLoader();
    }

    private bool AreAllActivePlayersHoldingTrainingExit()
    {
        if (_selectedPlayers == 1)
            return IsPlayerOneTrainingExitHeld() || IsPlayerTwoTrainingExitHeld();

        return IsPlayerOneTrainingExitHeld() && IsPlayerTwoTrainingExitHeld();
    }

    private static bool CombinedBoardSendsHandSensors()
    {
        var js = JoystickSerial.Instance;
        return js != null && js.IsConnected && js.HasHandSensors;
    }

    private static bool CombinedBoardSensorDuckHeld()
    {
        var js = JoystickSerial.Instance;
        if (js == null || !js.IsConnected)
            return false;
        int left = js.HandLeftMm;
        int right = js.HandRightMm;
        if (left < 0 || right < 0)
            return false;
        return left <= TrainingSensorDownThresholdMm && right <= TrainingSensorDownThresholdMm;
    }

    private bool IsPlayerOneTrainingExitHeld()
    {
        if (CombinedBoardSendsHandSensors())
            return CombinedBoardSensorDuckHeld();
        if (IsDuckHeld(gestureLeft))
            return true;
        if (!_useHardwareInput && Input.GetKey(KeyCode.S))
            return true;
        return false;
    }

    private bool IsPlayerTwoTrainingExitHeld()
    {
        if (IsDuckHeld(joystickRight) || IsDuckHeld(joystickLeft))
            return true;
        var js = JoystickSerial.Instance;
        if (js != null && js.IsConnected && js.Down)
            return true;
        if (IsDuckHeld(gestureRight))
            return true;
        if (!_useHardwareInput && Input.GetKey(KeyCode.K))
            return true;
        return false;
    }

    // Menu navigation used to have exactly one way to see the stick: a JoystickInput
    // wrapper. A wrapper only produces edges from its own Update, and Update does not
    // run on a disabled component OR on a deactivated GameObject — and UpdateVisuals
    // deactivates PlayerRight for the whole of 1-player mode, which is what the cabinet
    // preselects. So whether the menu could be listed at all came down to one line of
    // RestoreMenuGestureMode switching the OTHER wrapper on (90b3913), with nothing
    // anywhere to say so. A merge with the author's repo put that line back to `false`
    // once already, and the menu went dead on the stick without a single error.
    //
    // This tracker is the floor under that: the menu derives its own stick edges from
    // JoystickSerial — which is a scene-level reader that ticks regardless of which
    // player object is on — so navigation no longer depends on where a wrapper lives or
    // who remembered to enable it. The wrappers stay in the predicates exactly as before;
    // they are now an addition, not the only path. Guarded by _useHardwareInput so the
    // keyboard-only menu behaves precisely as it did.
    private sealed class MenuStickEdges
    {
        public bool UpHeld { get; private set; }
        public bool DownHeld { get; private set; }
        public bool LeftDown { get; private set; }
        public bool RightDown { get; private set; }

        private bool _prevLeft;
        private bool _prevRight;

        public void Poll(bool hardwareMenu)
        {
            JoystickSerial js = JoystickSerial.Instance;
            bool live = hardwareMenu && js != null && js.IsConnected;

            bool left = live && js.Left;
            bool right = live && js.Right;

            UpHeld = live && js.Up;
            DownHeld = live && js.Down;
            LeftDown = left && !_prevLeft;
            RightDown = right && !_prevRight;

            _prevLeft = left;
            _prevRight = right;
        }
    }

    private readonly MenuStickEdges _menuStick = new MenuStickEdges();

    private void AppendMenuJoystickNav(ref bool left, ref bool right)
    {
        // First joystick read of the menu's frame — Update calls this before
        // UpdateMenuJoystickUp and UpdateMenuDownHold, and both of them read the edges
        // refreshed here. Keep it that way if the order in Update ever moves.
        _menuStick.Poll(_useHardwareInput);

        if (JoystickSerial.Instance == null || !JoystickSerial.Instance.IsConnected)
            return;

        left |= IsJoystickLeftDown(joystickRight) || IsJoystickLeftDown(joystickLeft)
            || _menuStick.LeftDown;
        right |= IsJoystickRightDown(joystickRight) || IsJoystickRightDown(joystickLeft)
            || _menuStick.RightDown;
    }

    private bool MenuHorizontalLeftHeld()
    {
        return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.J)
            || MenuSensorLeanLeftHeld()
            || IsJoystickLeftHeld(joystickRight) || IsJoystickLeftHeld(joystickLeft)
            || (JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected && JoystickSerial.Instance.Left);
    }

    private bool MenuHorizontalRightHeld()
    {
        return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.L)
            || MenuSensorLeanRightHeld()
            || IsJoystickRightHeld(joystickRight) || IsJoystickRightHeld(joystickLeft)
            || (JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected && JoystickSerial.Instance.Right);
    }

    private void ApplyMenuHorizontalNavLock(ref bool left, ref bool right)
    {
        if (_menuHorizontalNavLocked)
        {
            left = false;
            right = false;
            if (!MenuHorizontalLeftHeld() && !MenuHorizontalRightHeld())
                _menuHorizontalNavLocked = false;
            return;
        }

        if (left || right)
            _menuHorizontalNavLocked = true;
    }

    private void ResetMenuHorizontalNavLock()
    {
        _menuHorizontalNavLocked = false;
    }

    private void UpdateMenuJoystickUp(ref bool up)
    {
        if (JoystickSerial.Instance == null || !JoystickSerial.Instance.IsConnected)
        {
            ResetJoystickUpHold();
            _prevJoystickUpHeld = false;
            return;
        }

        bool held = IsJoystickUpHeld(joystickRight) || IsJoystickUpHeld(joystickLeft)
            || _menuStick.UpHeld;
        if (held)
            _joystickUpHoldTimer += Time.deltaTime;
        else if (_prevJoystickUpHeld && _joystickUpHoldTimer < MenuJoystickUpTapMax)
            up = true;

        if (!held)
            ResetJoystickUpHold();

        _prevJoystickUpHeld = held;
    }

    // A flap needs two 55 mm swings inside 0.9 s before HandFlapTracker calls
    // it a flap, so the FIRST downstroke lands both hands in the Down zone
    // while _menuSensorFlapping is still false. On the upper rows a down is an
    // instant edge, so that single frame was enough to walk the cursor down at
    // the start of every jump gesture. Requiring the duck to survive longer
    // than a downstroke closes the window the tracker cannot cover.
    private const float MenuSensorDuckMinHold = 0.25f;
    private float _menuSensorDuckHeldFor;

    private void UpdateMenuDownHold(ref bool downEdge)
    {
        if (MenuSensorDuckHeld())
            _menuSensorDuckHeldFor += Time.deltaTime;
        else
            _menuSensorDuckHeldFor = 0f;

        // Keyboard and joystick are unambiguous — no flap can be mistaken for
        // them, so they stay instant.
        bool held = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.K)
            || _menuSensorDuckHeldFor >= MenuSensorDuckMinHold
            || IsJoystickDownHeld(joystickRight) || IsJoystickDownHeld(joystickLeft)
            || _menuStick.DownHeld
            || ArcadeConfirmHeld();

        // Upper rows have nothing to confirm — move down on the first frame
        // "down" is held. Only the start/training row uses hold-to-confirm.
        if (_row != StartRowIndex)
        {
            if (held && !_prevMenuDownHeld)
                downEdge = true;

            if (!held && _prevMenuDownHeld)
                _menuSuppressFlapUntil = Time.time + 0.35f;

            _prevMenuDownHeld = held;
            ResetMenuDownHold();
            return;
        }

        // Nothing held and nothing was held last frame, so there is no hold in
        // flight for the "already confirmed" latch to be protecting against —
        // drop it. The latch is what stops a confirm from firing twice while
        // the player is still holding down, and it is deliberately NOT cleared
        // on the way back from training: whoever left by holding duck is still
        // holding it as the menu reappears, and without the latch that hold
        // would sail past 5 seconds and walk straight back into training. This
        // row is the only place the latch can go stale, since the early return
        // above clears it every frame on the other rows.
        if (!held && !_prevMenuDownHeld && _menuDownConfirmTriggered)
            ResetMenuDownHold();

        // The cabinet's green button confirms the selected row outright — the
        // 5-second hold stays as the path for keyboard, sensors and joystick.
        // Only on this row, exactly like the hold: the rows above have nothing
        // to confirm.
        JoystickSerial panel = JoystickSerial.Instance;
        if (!_menuDownConfirmTriggered && panel != null && panel.GreenButtonDown)
        {
            _menuDownConfirmTriggered = true;
            ConfirmStartRow();
            _prevMenuDownHeld = held;
            return;
        }

        if (held)
        {
            _menuDownHoldTimer += Time.deltaTime;
            if (!_menuDownConfirmTriggered && _menuDownHoldTimer >= MenuConfirmHold)
            {
                _menuDownConfirmTriggered = true;
                ConfirmStartRow();
            }
        }
        else if (_prevMenuDownHeld)
        {
            // Short tap = move row down; a hold (countdown attempt) must not
            // fire navigation when the player releases early.
            if (_menuDownHoldTimer < MenuConfirmHold
                && !_menuDownConfirmTriggered
                && _menuDownHoldTimer < MenuJoystickUpTapMax)
                downEdge = true;
            // Rising hands after a duck often look like a flap — ignore up
            // briefly so "down" doesn't accidentally become "up" on release.
            _menuSuppressFlapUntil = Time.time + 0.35f;
            ResetMenuDownHold();
        }

        UpdateMenuConfirmCountdown(held);
        _prevMenuDownHeld = held;
    }

    // Acting on the start row, whichever way it was confirmed — the hold or
    // the panel's green button. One place, so the two paths can't drift.
    private void ConfirmStartRow()
    {
        if (_selectedStartOption == 0)
            BeginGame();
        else
            BeginTraining();
    }

    private void UpdateMenuConfirmCountdown(bool downHeld)
    {
        if (menuConfirmCountdownText == null)
            return;

        bool show = downHeld
            && _row == StartRowIndex
            && !_menuDownConfirmTriggered
            && _menuDownHoldTimer < MenuConfirmHold;

        menuConfirmCountdownText.gameObject.SetActive(show);
        if (!show)
            return;

        int secondsLeft = Mathf.Clamp(
            Mathf.CeilToInt(MenuConfirmHold - _menuDownHoldTimer),
            1,
            Mathf.CeilToInt(MenuConfirmHold));
        menuConfirmCountdownText.text = secondsLeft.ToString();
        PositionMenuConfirmCountdownOverSelection();
    }

    private RectTransform GetActiveSelectionButtonRect()
    {
        if (_row == PlayersRowIndex)
            return (_selectedPlayers == 1 ? option1Bg : option2Bg)?.rectTransform;
        if (_row == LanesRowIndex && laneOptionBgs != null
            && _selectedLanes >= 0 && _selectedLanes < laneOptionBgs.Length)
            return laneOptionBgs[_selectedLanes]?.rectTransform;
        if (_row == StartRowIndex)
            return (_selectedStartOption == 0 ? startBg : trainingBg)?.rectTransform;
        return null;
    }

    private void PositionMenuConfirmCountdownOverSelection()
    {
        if (menuConfirmCountdownText == null)
            return;

        RectTransform target = GetActiveSelectionButtonRect();
        RectTransform countdownRt = menuConfirmCountdownText.rectTransform;
        if (target == null || countdownRt == null)
            return;

        Canvas canvas = menuConfirmCountdownText.canvas;
        if (canvas == null)
            return;

        countdownRt.SetAsLastSibling();

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
        RectTransform canvasRt = canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, cam, out Vector2 localPoint))
            countdownRt.anchoredPosition = localPoint;
    }

    private void ResetMenuDownHold()
    {
        _menuDownHoldTimer = 0f;
        _menuDownConfirmTriggered = false;
        UpdateMenuConfirmCountdown(false);
    }

    private void ResetJoystickUpHold()
    {
        _joystickUpHoldTimer = 0f;
        _joystickUpConfirmTriggered = false;
    }

    private static bool IsJoystickLeftDown(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.LeftDown;

    private static bool IsJoystickRightDown(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.RightDown;

    private static bool IsJoystickUpHeld(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.UpHeld;

    private static bool IsJoystickLeftHeld(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.LeftHeld;

    private static bool IsJoystickRightHeld(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.RightHeld;

    private static bool IsJoystickDownHeld(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.DownHeld;

    private static bool IsJoystickDownDown(JoystickInput joystick) =>
        joystick != null && joystick.enabled && joystick.DownDown;

    private static bool IsFlapDown(GestureInput gesture) =>
        gesture != null && gesture.enabled && gesture.JumpDown;

    private static bool EdgeDuck(GestureInput gesture, ref bool prev)
    {
        bool now = gesture != null && gesture.enabled && gesture.DuckHeld;
        bool edgeUp = now && !prev;
        prev = now;
        return edgeUp;
    }

    private int MinSelectableLaneIndex() => _selectedPlayers == 1 ? 0 : 1;

    private int EffectiveLaneCount()
    {
        int laneCount = _selectedLanes + 1;
        if (_selectedPlayers == 2 && laneCount < 2)
            laneCount = 2;
        return laneCount;
    }

    // Rebuild road width/dividers and reposition players while the menu is
    // still up — same geometry BeginGame applies, so the background matches
    // the selection before Start is pressed.
    private void ApplyRoadPreview()
    {
        int laneCount = EffectiveLaneCount();
        if (_roadPreviewApplied && laneCount == _appliedPreviewLaneCount && _selectedPlayers == _appliedPreviewPlayers)
            return;

        RoadGeometryRuntime.Apply(laneCount, _selectedPlayers);
        _appliedPreviewLaneCount = laneCount;
        _appliedPreviewPlayers = _selectedPlayers;
        _roadPreviewApplied = true;
    }

    private void UpdateLaneOptionVisuals()
    {
        int minLane = MinSelectableLaneIndex();
        for (int i = 0; i < LaneOptionCount; i++)
        {
            bool disabled = i < minLane;
            bool selected = i == _selectedLanes;
            if (laneOptionBgs != null && i < laneOptionBgs.Length && laneOptionBgs[i] != null)
                laneOptionBgs[i].color = disabled ? DisabledColor : selected ? SelectedColor : UnselectedColor;
            if (laneOptionTexts != null && i < laneOptionTexts.Length && laneOptionTexts[i] != null)
            {
                laneOptionTexts[i].color = disabled ? DisabledTextColor : Color.white;
                laneOptionTexts[i].text = (selected ? "[X] " : "[ ] ") + (i + 1);
            }
        }
    }

    private void ApplyInputScheme()
    {
        if (_useHardwareInput)
        {
            if (_selectedPlayers == 2)
            {
                SetGestureEnabled(gestureLeft, true, true);
                SetJoystickEnabled(joystickLeft, false);

                bool joystickConnected = JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected;
                if (joystickConnected)
                {
                    SetGestureEnabled(gestureRight, false, false);
                    SetJoystickEnabled(joystickRight, true);
                }
                else
                {
                    SetGestureEnabled(gestureRight, true, true);
                    SetJoystickEnabled(joystickRight, false);
                }
            }
            else
            {
                // 1-player: P1 (left, light) on sensors only.
                SetGestureEnabled(gestureLeft, true, true);
                SetJoystickEnabled(joystickLeft, false);
                SetGestureEnabled(gestureRight, false, false);
                SetJoystickEnabled(joystickRight, false);
            }
        }
        else
        {
            SetGestureEnabled(gestureLeft, false, false);
            SetGestureEnabled(gestureRight, false, false);
            SetJoystickEnabled(joystickLeft, false);
            SetJoystickEnabled(joystickRight, false);
        }
    }

    // Clamps, deliberately — no wrap-around. The list is 3 short rows fully
    // visible at once, so wrapping reads as "the cursor jumped somewhere
    // random" rather than as a convenience: pressing down on the last row
    // used to land you back on the first one. It bit "down" specifically
    // because a short down-tap on the start/training row still counts as a
    // nav edge (see UpdateMenuDownHold — it has to, so an aborted
    // hold-to-confirm doesn't get stuck), and that edge then wrapped 2 -> 0.
    private void MoveRow(int delta)
    {
        int next = Mathf.Clamp(_row + delta, 0, RowCount - 1);
        if (next == _row)
            return;

        _row = next;
        UpdateVisuals();
    }

    private void RefreshControllerDetection()
    {
        bool wasHardware = _useHardwareInput;
        _useHardwareInput = IsHardwareConnected();

        if (_useHardwareInput)
            _controllerDetectionSettled = true;

        UpdateControllerStatusText();
        UpdateMenuHelpText();
        UpdateMenuGestureDebugHud();

        if (wasHardware != _useHardwareInput)
            RestoreMenuGestureMode();
    }

    private void HideScoreHudShowGestureDebug()
    {
        GameplayHudVisibility.SetWedgePanelsVisible(false);
        GameplayHudVisibility.SetTricksHudVisible(false);
        UpdateMenuGestureDebugHud();
    }

    private void UpdateMenuGestureDebugHud()
    {
        GameplayHudVisibility.SetGestureHudVisible(_useHardwareInput);
    }

    private bool TryReadCombinedBoardHandMm(out int leftMm, out int rightMm)
    {
        leftMm = -1;
        rightMm = -1;
        if (!CombinedBoardSendsHandSensors())
            return false;

        var js = JoystickSerial.Instance;
        leftMm = GestureInput.SanitizeDistanceMm(js.HandLeftMm);
        rightMm = GestureInput.SanitizeDistanceMm(js.HandRightMm);
        return true;
    }

    // A flap drives both hands through the Down zone on every downstroke, so
    // the raw-distance reads below would see duck (and flickering leans) all
    // the way through a jump gesture. GestureInput already guards against
    // exactly this — while flapping it force-clears DuckHeld/LeanHeld — but
    // the menu bypasses GestureInput and reads the board directly, so it has
    // to make the same guard itself.
    //
    // Symptom this fixes: on the sensors the HUD lit up ПРЫЖОК (that path goes
    // through GestureInput and was clean) while the menu refused to move up —
    // line "if (MenuSensorDuckHeld() ...) up = false;" cancelled it every
    // frame, and UpdateMenuDownHold read the same false duck as a down-tap, so
    // a flap actually walked the cursor DOWN until it hit the last row.
    private readonly GestureInput.HandFlapTracker _menuLeftFlapTracker = new GestureInput.HandFlapTracker();
    private readonly GestureInput.HandFlapTracker _menuRightFlapTracker = new GestureInput.HandFlapTracker();
    private bool _menuSensorFlapping;

    // Observe() mutates the trackers, so this must run exactly once per frame,
    // before anything reads MenuSensorDuckHeld/LeanHeld.
    private void UpdateMenuSensorFlapState()
    {
        if (!TryReadCombinedBoardHandMm(out int leftMm, out int rightMm))
        {
            _menuSensorFlapping = false;
            return;
        }

        _menuSensorFlapping = GestureInput.BothHandsFlapping(
            _menuLeftFlapTracker, _menuRightFlapTracker, leftMm, rightMm);
    }

    private bool MenuSensorDuckHeld()
    {
        if (TryReadCombinedBoardHandMm(out int leftMm, out int rightMm))
            return !_menuSensorFlapping && GestureInput.DuckHeldFromDistances(leftMm, rightMm);

        return IsDuckHeld(gestureLeft) || IsDuckHeld(gestureRight);
    }

    private bool MenuSensorLeanLeftHeld()
    {
        if (TryReadCombinedBoardHandMm(out int leftMm, out int rightMm))
            return !_menuSensorFlapping && GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm);

        return IsLeanLeftHeld(gestureLeft) || IsLeanLeftHeld(gestureRight);
    }

    private bool MenuSensorLeanRightHeld()
    {
        if (TryReadCombinedBoardHandMm(out int leftMm, out int rightMm))
            return !_menuSensorFlapping && GestureInput.LeanRightHeldFromDistances(leftMm, rightMm);

        return IsLeanRightHeld(gestureLeft) || IsLeanRightHeld(gestureRight);
    }

    private void AppendMenuCombinedBoardNav(ref bool left, ref bool right, ref bool up)
    {
        if (!TryReadCombinedBoardHandMm(out int leftMm, out int rightMm))
            return;

        // Same flap guard as the helpers above: mid-flap the two hands are
        // rarely level, so without this every jump gesture also fires a lean
        // edge and the cursor jumps sideways.
        bool leanLeftHeld = !_menuSensorFlapping && GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm);
        bool leanRightHeld = !_menuSensorFlapping && GestureInput.LeanRightHeldFromDistances(leftMm, rightMm);

        if (leanLeftHeld && !_menuCombinedPrevLeanLeftHeld)
            left = true;
        if (leanRightHeld && !_menuCombinedPrevLeanRightHeld)
            right = true;

        _menuCombinedPrevLeanLeftHeld = leanLeftHeld;
        _menuCombinedPrevLeanRightHeld = leanRightHeld;
    }

    // Only the selected row's hint is on screen. Called from both UpdateVisuals
    // (selection moved) and UpdateMenuHelpText (the detected controller
    // changed, which rewrites the copy) — either can happen without the other.
    private void UpdateRowHints()
    {
        string valueCopy = _useHardwareInput ? RowHintValueHardware : RowHintValueKeyboard;

        ApplyRowHint(rowHintPlayers, 0, valueCopy);
        ApplyRowHint(rowHintLanes, 1, valueCopy);
        ApplyRowHint(rowHintStart, StartRowIndex, _useHardwareInput ? RowHintStartHardware : RowHintStartKeyboard);
    }

    private void ApplyRowHint(Text hint, int row, string copy)
    {
        if (hint == null)
            return;

        bool selected = _row == row;
        hint.gameObject.SetActive(selected);
        if (selected)
            hint.text = copy;
    }

    private void UpdateControllerStatusText()
    {
        if (controllerStatusText == null)
            return;

        if (_useHardwareInput)
        {
            controllerStatusText.gameObject.SetActive(true);
            controllerStatusText.text = "КОНТРОЛЛЕР ОК";
            return;
        }

        controllerStatusText.gameObject.SetActive(true);
        controllerStatusText.text = _controllerDetectionSettled
            ? "КОНТРОЛЛЕР НЕ ОБНАРУЖЕН"
            : ControllerDetectBase + new string('.', _controllerDetectDotCount);
    }

    private void UpdateMenuHelpText()
    {
        UpdateRowHints();

        // Same switch on the upfront carousel page, so the instructions a
        // player reads before the menu match the hint under the menu itself.
        if (menuSelectionPlayer1Row != null)
            menuSelectionPlayer1Row.text = _useHardwareInput
                ? MenuSelectionPlayer1Hardware : MenuSelectionPlayer1Keyboard;

        if (menuSelectionPlayer2Row != null)
            menuSelectionPlayer2Row.text = _useHardwareInput
                ? MenuSelectionPlayer2Hardware : MenuSelectionPlayer2Keyboard;
    }

    // Same question as JoystickSerial.NoBoardConnected, asked the other way
    // round. Delegated rather than spelled out twice: the Esc-instead-of-
    // SYSTEM fallback keys off the negative form, and two copies of "is there
    // hardware" would eventually disagree.
    private static bool IsHardwareConnected() => !JoystickSerial.NoBoardConnected;

    private void HideLegacyControllerRow()
    {
        if (controllerRowBg != null)
            controllerRowBg.gameObject.SetActive(false);
    }

    private void EnsureLaneRowUI()
    {
        if (lanesRowBg != null && laneOptionBgs != null && laneOptionBgs.Length == LaneOptionCount)
            return;
        if (canvasRoot == null)
            return;

        var rowGo = new GameObject("LanesRow");
        rowGo.transform.SetParent(canvasRoot.transform, false);
        lanesRowBg = rowGo.AddComponent<Image>();
        lanesRowBg.color = RowIdleColor;
        lanesRowOutline = rowGo.AddComponent<Outline>();
        lanesRowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(900f, 80f);
        rowRt.anchoredPosition = new Vector2(0f, -390f);

        laneOptionBgs = new Image[LaneOptionCount];
        laneOptionTexts = new Text[LaneOptionCount];
        float spacing = 110f;
        float startX = -(LaneOptionCount - 1) * spacing / 2f;
        Font font = Resources.Load<Font>("lady_bug/Fonts/ComicCAT")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < LaneOptionCount; i++)
        {
            var optionGo = new GameObject("Lane" + (i + 1));
            optionGo.transform.SetParent(rowGo.transform, false);
            laneOptionBgs[i] = optionGo.AddComponent<Image>();
            laneOptionBgs[i].color = UnselectedColor;
            RectTransform optionRt = optionGo.GetComponent<RectTransform>();
            optionRt.anchorMin = new Vector2(0.5f, 0.5f);
            optionRt.anchorMax = new Vector2(0.5f, 0.5f);
            optionRt.pivot = new Vector2(0.5f, 0.5f);
            optionRt.sizeDelta = new Vector2(90f, 60f);
            optionRt.anchoredPosition = new Vector2(startX + i * spacing, 0f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(optionGo.transform, false);
            laneOptionTexts[i] = textGo.AddComponent<Text>();
            laneOptionTexts[i].font = font;
            laneOptionTexts[i].fontSize = 26;
            laneOptionTexts[i].fontStyle = FontStyle.Bold;
            laneOptionTexts[i].alignment = TextAnchor.MiddleCenter;
            laneOptionTexts[i].color = Color.white;
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
    }

    private void EnsureControllerStatusText()
    {
        if (controllerStatusText == null && canvasRoot != null)
        {
            var statusGo = new GameObject("ControllerStatusText");
            statusGo.transform.SetParent(canvasRoot.transform, false);
            controllerStatusText = statusGo.AddComponent<Text>();
            controllerStatusText.font = Resources.Load<Font>("lady_bug/Fonts/ComicCAT")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            controllerStatusText.text = ControllerDetectBase + new string('.', ControllerDetectDotMin);
            statusGo.AddComponent<Outline>().effectColor = Color.black;
        }

        if (controllerStatusText == null)
            return;

        ApplyControllerStatusLayout();
    }

    // The bottom-right MenuHelpText and its EnsureMenuHelpText() restyling
    // pass are gone — the per-row hints replaced it, and SceneSetup builds
    // those fully formed (CreateRowHint), so there is nothing to patch up at
    // runtime. A scene built before this change still has the old object;
    // Rebuild Scene clears it.

    private void EnsureMenuConfirmCountdownText()
    {
        if (menuConfirmCountdownText == null && canvasRoot != null)
        {
            Transform countdownTransform = canvasRoot.transform.Find("MenuConfirmCountdown");
            if (countdownTransform != null)
                menuConfirmCountdownText = countdownTransform.GetComponent<Text>();
            else
            {
                var countdownGo = new GameObject("MenuConfirmCountdown");
                countdownGo.transform.SetParent(canvasRoot.transform, false);
                menuConfirmCountdownText = countdownGo.AddComponent<Text>();
                menuConfirmCountdownText.font = Resources.Load<Font>("lady_bug/Fonts/ComicCAT")
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                countdownGo.AddComponent<Outline>().effectColor = Color.black;
            }
        }

        if (menuConfirmCountdownText == null)
            return;

        ApplyMenuConfirmCountdownLayout(menuConfirmCountdownText);
        menuConfirmCountdownText.gameObject.SetActive(false);
    }

    static void ApplyMenuConfirmCountdownLayout(Text text)
    {
        text.fontSize = 120;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.85f, 0.15f);
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 150f);
        rt.anchoredPosition = Vector2.zero;
    }

    static void ApplyControllerStatusLayout(Text text)
    {
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        // Bottom-LEFT — swapped with MenuHelpText, which took the right corner.
        // Keep in step with SceneSetup's own build-time placement.
        text.alignment = TextAnchor.LowerLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = new Color(0.9f, 0.9f, 0.9f);
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(680f, 80f);
        rt.anchoredPosition = new Vector2(30f, 30f);
    }

    private void ApplyControllerStatusLayout() => ApplyControllerStatusLayout(controllerStatusText);
}
}
