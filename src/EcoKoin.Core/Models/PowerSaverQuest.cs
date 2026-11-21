namespace EcoKoin.Models
{
    /// <summary>Counts PowerSaverMinutes events (progress += evt.Amount minutes).</summary>
    public sealed class PowerSaverQuest : DailyQuest
    {
        public PowerSaverQuest(string questId, string userId, string description, int targetValue, int rewardCoin,
                         DateOnly questDate, int currentProgress = 0, bool isClaimed = false)
            : base(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed)
        {
        }

        public override QuestType Type => QuestType.PowerSaver;

        /// <summary>Returns evt.Amount when evt.Kind == QuestEventKind.PowerSaverMinutes, otherwise 0.</summary>
        public override int Evaluate(QuestEvent evt)
        {
            return evt.Kind == QuestEventKind.PowerSaverMinutes ? evt.Amount : 0;
        }
    }
}
