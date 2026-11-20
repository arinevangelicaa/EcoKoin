using System;
using System.Windows;
using System.Windows.Threading;

namespace EcoKoin.WPF.Views
{
    public partial class SplashWindow : Window
    {
        private readonly DispatcherTimer _loadingTimer;
        private int _progress;

        public SplashWindow()
        {
            InitializeComponent();
            _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _loadingTimer.Tick += LoadingTimer_Tick;
            Loaded += (_, _) => _loadingTimer.Start();
            Closed += (_, _) => _loadingTimer.Stop();
        }

        private void LoadingTimer_Tick(object? sender, EventArgs e)
        {
            _progress = Math.Min(_progress + 1, 100);
            LoadingProgress.Value = _progress;
            if (_progress < 100) return;

            _loadingTimer.Stop();
            // Frontend demo: show Login before Dashboard, not a pre-authenticated session.
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            Close();
        }
    }
}