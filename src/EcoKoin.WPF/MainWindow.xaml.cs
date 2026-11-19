using EcoKoin.WPF.Components;
using System;
using System.Configuration;
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
            AppSidebar.AddHandler(ButtonBase.ClickEvent,
                new RoutedEventHandler(OnSidebarMenuClick), true);
            AppSidebar.AuthActionRequested += OnAuthActionRequested;
            AppSidebar.SetLoggedIn(true); // Demo starts signed in as in the Figma mockup.
        }

        private void OnSidebarMenuClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject target)
                return;

            var menu = FindParent<SidebarMenuItem>(target);
            if (menu == null) return;

            bool dashboard = menu.MenuText == "Dashboard";
            bool quest = menu.MenuText == "Misi & Reward";
            bool analysis = menu.MenuText == "Analisis";
            bool settings = menu.MenuText == "Pengaturan";
            if (!dashboard && !quest && !analysis && !settings) return;

            DashboardContent.Visibility = dashboard ? Visibility.Visible : Visibility.Collapsed;
            QuestRewardContent.Visibility = quest ? Visibility.Visible : Visibility.Collapsed;
            AnalysisContent.Visibility = analysis ? Visibility.Visible : Visibility.Collapsed;
            SettingsContent.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;

            Title = dashboard ? "EcoKoin - Dashboard"
                  : quest ? "EcoKoin - Misi & Reward"
                  : analysis ? "EcoKoin - Analisis"
                  : "EcoKoin - Pengaturan";

            foreach (var item in FindChildren<SidebarMenuItem>(AppSidebar))
                item.IsActive = item.MenuText == menu.MenuText;

            e.Handled = true;
        }

        private void OnAuthActionRequested(object? sender, EventArgs e)
        {
            if (AppSidebar.IsLoggedIn)
            {
                // Frontend-only logout; no backend session/cookie to revoke yet.
                AppSidebar.SetLoggedIn(false);
            }
            else
            {
                ShowFullLogin();
            }
        }

        private void CancelLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginPassword.Clear();
            LoginOverlay.Visibility = Visibility.Collapsed;
        }

        private void SubmitLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = LoginEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(LoginPassword.Password))
            {
                LoginValidation.Text = "Isi email dan password untuk demo login.";
                return;
            }
            // Demo: NOT an authenticated login. Never save or log entered password.
            AppSidebar.SetLoggedIn(true, email);
            LoginPassword.Clear();
            LoginOverlay.Visibility = Visibility.Collapsed;
        }

        private static T? FindParent<T>(DependencyObject source) where T : DependencyObject
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> FindChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) yield return found;
                foreach (var nested in FindChildren<T>(child)) yield return nested;
            }
        }
    }
}