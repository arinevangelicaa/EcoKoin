namespace EcoKoin.Models
{
    /// <summary>Something that happened which a quest may count. <see cref="Amount"/> defaults to 1.</summary>
    public record QuestEvent(QuestEventKind Kind, int Amount = 1);
}
