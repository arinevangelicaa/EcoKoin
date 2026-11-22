using EcoKoin.WPF.Components;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EcoKoin.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var user = AuthBridge.CurrentUser;
            DashboardContent.DataContext = new DashboardViewModel(user);
            InitializeDashboardInteractions();

            AppSidebar.AddHandler(
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnSidebarMenuClick),
                true);

            AppSidebar.AuthActionRequested +=
                OnAuthActionRequested;

            AppSidebar.SetLoggedIn(user is not null, user?.Email);

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

        private void CancelLogin_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoginPassword.Clear();
            LoginOverlay.Visibility =
                Visibility.Collapsed;
        }

        private void SubmitLogin_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Jalur overlay lama tidak digunakan.
            LoginPassword.Clear();
            LoginOverlay.Visibility =
                Visibility.Collapsed;

            ShowFullLogin();
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
