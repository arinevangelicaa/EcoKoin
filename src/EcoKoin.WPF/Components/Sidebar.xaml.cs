using System;
using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    public partial class Sidebar : UserControl
    {
        public event EventHandler? AuthActionRequested;
        public bool IsLoggedIn { get; private set; } = true;

        public Sidebar()
        {
            InitializeComponent();
            SetLoggedIn(true);
        }

        public void SetLoggedIn(bool loggedIn, string? email = null)
        {
            IsLoggedIn = loggedIn;
            AuthLabelText.Text = loggedIn ? "Logout" : "Login";
            AuthIconText.Text = loggedIn ? "\uE8AC" : "\uE8AC";
            ProfileNameText.Text = loggedIn ? "Hermione" : "Belum login";
            ProfileEmailText.Text = loggedIn ? (email ?? "hermione@mail.hogwarts.ac.id") : "Masuk untuk menggunakan akun";
            ProfileAvatarText.Text = loggedIn ? "\uE77B" : "\uE77B";
        }

        private void AuthActionButton_Click(object sender, RoutedEventArgs e)
        {
            AuthActionRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}