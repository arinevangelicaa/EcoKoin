namespace EcoKoin.Models
{
    /// <summary>
    /// Abstract daily quest. Spec: docs/03-domain-model.md (DailyQuest).
    /// Encapsulation: CurrentProgress / IsClaimed change only via UpdateProgress / Claim.
    /// </summary>
    public abstract class DailyQuest
    {
        protected DailyQuest(string questId, string userId, string description, int targetValue, int rewardCoin,
                             DateOnly questDate, int currentProgress = 0, bool isClaimed = false)
        {
            QuestId = questId;
            UserId = userId;
            Description = description;
            TargetValue = targetValue;
            RewardCoin = rewardCoin;
            QuestDate = questDate;
            CurrentProgress = currentProgress;
            IsClaimed = isClaimed;
        }

        public string QuestId { get; }
        public string UserId { get; }
        public string Description { get; }
        public int TargetValue { get; }
        public int RewardCoin { get; }
        public DateOnly QuestDate { get; }
        public int CurrentProgress { get; private set; }
        public bool IsClaimed { get; private set; }

        /// <summary>Discriminator persisted in DAILY_QUESTS.quest_type.</summary>
        public abstract QuestType Type { get; }

        /// <summary>Progress increase this quest wants for <paramref name="evt"/> (0 if the event is not relevant).</summary>
        public abstract int Evaluate(QuestEvent evt);

        /// <summary>Adds <paramref name="value"/> to progress, clamped to TargetValue.</summary>
        public void UpdateProgress(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Progress increment cannot be negative.");
            CurrentProgress = (int)Math.Min((long)CurrentProgress + value, TargetValue);
        }

        /// <summary>CurrentProgress &gt;= TargetValue.</summary>
        public bool CheckCompletion()
        {
            return CurrentProgress >= TargetValue;
        }

        /// <summary>If complete and not yet claimed: user.AddEcoKoin(RewardCoin), IsClaimed = true, return true. Otherwise false. Idempotent.</summary>
        public bool Claim(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (IsClaimed || !CheckCompletion())
                return false;

            user.AddEcoKoin(RewardCoin);
            IsClaimed = true;
            return true;
        }

        /// <summary>Rehydrates the concrete subclass for <paramref name="type"/> (used by the repository).</summary>
        public static DailyQuest Create(QuestType type, string questId, string userId, string description, int targetValue,
                                        int rewardCoin, DateOnly questDate, int currentProgress, bool isClaimed)
        {
            return type switch
            {
                QuestType.Sleep => new SleepQuest(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed),
                QuestType.PowerSaver => new PowerSaverQuest(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed),
                QuestType.WeatherAdaptive => new WeatherAdaptiveQuest(questId, userId, description, targetValue, rewardCoin, questDate, currentProgress, isClaimed),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown quest type.")
            };
        }

        /// <summary>The 3 default quests for a day: Sleep 1/+30, PowerSaver 240/+20, WeatherAdaptive 1/+15 (new Guid ids).</summary>
        public static IReadOnlyList<DailyQuest> CreateDefaultsFor(string userId, DateOnly date)
        {
            return new DailyQuest[]
            {
                new SleepQuest(Guid.NewGuid().ToString(), userId, "Aktifkan mode sleep saat PC ditinggal", 1, 30, date),
                new PowerSaverQuest(Guid.NewGuid().ToString(), userId, "Jalankan skema hemat daya selama 4 jam", 240, 20, date),
                new WeatherAdaptiveQuest(Guid.NewGuid().ToString(), userId, "Terapkan rekomendasi cuaca", 1, 15, date),
            };
        }
    }
}
