using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Tessel.Gallery.Pages;

public partial class ButtonsPage : UserControl
{
    private int _count;

    public ButtonsPage() => InitializeComponent();

    private void OnChipRemoved(object? sender, System.EventArgs e)
    {
        if (sender is FrameworkElement { Parent: Panel panel } chip) panel.Children.Remove(chip);
    }

    private void OnSplitClick(object sender, RoutedEventArgs e) => SplitStatus.Text = $"Last action: {((ContentControl)sender).Content} (main button)";

    private void OnMenuAction(object sender, RoutedEventArgs e) => SplitStatus.Text = $"Last action: {((MenuItem)sender).Header} (menu)";

    private void OnRepeat(object sender, RoutedEventArgs e) => RepeatCount.Text = (++_count).ToString();

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
