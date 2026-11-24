using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF.Views
{
    public partial class AnalysisView : UserControl
    {
        private readonly AnalysisViewModel _viewModel;
        private int _loadRequest;
        private readonly Dictionary<int, AnalysisPeriodData> _periodData =
            new Dictionary<int, AnalysisPeriodData>();

        public AnalysisView()
        {
            InitializeComponent();
            _viewModel = new AnalysisViewModel();
            DataContext = _viewModel;
            IsVisibleChanged += OnIsVisibleChanged;
            ShowSelectedPeriod();
        }

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || PeriodComboBox == null ||
                WeeklyKpiCards == null || WeeklyTrendChart == null ||
                UnavailablePeriodPanel == null || Co2ChartCanvas == null ||
                MoneyChartCanvas == null)
                return;

            ShowSelectedPeriod();
        }

        private void ShowSelectedPeriod()
        {
            if (_viewModel == null || PeriodComboBox == null ||
                PeriodDescription == null || WeeklyKpiCards == null ||
                WeeklyTrendChart == null || UnavailablePeriodPanel == null ||
                UnavailablePeriodDescription == null || Co2ChartCanvas == null ||
                MoneyChartCanvas == null)
                return;

            int period = PeriodComboBox.SelectedIndex;
            string description = period == 0 ? "7 hari terakhir" :
                period == 1 ? "Bulan ini" :
                period == 2 ? "Tahun ini" : "Pilih periode";
            PeriodDescription.Text = description;

            if (_periodData.TryGetValue(period, out var data) && data.Points.Count > 0)
            {
                PeriodDuration.Text = $"Durasi tercatat: {data.DurationHours:0.##} jam";
                PeriodDuration.Visibility = Visibility.Visible;
                _viewModel.SetData(data.Points, data.EnergySavedKwh,
                    data.Co2SavedKg, data.MoneySavedRp, description,
                    period == 2 ? "bulan" : "hari",
                    data.PowerSaverKwh, data.SleepKwh);
                DrawChart(Co2ChartCanvas, data.Points, p => p.Co2SavedKg,
                    Color.FromRgb(44, 76, 62), false);
                DrawChart(MoneyChartCanvas, data.Points, p => p.MoneySavedRp,
                    Color.FromRgb(217, 119, 70), true);
                WeeklyKpiCards.Visibility = Visibility.Visible;
                WeeklyTrendChart.Visibility = Visibility.Visible;
                UnavailablePeriodPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PeriodDuration.Visibility = Visibility.Collapsed;
                _viewModel.ClearData("Data analisis untuk periode ini belum tersedia.");
                Co2ChartCanvas.Children.Clear();
                MoneyChartCanvas.Children.Clear();
                WeeklyKpiCards.Visibility = Visibility.Collapsed;
                WeeklyTrendChart.Visibility = Visibility.Collapsed;
                UnavailablePeriodPanel.Visibility = Visibility.Visible;
                UnavailablePeriodDescription.Text =
                    "Data analisis untuk periode ini belum tersedia.";
            }
        }

        // FE integration point: caller supplies already-aggregated values.
        // Call from the WPF UI thread (Dispatcher) when real data is available.
        public void UpdatePeriodData(int period, IEnumerable<AnalysisDataPoint> points,
            double energySavedKwh, double co2SavedKg, double moneySavedRp,
            double powerSaverKwh = 0, double sleepKwh = 0, double durationHours = 0)
        {
            if (period < 0 || period > 2)
                throw new ArgumentOutOfRangeException(nameof(period));

            var pointList = (points ?? Enumerable.Empty<AnalysisDataPoint>()).ToList();
            if (pointList.Any(p => p == null || !IsValid(p.Co2SavedKg) ||
                !IsValid(p.MoneySavedRp)))
                throw new ArgumentException("Titik grafik harus berisi angka valid.", nameof(points));
            if (!IsValid(energySavedKwh) || !IsValid(co2SavedKg) || !IsValid(moneySavedRp) ||
                !IsValid(powerSaverKwh) || !IsValid(sleepKwh) || !IsValid(durationHours))
                throw new ArgumentException("Nilai KPI harus berupa angka valid.");

            _periodData[period] = new AnalysisPeriodData
            {
                Points = pointList,
                EnergySavedKwh = energySavedKwh,
                Co2SavedKg = co2SavedKg,
                MoneySavedRp = moneySavedRp,
                PowerSaverKwh = powerSaverKwh,
                SleepKwh = sleepKwh,
                DurationHours = durationHours
            };
            if (PeriodComboBox.SelectedIndex == period)
                ShowSelectedPeriod();
        }

        // Forgets a period so it shows the empty state again (no user / no session data).
        public void ClearPeriodData(int period)
        {
            if (period < 0 || period > 2)
                throw new ArgumentOutOfRangeException(nameof(period));

            if (_periodData.Remove(period) && PeriodComboBox.SelectedIndex == period)
                ShowSelectedPeriod();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
                _ = LoadAsync();
        }

        // Reads the logged-in user's power sessions off the UI thread and feeds the three periods.
        private async Task LoadAsync()
        {
            int request = ++_loadRequest;
            string? userId = AuthBridge.CurrentUser?.UserId;
            var accounts = (Application.Current as App)?.Accounts;
            if (string.IsNullOrEmpty(userId) || accounts == null)
            {
                for (int period = 0; period <= 2; period++)
                    ClearPeriodData(period);
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            try
            {
                var periods = await AnalyticsLoader.LoadPeriodsAsync(accounts.GetRepository(), userId, today);
                if (request != _loadRequest)
                    return;

                for (int period = 0; period < periods.Length; period++)
                {
                    try
                    {
                        var data = periods[period];
                        if (data == null)
                            ClearPeriodData(period);
                        else
                            UpdatePeriodData(period, data.Points, data.Kwh, data.Co2Kg, data.Rupiah,
                                data.PowerSaverKwh, data.SleepKwh, data.DurationHours);
                    }
                    catch (Exception ex) when (ex is ArgumentException)
                    {
                        // One bad period must not hide the others.
                        ClearPeriodData(period);
                        System.Diagnostics.Trace.TraceError("EcoKoin analysis period {0} invalid: {1}", period, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Keep whatever is shown; the next time the page opens it tries again.
                System.Diagnostics.Trace.TraceError("EcoKoin analysis load failed: {0}", ex.GetType().Name);
            }
        }

        private static bool IsValid(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static void DrawChart(Canvas canvas, IList<AnalysisDataPoint> points,
            Func<AnalysisDataPoint, double> selector, Color color, bool currency)
        {
            canvas.Children.Clear();
            const double left = 48, right = 453, top = 18, bottom = 154;
            double max = Math.Max(1, points.Max(selector));
            // Round the vertical scale up to a readable value.
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(max)));
            double step = Math.Ceiling(max / (3 * magnitude)) * magnitude;
            double ceiling = 3 * step;
            var axisBrush = new SolidColorBrush(Color.FromRgb(103, 129, 119));
            var gridBrush = new SolidColorBrush(Color.FromRgb(207, 219, 216));
            var lineBrush = new SolidColorBrush(color);

            for (int tick = 0; tick <= 3; tick++)
            {
                double value = tick * step;
                double y = bottom - tick * (bottom - top) / 3;
                canvas.Children.Add(new Line
                {
                    X1 = left,
                    Y1 = y,
                    X2 = right,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                });
                AddLabel(canvas, currency ? value.ToString("N0", new CultureInfo("id-ID")) :
                    value.ToString("0.##", new CultureInfo("id-ID")),
                    0, y - 8, 40, axisBrush, 11, TextAlignment.Right);
            }

            var polyline = new Polyline
            {
                Stroke = lineBrush,
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            };
            for (int i = 0; i < points.Count; i++)
            {
                double x = points.Count == 1 ? (left + right) / 2 :
                    left + i * (right - left) / (points.Count - 1);
                double y = bottom - selector(points[i]) / ceiling * (bottom - top);
                polyline.Points.Add(new Point(x, y));
            }
            canvas.Children.Add(polyline);

            for (int i = 0; i < points.Count; i++)
            {
                double x = polyline.Points[i].X;
                double y = polyline.Points[i].Y;
                var dot = new Ellipse { Width = 6, Height = 6, Fill = lineBrush };
                Canvas.SetLeft(dot, x - 3);
                Canvas.SetTop(dot, y - 3);
                canvas.Children.Add(dot);
                // Keep labels readable for longer monthly/yearly series.
                int labelStride = Math.Max(1, (int)Math.Ceiling(points.Count / 7.0));
                if (i % labelStride == 0 || i == points.Count - 1)
                    AddLabel(canvas, points[i].Label ?? "", x - 34, 171,
                        68, axisBrush, 11, TextAlignment.Center);
            }
        }

        private static void AddLabel(Canvas canvas, string value, double x,
            double y, double width, Brush foreground, double size,
            TextAlignment alignment)
        {
            var label = new TextBlock
            {
                Text = value,
                Width = width,
                Height = 18,
                FontSize = size,
                Foreground = foreground,
                TextAlignment = alignment,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        private class AnalysisPeriodData
        {
            public List<AnalysisDataPoint> Points { get; set; } =
                new List<AnalysisDataPoint>();
            public double EnergySavedKwh { get; set; }
            public double Co2SavedKg { get; set; }
            public double MoneySavedRp { get; set; }
            public double PowerSaverKwh { get; set; }
            public double SleepKwh { get; set; }
            public double DurationHours { get; set; }
        }
    }
}
