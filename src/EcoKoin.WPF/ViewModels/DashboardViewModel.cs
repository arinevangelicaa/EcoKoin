namespace EcoKoin.WPF.ViewModels
{
    // Data sementara untuk pengembangan frontend, bukan data aktual perangkat/pengguna.
    // Nanti nilai ini diisi dari service/backend melalui ViewModel.
    public sealed class DashboardViewModel
    {
        public string TotalKoinDisplay { get; set; } = "800 Koin";
        public string KoinMingguanDisplay { get; set; } = "+120 Koin minggu ini";
        public string SuhuDisplay { get; set; } = "31° C";
        public string CuacaDisplay { get; set; } = "Cerah Berawan";
        public string KelembapanDisplay { get; set; } = "65%";
        public string KondisiUdaraDisplay { get; set; } = "Udara Lembap";
        public string BateraiDisplay { get; set; } = "75%";
        public string StatusDayaDisplay { get; set; } = "On Battery";
        public string XpDisplay { get; set; } = "350 / 500 XP";
        public string EnergiDisplay { get; set; } = "2.5 kWh";
        public string EmisiDisplay { get; set; } = "1.2 kg";
        public string BiayaDisplay { get; set; } = "Rp12.500";
        public string MisiPersenDisplay { get; set; } = "67%";
        public string MisiSelesaiDisplay { get; set; } = "2 dari 3 misi selesai";
    }
}