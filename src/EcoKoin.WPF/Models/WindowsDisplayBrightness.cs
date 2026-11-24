using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace EcoKoin.WPF.Models
{
    // Sets the built-in display brightness through WMI (WmiMonitorBrightnessMethods).
    // The PowerShell script is a fixed template; the only interpolated value is a validated integer (10..100).
    public static class WindowsDisplayBrightness
    {
        public const int MinPercent = 10;
        public const int MaxPercent = 100;

        private const int NoInstanceExitCode = 3;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static readonly Regex RecommendationPattern = new(@"^\s*Turunkan kecerahan ke (\d{1,3})%\s*$",
            RegexOptions.CultureInvariant);

        public const string UnsupportedMessage =
            "Layar ini tidak mendukung pengaturan kecerahan lewat Windows (umum di PC desktop).";

        /// <summary>Reads the target from text such as "Turunkan kecerahan ke 40%" and clamps it to 10..100.</summary>
        public static bool TryParseTarget(string? recommendation, out int percent)
        {
            percent = 0;
            if (recommendation is null)
                return false;
            var match = RecommendationPattern.Match(recommendation);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                return false;
            percent = Math.Clamp(value, MinPercent, MaxPercent);
            return true;
        }

        /// <summary>Blocking (up to 10 s): call off the UI thread.</summary>
        public static WindowsActionResult Apply(int percent)
        {
            if (percent < MinPercent || percent > MaxPercent)
                return new WindowsActionResult(false, "Nilai kecerahan harus antara 10% dan 100%.");

            string script =
                "$ErrorActionPreference = 'Stop'; " +
                "try { " +
                "$m = @(Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightnessMethods); " +
                $"if ($m.Count -eq 0) {{ exit {NoInstanceExitCode} }}; " +
                "$m | ForEach-Object { Invoke-CimMethod -InputObject $_ -MethodName WmiSetBrightness " +
                "-Arguments @{Timeout=1;Brightness=" + percent.ToString(CultureInfo.InvariantCulture) + "} | Out-Null }; " +
                "exit 0 " +
                "} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 5 }";

            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);

            try
            {
                using var process = Process.Start(psi);
                if (process is null)
                    return new WindowsActionResult(false, "Windows PowerShell tidak bisa dijalankan.");

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(Timeout))
                {
                    try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    return new WindowsActionResult(false, "Windows tidak merespons dalam 10 detik, kecerahan tidak diubah.");
                }

                process.WaitForExit();
                if (process.ExitCode == 0)
                    return new WindowsActionResult(true, $"Kecerahan layar diatur ke {percent}%.");
                if (process.ExitCode == NoInstanceExitCode)
                    return new WindowsActionResult(false, UnsupportedMessage);

                string detail = (stdout.Result + stderr.Result).Trim();
                // Typical failure on desktop monitors / external displays: the WMI class or method is absent.
                if (detail.Contains("WmiMonitorBrightnessMethods", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("Not supported", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("tidak didukung", StringComparison.OrdinalIgnoreCase))
                    return new WindowsActionResult(false, UnsupportedMessage);

                return new WindowsActionResult(false, string.IsNullOrEmpty(detail)
                    ? $"Pengaturan kecerahan gagal (kode keluar {process.ExitCode})."
                    : $"Pengaturan kecerahan gagal: {detail}");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return new WindowsActionResult(false, $"Windows PowerShell tidak bisa dijalankan: {ex.Message}");
            }
        }
    }
}
