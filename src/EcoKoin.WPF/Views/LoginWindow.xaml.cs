using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.Views
{
    public partial class LoginWindow : Window
    {
        private bool _passwordShown;
        private bool _updatingPassword;
        private bool _submitting;

        public LoginWindow()
        {
            InitializeComponent();

            UpdateHints();

            Loaded += (_, _) =>
            {
                EmailInput.Focus();
            };
        }

        private void UpdateHints()
        {
            EmailHint.Visibility =
                string.IsNullOrWhiteSpace(EmailInput.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            string password = _passwordShown
                ? PasswordVisible.Text
                : PasswordInput.Password;

            PasswordHint.Visibility =
                string.IsNullOrEmpty(password)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void EmailInput_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateHints();
        }

        private void PasswordInput_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (!_updatingPassword)
                UpdateHints();
        }

        private void PasswordVisible_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (!_updatingPassword)
                UpdateHints();
        }

        private void TogglePassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            _updatingPassword = true;

            if (!_passwordShown)
            {
                PasswordVisible.Text =
                    PasswordInput.Password;

                PasswordInput.Visibility =
                    Visibility.Collapsed;

                PasswordVisible.Visibility =
                    Visibility.Visible;
            }
            else
            {
                PasswordInput.Password =
                    PasswordVisible.Text;

                PasswordVisible.Visibility =
                    Visibility.Collapsed;

                PasswordInput.Visibility =
                    Visibility.Visible;

                PasswordVisible.Clear();
            }

            _passwordShown = !_passwordShown;

            PasswordEyeIcon.Text =
                (string)FindResource(
                    _passwordShown
                        ? "IconEyeOff"
                        : "IconEye");

            TogglePassword.ToolTip = _passwordShown
                ? "Sembunyikan kata sandi"
                : "Tampilkan kata sandi";

            _updatingPassword = false;
            UpdateHints();

            if (_passwordShown)
                PasswordVisible.Focus();
            else
                PasswordInput.Focus();
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_submitting)
                return;

            string email = EmailInput.Text.Trim();

            string password = _passwordShown
                ? PasswordVisible.Text
                : PasswordInput.Password;

            ValidationMessage.Visibility =
                Visibility.Visible;

            if (!MailAddress.TryCreate(
                    email, out var parsed) ||
                !string.Equals(
                    parsed.Address,
                    email,
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage.Text =
                    "Masukkan alamat email yang valid.";

                EmailInput.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ValidationMessage.Text =
                    "Kata sandi wajib diisi.";

                if (_passwordShown)
                    PasswordVisible.Focus();
                else
                    PasswordInput.Focus();

                return;
            }

            _submitting = true;
            ValidationMessage.Text = "Sedang masuk...";

            try
            {
                bool success = await AuthBridge.LoginAsync(
                    email,
                    password,
                    RememberMe.IsChecked == true);

                if (!success)
                {
                    ValidationMessage.Text =
                        "Email atau kata sandi tidak sesuai.";
                    return;
                }

                if (!WelcomePreferencesStore.Load()
                        .HasCompletedOnboarding)
                {
                    var welcome = new WelcomeWindow
                    {
                        Owner = this
                    };

                    if (welcome.ShowDialog() != true)
                    {
                        ValidationMessage.Text =
                            "Selesaikan pengaturan awal untuk melanjutkan.";
                        return;
                    }
                }

                var dashboard = new MainWindow();

                Application.Current.MainWindow = dashboard;

                dashboard.Show();

                PasswordInput.Clear();
                PasswordVisible.Clear();

                Close();
            }
            catch (Exception ex)
            {
                ValidationMessage.Text = ex.Message;
            }
            finally
            {
                _submitting = false;
            }
        }

        private void OpenRegister_Click(
            object sender,
            RoutedEventArgs e)
        {
            var register = new RegisterWindow
            {
                Owner = this
            };

            register.ShowDialog();
            Activate();
        }

        private void ForgotPassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            var forgot = new ForgotPasswordWindow
            {
                Owner = this
            };

            forgot.EmailInput.Text =
                EmailInput.Text.Trim();

            forgot.ShowDialog();
            Activate();
        }
    }
}