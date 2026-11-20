namespace EcoKoin.Models
{
    /// <summary>Counts WeatherRecommendationApplied events (progress += evt.Amount).</summary>
    public sealed class WeatherAdaptiveQuest : DailyQuest
    {
        public WeatherAdaptiveQuest(string questId, string userId, string description, int targetValue, int rewardCoin,
                         DateOnly questDate, int currentProgress = 0, bool isClaimed = false)
            : base(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed)
        {
        }

        public override QuestType Type => QuestType.WeatherAdaptive;

        /// <summary>Returns evt.Amount when evt.Kind == QuestEventKind.WeatherRecommendationApplied, otherwise 0.</summary>
        public override int Evaluate(QuestEvent evt)
        {
            return evt.Kind == QuestEventKind.WeatherRecommendationApplied ? evt.Amount : 0;
        }
    }
}
