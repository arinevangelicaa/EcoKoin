using System;
using System.Collections.Generic;
using System.Linq;
using EcoKoin.Models;

namespace EcoKoin.WPF.ViewModels
{
    // Daily quest list shown on the dashboard card and on the Misi & Reward page.
    // Sleep and Weather rows are bound to QuestItemViewModel; the Power Saver row keeps its own
    // tracker-driven properties (PowerSaverStatus / PowerSaverReward / CanClaimPowerSaver) and only
    // takes its title from here. The mission summary (MisiSelesai / JumlahMisi) has ONE source of truth:
    // _questSnapshot, recomputed in RecomputeMissionSummary.
    public sealed partial class DashboardViewModel
    {
        private string _powerSaverTitle = "Memuat misi…";
        private IReadOnlyList<DailyQuest> _questSnapshot = Array.Empty<DailyQuest>();
        private bool? _powerSaverClaimedSeen;

        public QuestItemViewModel SleepQuestItem { get; } = new();
        public QuestItemViewModel WeatherQuestItem { get; } = new();
        public string PowerSaverTitle { get => _powerSaverTitle; private set => SetProperty(ref _powerSaverTitle, value); }

        // Shows today's quests (all three) coming from the database.
        internal void ShowQuests(IReadOnlyList<DailyQuest> quests, int? balance)
        {
            if (balance is int coins)
                ShowBalance(coins);

            _questSnapshot = quests;

            var sleep = quests.OfType<SleepQuest>().FirstOrDefault();
            if (sleep is not null) SleepQuestItem.Show(sleep); else SleepQuestItem.ShowUnavailable("Misi belum tersedia.");

            var weather = quests.OfType<WeatherAdaptiveQuest>().FirstOrDefault();
            if (weather is not null) WeatherQuestItem.Show(weather); else WeatherQuestItem.ShowUnavailable("Misi belum tersedia.");

            if (quests.OfType<PowerSaverQuest>().FirstOrDefault() is { } powerSaver)
                PowerSaverTitle = powerSaver.Description;

            RecomputeMissionSummary();
        }

        internal void ShowQuestsUnavailable()
        {
            SleepQuestItem.ShowUnavailable("Misi belum dapat dimuat.");
            WeatherQuestItem.ShowUnavailable("Misi belum dapat dimuat.");
            if (_powerSaverTitle == "Memuat misi…")
                PowerSaverTitle = "Misi Power Saver";

            // Do not keep counts that contradict the rows above.
            _questSnapshot = Array.Empty<DailyQuest>();
            RecomputeMissionSummary();
        }

        // Balance and the level derived from it always move together (Level = coins / 500 + 1).
        internal void ShowBalance(int coins)
        {
            TotalKoinDisplay = $"{coins.ToString("N0", IndonesianCulture)} Koin";
            int total = Math.Max(0, coins);
            XpTarget = User.XpPerLevel;
            Level = total / User.XpPerLevel + 1;
            Xp = total % User.XpPerLevel;
        }

        // Called by ShowPowerSaverQuest so the tracker's fresher Power Saver quest is part of the same summary.
        private void ApplyPowerSaverToSummary(PowerSaverQuest quest)
        {
            PowerSaverTitle = quest.Description;
            // Until the full list has been loaded once, a one-quest snapshot would flash "0 dari 1 misi".
            if (_questSnapshot.Count > 0)
            {
                _questSnapshot = _questSnapshot.Where(q => q is not PowerSaverQuest).Append(quest).ToList();
                RecomputeMissionSummary();
            }

            // A claim made through the tracker changes the balance: tell pet/analytics.
            bool? previous = _powerSaverClaimedSeen;
            _powerSaverClaimedSeen = quest.IsClaimed;
            if (previous == false && quest.IsClaimed)
                NotifyDataChanged();
        }

        private void RecomputeMissionSummary()
        {
            JumlahMisi = _questSnapshot.Count;
            MisiSelesai = _questSnapshot.Count(q => q.CheckCompletion());
        }
    }
}
