using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.Views
{
    public partial class LoginWindow : Window
    {
        // Frontend demo only: optional remembered email lasts for this app process.
        private static string? _rememberedEmail;
        private bool _passwordShown;
        private bool _updatingPassword;

        public LoginWindow()
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(_rememberedEmail))
            {
                EmailInput.Text = _rememberedEmail;
                RememberMe.IsChecked = true;
            }
            UpdateHints();
            Loaded += (_, _) => { if (string.IsNullOrEmpty(EmailInput.Text)) EmailInput.Focus(); else PasswordInput.Focus(); };
        }

        private void UpdateHints()
        {
            EmailHint.Visibility = string.IsNullOrWhiteSpace(EmailInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            string password = _passwordShown ? PasswordVisible.Text : PasswordInput.Password;
            PasswordHint.Visibility = string.IsNullOrEmpty(password) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void EmailInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateHints();
        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e) { if (!_updatingPassword) UpdateHints(); }
        private void PasswordVisible_TextChanged(object sender, TextChangedEventArgs e) { if (!_updatingPassword) UpdateHints(); }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _updatingPassword = true;
            if (!_passwordShown)
            {
                PasswordVisible.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordVisible.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordInput.Password = PasswordVisible.Text;
                PasswordVisible.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordVisible.Clear();
            }
            _passwordShown = !_passwordShown;
            PasswordEyeIcon.Text = _passwordShown ? "\uE7B3" : "\uE890";
            TogglePassword.ToolTip = _passwordShown ? "Sembunyikan kata sandi" : "Tampilkan kata sandi";
            _updatingPassword = false;
            UpdateHints();
            if (_passwordShown) PasswordVisible.Focus(); else PasswordInput.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailInput.Text.Trim();
            string password = _passwordShown ? PasswordVisible.Text : PasswordInput.Password;
            ValidationMessage.Visibility = Visibility.Visible;

            if (!MailAddress.TryCreate(email, out var parsed) ||
                !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage.Text = "Masukkan alamat email yang valid.";
                EmailInput.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ValidationMessage.Text = "Kata sandi wajib diisi.";
                if (_passwordShown) PasswordVisible.Focus(); else PasswordInput.Focus();
                return;
            }

            _rememberedEmail = RememberMe.IsChecked == true ? email : null;

            // First demo login: show onboarding once on this device before Dashboard.
            // This remains a frontend demo; do not interpret it as server authentication.
            if (!WelcomePreferencesStore.Load().HasCompletedOnboarding)
            {
                var welcome = new WelcomeWindow { Owner = this };
                if (welcome.ShowDialog() != true)
                    return; // User closed onboarding: keep Login visible.
            }

            // Demo: this is NOT real authentication. Never store the password.
            var dashboard = Owner as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (dashboard == null)
            {
                dashboard = new MainWindow();
                Application.Current.MainWindow = dashboard;
            }
            dashboard.AppSidebar.SetLoggedIn(true, email);
            if (!dashboard.IsVisible) dashboard.Show();
            dashboard.Activate();
            PasswordInput.Clear();
            PasswordVisible.Clear();
            Close();
        }

        private void OpenRegister_Click(object sender, RoutedEventArgs e)
        {
            // Keep login behind the registration dialog so 'Masuk' returns here.
            var register = new RegisterWindow { Owner = this };
            register.ShowDialog();
            Activate();
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var forgot = new ForgotPasswordWindow { Owner = this };
            forgot.EmailInput.Text = EmailInput.Text.Trim();
            forgot.ShowDialog();
            Activate();
        }
    }
}