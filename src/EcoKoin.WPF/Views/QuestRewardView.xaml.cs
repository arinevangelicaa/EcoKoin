using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoKoin.WPF.Views
{
    public partial class QuestRewardView : UserControl
    {
        public QuestRewardView()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                foreach (var button in
                    FindButtons(this))
                {
                    string label =
                        button.Content?.ToString() ?? "";

                    if (label == "Klaim +30")
                    {
                        button.Click -= ClaimSleep_Click;
                        button.Click += ClaimSleep_Click;
                    }
                    else if (label == "Klaim +15")
                    {
                        button.Click -= ClaimWeather_Click;
                        button.Click += ClaimWeather_Click;
                    }
                }
            };
        }

        private void ClaimSleep_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Klaim misi menunggu konfirmasi "
                + "dari layanan backend.",
                "Misi EcoKoin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ClaimWeather_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Klaim misi menunggu konfirmasi "
                + "dari layanan backend.",
                "Misi EcoKoin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static System.Collections.Generic.IEnumerable<Button>
            FindButtons(DependencyObject parent)
        {
            for (int i = 0;
                i < VisualTreeHelper.GetChildrenCount(parent);
                i++)
            {
                var child =
                    VisualTreeHelper.GetChild(parent, i);

                if (child is Button button)
                    yield return button;

                foreach (var nested in FindButtons(child))
                    yield return nested;
            }
        }
    }
}