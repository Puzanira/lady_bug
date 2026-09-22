using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LadyBug
{

// Reads player 2's joystick Arduino (see ArduinoFirmware/Joystick, or
// ArduinoFirmware/CombinedBoard if it's sharing one board with player 1's
// hand sensors instead of two separate ones) over serial and exposes its
// latest up/down/left/right switch state — plus, if the connected board is
// the combined variant, player 1's own 2 hand-sensor readings too (see
// HandLeftMm/HandRightMm below). Both boards identify themselves as plain
// "BOARD,JOYSTICK", so this is the one class that ends up owning the port
// either way — GestureSensorSerial (which owns the SEPARATE dedicated-
// sensor-board case) never touches this port at all, avoiding the two
// classes fighting over the same one. Same low-level approach as
// GestureSensorSerial (background-thread port I/O, macOS termios via direct
// libSystem calls since Unity doesn't expose System.IO.Ports) — duplicated
// rather than shared with it on purpose, matching this project's existing
// precedent of one self-contained reader per board (see GestureSensorSerial's
// own comment) so a change to one board's plumbing can't accidentally affect
// the other, and so both boards can be plugged in and identified
// independently at the same time.
//
// A third board also ends up here: the arcade cabinet's own panel, which
// puts every control on one Nano (Arduino/"Yandex (1).ino" — joystick, hand
// sensors, crank and 4 buttons). It answers the handshake with
// "BOARD,CABINET_PANEL", a string neither reader knows, but its joystick
// line is byte-for-byte CombinedBoard's, so IsJoystickDataLine adopts the
// port anyway. On top of that line it sends a "C,..." one carrying the
// crank and the panel buttons — see ParsePanelFields.
public sealed class JoystickSerial : MonoBehaviour
{
    public static JoystickSerial Instance { get; private set; }

    [SerializeField] private int baudRate = 115200;
    [SerializeField] private float portRetryInterval = 1f;
    [SerializeField] private float identificationTimeout = 3f;

    public bool IsConnected { get; private set; }
    public bool Up { get; private set; }
    public bool Down { get; private set; }
    public bool Left { get; private set; }
    public bool Right { get; private set; }

    // Only ever populated if the connected board sends hand readings (the
    // combined G+J line from CombinedBoard, or a legacy separate "G,..." line).
    public bool HasHandSensors { get; private set; }
    public int HandLeftMm { get; private set; } = -1;
    public int HandRightMm { get; private set; } = -1;

    // Cabinet panel buttons, from its "C,..." line. Stay false on the joystick
    // and combined boards, which never send that line — check HasPanelButtons
    // before reading them rather than treating "nothing pressed" as "no panel".
    // Nothing consumes these yet: this is the parsing half only, so that the
    // game side can be wired up against real data later.
    public bool HasPanelButtons { get; private set; }
    public bool RedButton { get; private set; }
    public bool GreenButton { get; private set; }
    public bool EscapeButton { get; private set; }

    // Just-pressed edges of the three above, true for the single frame the
    // press starts on — same held/just-pressed pair shape JoystickInput builds
    // for the stick directions. Holding a button gives exactly one edge and no
    // auto-repeat, which is what every consumer of these wants: they all
    // trigger a screen change (quit dialog, start, continue, skip).
    public bool RedButtonDown { get; private set; }
    public bool GreenButtonDown { get; private set; }
    public bool EscapeButtonDown { get; private set; }

    // No board of any kind on the other end of a port. The same question
    // StartScreenController.IsHardwareConnected asks, kept here in one place
    // and asked negatively, because that is the form the Esc fallback below
    // needs.
    public static bool NoBoardConnected =>
        (Instance == null || !Instance.IsConnected)
        && (GestureSensorSerial.Instance == null || !GestureSensorSerial.Instance.IsConnected);

    // The cabinet's SYSTEM button, with Esc standing in for it whenever no
    // board is connected at all. Three screens need this same answer — the
    // run's quit dialog, training, and the menu's way back to the loader — so
    // it lives here rather than being spelled out at each of them.
    //
    // Esc is deliberately gated on there being no hardware: with the cabinet
    // plugged in, SYSTEM is the button for this and a stray keypress on a
    // keyboard nobody is looking at should not back players out of a game.
    //
    // Inside the hub this is flatly false, and that is a contract, not a tuning
    // choice: on the cabinet the way out of a game is the touch «меню» button, which
    // the launcher handles itself — the game is not supposed to know it exists. Both
    // halves above would otherwise leak in. EscapeButtonDown stays false because the
    // facade publishes no system button (ApplyArcadePanelButtons), but the Esc half
    // is only held off by both readers reporting themselves connected, which is not
    // true on the very first frames before their Update has run. A stray Esc there
    // would back the player out to lady_bug's own attract screen on top of the
    // launcher. Said once, here, rather than at each of the three call sites.
    public static bool SystemMenuDown =>
        !ArcadeControlsReader.Available
        && ((Instance != null && Instance.EscapeButtonDown)
            || (NoBoardConnected && Input.GetKeyDown(KeyCode.Escape)));

    private Thread _thread;
    private volatile bool _stopRequested;
    private volatile bool _connected;
    private readonly object _lock = new object();
    private readonly int[] _latest = { 0, 0, 0, 0 };
    private readonly int[] _latestHands = { -1, -1 };
    private readonly int[] _latestPanelButtons = { 0, 0, 0 };
    private bool _hasNewValues;
    private bool _hasNewHandValues;
    private bool _hasNewPanelButtons;
    private bool _prevRedButton;
    private bool _prevGreenButton;
    private bool _prevEscapeButton;
    private bool _wasConnected;
    private float _lastHandValuesTime;
    private const float HandValuesStaleSeconds = 0.5f;

    // Cabinet panel line: "C,<crank>,<red>,<green>,<action>,<system>" — six
    // fields counting the "C" tag. Kept as named indices because which
    // physical button sits on which pin is the one thing here that can still
    // move: the firmware header says as much ("ЕСЛИ на стойке подписи иные —
    // поменяй define'ы").
    private const int PanelFieldCount = 6;
    private const int PanelRedField = 2;
    private const int PanelGreenField = 3;
    // The panel has two buttons besides red and green: ACTION («!») on field 4
    // and SYSTEM («меню») on field 5, in the firmware's own mapping. The
    // cabinet's exit button is SYSTEM — confirmed by the project owner, not
    // inferred; nothing in the repository records it, since the loader's panel
    // diagram (Sprites/loader/ControlPanelDiagram.png) draws only the yellow,
    // red and green buttons. Should the panel ever be re-wired, make this 4 for
    // ACTION — the index is not repeated anywhere else.
    private const int PanelEscapeField = 5;

    private void Awake()
    {
        Instance = this;
    }

    // Arcade cabinet: never open the port here — the hub's arcade-controls package
    // already owns the one combo board that carries this joystick (see the same
    // note on GestureSensorSerial). Since the protocol simplification the combo
    // board sends the joystick AND both hand sensors as ONE
    // "G,<left>,<right>,J,<u>,<d>,<l>,<r>" line, so a second reader on the port
    // would tear that single frame in half — all the more reason to stay off it.
    // The switch state comes from ArcadeInput.Joystick instead, so JoystickInput
    // and everything above it are untouched.
    private bool UseArcadeFacade => ArcadeControlsReader.Available;

    // The cabinet stick is analog (-1..1 per axis); the author's own firmware
    // already thresholds its stick into 4 discrete switches before sending it, so
    // this reader's contract is discrete. Half deflection is the same cut-off the
    // rest of the arcade port uses.
    private const float ArcadeAxisThreshold = 0.5f;

    private void OnEnable()
    {
        if (UseArcadeFacade)
            return; // hub owns the port — see UseArcadeFacade

        _stopRequested = false;
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "JoystickSerial" };
        _thread.Start();
    }

    private void OnDisable()
    {
        _stopRequested = true;
        _thread?.Join(500);
        _connected = false;
    }

    private void Update()
    {
        if (UseArcadeFacade)
        {
            ApplyArcadeJoystick();
            UpdatePanelButtonEdges();
            return;
        }

        bool connected = _connected;
        IsConnected = connected;

        if (!connected && _wasConnected)
        {
            ClearHandReadings();
            ClearPanelButtons();
        }

        _wasConnected = connected;

        lock (_lock)
        {
            if (_hasNewValues)
            {
                _hasNewValues = false;
                Up = _latest[0] != 0;
                Down = _latest[1] != 0;
                Left = _latest[2] != 0;
                Right = _latest[3] != 0;
            }

            if (_hasNewHandValues)
            {
                _hasNewHandValues = false;
                HandLeftMm = _latestHands[0];
                HandRightMm = _latestHands[1];
                _lastHandValuesTime = Time.realtimeSinceStartup;
            }

            if (_hasNewPanelButtons)
            {
                _hasNewPanelButtons = false;
                RedButton = _latestPanelButtons[0] != 0;
                GreenButton = _latestPanelButtons[1] != 0;
                EscapeButton = _latestPanelButtons[2] != 0;
            }
        }

        UpdatePanelButtonEdges();

        if (HasHandSensors
            && connected
            && Time.realtimeSinceStartup - _lastHandValuesTime > HandValuesStaleSeconds)
        {
            ClearHandReadings();
        }
    }

    private void ApplyArcadeJoystick()
    {
        IsConnected = true;

        Vector2 v = ArcadeControlsReader.Joystick;
        Up = v.y > ArcadeAxisThreshold;
        Down = v.y < -ArcadeAxisThreshold;
        Left = v.x < -ArcadeAxisThreshold;
        Right = v.x > ArcadeAxisThreshold;

        // The cabinet IS a combined board, so this class's own hand-sensor fields
        // get the cabinet's heights too, in the same millimetres the "G," parser
        // below would have produced. HasHandSensors is what gates every downstream
        // combined-board branch (GestureInput.TryGetLiveHandDistances /
        // HaveRealSensorFeed, GameplayHudVisibility.HasSensorHudFeed,
        // StartScreenController's sensor preview) — leaving it false would strand
        // all of them on a cabinet that demonstrably has both sensors.
        HasHandSensors = true;
        HandLeftMm = ArcadeControlsReader.HeightToSensorMm(ArcadeControlsReader.HeightA);
        HandRightMm = ArcadeControlsReader.HeightToSensorMm(ArcadeControlsReader.HeightB);

        ApplyArcadePanelButtons();
    }

    /// <summary>
    /// The cabinet's red and green, delivered by the hub instead of by this class's
    /// own "C,…" parser.
    ///
    /// The parser above is the author's, written for a panel he has never had in
    /// front of him — the board is ours, and inside the hub the arcade-controls
    /// package is the thing that actually talks to it. So on the cabinet not one
    /// line of ParsePanelFields runs: the port is never opened, no "C,…" ever
    /// arrives, and every consumer the author wired up this update (the quit
    /// dialog's ДА/НЕТ, the green confirm on the start row, the 10 km continue
    /// prompt, skipping a recap page) would sit dead with the buttons physically
    /// under the player's hand. Filling the same four fields off the facade is what
    /// keeps his game-side wiring working here, without the game owning the port.
    ///
    /// Outside the cabinet the facade does not exist, this never runs, and his
    /// panel parsing is the only source — exactly as he wrote it.
    /// </summary>
    private void ApplyArcadePanelButtons()
    {
        // The hub only publishes the two PLAYER buttons. There is deliberately no
        // EscapeButton here: the cabinet's way out of a game is the touch «меню»
        // button, and the hub — not the game — owns it. Leaving this false is what
        // keeps SystemMenuDown false in the hub, so the game never opens its own
        // quit dialog or backs itself out to its own attract screen inside the
        // launcher. See StartScreenController.ReturnToLoaderScreen.
        HasPanelButtons = true;
        RedButton = ArcadeControlsReader.RedHeld;
        GreenButton = ArcadeControlsReader.GreenHeld;
        EscapeButton = false;
    }

    // Held states -> single-frame edges. Every frame, not just on frames a fresh
    // line landed: the held states only change when one does, so comparing against
    // last frame's value is what turns them into edges. Shared by the serial path
    // and the facade path, so the two cannot drift.
    private void UpdatePanelButtonEdges()
    {
        RedButtonDown = RedButton && !_prevRedButton;
        _prevRedButton = RedButton;

        GreenButtonDown = GreenButton && !_prevGreenButton;
        _prevGreenButton = GreenButton;

        EscapeButtonDown = EscapeButton && !_prevEscapeButton;
        _prevEscapeButton = EscapeButton;
    }

    private void ClearHandReadings()
    {
        HandLeftMm = -1;
        HandRightMm = -1;
    }

    // Let go of every button when the board goes away, so a press that was
    // being held at the moment the cable was pulled doesn't stay latched.
    private void ClearPanelButtons()
    {
        RedButton = false;
        GreenButton = false;
        EscapeButton = false;

        RedButtonDown = false;
        GreenButtonDown = false;
        EscapeButtonDown = false;

        // Cleared alongside the states themselves, so the held/previous pair
        // stays consistent: a button that was down when the cable was pulled
        // must not read as a fresh press when the board comes back.
        _prevRedButton = false;
        _prevGreenButton = false;
        _prevEscapeButton = false;
    }

    private void RunLoop()
    {
        while (!_stopRequested)
        {
            string port = FindJoystickBoardPort();
            if (port == null)
            {
                Thread.Sleep((int)(portRetryInterval * 1000f));
                continue;
            }

            try
            {
                ReadFromPort(port);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[JoystickSerial] " + port + ": " + exception.Message);
            }

            _connected = false;
        }
    }

    // Tries every likely USB-serial device in turn, asking each "who are
    // you?" (send "?", expect "BOARD,JOYSTICK" back) so it doesn't
    // accidentally grab the gesture-sensor board (or an unrelated Arduino)
    // plugged in at the same time.
    private string FindJoystickBoardPort()
    {
        string[] candidates;
        try
        {
            candidates = Directory.GetFiles("/dev", "cu.*")
                .Where(p =>
                {
                    string lower = p.ToLowerInvariant();
                    return lower.Contains("usbserial") || lower.Contains("usbmodem") || lower.Contains("wchusbserial");
                })
                .ToArray();
        }
        catch (Exception)
        {
            return null;
        }

        foreach (string candidate in candidates)
        {
            if (_stopRequested)
                return null;
            if (TryIdentify(candidate))
                return candidate;
        }

        return null;
    }

    private bool TryIdentify(string portPath)
    {
        lock (MacSerialPort.ProbeLock)
        {
            int fd = OpenPort(portPath);
            if (fd < 0)
            {
                Debug.LogWarning("[JoystickSerial] Cannot open " + portPath
                    + " — close Arduino Serial Monitor / Plotter if it's using this port.");
                return false;
            }

            try
            {
                MacSerialPort.SetDtr(fd, true);
                Thread.Sleep(800);
                MacNative.tcflush(fd, MacNative.FlushInputAndOutput);
                WriteAscii(fd, "?");

                DateTime deadline = DateTime.UtcNow.AddSeconds(identificationTimeout);
                StringBuilder line = new StringBuilder();
                byte[] buffer = new byte[1];

                while (DateTime.UtcNow < deadline)
                {
                    long read = MacNative.read(fd, buffer, (UIntPtr)1);
                    if (read <= 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    char c = (char)buffer[0];
                    if (c == '\n')
                    {
                        string trimmed = line.ToString().Trim();
                        line.Length = 0;
                        if (trimmed == "BOARD,JOYSTICK" || IsJoystickDataLine(trimmed))
                            return true;
                        if (trimmed == "BOARD,GESTURE_SENSORS")
                            return false;
                    }
                    else if (c != '\r')
                    {
                        line.Append(c);
                    }
                }

                return false;
            }
            finally
            {
                MacNative.close(fd);
            }
        }
    }

    static bool IsJoystickDataLine(string line)
    {
        if (line.StartsWith("J,"))
            return line.Split(',').Length == 5;
        if (line.StartsWith("G,") && line.IndexOf(",J,", StringComparison.Ordinal) >= 0)
            return line.Split(',').Length == 8;
        if (line.StartsWith("G,"))
            return line.Split(',').Length == 3;
        return false;
    }

    private void ReadFromPort(string portPath)
    {
        int fd = OpenPort(portPath);
        if (fd < 0)
            throw new IOException("Could not open " + portPath);

        try
        {
            MacSerialPort.SetDtr(fd, true);
            Thread.Sleep(600);
            MacNative.tcflush(fd, MacNative.FlushInputAndOutput);

            _connected = true;
            HasHandSensors = false;
            HasPanelButtons = false;
            ClearHandReadings();
            ClearPanelButtons();
            Debug.Log("[JoystickSerial] Connected: " + portPath);

            StringBuilder line = new StringBuilder();
            byte[] buffer = new byte[1];

            while (!_stopRequested)
            {
                long read = MacNative.read(fd, buffer, (UIntPtr)1);
                if (read <= 0)
                {
                    Thread.Sleep(2);
                    continue;
                }

                char c = (char)buffer[0];
                if (c == '\n')
                {
                    ParseLine(line.ToString().Trim());
                    line.Length = 0;
                }
                else if (c != '\r')
                {
                    line.Append(c);
                }
            }
        }
        finally
        {
            MacNative.close(fd);
        }
    }

    // CombinedBoard: "G,<left_mm>,<right_mm>,J,<up>,<down>,<left>,<right>"
    // Standalone Joystick: "J,<up>,<down>,<left>,<right>" only.
    // Cabinet panel: the same combined G+J line, plus "C,<crank>,<red>,
    // <green>,<action>,<system>" once per cycle.
    private void ParseLine(string trimmedLine)
    {
        if (trimmedLine.StartsWith("C,", StringComparison.Ordinal))
        {
            ParsePanelFields(trimmedLine);
            return;
        }

        if (trimmedLine.StartsWith("G,", StringComparison.Ordinal))
        {
            int jMarker = trimmedLine.IndexOf(",J,", StringComparison.Ordinal);
            if (jMarker >= 0)
            {
                ParseGestureFields(trimmedLine.Substring(0, jMarker));
                ParseJoystickFields(trimmedLine.Substring(jMarker + 1));
                return;
            }

            ParseGestureFields(trimmedLine);
            return;
        }

        if (trimmedLine.StartsWith("J,", StringComparison.Ordinal))
            ParseJoystickFields(trimmedLine);
    }

    private void ParseJoystickFields(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("J,", StringComparison.Ordinal))
            return;

        string[] fields = trimmedLine.Split(',');
        if (fields.Length != 5)
            return;

        int[] values = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(fields[i + 1], out values[i]))
                return;
        }

        lock (_lock)
        {
            Array.Copy(values, _latest, 4);
            _hasNewValues = true;
        }
    }

    // Only the three buttons the game was asked for are lifted out of the line.
    // The crank and the spare fourth button are left unread on purpose — the
    // field count is still checked in full, so a panel whose line shape
    // changes stops being parsed instead of being misread.
    private void ParsePanelFields(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("C,", StringComparison.Ordinal))
            return;

        string[] fields = trimmedLine.Split(',');
        if (fields.Length != PanelFieldCount)
            return;

        int[] values = new int[3];
        if (!int.TryParse(fields[PanelRedField], out values[0])
            || !int.TryParse(fields[PanelGreenField], out values[1])
            || !int.TryParse(fields[PanelEscapeField], out values[2]))
        {
            return;
        }

        lock (_lock)
        {
            HasPanelButtons = true;
            Array.Copy(values, _latestPanelButtons, 3);
            _hasNewPanelButtons = true;
        }
    }

    private void ParseGestureFields(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("G,", StringComparison.Ordinal))
            return;

        string[] fields = trimmedLine.Split(',');
        if (fields.Length != 3)
            return;

        int[] values = new int[2];
        if (!int.TryParse(fields[1], out values[0]) || !int.TryParse(fields[2], out values[1]))
            return;

        values[0] = GestureInput.SanitizeDistanceMm(values[0]);
        values[1] = GestureInput.SanitizeDistanceMm(values[1]);

        lock (_lock)
        {
            HasHandSensors = true;
            Array.Copy(values, _latestHands, 2);
            _hasNewHandValues = true;
        }
    }

    private int OpenPort(string portPath)
    {
        int fd = MacNative.open(portPath, MacNative.OpenReadWrite | MacNative.OpenNoControllingTerminal | MacNative.OpenNonBlocking);
        if (fd < 0)
            return -1;

        var settings = new MacNative.Termios { controlCharacters = new byte[MacNative.ControlCharacterCount] };
        if (MacNative.tcgetattr(fd, ref settings) != 0)
        {
            MacNative.close(fd);
            return -1;
        }

        MacNative.cfmakeraw(ref settings);
        settings.controlFlags |= MacNative.EnableReceiver | MacNative.IgnoreModemControlLines;
        settings.controlFlags &= ~(MacNative.EnableParity | MacNative.TwoStopBits | MacNative.HardwareFlowControl);

        ulong speed = (ulong)baudRate;
        if (MacNative.cfsetispeed(ref settings, speed) != 0 ||
            MacNative.cfsetospeed(ref settings, speed) != 0 ||
            MacNative.tcsetattr(fd, MacNative.ApplyNow, ref settings) != 0)
        {
            MacNative.close(fd);
            return -1;
        }

        MacNative.tcflush(fd, MacNative.FlushInputAndOutput);
        return fd;
    }

    private static void WriteAscii(int fd, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        MacNative.write(fd, bytes, (UIntPtr)bytes.Length);
    }

    private static class MacNative
    {
        private const string LibSystem = "libSystem.B.dylib";

        internal const int ControlCharacterCount = 20;
        internal const int OpenReadWrite = 0x0002;
        internal const int OpenNonBlocking = 0x0004;
        internal const int OpenNoControllingTerminal = 0x20000;
        internal const int ApplyNow = 0;
        internal const int FlushInputAndOutput = 3;
        internal const ulong EnableReceiver = 0x00000800;
        internal const ulong EnableParity = 0x00001000;
        internal const ulong TwoStopBits = 0x00000400;
        internal const ulong IgnoreModemControlLines = 0x00008000;
        internal const ulong HardwareFlowControl = 0x00030000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Termios
        {
            internal ulong inputFlags;
            internal ulong outputFlags;
            internal ulong controlFlags;
            internal ulong localFlags;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ControlCharacterCount)]
            internal byte[] controlCharacters;

            internal ulong inputSpeed;
            internal ulong outputSpeed;
        }

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int open(string path, int flags);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int close(int fileDescriptor);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern long read(int fileDescriptor, [Out] byte[] buffer, UIntPtr count);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern long write(int fileDescriptor, byte[] buffer, UIntPtr count);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int tcgetattr(int fileDescriptor, ref Termios settings);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int tcsetattr(int fileDescriptor, int optionalActions, ref Termios settings);

        [DllImport(LibSystem)]
        internal static extern void cfmakeraw(ref Termios settings);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int cfsetispeed(ref Termios settings, ulong speed);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int cfsetospeed(ref Termios settings, ulong speed);

        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int tcflush(int fileDescriptor, int queueSelector);
    }
}
}
