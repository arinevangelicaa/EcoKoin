namespace EcoKoin.Models
{
    /// <summary>EcoPet mascot. Spec: docs/03-domain-model.md (EcoPet).</summary>
    public class EcoPet
    {
        public EcoPet(string petId, string userId, int happinessLevel = 0, PetState state = PetState.Lemas)
        {
            PetId = petId;
            UserId = userId;
            HappinessLevel = happinessLevel;
            CurrentState = state;
        }

        public string PetId { get; }
        public string UserId { get; }

        /// <summary>0..100, clamped.</summary>
        public int HappinessLevel { get; private set; }
        public PetState CurrentState { get; private set; }

        /// <summary>&gt;= 30 Segar; 10..29 Normal; &lt; 10 Lemas. Adjusts HappinessLevel (clamped 0..100).</summary>
        public void UpdateState(int koinEarnedToday)
        {
            // Happiness = koin earned today * 3, clamped to 0..100 (so 34 koin or more is fully happy).
            HappinessLevel = (int)Math.Clamp((long)koinEarnedToday * 3, 0, 100);
            CurrentState = koinEarnedToday >= 30 ? PetState.Segar
                         : koinEarnedToday >= 10 ? PetState.Normal
                         : PetState.Lemas;
        }

        /// <summary>Image file name for the current state: Segar "happy.png", Normal "netral.png", Lemas "sad.png".</summary>
        public string Animate()
        {
            return CurrentState switch
            {
                PetState.Segar => "happy.png",
                PetState.Normal => "netral.png",
                _ => "sad.png"
            };
        }
    }
}
