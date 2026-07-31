using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

// Reads the gesture-sensor Arduino (see ArduinoFirmware/GestureSensors) over
// serial and exposes the latest hand-distance readings and brake-button
// state for both players. Port I/O runs on a background thread; Update()
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
    public bool Player1Brake { get; private set; }
    public int Player2LeftMm { get; private set; } = -1;
    public int Player2RightMm { get; private set; } = -1;
    public bool Player2Brake { get; private set; }

    // Scaffold for an upcoming physical exit button on the controller —
    // which pin/button isn't decided yet, so this isn't wired into
    // ParseLine/the "G,..." wire protocol below at all yet and always
    // reads false. Once the button is chosen, extend the firmware sketch's
    // line format and set this from the new field the same way Player1Brake
    // etc. are set below — DuckToExitController already reacts to this
    // going true the instant it's wired, no other changes needed there.
    public bool ExitButtonPressed { get; private set; }

    private Thread _thread;
    private volatile bool _stopRequested;
    private volatile bool _connected;
    private readonly object _lock = new object();
    private readonly int[] _latest = { -1, -1, -1, -1, -1, -1 };
    private bool _hasNewValues;

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
        IsConnected = _connected;

        lock (_lock)
        {
            if (!_hasNewValues)
                return;

            _hasNewValues = false;
            Player1LeftMm = _latest[0];
            Player1RightMm = _latest[1];
            Player1Brake = _latest[2] != 0;
            Player2LeftMm = _latest[3];
            Player2RightMm = _latest[4];
            Player2Brake = _latest[5] != 0;
        }
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
        int fd = OpenPort(portPath);
        if (fd < 0)
            return false;

        try
        {
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
                    if (line.ToString().Trim() == "BOARD,GESTURE_SENSORS")
                        return true;
                    line.Length = 0;
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

    private void ReadFromPort(string portPath)
    {
        int fd = OpenPort(portPath);
        if (fd < 0)
            throw new IOException("Could not open " + portPath);

        try
        {
            _connected = true;
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

    // Expects "G,<p1Left>,<p1Right>,<p1Brake>,<p2Left>,<p2Right>,<p2Brake>" —
    // see ArduinoFirmware/GestureSensors for the exact protocol this is
    // matched against. Brake fields are 0/1, distances are millimetres.
    private void ParseLine(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("G,"))
            return;

        string[] fields = trimmedLine.Split(',');
        if (fields.Length != 7)
            return;

        int[] values = new int[6];
        for (int i = 0; i < 6; i++)
        {
            if (!int.TryParse(fields[i + 1], out values[i]))
                return;
        }

        lock (_lock)
        {
            Array.Copy(values, _latest, 6);
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
