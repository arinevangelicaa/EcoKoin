using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using EcoKoin.WPF.Components;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private async void ApplyBrightness_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AuthBridge.CurrentUser?.UserId is not string userId || Application.Current is not App app)
            {
                MessageBox.Show(
                    this,
                    "Silakan login terlebih dahulu untuk menerapkan rekomendasi cuaca.",
                    "Rekomendasi Cuaca",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var repo = app.Accounts.GetRepository();
            var date = DateOnly.FromDateTime(DateTime.Now);
            repo.TryApplyWeatherQuest(userId, date);

            await RefreshActiveQuestsAsync();

            MessageBox.Show(
                this,
                "Rekomendasi kecerahan cuaca berhasil diterapkan!\n\n"
                + "Misi harian 'Terapkan 1 rekomendasi cuaca' telah selesai. "
                + "Klaim reward +15 EcoKoin kamu di menu Misi & Reward atau kartu Daily Quest.",
                "Rekomendasi Cuaca Diterapkan",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ApplySleep_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                "Pengaturan sleep otomatis belum terhubung "
                + "ke layanan perangkat.",
                "Rekomendasi Hemat Daya",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DashboardClaimSleep_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                "Klaim koin memerlukan konfirmasi "
                + "dari layanan misi.",
                "Misi Harian",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void DashboardClaimWeather_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AuthBridge.CurrentUser?.UserId is not string userId || Application.Current is not App app)
                return;

            var repo = app.Accounts.GetRepository();
            var date = DateOnly.FromDateTime(DateTime.Now);
            repo.TryClaimWeatherQuest(userId, date);
            await RefreshActiveQuestsAsync();
        }

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

            if (analysis && AuthBridge.CurrentUser?.UserId is string currentUserId && Application.Current is App currentApp)
            {
                var repo = currentApp.Accounts.GetRepository();
                var date = DateOnly.FromDateTime(DateTime.Now);
                long minutes = repo.GetPowerSaverMinutes(currentUserId, date.AddDays(-6), date);
                var dailyMinutes = repo.GetDailyPowerSaverMinutes(currentUserId, date.AddDays(-6), date);

                double kwh = (20.0 * (minutes / 60.0)) / 1000.0;
                double co2 = kwh * 0.87;
                double rp = kwh * 1444.70;

                var idCulture = CultureInfo.GetCultureInfo("id-ID");
                var points = new List<AnalysisDataPoint>(7);
                for (int i = 6; i >= 0; i--)
                {
                    var d = date.AddDays(-i);
                    long m = dailyMinutes.TryGetValue(d, out var min) ? min : 0;
                    double dayKwh = (20.0 * (m / 60.0)) / 1000.0;
                    points.Add(new AnalysisDataPoint
                    {
                        Label = d.ToString("ddd", idCulture),
                        Co2SavedKg = dayKwh * 0.87,
                        MoneySavedRp = dayKwh * 1444.70
                    });
                }

                AnalysisContent.UpdatePeriodData(0, points, kwh, co2, rp);
                AnalysisContent.ShowWeeklyPowerSaverMinutes(minutes);
            }

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