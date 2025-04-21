using Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Themes.Controls;

namespace MemEngine360.Avalonia;

public partial class AboutWindow : WindowEx {
    public AboutWindow() {
        this.InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e) {
        this.Close();
    }
}