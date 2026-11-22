using System.Windows;
using EcoKoin.WPF.Components;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        // ApplyBrightness_Click / ApplySleep_Click live in MainWindow.UiFixes.cs; quests are claimed through
        // QuestItemViewModel.ClaimCommand (MainWindow.Quests.cs).
        private void OpenFullAnalysis_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection("Analisis");
        }

        private void OpenFullQuests_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection("Misi & Reward");
        }

        private void ShowSection(string section)
        {
            if (!AuthBridge.IsAuthenticated)
            {
                ShowFullLogin();
                return;
            }

            bool dashboard = section == "Dashboard";
            bool quest = section == "Misi & Reward";
            bool analysis = section == "Analisis";
            bool settings = section == "Pengaturan";

            DashboardContent.Visibility = dashboard
                ? Visibility.Visible
                : Visibility.Collapsed;

            QuestRewardContent.Visibility = quest
                ? Visibility.Visible
                : Visibility.Collapsed;

            AnalysisContent.Visibility = analysis
                ? Visibility.Visible
                : Visibility.Collapsed;

            SettingsContent.Visibility = settings
                ? Visibility.Visible
                : Visibility.Collapsed;

            Title = "EcoKoin - " + section;

            foreach (var item in
                     FindChildren<SidebarMenuItem>(AppSidebar))
            {
                item.IsActive =
                    item.MenuText == section;
            }
        }
    }
}