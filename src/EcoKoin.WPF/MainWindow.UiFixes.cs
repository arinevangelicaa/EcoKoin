using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private bool _dashboardInteractionsInitialized;

        private void InitializeDashboardInteractions()
        {
            DashboardContent.Loaded += (_, _) =>
            {
                if (_dashboardInteractionsInitialized)
                    return;

                _dashboardInteractionsInitialized = true;

                var buttons = FindChildren<Button>(DashboardContent).ToList();

                foreach (var button in buttons)
                {
                    button.Cursor = Cursors.Hand;
                }

                ConfigureDashboardDetailLink(AnalysisDetailLink, "Analisis");
                ConfigureDashboardDetailLink(QuestDetailLink, "Misi & Reward");
            };
        }

        private bool _windowsApplyBusy;

        // The window may have been closed (logout) while the Windows action ran: a MessageBox owned by a closed
        // window throws, and this is called from async void handlers, so it must never throw.
        private void ShowFailure(string message, string title)
        {
            try
            {
                if (IsLoaded)
                    MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException)
            {
                // Window already closed: nothing left to tell.
            }
        }

        // "Terapkan" on the sleep card: changes the Windows standby timeout, only after an explicit Yes.
        private async void ApplySleep_Click(object sender, RoutedEventArgs e)
        {
            if (_windowsApplyBusy)
                return;
            _windowsApplyBusy = true;
            const string title = "Rekomendasi Hemat Daya";
            try
            {
                if (!WindowsSleepTimeout.TryParseMinutes(WelcomePreferencesStore.Load().SleepDelay, out int minutes))
                {
                    MessageBox.Show(this,
                        "Sleep otomatis dimatikan di preferensi, jadi tidak ada pengaturan yang bisa diterapkan.",
                        title, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var answer = MessageBox.Show(this,
                    $"Waktu sleep otomatis Windows untuk paket daya yang aktif akan diubah menjadi {minutes} menit, "
                    + "baik saat tersambung ke listrik maupun saat memakai baterai (powercfg /change standby-timeout).\n\n"
                    + "Lanjutkan?",
                    title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                    return;

                WindowsActionResult result = await Task.Run(
                    () => WindowsSleepTimeout.Apply(minutes, new ProcessPowerCommandRunner()));
                MessageBox.Show(this, result.Message, title, MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                ShowFailure("Pengaturan sleep gagal diterapkan: " + ex.Message, title);
            }
            finally
            {
                _windowsApplyBusy = false;
            }
        }

        // "Terapkan" on the weather card: sets the screen brightness through WMI, only after an explicit Yes.
        private async void ApplyBrightness_Click(object sender, RoutedEventArgs e)
        {
            if (_windowsApplyBusy)
                return;
            _windowsApplyBusy = true;
            const string title = "Rekomendasi Cuaca";
            try
            {
                string? recommendation = (DashboardContent.DataContext as DashboardViewModel)?.RekomendasiCuacaDisplay;
                if (!WindowsDisplayBrightness.TryParseTarget(recommendation, out int percent))
                {
                    MessageBox.Show(this, "Belum ada rekomendasi kecerahan yang bisa diterapkan.",
                        title, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var answer = MessageBox.Show(this,
                    $"Kecerahan layar akan diubah menjadi {percent}% lewat Windows (WMI).\n\n"
                    + "Lanjutkan?",
                    title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                    return;

                WindowsActionResult result = await Task.Run(() => WindowsDisplayBrightness.Apply(percent));
                if (result.Success)
                    await RecordQuestEventAsync(EcoKoin.Models.QuestEventKind.WeatherRecommendationApplied, 1);

                MessageBox.Show(this, result.Message, title, MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ShowFailure("Kecerahan gagal diubah: " + ex.Message, title);
            }
            finally
            {
                _windowsApplyBusy = false;
            }
        }

        private void ConfigureDashboardDetailLink(TextBlock link, string section)
        {
            if (link == null)
                return;

            Brush originalForeground = link.Foreground;
            TextDecorationCollection originalDecorations = link.TextDecorations;

            link.Cursor = Cursors.Hand;
            link.Focusable = true;

            link.MouseEnter += (_, _) =>
            {
                link.Foreground = Brushes.DarkOrange;
                link.TextDecorations = TextDecorations.Underline;
            };

            link.MouseLeave += (_, _) =>
            {
                link.Foreground = originalForeground;
                link.TextDecorations = originalDecorations;
            };

            link.MouseLeftButtonUp += (_, e) =>
            {
                ShowSection(section);
                e.Handled = true;
            };

            link.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    ShowSection(section);
                    e.Handled = true;
                }
            };
        }
    }
}
