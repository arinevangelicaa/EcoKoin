using System;
using System.IO;
using System.Text.RegularExpressions;
using EcoKoin.Services;

namespace EcoKoin.WPF.Models
{
    // Changes the Windows sleep (standby) timeout of the ACTIVE power plan with powercfg.
    // Only an integer in 1..720 is ever placed into the command line, so nothing user-supplied is interpreted.
    public static class WindowsSleepTimeout
    {
        public const int MinMinutes = 1;
        public const int MaxMinutes = 720;

        private static readonly Regex DelayPattern = new(@"^\s*(\d{1,3})\s+menit\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Parses a saved preference such as "5 menit". "Tidak pernah" and anything else outside 1..720 gives false.</summary>
        public static bool TryParseMinutes(string? delay, out int minutes)
        {
            minutes = 0;
            if (delay is null)
                return false;
            var match = DelayPattern.Match(delay);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int value))
                return false;
            if (value < MinMinutes || value > MaxMinutes)
                return false;
            minutes = value;
            return true;
        }

        /// <summary>Blocking: call off the UI thread.</summary>
        public static WindowsActionResult Apply(int minutes, IPowerCommandRunner runner)
        {
            if (minutes < MinMinutes || minutes > MaxMinutes)
                return new WindowsActionResult(false, "Waktu sleep harus antara 1 dan 720 menit.");

            string powercfg = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
            foreach (string target in new[] { "standby-timeout-ac", "standby-timeout-dc" })
            {
                CommandResult result = runner.Run(powercfg, $"/change {target} {minutes}");
                if (result.ExitCode != 0)
                {
                    string detail = string.IsNullOrWhiteSpace(result.Output) ? $"kode keluar {result.ExitCode}" : result.Output;
                    return new WindowsActionResult(false, $"Windows menolak perubahan ({target}): {detail}");
                }
            }

            return new WindowsActionResult(true,
                $"Sleep otomatis untuk paket daya aktif sekarang {minutes} menit (listrik dan baterai).");
        }
    }
}
