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
        private void InitializeDashboardInteractions()
        {
            var buttons = FindChildren<Button>(
                DashboardContent).ToList();

            int applyIndex = 0;

            foreach (var button in buttons)
            {
                string label =
                    button.Content?.ToString() ?? "";

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

            int linkIndex = 0;

            var links = FindChildren<TextBlock>(
                DashboardContent)
                .Where(t => t.Text.StartsWith(
                    "Lihat Selengkapnya"))
                .ToList();

            foreach (var link in links)
            {
                int index = linkIndex++;

                link.Cursor = Cursors.Hand;

                Brush original = link.Foreground;

                link.MouseEnter += (_, _) =>
                {
                    link.Foreground = Brushes.DarkOrange;
                    link.TextDecorations =
                        TextDecorations.Underline;
                };

                link.MouseLeave += (_, _) =>
                {
                    link.Foreground = original;
                    link.TextDecorations = null;
                };

                link.MouseLeftButtonUp += (_, _) =>
                {
                    if (index == 0)
                        ShowSection("Analisis");
                    else
                        ShowSection("Misi & Reward");
                };
            }
        }
    }
}