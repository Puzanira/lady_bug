using System.Collections.Generic;
using UnityEngine;

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
    private const int DownThresholdMm = 150;
    private const int UpThresholdMm = 200;

    // True while this player should read GestureSensorSerial instead of the
    // keyboard — set at runtime by StartScreenController when "Датчики" (real
    // hardware) is the chosen controller, as opposed to "Имитатор" (keyboard).
    public bool UseRealSensors { get; set; }

    // Which half of the board (see ArduinoFirmware/GestureSensors) this
    // player reads — inferred from the GameObject name set in
    // SceneSetup.CreatePlayer ("PlayerRight"/"PlayerLeft") so no extra wiring
    // is needed there.
    private bool _isPlayerOne;

    private void Awake()
    {
        _isPlayerOne = gameObject.name.Contains("Right");
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

    // Real hardware: both hands rhythmically flipping Up/Down together
    // recently — the genuine physical "flapping arms like wings" gesture.
    // A real flap naturally shows up as rapid alternation here.
    private readonly FlapTracker _leftFlapTracker = new FlapTracker();
    private readonly FlapTracker _rightFlapTracker = new FlapTracker();

    public bool RealFlapDetected { get; private set; }

    private void Update()
    {
        bool haveRealSensors = UseRealSensors && GestureSensorSerial.Instance != null;
        bool flapping;

        if (haveRealSensors)
        {
            GestureSensorSerial sensors = GestureSensorSerial.Instance;
            LeftHandDistanceMm = _isPlayerOne ? sensors.Player1LeftMm : sensors.Player2LeftMm;
            RightHandDistanceMm = _isPlayerOne ? sensors.Player1RightMm : sensors.Player2RightMm;

            _leftHand = HandStateForDistance(LeftHandDistanceMm);
            _rightHand = HandStateForDistance(RightHandDistanceMm);

            _leftFlapTracker.Observe(_leftHand);
            _rightFlapTracker.Observe(_rightHand);
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

    // Timing-based rhythm detector for one hand, feeding RealFlapDetected —
    // counts how many times a hand's reading has flipped between Up and
    // Down within a short rolling window; enough flips in a row means it's
    // actively flapping rather than just resting at one extreme.
    private class FlapTracker
    {
        private const float Window = 1.2f;
        private const int RequiredFlips = 3;

        private readonly List<float> _flipTimes = new List<float>();
        private HandState _prev = HandState.Neutral;

        public bool IsFlapping => _flipTimes.Count >= RequiredFlips;

        public void Observe(HandState current)
        {
            bool isFlip = (_prev == HandState.Up && current == HandState.Down)
                       || (_prev == HandState.Down && current == HandState.Up);
            if (isFlip)
                _flipTimes.Add(Time.time);
            _prev = current;

            _flipTimes.RemoveAll(t => Time.time - t > Window);
        }
    }
}
