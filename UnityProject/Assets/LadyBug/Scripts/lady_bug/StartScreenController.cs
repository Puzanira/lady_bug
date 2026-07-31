using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Pre-game menu: pick 1 or 2 players and confirm with Space/Enter — a
// neutral key, not tied to either player's own scheme, since nothing is
// bound to a specific player yet at this point.
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

    [SerializeField] private Image controller1Bg;
    [SerializeField] private Image controller2Bg;
    [SerializeField] private Image controller3Bg;
    [SerializeField] private Text controller1Text;
    [SerializeField] private Text controller2Text;
    [SerializeField] private Text controller3Text;
    [SerializeField] private Outline controllerRowOutline;
    [SerializeField] private Image controllerRowBg;

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
    // visible 5,4,3,2,1 countdown for TrainingExitCountdownPhase more,
    // per feedback (was one flat 5s hold with the countdown visible the
    // whole time).
    private const float TrainingExitSilentPhase = 5f;
    private const float TrainingExitCountdownPhase = 5f;
    private float _trainingHoldTimer;

    // ТРЕНИРОВКА now leads here first — the same trick-instruction pages
    // that used to be part of the general upfront carousel (everyone saw
    // them whether they cared or not), moved so only someone who actually
    // picked training sees them. Confirm (same key as the main menu) moves
    // on into the live practice screen above; holding down does the same
    // 2-phase exit the live screen itself uses, but takes you all the way
    // back to the main menu instead.
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

    private const int RowCount = 3; // 0 = player count, 1 = controller type, 2 = start button
    private const int ControllerCount = 3; // 0 = keyboard, 1 = distance sensors, 2 = sensor simulator

    private int _selectedPlayers = 2;
    private int _selectedController; // 0 = keyboard, 1 = distance sensors, 2 = simulator
    private int _selectedStartOption; // 0 = СТАРТ, 1 = ТРЕНИРОВКА
    private int _row;

    // TEMPORARY, for faster debug/test cycling — revert to 10f for real play.
    // Floor for every page's dwell time — animated pages (arch/ring trick,
    // gesture diagrams) can ask for longer via GetPageDwellDuration so a
    // slower carousel here doesn't also stretch their fixed-length loops
    // out further than they already run.
    private const float CarouselInterval = 4f;
    // Pure pause before the carousel shows anything at all — first
    // impression is the game running behind the menu buttons, not a table,
    // same as before the carousel/winner-tables existed.
    private const float CarouselStartDelay = 4f;
    private const int NoCarouselPage = -2; // sentinel distinct from _lastCarouselPage's initial -1
    private int _lastCarouselPage = -1;
    private float _pageDwellElapsed;

    private PlayerController _rightController;
    private int _rightHomeLane;

    // Edge-detect state for the two menu-only gestures that GestureInput
    // doesn't already expose as a "just happened" signal (LeanLeft/RightDown
    // and JumpDown already are — DuckHeld and "both hands up" are level
    // signals there since ducking/jumping mid-run don't need edges).
    private bool _prevBothUpRight, _prevBothUpLeft;
    private bool _prevDuckRight, _prevDuckLeft;

    private void Awake()
    {
        // Gameplay stays inert (players unresponsive, road stopped, nothing
        // spawns — SpeedController holds at 0) until a mode is confirmed.
        SetPlayerControlEnabled(playerRight, false);
        SetPlayerControlEnabled(playerLeft, false);

        if (playerRight != null)
        {
            _rightController = playerRight.GetComponent<PlayerController>();
            if (_rightController != null)
                _rightHomeLane = _rightController.HomeLane;
        }

        // The per-player gesture HUD is feedback for actual play, not menu
        // chrome — hidden while this menu is up (MenuHelpText covers the
        // freed-up space instead), shown again once BeginGame fires.
        if (gestureCanvasRight != null)
            gestureCanvasRight.SetActive(false);
        if (gestureCanvasLeft != null)
            gestureCanvasLeft.SetActive(false);

        RestoreMenuGestureMode();

        UpdateVisuals();
        UpdateCarousel();

        // Loader/intro (LoaderScreenController, IntroSequence) are skipped
        // for now (see SceneSetup's own comment on why) — nothing else is
        // ever going to call PlayMusic()/OnRevealed() for us, so this menu
        // has to do it itself, immediately, instead of waiting on a reveal
        // event from a screen that no longer runs.
        PlayMusic();
        OnRevealed();
    }

    // Called by IntroSequence once the flower-fill/countdown screen
    // finishes and this menu is actually revealed — see the Awake comment
    // above for why it doesn't just start here.
    public void PlayMusic()
    {
        if (musicSource != null)
            musicSource.Play();
    }

    // Also called by IntroSequence.Finish(), for every one of the loader's
    // 7 game slots (not just БК's own PlayMusic) — the carousel's own
    // CarouselStartDelay/dwell timing runs on Time.time from scene load,
    // but this menu can sit hidden behind the loader + a full intro
    // sequence (~15-20s) before a player ever actually sees it. Without
    // this reset, the carousel silently cycles the whole time it's hidden,
    // so by the time it's finally revealed it can already be several pages
    // past ЦЕЛЬ (e.g. showing a leaderboard first) instead of starting
    // fresh. Resetting right at reveal time guarantees page 0 (ЦЕЛЬ) is
    // what's actually on screen the instant it becomes visible.
    public void OnRevealed()
    {
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

        UpdateCarousel();

        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

        // Gesture nav (works from either player, whichever the keyboard
        // simulator or real sensors are hooked up to): leaning one hand
        // moves left/right, both hands up moves the row cursor up, both
        // hands down (duck) moves it down, and flapping (the same signal
        // that means "jump" in actual play) confirms — "start and wave"
        // rather than a dedicated new gesture.
        left |= IsLeanLeftDown(gestureRight) || IsLeanLeftDown(gestureLeft);
        right |= IsLeanRightDown(gestureRight) || IsLeanRightDown(gestureLeft);
        up |= EdgeBothHandsUp(gestureRight, ref _prevBothUpRight) || EdgeBothHandsUp(gestureLeft, ref _prevBothUpLeft);
        down |= EdgeDuck(gestureRight, ref _prevDuckRight) || EdgeDuck(gestureLeft, ref _prevDuckLeft);
        confirm |= IsJumpDown(gestureRight) || IsJumpDown(gestureLeft);

        if (left || right)
        {
            if (_row == 0)
            {
                _selectedPlayers = _selectedPlayers == 1 ? 2 : 1;
            }
            else if (_row == 1)
            {
                int delta = right ? 1 : -1;
                _selectedController = (_selectedController + delta + ControllerCount) % ControllerCount;
            }
            else if (_row == 2)
            {
                _selectedStartOption = _selectedStartOption == 0 ? 1 : 0;
            }
            UpdateVisuals();
        }

        if (up)
        {
            _row = (_row - 1 + RowCount) % RowCount;
            UpdateVisuals();
        }
        else if (down)
        {
            _row = (_row + 1) % RowCount;
            UpdateVisuals();
        }

        if (confirm && _row == 2)
        {
            if (_selectedStartOption == 0)
                BeginGame();
            else
                BeginTraining();
        }
    }

    // Duck-held (real gesture or the same Down/S keys the menu itself
    // reads) for TrainingExitSilentPhase + TrainingExitCountdownPhase —
    // mirrors DuckToExitController's own 2-phase hold-to-confirm feel
    // exactly (silent first, then a visible countdown), just without its
    // confirm dialog: this screen has nothing on it to lose, so holding
    // down all the way just takes you straight back to the menu.
    private void UpdateTrainingScreen()
    {
        bool holding = Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.S)
            || IsDuckHeld(gestureRight) || IsDuckHeld(gestureLeft) || IsDuckHeld(joystickRight) || IsDuckHeld(joystickLeft);

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

        // Same input-scheme choice a real run applies (see BeginGame) —
        // without this, the live "ВАШИ ДЕЙСТВИЯ" bugs on the trick carousel
        // just keep reading whatever GestureInput/JoystickInput were left
        // at by default (disabled) instead of whatever was actually picked
        // on this same menu, and never react to anything. No hardware-
        // connection check here, unlike BeginGame — a practice screen
        // shouldn't refuse entry just because a board isn't plugged in yet.
        bool gestureActive = _selectedController == 1 || _selectedController == 2;
        bool useRealSensors = _selectedController == 1;
        if (_selectedPlayers == 2)
        {
            if (_selectedController == 1)
            {
                // Real hardware: player 1 (left) reads distance sensors —
                // see joystickRight's own comment for player 2's side.
                SetGestureEnabled(gestureLeft, true, true);
                SetJoystickEnabled(joystickLeft, false);
            }
            else
            {
                SetGestureEnabled(gestureLeft, gestureActive, useRealSensors);
                SetJoystickEnabled(joystickLeft, false);
            }
        }
        if (_selectedController == 1 && _selectedPlayers == 2)
        {
            // "Датчики" for player 2 means their own joystick board, not a
            // second pair of hand sensors — see joystickRight's comment.
            // Only in 2-player mode: solo play has no partner to hand the
            // sensors to, so it keeps using them itself (same as before the
            // left/right swap) instead of switching to an unplugged joystick.
            SetGestureEnabled(gestureRight, false, false);
            SetJoystickEnabled(joystickRight, true);
        }
        else
        {
            SetGestureEnabled(gestureRight, gestureActive, useRealSensors);
            SetJoystickEnabled(joystickRight, false);
        }

        // Trick-instruction carousel first, not the live screen directly —
        // see trickCarouselCanvasRoot's own comment.
        if (trickCarouselCanvasRoot != null)
            trickCarouselCanvasRoot.SetActive(true);
        _lastTrickPage = -1;
        _trickPageDwellElapsed = 0f;
        _trickExitHoldTimer = 0f;
    }

    // Confirm, from the trick carousel, moves on into the actual live
    // practice screen — holding down instead (same gesture the live screen
    // itself uses to exit) backs all the way out to the main menu, see
    // ExitTrickCarouselToMenu.
    private void UpdateTrickCarousel()
    {
        UpdateCarouselGeneric(trickCarouselPages, trickCarouselBackground, ref _lastTrickPage, ref _trickPageDwellElapsed);

        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        confirm |= IsJumpDown(gestureRight) || IsJumpDown(gestureLeft);
        if (confirm)
        {
            EnterLiveTraining();
            return;
        }

        bool holding = Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.S)
            || IsDuckHeld(gestureRight) || IsDuckHeld(gestureLeft) || IsDuckHeld(joystickRight) || IsDuckHeld(joystickLeft);

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

    private void EnterLiveTraining()
    {
        if (trickCarouselCanvasRoot != null)
            trickCarouselCanvasRoot.SetActive(false);
        if (trainingCanvasRoot != null)
            trainingCanvasRoot.SetActive(true);
        _trainingHoldTimer = 0f;
    }

    private void ExitTrickCarouselToMenu()
    {
        if (trickCarouselCanvasRoot != null)
            trickCarouselCanvasRoot.SetActive(false);
        RestoreMenuGestureMode();
        Canvas startCanvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (startCanvas != null)
            startCanvas.enabled = true;
    }

    private void ExitTraining()
    {
        if (trainingCanvasRoot != null)
            trainingCanvasRoot.SetActive(false);
        if (trainingExitCountdownText != null)
            trainingExitCountdownText.gameObject.SetActive(false);
        RestoreMenuGestureMode();
        Canvas startCanvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
        if (startCanvas != null)
            startCanvas.enabled = true;
    }

    private void UpdateVisuals()
    {
        bool oneSelected = _selectedPlayers == 1;

        // Live preview: the second ladybug appears/disappears on the road
        // the instant the selection changes, before Start is even pressed.
        if (playerLeft != null)
            playerLeft.SetActive(!oneSelected);
        // Same choice governs every gesture/trick page's own live preview —
        // the player-1 bug always shows, the player-2 one on each page
        // follows 1-player/2-player just like the real playerLeft above.
        if (trainingPreviewLeftBugs != null)
        {
            foreach (var bug in trainingPreviewLeftBugs)
                if (bug != null)
                    bug.SetActive(!oneSelected);
        }

        // Solo mode stands the lone player in the middle lane instead of
        // its usual (right-edge) co-op starting lane.
        if (_rightController != null)
            _rightController.SetPreviewLane(oneSelected ? _rightController.LaneCount / 2 : _rightHomeLane);

        if (option1Bg != null)
            option1Bg.color = oneSelected ? SelectedColor : UnselectedColor;
        if (option2Bg != null)
            option2Bg.color = oneSelected ? UnselectedColor : SelectedColor;
        if (option1Text != null)
            option1Text.text = (oneSelected ? "[X] " : "[ ] ") + "1 ИГРОК";
        if (option2Text != null)
            option2Text.text = (oneSelected ? "[ ] " : "[X] ") + "2 ИГРОКА";

        bool keyboardSelected = _selectedController == 0;
        bool sensorsSelected = _selectedController == 1;
        bool simulatorSelected = _selectedController == 2;
        if (controller1Bg != null)
            controller1Bg.color = keyboardSelected ? SelectedColor : UnselectedColor;
        if (controller2Bg != null)
            controller2Bg.color = sensorsSelected ? SelectedColor : UnselectedColor;
        if (controller3Bg != null)
            controller3Bg.color = simulatorSelected ? SelectedColor : UnselectedColor;
        if (controller1Text != null)
            controller1Text.text = (keyboardSelected ? "[X] " : "[ ] ") + "КЛАВИАТУРА";
        if (controller2Text != null)
            controller2Text.text = (sensorsSelected ? "[X] " : "[ ] ") + "ДАТЧИКИ";
        if (controller3Text != null)
            controller3Text.text = (simulatorSelected ? "[X] " : "[ ] ") + "ИМИТАТОР";

        if (optionsRowOutline != null)
            optionsRowOutline.effectColor = _row == 0 ? FocusOutline : IdleOutline;
        if (optionsRowBg != null)
            optionsRowBg.color = _row == 0 ? RowFocusColor : RowIdleColor;
        if (controllerRowOutline != null)
            controllerRowOutline.effectColor = _row == 1 ? FocusOutline : IdleOutline;
        if (controllerRowBg != null)
            controllerRowBg.color = _row == 1 ? RowFocusColor : RowIdleColor;
        if (startOutline != null)
            startOutline.effectColor = _row == 2 ? FocusOutline : IdleOutline;
        if (startRowBg != null)
            startRowBg.color = _row == 2 ? RowFocusColor : RowIdleColor;

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

    private void BeginGame()
    {
        if (_selectedController == 1)
        {
            bool connected = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
            if (!connected)
            {
                if (notImplementedText != null)
                {
                    notImplementedText.gameObject.SetActive(true);
                    notImplementedText.text = "Датчики не найдены — проверь подключение платы";
                }
                return;
            }

            // Player 2's board is separate hardware (see joystickRight) —
            // only needed in 2-player mode, and checked on its own so a
            // missing joystick doesn't get misreported as the (already-
            // connected) hand-sensor board being the problem.
            if (_selectedPlayers == 2)
            {
                bool joystickConnected = JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected;
                if (!joystickConnected)
                {
                    if (notImplementedText != null)
                    {
                        notImplementedText.gameObject.SetActive(true);
                        notImplementedText.text = "Джойстик игрока 2 не найден — проверь подключение платы";
                    }
                    return;
                }
            }
        }

        bool gestureActive = _selectedController == 1 || _selectedController == 2;
        bool useRealSensors = _selectedController == 1;

        // playerLeft's active state already reflects the selection (toggled
        // live in UpdateVisuals) — only need to arm its controls if present.
        if (_selectedPlayers == 2)
        {
            SetPlayerControlEnabled(playerLeft, true);

            if (_selectedController == 1)
            {
                // Real hardware: player 1 (left) reads distance sensors —
                // see joystickRight's own comment for player 2's side.
                SetGestureEnabled(gestureLeft, true, true);
                SetJoystickEnabled(joystickLeft, false);
            }
            else
            {
                SetGestureEnabled(gestureLeft, gestureActive, useRealSensors);
                SetJoystickEnabled(joystickLeft, false);
            }
        }

        SetPlayerControlEnabled(playerRight, true);
        if (_selectedController == 1 && _selectedPlayers == 2)
        {
            // "Датчики" for player 2 means their own joystick board, not a
            // second pair of hand sensors — see joystickRight's comment.
            // Only in 2-player mode: solo play has no partner to hand the
            // sensors to, so it keeps using them itself (same as before the
            // left/right swap) instead of switching to an unplugged joystick.
            SetGestureEnabled(gestureRight, false, false);
            SetJoystickEnabled(joystickRight, true);
        }
        else
        {
            SetGestureEnabled(gestureRight, gestureActive, useRealSensors);
            SetJoystickEnabled(joystickRight, false);
        }

        // The gesture HUD was hidden for the menu (see Awake) — back on now
        // that the run itself is starting.
        if (gestureCanvasRight != null)
            gestureCanvasRight.SetActive(true);
        if (gestureCanvasLeft != null)
            gestureCanvasLeft.SetActive(true);

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
    // counter per page rather than a fixed CarouselInterval for every page
    // (see GetPageDwellDuration), so an animated page's own loop doesn't
    // get cut off mid-cycle by a shorter global timer. Pages are whole
    // pre-built GameObjects (not just swapped text) so some can be visual
    // diagrams instead of plain text.
    private void UpdateCarousel()
    {
        if (carouselPages == null || carouselPages.Length == 0)
            return;

        if (Time.time < CarouselStartDelay)
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
    // carousel's CarouselStartDelay pause is handled by its own caller
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
    // Falls back to the plain CarouselInterval for static pages (checklists,
    // object grids, plain diagrams) that don't have any of these.
    private float GetPageDwellDuration(GameObject[] pages, int pageIndex)
    {
        GameObject page = pages != null && pageIndex >= 0 && pageIndex < pages.Length
            ? pages[pageIndex]
            : null;
        if (page == null)
            return CarouselInterval;

        ArchTrickAnimation arch = page.GetComponent<ArchTrickAnimation>();
        if (arch != null)
            return Mathf.Max(CarouselInterval, arch.TotalDisplayDuration);

        RingTrickAnimation ring = page.GetComponent<RingTrickAnimation>();
        if (ring != null)
            return Mathf.Max(CarouselInterval, ring.TotalDisplayDuration);

        GestureDiagramAnimation gesture = page.GetComponent<GestureDiagramAnimation>();
        if (gesture != null)
            return Mathf.Max(CarouselInterval, gesture.TotalDisplayDuration);

        TrickDiagramAnimation trick = page.GetComponent<TrickDiagramAnimation>();
        if (trick != null)
            return Mathf.Max(CarouselInterval, trick.TotalDisplayDuration);

        return CarouselInterval;
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

    // Nothing's chosen a controller yet on the button-selection menu (that's
    // what row 1 picks), so it listens for whichever gesture source is
    // actually available — real sensors if connected, the keyboard
    // simulator otherwise — on both players at once, regardless of
    // whatever a training session just set gestureRight/gestureLeft to
    // (see BeginTraining). Called once from Awake, and again on every path
    // back to this menu (ExitTrickCarouselToMenu, ExitTraining) so a
    // keyboard-only training visit doesn't leave the confirm-jump gesture
    // dead once you're back.
    private void RestoreMenuGestureMode()
    {
        bool useRealSensors = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
        EnableGestureForMenu(gestureRight, useRealSensors);
        EnableGestureForMenu(gestureLeft, useRealSensors);
    }

    // Menu-only: turns a GestureInput on regardless of the (not yet made)
    // controller choice, purely so this screen can read hand state from it.
    // Harmless to actual gameplay — PlayerController is disabled at this
    // point, so nothing consumes GestureInput's output but this menu.
    // BeginGame overwrites .enabled/.UseRealSensors with the real choice
    // right after, once a mode is confirmed.
    private static void EnableGestureForMenu(GestureInput gesture, bool useRealSensors)
    {
        if (gesture == null)
            return;

        gesture.enabled = true;
        gesture.UseRealSensors = useRealSensors;
    }

    private static bool IsLeanLeftDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanLeftDown;
    private static bool IsLeanRightDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanRightDown;
    private static bool IsJumpDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.JumpDown;
    private static bool IsDuckHeld(GestureInput gesture) => gesture != null && gesture.enabled && gesture.DuckHeld;
    // A real joystick (player-right's own hardware now, see joystickRight)
    // needs the same hold-to-exit path gesture already has — without this a
    // joystick-only player (no keyboard fallback in hand) would have no way
    // to back out of training at all.
    private static bool IsDuckHeld(JoystickInput joystick) => joystick != null && joystick.enabled && joystick.DownHeld;

    // "Both hands up, held" doesn't mean anything during actual play (only
    // the flapping motion does, to avoid an accidental jump from just
    // resting hands up) — but it's a natural, otherwise-unused signal for
    // "move the row cursor up" here, so this menu reads it directly off
    // GestureInput's raw per-hand state instead of its interpreted Jump.
    private static bool EdgeBothHandsUp(GestureInput gesture, ref bool prev)
    {
        bool now = gesture != null && gesture.enabled && gesture.LeftHandUp && gesture.RightHandUp;
        bool edgeUp = now && !prev;
        prev = now;
        return edgeUp;
    }

    private static bool EdgeDuck(GestureInput gesture, ref bool prev)
    {
        bool now = gesture != null && gesture.enabled && gesture.DuckHeld;
        bool edgeUp = now && !prev;
        prev = now;
        return edgeUp;
    }
}
