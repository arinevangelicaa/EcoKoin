using System.Windows;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.Views;

namespace EcoKoin.WPF
{
    public partial class App : Application
    {
        internal SqliteAccountService Accounts { get; } = SqliteAccountService.CreateDefault();

        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

            AuthBridge.LoginHandler ??= Accounts.LoginAsync;
            AuthBridge.RestoreSessionHandler ??= Accounts.RestoreSessionAsync;
            AuthBridge.LogoutHandler ??= Accounts.LogoutAsync;

            Window startWindow;

            if (!WelcomePreferencesStore.Load()
                    .HasCompletedOnboarding)
            {
                startWindow = new SplashWindow();
            }
            else if (await AuthBridge.RestoreAsync())
            {
                startWindow = new MainWindow();
            }
            else
            {
                startWindow = new LoginWindow();
            }

            MainWindow = startWindow;
            startWindow.Show();
        }
    }
}
