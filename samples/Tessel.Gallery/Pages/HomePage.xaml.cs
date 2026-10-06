using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Controls;

namespace Tessel.Gallery.Pages;

public partial class HomePage : UserControl
{
    public HomePage() => InitializeComponent();

    private NavigationView? Navigation => (Window.GetWindow(this) as MainWindow)?.Navigation;

    private void OnGetStarted(object sender, RoutedEventArgs e) => Navigation?.Navigate(typeof(ButtonsPage));

    private void OnOpenSettings(object sender, RoutedEventArgs e) => Navigation?.Navigate(typeof(SettingsPage));

    private void OnOpenPage(object sender, RoutedEventArgs e)
    {
        var page = ((Button)sender).Tag switch
        {
            "Dialogs" => typeof(DialogsPage),
            "Navigation" => typeof(NavigationPage),
            "Inputs" => typeof(InputsPage),
            "Buttons" => typeof(ButtonsPage),
            "Feedback" => typeof(FeedbackPage),
            _ => null,
        };
        if (page != null) Navigation?.Navigate(page);
    }
}
