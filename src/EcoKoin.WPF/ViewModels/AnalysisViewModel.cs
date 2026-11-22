using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private long _powerSaverDurationMinutes;
        private string _powerSaverDurationDisplay = "—";
        private string _insightMessage = "Wawasan personal akan ditampilkan setelah data penghematan tersedia.";

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

        public long PowerSaverDurationMinutes
        {
            get => _powerSaverDurationMinutes;
            private set
            {
                _powerSaverDurationMinutes = value;
                OnPropertyChanged();
            }
        }

        public string PowerSaverDurationDisplay
        {
            get => _powerSaverDurationDisplay;
            private set
            {
                _powerSaverDurationDisplay = value;
                OnPropertyChanged();
            }
        }

        public string InsightMessage
        {
            get => _insightMessage;
            private set
            {
                _insightMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasData => ChartData.Count > 0;

        public void SetPowerSaverDuration(long minutes)
        {
            PowerSaverDurationMinutes = Math.Max(0, minutes);
            PowerSaverDurationDisplay = FormatDuration(PowerSaverDurationMinutes);
            if (PowerSaverDurationMinutes > 0)
            {
                InsightMessage = $"Bagus! Kamu telah menghemat daya selama {PowerSaverDurationDisplay} pada periode ini.";
            }
            else
            {
                InsightMessage = "Belum ada aktivitas hemat daya pada periode ini. Aktifkan Mode hemat daya atau Energy Saver di Windows untuk mulai menghemat.";
            }
        }

        public void ClearPowerSaverDuration()
        {
            PowerSaverDurationMinutes = 0;
            PowerSaverDurationDisplay = "—";
            InsightMessage = "Wawasan personal akan ditampilkan setelah data penghematan tersedia.";
        }

        public static string FormatDuration(long totalMinutes)
        {
            if (totalMinutes <= 0)
                return "0 menit";

            long hours = totalMinutes / 60;
            long mins = totalMinutes % 60;

            if (hours == 0)
                return $"{mins} menit";
            if (mins == 0)
                return $"{hours} jam";

            return $"{hours} jam {mins} menit";
        }

        public void SetData(
            IEnumerable<AnalysisDataPoint> points,
            double energySavedKwh,
            double co2SavedKg,
            double moneySavedRp,
            string periodDescription)
        {
            ChartData = new List<AnalysisDataPoint>(
                points ?? Array.Empty<AnalysisDataPoint>());

            EnergySavedKwh = energySavedKwh;
            Co2SavedKg = co2SavedKg;
            MoneySavedRp = moneySavedRp;
            PeriodDescription = periodDescription;

            OnPropertyChanged(nameof(HasData));
        }

        public void ClearData(string message)
        {
            ChartData = Array.Empty<AnalysisDataPoint>();

            EnergySavedKwh = 0;
            Co2SavedKg = 0;
            MoneySavedRp = 0;

            EmptyMessage = message;

            OnPropertyChanged(nameof(HasData));
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
