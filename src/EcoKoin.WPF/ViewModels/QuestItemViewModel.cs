using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EcoKoin.Models;

namespace EcoKoin.WPF.ViewModels
{
    // One row of the daily quest list. Every text comes from the quest stored in the database.
    public sealed class QuestItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _title = "";
        private string _statusText = "Memuat misi…";
        private string _rewardLabel = "—";
        private bool _canClaim;

        public string Title { get => _title; private set => SetProperty(ref _title, value); }
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
        public string RewardLabel { get => _rewardLabel; private set => SetProperty(ref _rewardLabel, value); }
        public bool CanClaim { get => _canClaim; internal set => SetProperty(ref _canClaim, value); }
        public ICommand? ClaimCommand { get; internal set; }

        // Identity of the quest currently shown; used by the claim command.
        internal string? QuestId { get; private set; }
        internal DateOnly QuestDate { get; private set; }

        internal void Show(DailyQuest quest)
        {
            QuestId = quest.QuestId;
            QuestDate = quest.QuestDate;
            Title = quest.Description;

            bool complete = quest.CheckCompletion();
            if (quest.IsClaimed)
            {
                StatusText = "Hadiah sudah diklaim.";
                RewardLabel = "Diklaim";
            }
            else if (complete)
            {
                StatusText = "Selesai. Hadiah siap diklaim.";
                RewardLabel = $"Klaim +{quest.RewardCoin.ToString(CultureInfo.InvariantCulture)}";
            }
            else
            {
                StatusText = quest.CurrentProgress <= 0
                    ? "Belum dijalankan. " + HowToProgress(quest)
                    : $"{quest.CurrentProgress}/{quest.TargetValue}";
                RewardLabel = $"+{quest.RewardCoin.ToString(CultureInfo.InvariantCulture)} Koin";
            }

            CanClaim = complete && !quest.IsClaimed;
        }

        // Tells the user what actually moves this quest forward (no quest is silently impossible).
        private static string HowToProgress(DailyQuest quest) => quest switch
        {
            SleepQuest => "Biarkan PC tidur saat kamu tinggal.",
            WeatherAdaptiveQuest => "Terapkan rekomendasi cuaca saat tersedia.",
            _ => ""
        };

        internal void ShowUnavailable(string status)
        {
            QuestId = null;
            if (string.IsNullOrEmpty(Title))
                Title = "Misi harian";
            StatusText = status;
            RewardLabel = "—";
            CanClaim = false;
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
