using System;
using System.Net.Mail;
using System.Windows;
using System.Windows.Media;

namespace EcoKoin.WPF.Views
{
    public partial class ForgotPasswordWindow : Window
    {
        public ForgotPasswordWindow() { InitializeComponent(); }

        private void SendReset_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailInput.Text.Trim();
            ValidationMessage.Visibility = Visibility.Visible;
            if (!MailAddress.TryCreate(email, out var parsed) ||
                !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage.Foreground = Brushes.Firebrick;
                ValidationMessage.Text = "Masukkan alamat email yang valid.";
                return;
            }
            // Frontend only: no reset email has been sent.
            ValidationMessage.Foreground = new SolidColorBrush(Color.FromRgb(44, 76, 62));
            ValidationMessage.Text = "Email valid. Pengiriman tautan belum tersedia karena layanan reset password belum terhubung.";
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e) => Close();
    }
}