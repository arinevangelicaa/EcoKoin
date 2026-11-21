using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EcoKoin.Services
{
    /// <summary>Idle time via Win32 GetLastInputInfo. Windows only; returns zero elsewhere.</summary>
    public class WindowsIdleTimeProvider : IIdleTimeProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [SupportedOSPlatform("windows")]
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public TimeSpan GetIdleTime()
        {
            if (!OperatingSystem.IsWindows())
                return TimeSpan.Zero;
            return GetWindowsIdleTime();
        }

        [SupportedOSPlatform("windows")]
        private static TimeSpan GetWindowsIdleTime()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info))
                return TimeSpan.Zero;

            // Both values are 32-bit tick counts that wrap; unsigned subtraction handles wrap-around.
            uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
            return TimeSpan.FromMilliseconds(idleMs);
        }
    }
}
