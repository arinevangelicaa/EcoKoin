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

        public event PropertyChangedEventHandler PropertyChanged;

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
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}