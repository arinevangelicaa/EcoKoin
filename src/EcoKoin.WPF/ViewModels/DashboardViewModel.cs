using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Input;
using EcoKoin.Models;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.ViewModels
{
    // Dashboard presentation. Values are filled from the database / Windows by the feature files
    // (MainWindow.Quests/Pet/Analytics/PowerSessions); the defaults below are only loading placeholders.
    public sealed partial class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private static readonly CultureInfo IndonesianCulture = CultureInfo.GetCultureInfo("id-ID");

        private string _totalKoinDisplay;
        private string _koinMingguanDisplay = "";
        private string _suhuDisplay = "—";
        private string _cuacaDisplay = "Memuat cuaca…";
        private string _kelembapanDisplay = "—";
        private string _kondisiUdaraDisplay = "";
        private string _bateraiDisplay = "—";
        private string _statusDayaDisplay = "Memuat status daya…";
        private string _energiDisplay = "—";
        private string _emisiDisplay = "—";
        private string _biayaDisplay = "—";
        private string _rekomendasiCuacaDisplay = "Memuat cuaca…";
        private string _alasanRekomendasiCuacaDisplay = "";
        private bool _canApplyWeatherRecommendation;
        private bool _weatherRecommendationsEnabled = true;
        private string _rekomendasiSleepDisplay = "Memuat rekomendasi…";
        private string _alasanRekomendasiSleepDisplay = "";
        private int _level = 1;
        private int _xp;
        private int _xpTarget = EcoKoin.Models.User.XpPerLevel;
        private int _misiSelesai;
        private int _jumlahMisi;
        private string _powerSaverStatus = "Memuat progres misi…";
        private string _powerSaverReward = "—";
        private bool _canClaimPowerSaver;
        private string _grafikStatusDisplay = "Memuat data…";
        private double[] _grafikMingguan = new double[7]; // flat baseline until the first load

        public DashboardViewModel(AuthUser? user)
        {
            var name = user?.Username?.Trim();
            if (string.IsNullOrEmpty(name))
                name = user?.Email.Split('@')[0].Trim();

            AccountDisplayName = string.IsNullOrEmpty(name) ? null : name;
            _totalKoinDisplay = user?.TotalEcoKoin is int balance
                ? $"{balance.ToString("N0", IndonesianCulture)} Koin"
                : "—";
        }

        public string? AccountDisplayName { get; }
        public string GreetingDisplay => AccountDisplayName is null ? "Hai!" : $"Hai, {AccountDisplayName}!";
        public string TotalKoinDisplay { get => _totalKoinDisplay; set => SetProperty(ref _totalKoinDisplay, value); }
        public string KoinMingguanDisplay { get => _koinMingguanDisplay; set => SetProperty(ref _koinMingguanDisplay, value); }
        public string SuhuDisplay { get => _suhuDisplay; set => SetProperty(ref _suhuDisplay, value); }
        public string CuacaDisplay { get => _cuacaDisplay; set => SetProperty(ref _cuacaDisplay, value); }
        public string KelembapanDisplay { get => _kelembapanDisplay; set => SetProperty(ref _kelembapanDisplay, value); }
        public string KondisiUdaraDisplay { get => _kondisiUdaraDisplay; set => SetProperty(ref _kondisiUdaraDisplay, value); }
        public string BateraiDisplay { get => _bateraiDisplay; set => SetProperty(ref _bateraiDisplay, value); }
        public string StatusDayaDisplay { get => _statusDayaDisplay; set => SetProperty(ref _statusDayaDisplay, value); }
        public string EnergiDisplay { get => _energiDisplay; set => SetProperty(ref _energiDisplay, value); }
        public string EmisiDisplay { get => _emisiDisplay; set => SetProperty(ref _emisiDisplay, value); }
        public string BiayaDisplay { get => _biayaDisplay; set => SetProperty(ref _biayaDisplay, value); }
        public string RekomendasiCuacaDisplay { get => _rekomendasiCuacaDisplay; set => SetProperty(ref _rekomendasiCuacaDisplay, value); }
        public string AlasanRekomendasiCuacaDisplay { get => _alasanRekomendasiCuacaDisplay; set => SetProperty(ref _alasanRekomendasiCuacaDisplay, value); }
        public bool CanApplyWeatherRecommendation
        {
            get => _canApplyWeatherRecommendation;
            private set => SetProperty(ref _canApplyWeatherRecommendation, value);
        }

        internal void ShowWeatherStatus(string status, string detail)
        {
            SuhuDisplay = "—";
            CuacaDisplay = status;
            KelembapanDisplay = "—";
            KondisiUdaraDisplay = "";
            RekomendasiCuacaDisplay = status;
            AlasanRekomendasiCuacaDisplay = detail;
            CanApplyWeatherRecommendation = false;
        }

        internal void ShowWeather(WeatherData weather, string recommendation, bool isStale)
        {
            SuhuDisplay = weather.Temperature.ToString("0.#", IndonesianCulture) + " °C";
            KelembapanDisplay = weather.Humidity.ToString(IndonesianCulture) + "%";
            CuacaDisplay = weather.Condition switch
            {
                "Clear" => "Cerah",
                "Clouds" => "Berawan",
                "Rain" => "Hujan",
                "Drizzle" => "Gerimis",
                "Thunderstorm" => "Badai petir",
                "Mist" or "Fog" or "Haze" => "Berkabut",
                "Snow" => "Salju",
                "" => "Kondisi tidak tersedia",
                _ => weather.Condition
            };
            KondisiUdaraDisplay = isStale ? "Data terakhir" : "";
            if (isStale)
                CuacaDisplay += " (data terakhir)";

            RekomendasiCuacaDisplay = recommendation;
            string cloudiness = weather.Cloudiness.ToString(IndonesianCulture) + "%";
            AlasanRekomendasiCuacaDisplay = isStale
                ? "Berdasarkan data terakhir. Cuaca terbaru belum tersedia."
                : $"Tutupan awan {cloudiness}.";
            CanApplyWeatherRecommendation = !isStale && weather.Cloudiness >= 30;
        }

        public bool WeatherRecommendationsEnabled
        {
            get => _weatherRecommendationsEnabled;
            set => SetProperty(ref _weatherRecommendationsEnabled, value);
        }

        public string RekomendasiSleepDisplay { get => _rekomendasiSleepDisplay; set => SetProperty(ref _rekomendasiSleepDisplay, value); }
        public string AlasanRekomendasiSleepDisplay { get => _alasanRekomendasiSleepDisplay; set => SetProperty(ref _alasanRekomendasiSleepDisplay, value); }
        public string GrafikStatusDisplay { get => _grafikStatusDisplay; set => SetProperty(ref _grafikStatusDisplay, value); }

        public int Level { get => _level; set { if (SetProperty(ref _level, Math.Max(1, value))) Notify(nameof(LevelDisplay)); } }
        public string LevelDisplay => $"Level {Level}";

        // XP and quest labels AND progress bars are calculated from the SAME numeric values.
        public int Xp { get => _xp; set { if (SetProperty(ref _xp, Math.Max(0, value))) NotifyXp(); } }
        public int XpTarget { get => _xpTarget; set { if (SetProperty(ref _xpTarget, Math.Max(1, value))) NotifyXp(); } }
        public string XpDisplay => $"{Xp} / {XpTarget} XP";
        public double XpProgress => Math.Clamp((double)Xp / XpTarget * 100, 0, 100);

        public int MisiSelesai { get => _misiSelesai; set { if (SetProperty(ref _misiSelesai, Math.Max(0, value))) NotifyMisi(); } }
        public int JumlahMisi { get => _jumlahMisi; set { if (SetProperty(ref _jumlahMisi, Math.Max(0, value))) NotifyMisi(); } }
        public string MisiSelesaiDisplay => JumlahMisi == 0
            ? "Misi belum dimuat"
            : $"{Math.Min(MisiSelesai, JumlahMisi)} dari {JumlahMisi} misi selesai";
        public string MisiPersenDisplay => JumlahMisi == 0
            ? "—"
            : $"{Math.Round(MisiProgress, MidpointRounding.AwayFromZero):0}%";
        public double MisiProgress => JumlahMisi == 0 ? 0 : Math.Clamp((double)MisiSelesai / JumlahMisi * 100, 0, 100);

        public ICommand? ClaimPowerSaverCommand { get; internal set; }
        public string PowerSaverStatus { get => _powerSaverStatus; internal set => SetProperty(ref _powerSaverStatus, value); }
        public string PowerSaverReward { get => _powerSaverReward; private set => SetProperty(ref _powerSaverReward, value); }
        public bool CanClaimPowerSaver { get => _canClaimPowerSaver; internal set => SetProperty(ref _canClaimPowerSaver, value); }

        internal void ShowPowerSaverQuest(PowerSaverQuest quest, int? balance, bool? isActive)
        {
            // The balance is deliberately NOT applied here: this result was read on the tracker's own schedule and can
            // be older than a claim that just happened, which would flip the shown balance back. The quest list owns the
            // balance (ShowQuests) and is reloaded through NotifyDataChanged when the tracker's claim goes through.
            bool complete = quest.CheckCompletion();
            ApplyPowerSaverToSummary(quest);
            PowerSaverReward = quest.IsClaimed ? "Diklaim" : $"Klaim +{quest.RewardCoin}";
            PowerSaverStatus = quest.IsClaimed ? "Hadiah sudah diklaim."
                : complete ? "Selesai. Hadiah siap diklaim."
                : $"{quest.CurrentProgress}/{quest.TargetValue} menit. " + (isActive switch
                {
                    true => "Power Saver aktif.",
                    false => "Aktifkan Energy Saver atau plan Power Saver di Windows.",
                    null => "Status plan daya belum terbaca."
                });
        }

        // Chart takes exactly seven non-negative values, oldest to newest.
        public void SetGrafikMingguan(IReadOnlyList<double> values, bool isDemo = false)
        {
            if (values is null || values.Count != 7)
                throw new ArgumentException("Grafik membutuhkan tepat 7 nilai harian.", nameof(values));
            var next = new double[7];
            for (int i = 0; i < 7; i++)
            {
                if (!double.IsFinite(values[i]) || values[i] < 0)
                    throw new ArgumentException("Nilai grafik harus finite dan tidak negatif.", nameof(values));
                next[i] = values[i];
            }
            _grafikMingguan = next;
            GrafikStatusDisplay = isDemo ? "Ilustrasi" : "Estimasi mingguan";
            Notify(nameof(GrafikPoints));
            for (int i = 0; i < 7; i++) Notify($"GrafikDot{i}Top");
        }

        // Y-axis normalized to current seven-day maximum; empty data is shown at baseline.
        private double ChartY(int index)
        {
            double max = 0;
            foreach (double v in _grafikMingguan) max = Math.Max(max, v);
            return max <= 0 ? 72 : 72 - _grafikMingguan[index] / max * 46;
        }
        public PointCollection GrafikPoints
        {
            get
            {
                var points = new PointCollection();
                for (int i = 0; i < 7; i++)
                    points.Add(new System.Windows.Point(10 + i * 65, ChartY(i)));
                return points;
            }
        }
        public double GrafikDot0Top => ChartY(0) - 4;
        public double GrafikDot1Top => ChartY(1) - 4;
        public double GrafikDot2Top => ChartY(2) - 4;
        public double GrafikDot3Top => ChartY(3) - 4;
        public double GrafikDot4Top => ChartY(4) - 4;
        public double GrafikDot5Top => ChartY(5) - 4;
        public double GrafikDot6Top => ChartY(6) - 5;

        private void NotifyXp() { Notify(nameof(XpDisplay)); Notify(nameof(XpProgress)); }
        private void NotifyMisi() { Notify(nameof(MisiSelesaiDisplay)); Notify(nameof(MisiPersenDisplay)); Notify(nameof(MisiProgress)); }
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Notify(name);
            return true;
        }
        private void Notify(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
