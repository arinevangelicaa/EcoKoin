using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EcoKoin.WPF.Views
{
    public partial class ForgotPasswordWindow : Window
    {
        public ForgotPasswordWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                UpdatePlaceholder();
                EmailInput.Focus();
            };
        }

        private void EmailInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder();
            if (ValidationMessage != null)
            {
                ValidationMessage.Text = string.Empty;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePlaceholder()
        {
            if (EmailPlaceholder != null && EmailInput != null)
                EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailInput.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SendReset_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailInput.Text.Trim();

            // Cek format saja. Tanpa backend, aplikasi tidak boleh mengklaim email telah dikirim.
            if (!MailAddress.TryCreate(email, out var parsed) ||
                !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                ShowFeedback("Masukkan alamat email yang valid, misalnya nama@contoh.com.", true);
                EmailInput.Focus();
                return;
            }

            ShowFeedback(
                "Format email valid. Pengiriman tautan reset belum tersedia karena layanan email/backend belum terhubung.",
                false);
        }

        private void ShowFeedback(string message, bool isError)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(181, 71, 56))
                : new SolidColorBrush(Color.FromRgb(44, 76, 62));
            ValidationMessage.Visibility = Visibility.Visible;
        }

        // Dibuka menggunakan ShowDialog() dari LoginWindow, jadi cukup Close() untuk kembali.
        private void BackToLogin_Click(object sender, RoutedEventArgs e) => Close();
    }
}