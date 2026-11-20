using System;

namespace EcoKoin.Models
{
    public class EcoPet
    {
        public string PetId { get; set; }
        public int HappinessLevel { get; set; }
        public PetState CurrentState { get; set; }

        public void UpdateState(int koinEarnedToday)
        {
            throw new NotImplementedException();
        }

        public void Animate()
        {
            throw new NotImplementedException();
        }
    }
}
