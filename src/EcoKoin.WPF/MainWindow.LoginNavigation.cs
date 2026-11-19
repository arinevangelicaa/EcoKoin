using EcoKoin.WPF.Views;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        public void ShowFullLogin()
        {
            var login = new LoginWindow { Owner = this };
            login.ShowDialog();
        }
    }
}