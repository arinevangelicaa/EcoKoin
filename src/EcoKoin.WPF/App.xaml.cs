using System.Windows;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.Views;

namespace EcoKoin.WPF
{
    public partial class App : Application
    {
        internal AccountService Accounts { get; private set; } = null!;

        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Koneksi PostgreSQL dibaca dari environment variable ECOKOIN_DB_CONNECTION.
                Accounts = new AccountService(DatabaseConfig.CreateFromEnvironment());
                await Accounts.InitializeAsync();
            }
            catch (Exception ex)
            {
                // Jangan menampilkan connection string / password di pesan.
                System.Diagnostics.Trace.TraceError("EcoKoin database startup failed: {0}", ex.GetType().Name);
                MessageBox.Show(
                    "EcoKoin tidak dapat terhubung ke database PostgreSQL.\n\n" +
                    "Pastikan environment variable " + DatabaseConfig.ConnectionStringVariable +
                    " sudah diatur, misalnya (PowerShell):\n" +
                    "  setx " + DatabaseConfig.ConnectionStringVariable +
                    " \"Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>\"\n\n" +
                    "Lalu tutup dan buka kembali aplikasi. Periksa juga koneksi internet dan hak akses pengguna database.",
                    "EcoKoin - Database tidak tersedia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

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
