using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Tessel.Gallery.Pages;

public partial class SelectionPage : UserControl
{
    private bool _updating;

    public SelectionPage() => InitializeComponent();

    private void OnSelectAllChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || SelectAll.IsChecked == null) return;
        _updating = true;
        foreach (var box in Toppings.Children.OfType<CheckBox>()) box.IsChecked = SelectAll.IsChecked;
        _updating = false;
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        var boxes = Toppings.Children.OfType<CheckBox>().ToList();
        var count = boxes.Count(b => b.IsChecked == true);

        _updating = true;
        SelectAll.IsChecked = count == 0 ? false : count == boxes.Count ? true : null;
        _updating = false;
    }
}
