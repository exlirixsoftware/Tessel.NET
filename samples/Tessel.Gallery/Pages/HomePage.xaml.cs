using System.Windows;
using System.Windows.Controls;
using Tessel.UI.Controls;

namespace Tessel.Gallery.Pages;

public partial class HomePage : UserControl
{
    public HomePage() => InitializeComponent();

    private NavigationView? Navigation => (Window.GetWindow(this) as MainWindow)?.Navigation;

    private void OnGetStarted(object sender, RoutedEventArgs e) => Navigation?.Navigate(typeof(ButtonsPage));

    private void OnOpenSettings(object sender, RoutedEventArgs e) => Navigation?.Navigate(typeof(SettingsPage));
}
