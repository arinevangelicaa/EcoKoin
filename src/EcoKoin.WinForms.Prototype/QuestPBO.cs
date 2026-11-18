namespace ecokoin7
{
    // Kelas dasar untuk semua jenis misi.
    public abstract class Quest
    {
        // Bisa dibaca dari luar, hanya kelas ini yang dapat mengubahnya.
        public string QuestId { get; private set; }
        public string Description { get; private set; }

        // Bisa dibaca dari luar, dapat diubah kelas ini dan kelas turunannya.
        public int RewardCoin { get; protected set; }
        public bool IsCompleted { get; protected set; }

        protected Quest(string id, string description, int reward)
        {
            QuestId = id;
            Description = description;
            RewardCoin = reward;
            IsCompleted = false;
        }

        // Setiap kelas turunan wajib menyediakan implementasinya.
        public abstract bool SelesaikanQuest();

        // Digunakan layanan di proyek ini jika penyimpanan reward gagal.
        internal void BatalkanPenyelesaian()
        {
            IsCompleted = false;
        }
    }

    // INHERITANCE: mewarisi properti dan perilaku dasar Quest.
    public class WeatherAdaptiveQuest : Quest
    {
        public int TargetKecerahan { get; private set; }

        public WeatherAdaptiveQuest(
            string id,
            string description,
            int reward,
            int targetKecerahan)
            : base(id, description, reward)
        {
            TargetKecerahan = targetKecerahan;
        }

        public override bool SelesaikanQuest()
        {
            if (IsCompleted)
                return false;

            // Simulasi penyelesaian misi kecerahan.
            // Belum mengubah kecerahan perangkat secara langsung.
            IsCompleted = true;
            return true;
        }
    }

    // INHERITANCE: jenis misi lain dengan kelas dasar yang sama.
    public class PowerSaverQuest : Quest
    {
        public string SchemeName { get; private set; }

        public PowerSaverQuest(
            string id,
            string description,
            int reward,
            string schemeName)
            : base(id, description, reward)
        {
            SchemeName = schemeName;
        }

        public override bool SelesaikanQuest()
        {
            if (IsCompleted)
                return false;

            // Simulasi penyelesaian misi hemat daya.
            // Belum mengaktifkan Power Saver Windows secara langsung.
            IsCompleted = true;
            return true;
        }
    }

    public static class QuestService
    {
        public static bool ProsesPenyelesaianQuest(
            Quest quest,
            string username)
        {
            if (quest == null || quest.IsCompleted)
                return false;

            // POLYMORPHISM:
            // Implementasi mengikuti jenis objek quest yang diterima.
            bool berhasil = quest.SelesaikanQuest();

            if (!berhasil)
                return false;

            bool tersimpan = DatabaseHelper.TambahKoin(
                username,
                quest.RewardCoin
            );

            if (!tersimpan)
            {
                // Misi dapat dicoba kembali jika database gagal.
                quest.BatalkanPenyelesaian();
                return false;
            }

            EcoKoinData.TotalKoin += quest.RewardCoin;
            return true;
        }
    }
}