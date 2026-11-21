using System.Windows;
using EcoKoin.WPF.Views;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        public void ShowFullLogin()
        {
            var login = new LoginWindow();

            Application.Current.MainWindow = login;

            login.Show();

            Close();
        }
    }
}