namespace EcoKoin.WPF.Models
{
    /// <summary>Outcome of a Windows setting change: a user-facing Indonesian message and whether it worked.</summary>
    public readonly record struct WindowsActionResult(bool Success, string Message);
}
