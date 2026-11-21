using EcoKoin.Services;

namespace EcoKoin.Models
{
    /// <summary>Power profile. Spec: docs/03-domain-model.md (PowerScheme).</summary>
    public class PowerScheme
    {
        public PowerScheme(string schemeId, string schemeName, int cpuMaxPerformance)
        {
            SchemeId = schemeId;
            SchemeName = schemeName;
            CpuMaxPerformance = cpuMaxPerformance;
        }

        public string SchemeId { get; }
        public string SchemeName { get; }

        /// <summary>Percent (0..100).</summary>
        public int CpuMaxPerformance { get; }

        /// <summary>
        /// Applies via powercfg through <paramref name="runner"/> (default: real process runner):
        /// set PROCTHROTTLEMAX to CpuMaxPerformance, then /setactive. Throws InvalidOperationException if a command exits non-zero.
        /// </summary>
        public void Activate(IPowerCommandRunner? runner = null)
        {
            runner ??= new ProcessPowerCommandRunner();

            Run(runner, $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {CpuMaxPerformance}");
            Run(runner, $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {CpuMaxPerformance}");
            Run(runner, "/setactive SCHEME_CURRENT");
        }

        private static void Run(IPowerCommandRunner runner, string arguments)
        {
            var result = runner.Run("powercfg", arguments);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"powercfg {arguments} failed with exit code {result.ExitCode}: {result.Output}");
        }
    }
}
