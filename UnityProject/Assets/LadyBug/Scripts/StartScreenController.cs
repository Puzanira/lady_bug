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
    [SerializeField] private Outline startOutline;
    [SerializeField] private Image startRowBg;

    [SerializeField] private Text notImplementedText;

    [SerializeField] private GameObject playerRight;
    [SerializeField] private GameObject playerLeft;
    [SerializeField] private GestureInput gestureRight;
    [SerializeField] private GestureInput gestureLeft;

    // "Датчики" now means an asymmetric pair, not two sets of hand sensors:
    // player 1 (right) reads real hand-distance sensors as before, player 2
    // (left) reads their own physical joystick board instead — see
    // JoystickInput/JoystickSerial and ArduinoFirmware/Joystick.
    [SerializeField] private JoystickInput joystickLeft;

    // Per-player КЛАВИШИ/ЖЕСТЫ gameplay HUD (CreateGesturePanel) — hidden
    // while this menu is up (they're feedback for actual play, not menu
    // chrome) and revealed once BeginGame fires.
    [SerializeField] private GameObject gestureCanvasRight;
    [SerializeField] private GameObject gestureCanvasLeft;

    // Live in-game "ТОП" panel — also gameplay HUD, not menu chrome, and
    // its own semi-transparent background bled through the (also
    // semi-transparent) carousel behind the menu otherwise. The carousel's
    // own top-results pages already show the same info cleanly.
    [SerializeField] private GameObject topScoresPanel;

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
        if (topScoresPanel != null)
            topScoresPanel.SetActive(false);

        // Nothing's chosen a controller yet at this point (that's what row 1
        // picks), so the menu itself listens for whichever gesture source is
        // actually available — real sensors if connected, the keyboard
        // simulator otherwise — on both players at once, in addition to the
        // plain arrow/WASD keys already handled below.
        bool useRealSensors = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
        EnableGestureForMenu(gestureRight, useRealSensors);
        EnableGestureForMenu(gestureLeft, useRealSensors);

        // Not started here anymore — Awake runs the instant the scene
        // loads, while the flower-fill/countdown intro screen is still
        // covering this menu, so playing it here meant music was already
        // running underneath a screen that's supposed to be silent. Started
        // by IntroSequence.Finish() instead, once that screen actually goes
        // away. PlayMusic() below is what it calls.

        UpdateVisuals();
        UpdateCarousel();
    }

    // Called by IntroSequence once the flower-fill/countdown screen
    // finishes and this menu is actually revealed — see the Awake comment
    // above for why it doesn't just start here.
    public void PlayMusic()
    {
        if (musicSource != null)
            musicSource.Play();
    }

    private void Update()
    {
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
            BeginGame();
    }

    private void UpdateVisuals()
    {
        bool oneSelected = _selectedPlayers == 1;

        // Live preview: the second ladybug appears/disappears on the road
        // the instant the selection changes, before Start is even pressed.
        if (playerLeft != null)
            playerLeft.SetActive(!oneSelected);

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
        if (startBg != null)
            startBg.color = _row == 2 ? SelectedColor : UnselectedColor;

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

            // Player 2's board is separate hardware (see joystickLeft) — only
            // needed in 2-player mode, and checked on its own so a missing
            // joystick doesn't get misreported as the (already-connected)
            // hand-sensor board being the problem.
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
                // "Датчики" for player 2 means their own joystick board, not
                // a second pair of hand sensors — see joystickLeft's comment.
                SetGestureEnabled(gestureLeft, false, false);
                SetJoystickEnabled(joystickLeft, true);
            }
            else
            {
                SetGestureEnabled(gestureLeft, gestureActive, useRealSensors);
                SetJoystickEnabled(joystickLeft, false);
            }
        }

        SetPlayerControlEnabled(playerRight, true);
        SetGestureEnabled(gestureRight, gestureActive, useRealSensors);

        // The gesture HUD was hidden for the menu (see Awake) — back on now
        // that the run itself is starting.
        if (gestureCanvasRight != null)
            gestureCanvasRight.SetActive(true);
        if (gestureCanvasLeft != null)
            gestureCanvasLeft.SetActive(true);
        // Not shown at all if there's nothing on any leaderboard yet — a
        // fresh save otherwise showed this panel immediately with nothing
        // but 3 rows of "--" and no photos in it, reading as an empty gray
        // box rather than a real feature. See HasAnyRecordAnywhere's own
        // comment.
        if (topScoresPanel != null)
            topScoresPanel.SetActive(HighScoreManager.Instance != null && HighScoreManager.Instance.HasAnyRecordAnywhere());

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

        if (_lastCarouselPage < 0)
        {
            ShowCarouselPage(0);
            return;
        }

        _pageDwellElapsed += Time.deltaTime;
        if (_pageDwellElapsed >= GetPageDwellDuration(_lastCarouselPage))
            ShowCarouselPage((_lastCarouselPage + 1) % carouselPages.Length);
    }

    // Always shows every page in order, including a TopResultsPage with
    // nothing on it yet (3 rows of "--", same placeholder any other empty
    // table shows) — previously skipped those entirely, which meant a save
    // with only 2 of the 4 categories played only ever cycled through those
    // 2, with the other 2 never appearing until played at least once.
    private void ShowCarouselPage(int rawIndex)
    {
        if (carouselBackground != null)
            carouselBackground.SetActive(true);
        for (int i = 0; i < carouselPages.Length; i++)
            if (carouselPages[i] != null)
                carouselPages[i].SetActive(i == rawIndex);

        _lastCarouselPage = rawIndex;
        _pageDwellElapsed = 0f;
    }

    // Animated pages report their own natural loop length (see each
    // component's own CycleDuration) so this carousel doesn't cut them off
    // partway through — gesture pages specifically ask for
    // GestureDiagramAnimation.RepeatCount full loops before advancing.
    // Falls back to the plain CarouselInterval for static pages (checklists,
    // object grids, plain diagrams) that don't have any of these.
    private float GetPageDwellDuration(int pageIndex)
    {
        GameObject page = carouselPages != null && pageIndex >= 0 && pageIndex < carouselPages.Length
            ? carouselPages[pageIndex]
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
