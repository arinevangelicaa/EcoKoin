using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoKoin.WPF.Views
{
    public partial class ResetPasswordWindow : Window
    {
        private bool _showNew;
        private bool _showConfirm;
        private bool _syncing;

        // The backend must validate and consume the reset token.
        // No token or callback is provided in the UI-only version.
        public string? ResetToken { get; set; }
        public Func<string, string, Task<bool>>? SubmitAuthorizedResetAsync { get; set; }

        public ResetPasswordWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => NewPassword.Focus();
            RefreshHints();
        }

        private void RefreshHints()
        {
            if (NewHint == null || ConfirmHint == null) return;
            NewHint.Visibility = string.IsNullOrEmpty(_showNew ? NewVisible.Text : NewPassword.Password)
                ? Visibility.Visible : Visibility.Collapsed;
            ConfirmHint.Visibility = string.IsNullOrEmpty(_showConfirm ? ConfirmVisible.Text : ConfirmPassword.Password)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NewPassword_Changed(object sender, RoutedEventArgs e) { if (!_syncing) RefreshHints(); }
        private void ConfirmPassword_Changed(object sender, RoutedEventArgs e) { if (!_syncing) RefreshHints(); }
        private void NewVisible_Changed(object sender, TextChangedEventArgs e) { if (!_syncing) RefreshHints(); }
        private void ConfirmVisible_Changed(object sender, TextChangedEventArgs e) { if (!_syncing) RefreshHints(); }

        private void NewEye_Click(object sender, RoutedEventArgs e)
        {
            _syncing = true;
            if (!_showNew)
            {
                NewVisible.Text = NewPassword.Password;
                NewPassword.Visibility = Visibility.Collapsed;
                NewVisible.Visibility = Visibility.Visible;
            }
            else
            {
                NewPassword.Password = NewVisible.Text;
                NewVisible.Clear();
                NewPassword.Visibility = Visibility.Visible;
                NewVisible.Visibility = Visibility.Collapsed;
            }
            _showNew = !_showNew;
            NewEyeIcon.Text = _showNew ? "\uE7B3" : "\uE890";
            NewEye.ToolTip = _showNew ? "Sembunyikan kata sandi" : "Tampilkan kata sandi";
            _syncing = false;
            RefreshHints();
            if (_showNew) NewVisible.Focus(); else NewPassword.Focus();
        }

        private void ConfirmEye_Click(object sender, RoutedEventArgs e)
        {
            _syncing = true;
            if (!_showConfirm)
            {
                ConfirmVisible.Text = ConfirmPassword.Password;
                ConfirmPassword.Visibility = Visibility.Collapsed;
                ConfirmVisible.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmPassword.Password = ConfirmVisible.Text;
                ConfirmVisible.Clear();
                ConfirmPassword.Visibility = Visibility.Visible;
                ConfirmVisible.Visibility = Visibility.Collapsed;
            }
            _showConfirm = !_showConfirm;
            ConfirmEyeIcon.Text = _showConfirm ? "\uE7B3" : "\uE890";
            ConfirmEye.ToolTip = _showConfirm ? "Sembunyikan kata sandi" : "Tampilkan kata sandi";
            _syncing = false;
            RefreshHints();
            if (_showConfirm) ConfirmVisible.Focus(); else ConfirmPassword.Focus();
        }

        private void Notify(string message, bool isError = true)
        {
            ValidationMessage.Foreground = isError ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(44, 76, 62));
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
        }

        private async void SavePassword_Click(object sender, RoutedEventArgs e)
        {
            string password = _showNew ? NewVisible.Text : NewPassword.Password;
            string confirmation = _showConfirm ? ConfirmVisible.Text : ConfirmPassword.Password;

            if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                Notify("Kata sandi minimal 8 karakter dan harus mengandung huruf serta angka.");
                return;
            }
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                Notify("Konfirmasi kata sandi belum sama.");
                return;
            }
            if (string.IsNullOrWhiteSpace(ResetToken) || SubmitAuthorizedResetAsync == null)
            {
                Notify("Form sudah valid. Untuk menyimpan kata sandi sungguhan, buka tautan reset yang sah dan hubungkan layanan backend. Belum ada perubahan password.", false);
                return;
            }

            try
            {
                if (sender is Button button) button.IsEnabled = false;
                // Backend callback MUST verify token expiration, ownership, one-time use and update password hash.
                bool success = await SubmitAuthorizedResetAsync(ResetToken, password);
                if (success)
                {
                    NewPassword.Clear(); NewVisible.Clear(); ConfirmPassword.Clear(); ConfirmVisible.Clear();
                    MessageBox.Show(this, "Kata sandi berhasil diperbarui. Silakan masuk kembali.",
                        "EcoKoin", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else Notify("Tautan reset tidak berlaku atau sudah kedaluwarsa. Silakan minta tautan baru.");
            }
            catch
            {
                Notify("Reset gagal diproses. Periksa koneksi atau coba minta tautan reset baru.");
            }
            finally
            {
                if (sender is Button button && IsLoaded) button.IsEnabled = true;
            }
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            if (Owner != null)
            {
                Close(); // Returns control to the existing LoginWindow.
                Owner.Activate();
                return;
            }
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            Close();
        }
    }
}