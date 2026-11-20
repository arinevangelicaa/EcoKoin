namespace EcoKoin.Models
{
    public class DailyQuest
    {
        public string QuestId { get; set; }
        public string Description { get; set; }
        public int TargetValue { get; set; }
        public int CurrentProgress { get; set; }

        public void UpdateProgress(int value)
        {
            CurrentProgress += value;
        }

        public bool CheckCompletion()
        {
            return CurrentProgress >= TargetValue;
        }
    }
}
