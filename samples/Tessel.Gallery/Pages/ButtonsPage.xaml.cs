using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Tessel.Gallery.Pages;

public partial class ButtonsPage : UserControl
{
    private int _count;

    public ButtonsPage() => InitializeComponent();

    private void OnRepeat(object sender, RoutedEventArgs e) => RepeatCount.Text = (++_count).ToString();

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
