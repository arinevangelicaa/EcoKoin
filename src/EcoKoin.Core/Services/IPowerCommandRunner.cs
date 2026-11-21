namespace EcoKoin.Services
{
    /// <summary>Seam over launching an external process (powercfg). Lets tests run without Windows.</summary>
    public interface IPowerCommandRunner
    {
        CommandResult Run(string fileName, string arguments);
    }
}
