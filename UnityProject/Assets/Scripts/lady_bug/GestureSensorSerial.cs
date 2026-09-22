using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

// Reads the gesture-sensor Arduino (see ArduinoFirmware/GestureSensors) over
// serial and exposes the latest hand-distance readings (player 1, left/right
// mm). Port I/O runs on a background thread; Update()
// just publishes the latest snapshot to the main thread. macOS only,
// matching this project's build target (BuildScript.cs only builds
// StandaloneOSX) — Unity doesn't expose System.IO.Ports, so this talks to
// the OS termios API directly, the same approach as
// ArduinoFiles4WorkShop/UnityGrabber/ArduinoSensorsGrabber.cs, trimmed down
// to one known board instead of that script's generic scan.
public sealed class GestureSensorSerial : MonoBehaviour
{
    public static GestureSensorSerial Instance { get; private set; }

    [SerializeField] private int baudRate = 115200;
    [SerializeField] private float portRetryInterval = 1f;
    [SerializeField] private float identificationTimeout = 3f;

    public bool IsConnected { get; private set; }
    public int Player1LeftMm { get; private set; } = -1;
    public int Player1RightMm { get; private set; } = -1;

    // No exit button on this board: the sketch behind it (GestureSensors.ino)
    // sends nothing but "G,<left>,<right>", and the physical exit button ended
    // up on the cabinet panel instead — read it from
    // JoystickSerial.EscapeButtonDown, which is what DuckToExitController now
    // does. A scaffold property used to sit here for it and could only ever
    // read false.

    private Thread _thread;
    private volatile bool _stopRequested;
    private volatile bool _connected;
    private readonly object _lock = new object();
    private readonly int[] _latest = { -1, -1 };
    private bool _hasNewValues;
    private bool _wasConnected;
    private float _lastValuesTime;
    private const float ValuesStaleSeconds = 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        _stopRequested = false;
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "GestureSensorSerial" };
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
        bool connected = _connected;
        IsConnected = connected;

        if (!connected && _wasConnected)
            ClearReadings();

        _wasConnected = connected;

        lock (_lock)
        {
            if (_hasNewValues)
            {
                _hasNewValues = false;
                Player1LeftMm = _latest[0];
                Player1RightMm = _latest[1];
                _lastValuesTime = Time.realtimeSinceStartup;
            }
        }

        if (connected && Time.realtimeSinceStartup - _lastValuesTime > ValuesStaleSeconds)
            ClearReadings();
    }

    private void ClearReadings()
    {
        Player1LeftMm = -1;
        Player1RightMm = -1;
    }

    private void RunLoop()
    {
        while (!_stopRequested)
        {
            string port = FindGestureBoardPort();
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
                Debug.LogWarning("[GestureSensorSerial] " + port + ": " + exception.Message);
            }

            _connected = false;
        }
    }

    // Tries every likely USB-serial device in turn, asking each "who are
    // you?" (send "?", expect "BOARD,GESTURE_SENSORS" back) so it doesn't
    // accidentally grab an unrelated Arduino plugged in at the same time.
    private string FindGestureBoardPort()
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
                return false;

            try
            {
                MacSerialPort.SetDtr(fd, true);
                Thread.Sleep(150);
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
                        if (trimmed == "BOARD,GESTURE_SENSORS")
                            return true;
                        // Combined / joystick / cabinet-panel board — not ours; bail
                        // immediately so JoystickSerial can probe the same port
                        // without waiting. This matters more than it looks: the whole
                        // probe runs inside MacSerialPort.ProbeLock, so sitting out
                        // the full identificationTimeout here blocks the other
                        // reader's probing too, not just this one's.
                        //
                        // The cabinet panel needs both of its own checks. It answers
                        // the handshake with "BOARD,CABINET_PANEL", and if that reply
                        // is missed (flushed, or the board was already mid-cycle when
                        // we opened the port) its "C,..." line arrives within one
                        // poll anyway. Its data line can't be used for this: it also
                        // sends "G,..." lines, which is exactly what a real
                        // sensor board sends.
                        if (trimmed == "BOARD,JOYSTICK"
                            || trimmed == "BOARD,CABINET_PANEL"
                            || trimmed.StartsWith("J,")
                            || trimmed.StartsWith("C,"))
                        {
                            return false;
                        }
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

    private void ReadFromPort(string portPath)
    {
        int fd = OpenPort(portPath);
        if (fd < 0)
            throw new IOException("Could not open " + portPath);

        try
        {
            MacSerialPort.SetDtr(fd, true);
            Thread.Sleep(400);
            MacNative.tcflush(fd, MacNative.FlushInputAndOutput);

            _connected = true;
            ClearReadings();
            Debug.Log("[GestureSensorSerial] Connected: " + portPath);

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

    // Expects "G,<left_mm>,<right_mm>" — see ArduinoFirmware/GestureSensors.
    private void ParseLine(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("G,"))
            return;

        string[] fields = trimmedLine.Split(',');
        if (fields.Length != 3)
            return;

        int[] values = new int[2];
        for (int i = 0; i < 2; i++)
        {
            if (!int.TryParse(fields[i + 1], out values[i]))
                return;
        }

        values[0] = GestureInput.SanitizeDistanceMm(values[0]);
        values[1] = GestureInput.SanitizeDistanceMm(values[1]);

        lock (_lock)
        {
            Array.Copy(values, _latest, 2);
            _hasNewValues = true;
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
