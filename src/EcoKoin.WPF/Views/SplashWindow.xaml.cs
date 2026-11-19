
using System;
using System.Windows;
using System.Windows.Threading;

namespace EcoKoin.WPF.Views
{
    public partial class SplashWindow : Window
    {
        private readonly DispatcherTimer _loadingTimer;
        private int _progress = 0;

        public SplashWindow()
        {
            InitializeComponent();

            _loadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };

            _loadingTimer.Tick += LoadingTimer_Tick;

            Loaded += (sender, e) => _loadingTimer.Start();

            Closed += (sender, e) => _loadingTimer.Stop();
        }

        private void LoadingTimer_Tick(object? sender, EventArgs e)
        {
            _progress++;

            LoadingProgress.Value = _progress;

            if (_progress >= 100)
            {
                _loadingTimer.Stop();

                MainWindow dashboard = new MainWindow();

                Application.Current.MainWindow = dashboard;

                dashboard.Show();

                Close();
            }
        }

        private void LoadingProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<T> e)
        {

        }
    }
}