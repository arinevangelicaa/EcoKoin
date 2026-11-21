using System.Windows;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.Views;

namespace EcoKoin.WPF
{
    public partial class App : Application
    {
        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

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