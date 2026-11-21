using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
                int applyIndex = 0;

                foreach (var button in buttons)
                {
                    string label = button.Content?.ToString() ?? "";

                    switch (label)
                    {
                        case "Terapkan":
                            if (applyIndex == 0)
                                button.Click += ApplyBrightness_Click;
                            else if (applyIndex == 1)
                                button.Click += ApplySleep_Click;
                            applyIndex++;
                            break;

                        case "Klaim +30":
                            button.Click += DashboardClaimSleep_Click;
                            break;

                        case "Klaim +15":
                            button.Click += DashboardClaimWeather_Click;
                            break;
                    }

                    button.Cursor = Cursors.Hand;
                }

                ConfigureDashboardDetailLink(AnalysisDetailLink, "Analisis");
                ConfigureDashboardDetailLink(QuestDetailLink, "Misi & Reward");
            };
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