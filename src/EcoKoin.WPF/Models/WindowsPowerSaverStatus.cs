using System.Runtime.InteropServices;
using System.Windows.Interop;
using EcoKoin.Services;
using Windows.System.Power;

namespace EcoKoin.WPF.Models
{
    internal sealed class WindowsPowerSaverStatus
    {
        private const string PowerSaverSchemeId = "a1841308-3541-4fab-bc81-f71556f20b4a";
        private readonly ProcessPowerCommandRunner _runner = new();
        private static readonly Guid EnergySaverSetting = new("550e8400-e29b-41d4-a716-446655440000");
        private volatile int _energySaver = -1;
        private HwndSource? _source;
        private IntPtr _notification;

        public void Register(HwndSource? source)
        {
            if (source is null)
                return;

            _source = source;
            source.AddHook(OnPowerMessage);
            var setting = EnergySaverSetting;
            _notification = RegisterPowerSettingNotification(source.Handle, ref setting, 0);
        }

        public void Unregister()
        {
            if (_notification != IntPtr.Zero)
                UnregisterPowerSettingNotification(_notification);
            if (_source is { IsDisposed: false })
                _source.RemoveHook(OnPowerMessage);
        }

        private IntPtr OnPowerMessage(IntPtr hwnd, int message, IntPtr parameter, IntPtr data, ref bool handled)
        {
            const int powerBroadcast = 0x0218, powerSettingChange = 0x8013;
            if (message == powerBroadcast && parameter.ToInt64() == powerSettingChange &&
                data != IntPtr.Zero && Marshal.PtrToStructure<Guid>(data) == EnergySaverSetting &&
                Marshal.ReadInt32(data, 16) >= sizeof(int))
            {
                _energySaver = Marshal.ReadInt32(data, 20);
            }

            return IntPtr.Zero;
        }

        public static (int? Percent, string Status) ReadBattery()
        {
            if (!GetSystemPowerStatus(out var status))
                return (null, "Status daya tidak tersedia");

            int? percent = status.BatteryLifePercent <= 100 && (status.BatteryFlag & 128) == 0
                ? status.BatteryLifePercent
                : null;
            string label = status.AcLineStatus switch
            {
                0 => "Menggunakan baterai",
                1 when status.BatteryFlag != 255 && (status.BatteryFlag & 8) != 0 => "Sedang mengisi daya",
                1 when (status.BatteryFlag & 128) != 0 && status.BatteryFlag != 255 => "Tersambung listrik · tanpa baterai",
                1 => "Tersambung listrik",
                _ => "Status daya tidak tersedia"
            };
            return (percent, label);
        }

        public bool? IsActive()
        {
            bool statusAvailable = GetSystemPowerStatus(out var status);
            if (_energySaver is 1 or 2 || (_energySaver < 0 && statusAvailable && status.SystemStatusFlag == 1))
                return true;

            try
            {
                if (_energySaver < 0 && PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                    return true;
            }
            catch (Exception ex) when (ex is COMException or PlatformNotSupportedException)
            {
                // Some Windows installations do not expose the WinRT power status to desktop apps.
            }

            var scheme = _runner.Run("powercfg", "/getactivescheme");
            return scheme.ExitCode == 0
                ? scheme.Output.Contains(PowerSaverSchemeId, StringComparison.OrdinalIgnoreCase)
                : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus
        {
            public byte AcLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid setting, int flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    }
}
