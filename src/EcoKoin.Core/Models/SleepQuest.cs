namespace EcoKoin.Models
{
    /// <summary>Counts SleepEntered events (progress += evt.Amount).</summary>
    public sealed class SleepQuest : DailyQuest
    {
        public SleepQuest(string questId, string userId, string description, int targetValue, int rewardCoin,
                         DateOnly questDate, int currentProgress = 0, bool isClaimed = false)
            : base(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed)
        {
        }

        public override QuestType Type => QuestType.Sleep;

        /// <summary>Returns evt.Amount when evt.Kind == QuestEventKind.SleepEntered, otherwise 0.</summary>
        public override int Evaluate(QuestEvent evt)
        {
            return evt.Kind == QuestEventKind.SleepEntered ? evt.Amount : 0;
        }
    }
}
