using System.Collections.Generic;
using UnityEngine;

namespace LadyBug
{

// Reads two downward-facing distance sensors per player (one over each
// hand) and turns them into the same virtual signals PlayerController
// normally reads from keys. Two sources: real hardware via
// GestureSensorSerial (UseRealSensors == true — see
// ArduinoFirmware/GestureSensors for the sketch that feeds it), or the
// keyboard simulator (ReadHand(), still the only option when
// UseRealSensors == false, e.g. the "Имитатор" controller choice).
//
// Gestures (assumes the player faces the screen, sensors mounted above each
// hand, looking straight down):
//   both hands down             -> duck
//   one up, one down            -> lean toward whichever hand is down
//   both hands flapping rapidly -> jump/flight (a held "both up" alone does
//                                  nothing — it has to be the flapping motion)
// No accelerate/brake gesture anymore — the road accelerates on its own
// (SpeedController) and braking was removed from every control scheme.
public class GestureInput : MonoBehaviour
{
    private enum HandState { Neutral, Up, Down }

    [SerializeField] private KeyCode leftHandUpKey;
    [SerializeField] private KeyCode leftHandDownKey;
    [SerializeField] private KeyCode rightHandUpKey;
    [SerializeField] private KeyCode rightHandDownKey;

    // Exposed so the simulator's raw key-state debug indicator (which just
    // shows every physical key press, unlike GestureIndicator's interpreted
    // arrows) can read back what's actually assigned to this player. The up
    // keys double as the flap keys — rapid taps on both, together, is what
    // triggers a jump (see RapidPressTracker below).
    public KeyCode LeftHandUpKey => leftHandUpKey;
    public KeyCode LeftHandDownKey => leftHandDownKey;
    public KeyCode RightHandUpKey => rightHandUpKey;
    public KeyCode RightHandDownKey => rightHandDownKey;

    // Raw distance a real sensor would report, in millimetres — the simulator
    // fabricates a plausible number per key state (STUB: swap for the actual
    // reading once hardware is connected) so the debug HUD has real numbers
    // to show, not just the thresholded Up/Down state derived from them.
    private const int SimulatedNearMm = 40;
    private const int SimulatedFarMm = 250;
    private const int SimulatedMidMm = 150; // between the two thresholds ("neutral")

    // Cutoffs applied to real sensor readings (GestureSensorSerial). Tuned
    // empirically on real hardware: close to the sensor reads as the hand
    // being Down, far from it reads as Up (the opposite of the simulator's
    // Near=Up/Far=Down naming below — real mounting geometry decides this,
    // not a fixed convention).
    private const int DownThresholdMm = 100;
    private const int UpThresholdMm = 200;

    // Matches ArduinoFirmware NO_TARGET_MM — VL53L0X returns ~8190 mm with
    // no close target; treat as -1 ("no reading") so graphs and flap logic
    // don't see an 8000 mm spike when the hand leaves the sensor.
    public const int NoTargetMm = 2000;

    public static int SanitizeDistanceMm(int mm)
    {
        return mm < 0 || mm >= NoTargetMm ? -1 : mm;
    }

    public static bool DuckHeldFromDistances(int leftMm, int rightMm)
    {
        return HandStateForDistance(leftMm) == HandState.Down
            && HandStateForDistance(rightMm) == HandState.Down;
    }

    public static bool LeanLeftHeldFromDistances(int leftMm, int rightMm)
    {
        return HandStateForDistance(leftMm) == HandState.Down
            && HandStateForDistance(rightMm) == HandState.Up;
    }

    public static bool LeanRightHeldFromDistances(int leftMm, int rightMm)
    {
        return HandStateForDistance(leftMm) == HandState.Up
            && HandStateForDistance(rightMm) == HandState.Down;
    }

    public static bool BothHandsUpFromDistances(int leftMm, int rightMm)
    {
        return HandStateForDistance(leftMm) == HandState.Up
            && HandStateForDistance(rightMm) == HandState.Up;
    }

    // Live mm for debug HUD — reads serial directly so values don't freeze
    // when GestureInput is temporarily disabled (menu, 1-player preview).
    public static bool TryGetLiveHandDistances(GestureInput gesture, out int leftMm, out int rightMm)
    {
        leftMm = -1;
        rightMm = -1;

        var joystickBoard = JoystickSerial.Instance;
        if (joystickBoard != null && joystickBoard.IsConnected && joystickBoard.HasHandSensors)
        {
            leftMm = joystickBoard.HandLeftMm;
            rightMm = joystickBoard.HandRightMm;
            return true;
        }

        var sensorBoard = GestureSensorSerial.Instance;
        if (sensorBoard != null && sensorBoard.IsConnected && gesture != null
            && gesture.gameObject.name.Contains("Left"))
        {
            leftMm = sensorBoard.Player1LeftMm;
            rightMm = sensorBoard.Player1RightMm;
            return true;
        }

        if (gesture != null && gesture.enabled && gesture.UseRealSensors)
        {
            leftMm = gesture.LeftHandDistanceMm;
            rightMm = gesture.RightHandDistanceMm;
            return leftMm >= 0 || rightMm >= 0;
        }

        return false;
    }

    public static bool HandIsUp(int mm) => HandStateForDistance(mm) == HandState.Up;
    public static bool HandIsDown(int mm) => HandStateForDistance(mm) == HandState.Down;

    public static bool BothHandsFlapping(HandFlapTracker left, HandFlapTracker right, int leftMm, int rightMm)
    {
        left.Observe(leftMm);
        right.Observe(rightMm);
        return left.IsFlapping && right.IsFlapping;
    }

    private static bool HaveRealSensorFeed(bool useRealSensors, bool isPlayerOne)
    {
        if (!useRealSensors)
            return false;

        if (GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected)
            return isPlayerOne;

        // CombinedBoard sensors are player-1 hardware; P1 = PlayerLeft / gestureLeft.
        return JoystickSerial.Instance != null
            && JoystickSerial.Instance.IsConnected
            && JoystickSerial.Instance.HasHandSensors;
    }

    // True while this player should read GestureSensorSerial instead of the
    // keyboard — set at runtime by StartScreenController when "Датчики" (real
    // hardware) is the chosen controller, as opposed to "Имитатор" (keyboard).
    public bool UseRealSensors { get; set; }

    // Which half of the board (see ArduinoFirmware/GestureSensors) this
    // player reads — inferred from the GameObject name set in
    // SceneSetup.CreatePlayer ("PlayerRight"/"PlayerLeft") so no extra wiring
    // is needed there. Player 1 (distance sensors) is player-left now, not
    // player-right — see joystickRight's own comment in
    // StartScreenController for the left/right swap this reflects.
    private bool _isPlayerOne;

    private void Awake()
    {
        _isPlayerOne = gameObject.name.Contains("Left");
    }

    private void OnDisable()
    {
        ClearInterpretedState();
    }

    private void ClearInterpretedState()
    {
        _leftHand = HandState.Neutral;
        _rightHand = HandState.Neutral;
        LeftHandDistanceMm = -1;
        RightHandDistanceMm = -1;
        JumpHeld = false;
        JumpDown = false;
        DuckHeld = false;
        LeanLeftHeld = false;
        LeanLeftDown = false;
        LeanRightHeld = false;
        LeanRightDown = false;
        RealFlapDetected = false;
        _leftFlapTracker.Reset();
        _rightFlapTracker.Reset();
    }

    private HandState _leftHand;
    private HandState _rightHand;

    public bool LeftHandUp => _leftHand == HandState.Up;
    public bool LeftHandDown => _leftHand == HandState.Down;
    public bool RightHandUp => _rightHand == HandState.Up;
    public bool RightHandDown => _rightHand == HandState.Down;

    public int LeftHandDistanceMm { get; private set; }
    public int RightHandDistanceMm { get; private set; }

    // Both hands flapping together — mirrors a held/just-pressed Up key.
    public bool JumpHeld { get; private set; }
    public bool JumpDown { get; private set; }
    public bool DuckHeld { get; private set; }

    // One hand up, the other down — mirrors a held/just-pressed lane key.
    public bool LeanLeftHeld { get; private set; }
    public bool LeanLeftDown { get; private set; }
    public bool LeanRightHeld { get; private set; }
    public bool LeanRightDown { get; private set; }

    // Simulator-only: the up key per hand doubles as the flap key — tapped
    // rapidly and repeatedly (not just held) stands in for a real sensor
    // picking up rapid hand movement. See RapidPressTracker below.
    private readonly RapidPressTracker _leftFlapPresses = new RapidPressTracker();
    private readonly RapidPressTracker _rightFlapPresses = new RapidPressTracker();

    // Real hardware: both hands rhythmically reversing distance — the
    // genuine physical "flapping arms like wings" gesture. HandFlapTracker
    // is also used by StartScreenController for menu navigation.
    public class HandFlapTracker
    {
        private const float Window = 0.9f;
        private const float MinSwingMm = 55f;
        private const int RequiredSwings = 2;

        private readonly List<float> _swingTimes = new List<float>();
        private float _lastValue = -1f;
        private float _extremeValue = -1f;
        private int _direction; // -1 falling, +1 rising, 0 not established yet
        private bool _hasLast;

        public bool IsFlapping => _swingTimes.Count >= RequiredSwings;

        public void Reset()
        {
            _swingTimes.Clear();
            _hasLast = false;
            _direction = 0;
        }

        public void Observe(int mm)
        {
            if (mm < 0)
            {
                _hasLast = false;
                _swingTimes.RemoveAll(t => Time.time - t > Window);
                return;
            }

            if (!_hasLast)
            {
                _lastValue = mm;
                _extremeValue = mm;
                _hasLast = true;
            }
            else
            {
                int newDirection = mm > _lastValue ? 1 : (mm < _lastValue ? -1 : _direction);
                if (_direction != 0 && newDirection != 0 && newDirection != _direction)
                {
                    if (Mathf.Abs(_lastValue - _extremeValue) >= MinSwingMm)
                    {
                        _swingTimes.Add(Time.time);
                        _extremeValue = _lastValue;
                    }
                }
                if (newDirection != 0)
                    _direction = newDirection;
                _lastValue = mm;
            }

            _swingTimes.RemoveAll(t => Time.time - t > Window);
        }
    }

    private readonly HandFlapTracker _leftFlapTracker = new HandFlapTracker();
    private readonly HandFlapTracker _rightFlapTracker = new HandFlapTracker();

    public bool RealFlapDetected { get; private set; }

    private void Update()
    {
        bool haveRealSensors = HaveRealSensorFeed(UseRealSensors, _isPlayerOne);
        bool flapping;

        if (haveRealSensors)
        {
            GestureSensorSerial sensors = GestureSensorSerial.Instance;
            bool useCombinedBoard = (sensors == null || !sensors.IsConnected)
                && JoystickSerial.Instance != null
                && JoystickSerial.Instance.IsConnected
                && JoystickSerial.Instance.HasHandSensors;

            if (useCombinedBoard)
            {
                // Combined board (ArduinoFirmware/CombinedBoard) — its 2
                // hand sensors are always player 1's, carried alongside
                // player 2's joystick on the same board/port (see
                // JoystickSerial's own comment on why it, not
                // GestureSensorSerial, ends up owning this data).
                LeftHandDistanceMm = GestureInput.SanitizeDistanceMm(JoystickSerial.Instance.HandLeftMm);
                RightHandDistanceMm = GestureInput.SanitizeDistanceMm(JoystickSerial.Instance.HandRightMm);
            }
            else
            {
                LeftHandDistanceMm = GestureInput.SanitizeDistanceMm(sensors.Player1LeftMm);
                RightHandDistanceMm = GestureInput.SanitizeDistanceMm(sensors.Player1RightMm);
            }

            _leftHand = HandStateForDistance(LeftHandDistanceMm);
            _rightHand = HandStateForDistance(RightHandDistanceMm);

            // Raw mm values, not the thresholded state above — a flap
            // confined to one zone (e.g. bobbing between 220mm and 260mm,
            // never actually reaching the Down threshold) never shows up as
            // a Down<->Up state change, but it's still a real flap.
            _leftFlapTracker.Observe(LeftHandDistanceMm);
            _rightFlapTracker.Observe(RightHandDistanceMm);
            RealFlapDetected = _leftFlapTracker.IsFlapping && _rightFlapTracker.IsFlapping;
            flapping = RealFlapDetected;
        }
        else
        {
            _leftHand = ReadHand(leftHandUpKey, leftHandDownKey);
            _rightHand = ReadHand(rightHandUpKey, rightHandDownKey);
            LeftHandDistanceMm = DistanceForState(_leftHand);
            RightHandDistanceMm = DistanceForState(_rightHand);

            _leftFlapPresses.Observe(Input.GetKeyDown(leftHandUpKey));
            _rightFlapPresses.Observe(Input.GetKeyDown(rightHandUpKey));
            flapping = _leftFlapPresses.IsActive && _rightFlapPresses.IsActive;
        }

        bool wasJumpHeld = JumpHeld;
        JumpHeld = flapping;
        JumpDown = JumpHeld && !wasJumpHeld;

        if (flapping)
        {
            // Actively flapping — that's the jump signal on its own; the same
            // hand movement would otherwise also flicker duck/lean, which a
            // real flap and (to a lesser extent) a rapid up-key tap both do.
            DuckHeld = false;
            LeanLeftHeld = false;
            LeanLeftDown = false;
            LeanRightHeld = false;
            LeanRightDown = false;
            return;
        }

        DuckHeld = _leftHand == HandState.Down && _rightHand == HandState.Down;

        bool wasLeanLeftHeld = LeanLeftHeld;
        LeanLeftHeld = _leftHand == HandState.Down && _rightHand == HandState.Up;
        LeanLeftDown = LeanLeftHeld && !wasLeanLeftHeld;

        bool wasLeanRightHeld = LeanRightHeld;
        LeanRightHeld = _leftHand == HandState.Up && _rightHand == HandState.Down;
        LeanRightDown = LeanRightHeld && !wasLeanRightHeld;
    }

    // STUB — keyboard stand-in for one hand's up/down distance sensor.
    // Real hardware: read the sensor's distance and threshold it into
    // Up (near) / Down (far) / Neutral (in between neither extreme).
    private static HandState ReadHand(KeyCode upKey, KeyCode downKey)
    {
        bool up = Input.GetKey(upKey);
        bool down = Input.GetKey(downKey);
        if (up && !down) return HandState.Up;
        if (down && !up) return HandState.Down;
        return HandState.Neutral;
    }

    // Real hardware: threshold a hand sensor's raw millimetre reading into
    // Down (close to the sensor) / Up (far from it) / Neutral (in the dead
    // zone between, or no valid target at all — GestureSensorSerial reports
    // -1 for that).
    private static HandState HandStateForDistance(int mm)
    {
        if (mm < 0) return HandState.Neutral;
        if (mm <= DownThresholdMm) return HandState.Down;
        if (mm >= UpThresholdMm) return HandState.Up;
        return HandState.Neutral;
    }

    // STUB — fabricates the raw millimetre reading a real hand sensor would
    // have produced to justify the given thresholded state.
    private static int DistanceForState(HandState state)
    {
        switch (state)
        {
            case HandState.Up: return SimulatedNearMm;
            case HandState.Down: return SimulatedFarMm;
            default: return SimulatedMidMm;
        }
    }

    // Counts how many times a flap key has been pressed within a short
    // rolling window — a couple of quick taps count as "actively flapping",
    // a single press or a slow hold doesn't.
    private class RapidPressTracker
    {
        private const float Window = 0.6f;
        private const int RequiredPresses = 3;

        private readonly List<float> _pressTimes = new List<float>();

        public bool IsActive => _pressTimes.Count >= RequiredPresses;

        public void Observe(bool pressedThisFrame)
        {
            if (pressedThisFrame)
                _pressTimes.Add(Time.time);

            _pressTimes.RemoveAll(t => Time.time - t > Window);
        }
    }
}
}
