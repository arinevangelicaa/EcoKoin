using System;
using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    public partial class Sidebar : UserControl
    {
        public event EventHandler? AuthActionRequested;

        public bool IsLoggedIn { get; private set; }

        public Sidebar()
        {
            InitializeComponent();
            SetLoggedIn(false);
        }

        public void SetLoggedIn(
            bool loggedIn,
            string? email = null)
        {
            IsLoggedIn = loggedIn;

            AuthLabelText.Text =
                loggedIn ? "Logout" : "Login";

            ProfileNameText.Text = loggedIn
                ? (email?.Split('@')[0] ?? "Pengguna")
                : "Belum login";

            ProfileEmailText.Text = loggedIn
                ? (email ?? "Pengguna EcoKoin")
                : "Masuk untuk menggunakan akun";

            AuthIconText.Text =
                (string)FindResource("IconLogout");

            ProfileAvatarText.Text =
                (string)FindResource("IconUser");
        }

        private void AuthActionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AuthActionRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}