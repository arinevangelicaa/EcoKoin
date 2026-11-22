using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EcoKoin.WPF.ViewModels
{
    public class AnalysisDataPoint
    {
        public string Label { get; set; } = "";
        public double Co2SavedKg { get; set; }
        public double MoneySavedRp { get; set; }
    }

    public class AnalysisViewModel : INotifyPropertyChanged
    {
        private double _energySavedKwh;
        private double _co2SavedKg;
        private double _moneySavedRp;

        private IReadOnlyList<AnalysisDataPoint> _chartData =
            Array.Empty<AnalysisDataPoint>();

        private string _periodDescription = "";
        private string _emptyMessage = "Data analisis belum tersedia.";
        private string _bucketUnit = "hari";
        private string _powerSaverSourceDisplay = "—";
        private string _sleepSourceDisplay = "—";
        private string _insightTitle = "Belum ada wawasan";
        private string _insightText = "Wawasan personal akan ditampilkan setelah data penghematan tersedia.";

        private static readonly CultureInfo IndonesianCulture = CultureInfo.GetCultureInfo("id-ID");

        public event PropertyChangedEventHandler? PropertyChanged;

        public double EnergySavedKwh
        {
            get => _energySavedKwh;
            private set
            {
                _energySavedKwh = value;
                OnPropertyChanged();
            }
        }

        public double Co2SavedKg
        {
            get => _co2SavedKg;
            private set
            {
                _co2SavedKg = value;
                OnPropertyChanged();
            }
        }

        public double MoneySavedRp
        {
            get => _moneySavedRp;
            private set
            {
                _moneySavedRp = value;
                OnPropertyChanged();
            }
        }

        public IReadOnlyList<AnalysisDataPoint> ChartData
        {
            get => _chartData;
            private set
            {
                _chartData = value;
                OnPropertyChanged();
            }
        }

        public string PeriodDescription
        {
            get => _periodDescription;
            private set
            {
                _periodDescription = value;
                OnPropertyChanged();
            }
        }

        public string EmptyMessage
        {
            get => _emptyMessage;
            private set
            {
                _emptyMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasData => ChartData.Count > 0;

        // "hari" for daily charts, "bulan" for the yearly (monthly) chart.
        public string BucketUnit
        {
            get => _bucketUnit;
            private set
            {
                _bucketUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Co2UnitLabel));
                OnPropertyChanged(nameof(MoneyUnitLabel));
                OnPropertyChanged(nameof(TrendNote));
            }
        }

        public string Co2UnitLabel => $"kg CO2 / {BucketUnit}";
        public string MoneyUnitLabel => $"Rupiah / {BucketUnit}";
        public string TrendNote =>
            $"Estimasi dampak per {BucketUnit}, bukan akumulasi. Setiap grafik memakai skala dan satuan tersendiri.";

        public string PowerSaverSourceDisplay
        {
            get => _powerSaverSourceDisplay;
            private set
            {
                _powerSaverSourceDisplay = value;
                OnPropertyChanged();
            }
        }

        public string SleepSourceDisplay
        {
            get => _sleepSourceDisplay;
            private set
            {
                _sleepSourceDisplay = value;
                OnPropertyChanged();
            }
        }

        public string InsightTitle
        {
            get => _insightTitle;
            private set
            {
                _insightTitle = value;
                OnPropertyChanged();
            }
        }

        public string InsightText
        {
            get => _insightText;
            private set
            {
                _insightText = value;
                OnPropertyChanged();
            }
        }

        public void SetData(
            IEnumerable<AnalysisDataPoint> points,
            double energySavedKwh,
            double co2SavedKg,
            double moneySavedRp,
            string periodDescription,
            string bucketUnit = "hari",
            double powerSaverKwh = 0,
            double sleepKwh = 0)
        {
            ChartData = new List<AnalysisDataPoint>(
                points ?? Array.Empty<AnalysisDataPoint>());

            EnergySavedKwh = energySavedKwh;
            Co2SavedKg = co2SavedKg;
            MoneySavedRp = moneySavedRp;
            PeriodDescription = periodDescription;
            BucketUnit = bucketUnit;
            PowerSaverSourceDisplay = FormatKwh(powerSaverKwh);
            SleepSourceDisplay = FormatKwh(sleepKwh);

            var best = ChartData.OrderByDescending(p => p.Co2SavedKg).FirstOrDefault();
            if (best != null && best.Co2SavedKg > 0)
            {
                string when = int.TryParse(best.Label, out _) ? $"Tanggal {best.Label}" : best.Label;
                InsightTitle = $"Terbaik: {when}";
                InsightText = $"Penghematan tertinggi per {bucketUnit} ({periodDescription.ToLowerInvariant()}): " +
                    $"{best.Co2SavedKg.ToString("N2", IndonesianCulture)} kg CO2 atau " +
                    $"Rp{best.MoneySavedRp.ToString("N0", IndonesianCulture)}.";
            }
            else
            {
                ResetInsight();
            }

            OnPropertyChanged(nameof(HasData));
        }

        public void ClearData(string message)
        {
            ChartData = Array.Empty<AnalysisDataPoint>();

            EnergySavedKwh = 0;
            Co2SavedKg = 0;
            MoneySavedRp = 0;
            PowerSaverSourceDisplay = "—";
            SleepSourceDisplay = "—";
            ResetInsight();

            EmptyMessage = message;

            OnPropertyChanged(nameof(HasData));
        }

        private void ResetInsight()
        {
            InsightTitle = "Belum ada wawasan";
            InsightText = "Wawasan personal akan ditampilkan setelah data penghematan tersedia.";
        }

        private static string FormatKwh(double kwh) =>
            kwh.ToString("0.##", IndonesianCulture) + " kWh";

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}