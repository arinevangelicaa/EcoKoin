using System.Windows;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        public void ShowLoginOverlay()
        {
            ShowFullLogin();
        }

        private void OpenRegistration_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowFullLogin();
        }
    }
}