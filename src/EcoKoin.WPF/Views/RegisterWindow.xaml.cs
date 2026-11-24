using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Media;

namespace EcoKoin.WPF.Views
{
    public partial class RegisterWindow : Window
    {
        private bool _passwordShown;
        private bool _confirmationShown;
        private bool _syncingText;

        internal string RegisteredEmail { get; private set; } = "";

        public RegisterWindow()
        {
            InitializeComponent();
            NameInput.TextChanged += (_, _) => UpdateNameHint();
            EmailInput.TextChanged += (_, _) =>
                EmailHint.Visibility = string.IsNullOrEmpty(EmailInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            UpdateNameHint();
            UpdatePasswordHint();
            UpdateConfirmHint();
        }

        private void UpdateNameHint()
        {
            NameHint.Visibility = string.IsNullOrEmpty(NameInput.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePasswordHint()
        {
            var value = _passwordShown ? PasswordVisible.Text : PasswordInput.Password;
            PasswordHint.Visibility = string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void UpdateConfirmHint()
        {
            var value = _confirmationShown ? ConfirmVisible.Text : ConfirmInput.Password;
            ConfirmHint.Visibility = string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void PasswordInput_Changed(object sender, RoutedEventArgs e) { if (!_syncingText) UpdatePasswordHint(); }
        private void ConfirmInput_Changed(object sender, RoutedEventArgs e) { if (!_syncingText) UpdateConfirmHint(); }
        private void PasswordVisible_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (!_syncingText) UpdatePasswordHint(); }
        private void ConfirmVisible_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (!_syncingText) UpdateConfirmHint(); }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _syncingText = true;
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
            PasswordEyeIcon.Text = (string)FindResource(
                _passwordShown ? "IconEyeOff" : "IconEye");
            TogglePassword.ToolTip = _passwordShown ? "Sembunyikan kata sandi" : "Tampilkan kata sandi";
            _syncingText = false;
            UpdatePasswordHint();
            if (_passwordShown) PasswordVisible.Focus(); else PasswordInput.Focus();
        }
        private void ToggleConfirm_Click(object sender, RoutedEventArgs e)
        {
            _syncingText = true;
            if (!_confirmationShown)
            {
                ConfirmVisible.Text = ConfirmInput.Password;
                ConfirmInput.Visibility = Visibility.Collapsed;
                ConfirmVisible.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmInput.Password = ConfirmVisible.Text;
                ConfirmVisible.Visibility = Visibility.Collapsed;
                ConfirmInput.Visibility = Visibility.Visible;
                ConfirmVisible.Clear();
            }
            _confirmationShown = !_confirmationShown;
            ConfirmEyeIcon.Text = _confirmationShown ? "\uE7B3" : "\uE890";
            ToggleConfirm.ToolTip = _confirmationShown ? "Sembunyikan konfirmasi" : "Tampilkan konfirmasi";
            _syncingText = false;
            UpdateConfirmHint();
            if (_confirmationShown) ConfirmVisible.Focus(); else ConfirmInput.Focus();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var name = NameInput.Text.Trim();
            var email = EmailInput.Text.Trim();
            var password = _passwordShown ? PasswordVisible.Text : PasswordInput.Password;
            var confirm = _confirmationShown ? ConfirmVisible.Text : ConfirmInput.Password;

            ValidationMessage.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(181, 71, 56));
            if (name.Length < 2)
            {
                ValidationMessage.Text = "Isi nama minimal 2 karakter.";
                NameInput.Focus();
                return;
            }
            if (!MailAddress.TryCreate(email, out var parsed) ||
                !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage.Text = "Masukkan alamat email yang valid.";
                EmailInput.Focus();
                return;
            }
            if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                ValidationMessage.Text = "Kata sandi minimal 8 karakter dan harus berisi huruf serta angka.";
                if (_passwordShown) PasswordVisible.Focus(); else PasswordInput.Focus();
                return;
            }
            if (password != confirm)
            {
                ValidationMessage.Text = "Konfirmasi kata sandi belum sama.";
                if (_confirmationShown) ConfirmVisible.Focus(); else ConfirmInput.Focus();
                return;
            }

            RegisterButton.IsEnabled = false;
            ValidationMessage.Text = "Sedang menyimpan akun...";
            try
            {
                var accounts = ((App)Application.Current).Accounts;
                if (!await accounts.RegisterAsync(name, email, password))
                {
                    ValidationMessage.Text = "Email ini sudah terdaftar. Gunakan email lain atau masuk.";
                    EmailInput.Focus();
                    return;
                }

                RegisteredEmail = email;
                if (IsLoaded)
                    DialogResult = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("EcoKoin registration failed: {0}", ex.GetType().Name);
                ValidationMessage.Text = "Akun belum dapat disimpan. Coba lagi dan pastikan koneksi internet serta database EcoKoin tersedia.";
            }
            finally
            {
                RegisterButton.IsEnabled = true;
            }
        }

        private void OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            // Registration is only opened from the login page, which shows itself again when this closes.
            Close();
        }
    }
}
