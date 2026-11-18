namespace ecokoin7
{
    public static class EcoKoinData
    {
        // Pengguna aktif yang dibuka datanya
        public static string CurrentUser = "adnan";

        public static int TotalKoin = 120;
        public static string PetState = "Segar";

        // Objek quest berbasis kelas turunan PBO
        public static WeatherAdaptiveQuest MisiKecerahan = new WeatherAdaptiveQuest(
            "q_cuaca", "Kurangi kecerahan layar saat cuaca mendung", 15, 50);

        public static PowerSaverQuest MisiPowerSaver = new PowerSaverQuest(
            "q_saver", "Aktifkan skema Power Saver", 20, "Power Saver");

        public static bool MisiKecerahanSelesai => MisiKecerahan.IsCompleted;
        public static bool MisiPowerSaverSelesai => MisiPowerSaver.IsCompleted;

        // Memuat data riil dari PostgreSQL
        public static void MuatDataDariDatabase()
        {
            if (DatabaseHelper.LoadUserData(CurrentUser, out int koin, out string pet))
            {
                TotalKoin = koin;
                PetState = pet;
            }
        }

        public static int MisiSelesai => (MisiKecerahanSelesai ? 1 : 0) + (MisiPowerSaverSelesai ? 1 : 0);
        public static int ProgressMisi => MisiSelesai * 100 / 2;

        // Eksekusi misi melalui method polimorfik
        public static bool SelesaikanMisiKecerahan()
        {
            return QuestService.ProsesPenyelesaianQuest(MisiKecerahan, CurrentUser);
        }

        public static bool SelesaikanMisiPowerSaver()
        {
            return QuestService.ProsesPenyelesaianQuest(MisiPowerSaver, CurrentUser);
        }
    }
}