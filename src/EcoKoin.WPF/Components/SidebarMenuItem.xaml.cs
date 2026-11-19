using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    public partial class SidebarMenuItem : UserControl
    {
        public SidebarMenuItem()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateActiveStyle();
        }

        public static readonly DependencyProperty MenuTextProperty =
            DependencyProperty.Register(
                nameof(MenuText),
                typeof(string),
                typeof(SidebarMenuItem),
                new PropertyMetadata(string.Empty));

        public string MenuText
        {
            get => (string)GetValue(MenuTextProperty);
            set => SetValue(MenuTextProperty, value);
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(
                nameof(IconGlyph),
                typeof(string),
                typeof(SidebarMenuItem),
                new PropertyMetadata(string.Empty));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive),
                typeof(bool),
                typeof(SidebarMenuItem),
                new PropertyMetadata(false, OnIsActiveChanged));

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        private static void OnIsActiveChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is SidebarMenuItem item)
                item.UpdateActiveStyle();
        }

        private void UpdateActiveStyle()
        {
            if (MenuButton == null)
                return;

            string key = IsActive
                ? "SidebarMenuActiveStyle"
                : "SidebarMenuButtonStyle";

            MenuButton.Style = (Style)FindResource(key);
        }
    }
}