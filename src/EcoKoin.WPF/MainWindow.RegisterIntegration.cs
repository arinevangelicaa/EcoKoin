using EcoKoin.WPF.Views;
using System.Windows;

namespace EcoKoin.WPF
{
    // This is a separate partial class: no need to overwrite MainWindow.xaml.cs.
    public partial class MainWindow
    {
        public void ShowLoginOverlay()
        {
            LoginValidation.Text = string.Empty;
            LoginPassword.Clear();
            LoginOverlay.Visibility = Visibility.Visible;
            LoginEmail.Focus();
        }

        private void OpenRegistration_Click(object sender, RoutedEventArgs e)
        {
            LoginOverlay.Visibility = Visibility.Collapsed;
            var registration = new RegisterWindow { Owner = this };
            registration.ShowDialog();
        }
    }
}