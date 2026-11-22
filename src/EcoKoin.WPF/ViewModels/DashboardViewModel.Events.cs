using System;

namespace EcoKoin.WPF.ViewModels
{
    // Shared signal between the dashboard features (quests, pet/level, analytics).
    // Anything that changes persisted data (claiming a quest, quest progress, a new power session)
    // calls NotifyDataChanged() on the UI thread; the other features subscribe and reload what they show.
    public sealed partial class DashboardViewModel
    {
        internal event EventHandler? DataChanged;

        internal void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
