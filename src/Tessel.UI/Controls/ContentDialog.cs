using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tessel.UI.Helpers;

namespace Tessel.UI.Controls;

public enum ContentDialogResult
{
    /// <summary>Closed with the close button, Escape, or <see cref="ContentDialog.Hide()"/>.</summary>
    None,
    Primary,
    Secondary,
}

public sealed class ContentDialogButtonClickEventArgs : EventArgs
{
    /// <summary>Set to true to keep the dialog open.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// A modal dialog displayed over the window content.
/// <code>
/// var result = await new ContentDialog { Title = "Delete?", Content = "...", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" }.ShowAsync();
/// </code>
/// </summary>
[TemplatePart(Name = "PART_PrimaryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_SecondaryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Dialog", Type = typeof(FrameworkElement))]
public class ContentDialog : ContentControl
{
    private ButtonBase? _primaryButton;
    private ButtonBase? _secondaryButton;
    private ButtonBase? _closeButton;
    private Adorner? _adorner;
    private TaskCompletionSource<ContentDialogResult>? _completion;
    private IInputElement? _previousFocus;

    static ContentDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentDialog), new FrameworkPropertyMetadata(typeof(ContentDialog)));
    }

    #region Dependency properties

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(object), typeof(ContentDialog), new PropertyMetadata(null));

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register(
        nameof(PrimaryButtonText), typeof(string), typeof(ContentDialog), new PropertyMetadata(string.Empty));

    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public static readonly DependencyProperty SecondaryButtonTextProperty = DependencyProperty.Register(
        nameof(SecondaryButtonText), typeof(string), typeof(ContentDialog), new PropertyMetadata(string.Empty));

    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public static readonly DependencyProperty CloseButtonTextProperty = DependencyProperty.Register(
        nameof(CloseButtonText), typeof(string), typeof(ContentDialog), new PropertyMetadata(string.Empty));

    public string CloseButtonText
    {
        get => (string)GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }

    public static readonly DependencyProperty IsPrimaryButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsPrimaryButtonEnabled), typeof(bool), typeof(ContentDialog), new PropertyMetadata(true));

    public bool IsPrimaryButtonEnabled
    {
        get => (bool)GetValue(IsPrimaryButtonEnabledProperty);
        set => SetValue(IsPrimaryButtonEnabledProperty, value);
    }

    /// <summary>Uses the danger (red) style for the primary button.</summary>
    public static readonly DependencyProperty IsPrimaryDestructiveProperty = DependencyProperty.Register(
        nameof(IsPrimaryDestructive), typeof(bool), typeof(ContentDialog), new PropertyMetadata(false));

    public bool IsPrimaryDestructive
    {
        get => (bool)GetValue(IsPrimaryDestructiveProperty);
        set => SetValue(IsPrimaryDestructiveProperty, value);
    }

    #endregion

    public event EventHandler<ContentDialogButtonClickEventArgs>? PrimaryButtonClick;
    public event EventHandler<ContentDialogButtonClickEventArgs>? SecondaryButtonClick;

    public bool IsOpen => _completion != null;

    /// <summary>Shows the dialog over <paramref name="owner"/> (default: the active window).</summary>
    public Task<ContentDialogResult> ShowAsync(Window? owner = null)
    {
        if (_completion != null) throw new InvalidOperationException("The dialog is already open.");

        var window = OverlayHost.ResolveOwner(owner) ?? throw new InvalidOperationException("No window to show the dialog in.");
        _previousFocus = Keyboard.FocusedElement;
        _adorner = OverlayHost.Show(window, this) ?? throw new InvalidOperationException("The window has no adorner layer.");
        _completion = new TaskCompletionSource<ContentDialogResult>();

        Dispatcher.BeginInvoke(() =>
        {
            AnimateIn();
            // Focus the first focusable element (an input in the content, or the first button).
            if (!MoveFocus(new TraversalRequest(FocusNavigationDirection.First)))
            {
                _closeButton?.Focus();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        return _completion.Task;
    }

    /// <summary>Closes the dialog.</summary>
    public void Hide() => Hide(ContentDialogResult.None);

    public void Hide(ContentDialogResult result)
    {
        if (_completion == null) return;

        if (_adorner != null) OverlayHost.Close(_adorner);
        _adorner = null;

        var completion = _completion;
        _completion = null;

        if (_previousFocus is { } focus) Keyboard.Focus(focus);
        _previousFocus = null;

        completion.TrySetResult(result);
    }

    public override void OnApplyTemplate()
    {
        if (_primaryButton != null) _primaryButton.Click -= OnPrimaryClick;
        if (_secondaryButton != null) _secondaryButton.Click -= OnSecondaryClick;
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;

        base.OnApplyTemplate();

        _primaryButton = GetTemplateChild("PART_PrimaryButton") as ButtonBase;
        _secondaryButton = GetTemplateChild("PART_SecondaryButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;

        if (_primaryButton != null) _primaryButton.Click += OnPrimaryClick;
        if (_secondaryButton != null) _secondaryButton.Click += OnSecondaryClick;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && !e.Handled)
        {
            Hide(ContentDialogResult.None);
            e.Handled = true;
        }
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        var args = new ContentDialogButtonClickEventArgs();
        PrimaryButtonClick?.Invoke(this, args);
        if (!args.Cancel) Hide(ContentDialogResult.Primary);
    }

    private void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        var args = new ContentDialogButtonClickEventArgs();
        SecondaryButtonClick?.Invoke(this, args);
        if (!args.Cancel) Hide(ContentDialogResult.Secondary);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide(ContentDialogResult.None);

    private void AnimateIn()
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));

        if (GetTemplateChild("PART_Dialog") is FrameworkElement dialog)
        {
            var scale = new ScaleTransform(0.96, 0.96);
            dialog.RenderTransformOrigin = new Point(0.5, 0.5);
            dialog.RenderTransform = scale;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        }
    }
}
