using EcoKoin.WPF.Components;
using EcoKoin.WPF.Models;
using EcoKoin.Services;
using EcoKoin.WPF.ViewModels;
using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EcoKoin.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(WeatherHttpClient, new SystemClock(), WelcomePreferencesStore.Load)
        {
        }

        internal MainWindow(HttpClient weatherClient, IClock clock, Func<WelcomePreferences> loadPreferences)
        {
            InitializeComponent();
            var user = AuthBridge.CurrentUser;
            var dashboard = new DashboardViewModel(user);
            InitializeDashboardWeather(dashboard, weatherClient, clock, loadPreferences);
            var powerStatus = new WindowsPowerSaverStatus();
            var batteryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            void RefreshBattery()
            {
                var battery = WindowsPowerSaverStatus.ReadBattery();
                dashboard.BateraiDisplay = battery.Percent is int percent ? $"{percent}%" : "—";
                dashboard.StatusDayaDisplay = battery.Status;
            }
            SourceInitialized += (_, _) => powerStatus.Register(PresentationSource.FromVisual(this) as HwndSource);
            Loaded += (_, _) =>
            {
                RefreshBattery();
                batteryTimer.Start();
            };
            batteryTimer.Tick += (_, _) => RefreshBattery();
            Closed += (_, _) =>
            {
                batteryTimer.Stop();
                powerStatus.Unregister();
            };
            if (user?.UserId is not null && Application.Current is App app)
            {
                InitializePowerSaverQuest(dashboard, user, app.Accounts, powerStatus);
                InitializeDashboardQuests(dashboard, user, app.Accounts, clock);
                InitializeDashboardPet(dashboard, user, app.Accounts, clock);
                InitializeDashboardAnalytics(dashboard, user, app.Accounts, clock);
                InitializePowerSessions(dashboard, user, app.Accounts, powerStatus, clock);
            }

            DashboardContent.DataContext = dashboard;
            QuestRewardContent.DataContext = dashboard;

            InitializeDashboardInteractions();

            AppSidebar.AddHandler(
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnSidebarMenuClick),
                true);

            AppSidebar.AuthActionRequested +=
                OnAuthActionRequested;

            AppSidebar.SetLoggedIn(user is not null, user?.Email, dashboard.AccountDisplayName);

            Loaded += (_, _) =>
            {
                if (!AuthBridge.IsAuthenticated)
                    ShowFullLogin();
            };
        }

        private void OnSidebarMenuClick(
            object sender,
            RoutedEventArgs e)
        {
            if (!AuthBridge.IsAuthenticated)
            {
                ShowFullLogin();
                return;
            }

            if (e.OriginalSource is not DependencyObject target)
                return;

            var menu = FindParent<SidebarMenuItem>(target);

            if (menu == null)
                return;

            string section = menu.MenuText;

            if (section != "Dashboard" &&
                section != "Misi & Reward" &&
                section != "Analisis" &&
                section != "Pengaturan")
                return;

            ShowSection(section);
            e.Handled = true;
        }

        private async void OnAuthActionRequested(
            object? sender,
            EventArgs e)
        {
            if (!AuthBridge.IsAuthenticated)
            {
                ShowFullLogin();
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Apakah kamu yakin ingin logout?",
                "Konfirmasi Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                bool success =
                    await AuthBridge.LogoutAsync();

                if (success)
                {
                    ShowFullLogin();
                }
                else
                {
                    MessageBox.Show(
                        this,
                        "Logout gagal. Silakan coba lagi.",
                        "EcoKoin",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Logout belum tersedia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static T? FindParent<T>(
            DependencyObject source)
            where T : DependencyObject
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (current is T match)
                    return match;

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<T>
            FindChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (int i = 0;
                 i < VisualTreeHelper.GetChildrenCount(parent);
                 i++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(parent, i);

                if (child is T found)
                    yield return found;

                foreach (var nested in
                         FindChildren<T>(child))
                    yield return nested;
            }
        }

        private void AppSidebar_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
