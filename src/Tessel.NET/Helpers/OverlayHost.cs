using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Tessel.NET.Controls;

namespace Tessel.NET.Helpers;

/// <summary>
/// Places UI (dialogs, snackbars) on top of a window's content using its adorner layer,
/// so overlays work in any <see cref="Window"/> without template changes.
/// </summary>
internal static class OverlayHost
{
    public static Window? ResolveOwner(Window? owner)
    {
        if (owner != null) return owner;
        var app = Application.Current;
        if (app == null) return null;
        foreach (Window window in app.Windows)
        {
            if (window.IsActive) return window;
        }
        return app.MainWindow;
    }

    public static UIElement? GetHostElement(Window window)
    {
        if (window is TesselWindow { OverlayRoot: { } root }) return root;
        return window.Content as UIElement;
    }

    public static Adorner? Show(Window window, FrameworkElement content)
    {
        var host = GetHostElement(window);
        if (host == null) return null;

        var layer = AdornerLayer.GetAdornerLayer(host);
        if (layer == null) return null;

        var adorner = new OverlayAdorner(host, content);
        layer.Add(adorner);
        return adorner;
    }

    public static void Close(Adorner adorner)
    {
        if (VisualTreeHelper.GetParent(adorner) is AdornerLayer layer)
        {
            layer.Remove(adorner);
        }
    }

    private sealed class OverlayAdorner : Adorner
    {
        private readonly FrameworkElement _child;

        public OverlayAdorner(UIElement adornedElement, FrameworkElement child) : base(adornedElement)
        {
            _child = child;
            AddVisualChild(child);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _child;

        protected override Size MeasureOverride(Size constraint)
        {
            var size = AdornedElement.RenderSize;
            _child.Measure(size);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _child.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
