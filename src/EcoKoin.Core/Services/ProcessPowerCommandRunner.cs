using System.Diagnostics;

namespace EcoKoin.Services
{
    /// <summary>Runs an external process (no shell), captures stdout+stderr, 10 s timeout.</summary>
    public class ProcessPowerCommandRunner : IPowerCommandRunner
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public CommandResult Run(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(Timeout))
                {
                    try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    return new CommandResult(-1, $"'{fileName}' timed out after {Timeout.TotalSeconds:0} s.");
                }

                process.WaitForExit(); // flush async output
                return new CommandResult(process.ExitCode, (stdout.Result + stderr.Result).Trim());
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return new CommandResult(-1, $"Could not start '{fileName}': {ex.Message}");
            }
        }
    }
}
