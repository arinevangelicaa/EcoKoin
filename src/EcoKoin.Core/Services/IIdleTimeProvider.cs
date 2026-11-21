namespace EcoKoin.Services
{
    /// <summary>Time since last user input. Real impl uses Win32 GetLastInputInfo (Windows only).</summary>
    public interface IIdleTimeProvider
    {
        TimeSpan GetIdleTime();
    }
}
