using System.Runtime.InteropServices;

namespace LadyBug
{


// Shared macOS serial helpers for the two board readers — keeps port probing
// from fighting itself and asserts DTR like Arduino IDE does (CH340/WCH).
internal static class MacSerialPort
{
    internal static readonly object ProbeLock = new object();

    internal static void SetDtr(int fd, bool on)
    {
        const int TioCmDtr = 0x002;
        const uint TioCmbis = 0x8004746c;
        const uint TioCmbic = 0x8004746b;
        int arg = TioCmDtr;
        MacNative.ioctl(fd, on ? TioCmbis : TioCmbic, ref arg);
    }

    internal static class MacNative
    {
        [DllImport("libSystem.B.dylib", SetLastError = true)]
        internal static extern int ioctl(int fileDescriptor, uint request, ref int arg);
    }
}
}
