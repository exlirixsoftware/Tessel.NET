using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Tessel.NET.Helpers;

/// <summary>
/// Attached properties shared by the Tessel control templates
/// (corner radius, placeholder text, header, icon, hover/pressed brushes, clear button).
/// </summary>
public static class ControlHelper
{
    #region CornerRadius

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(ControlHelper),
        new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender));

    public static CornerRadius GetCornerRadius(DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);

    #endregion

    #region PlaceholderText

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.RegisterAttached(
        "PlaceholderText", typeof(string), typeof(ControlHelper), new PropertyMetadata(null));

    public static string? GetPlaceholderText(DependencyObject obj) => (string?)obj.GetValue(PlaceholderTextProperty);
    public static void SetPlaceholderText(DependencyObject obj, string? value) => obj.SetValue(PlaceholderTextProperty, value);

    #endregion

    #region Header

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.RegisterAttached(
        "Header", typeof(object), typeof(ControlHelper), new PropertyMetadata(null));

    public static object? GetHeader(DependencyObject obj) => obj.GetValue(HeaderProperty);
    public static void SetHeader(DependencyObject obj, object? value) => obj.SetValue(HeaderProperty, value);

    #endregion

    #region Icon

    /// <summary>
    /// An icon shown by buttons and text inputs. A string is rendered with the icon font
    /// (use <c>{tessel:Glyph Home}</c>), any other object is rendered as-is.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(object), typeof(ControlHelper), new PropertyMetadata(null));

    public static object? GetIcon(DependencyObject obj) => obj.GetValue(IconProperty);
    public static void SetIcon(DependencyObject obj, object? value) => obj.SetValue(IconProperty, value);

    #endregion

    #region HoverBackground / PressedBackground

    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.RegisterAttached(
        "HoverBackground", typeof(Brush), typeof(ControlHelper), new PropertyMetadata(null));

    public static Brush? GetHoverBackground(DependencyObject obj) => (Brush?)obj.GetValue(HoverBackgroundProperty);
    public static void SetHoverBackground(DependencyObject obj, Brush? value) => obj.SetValue(HoverBackgroundProperty, value);

    public static readonly DependencyProperty PressedBackgroundProperty = DependencyProperty.RegisterAttached(
        "PressedBackground", typeof(Brush), typeof(ControlHelper), new PropertyMetadata(null));

    public static Brush? GetPressedBackground(DependencyObject obj) => (Brush?)obj.GetValue(PressedBackgroundProperty);
    public static void SetPressedBackground(DependencyObject obj, Brush? value) => obj.SetValue(PressedBackgroundProperty, value);

    #endregion

    #region ShowClearButton / IsClearButton

    /// <summary>Shows a clear ("x") button inside a TextBox or PasswordBox when it has text.</summary>
    public static readonly DependencyProperty ShowClearButtonProperty = DependencyProperty.RegisterAttached(
        "ShowClearButton", typeof(bool), typeof(ControlHelper), new PropertyMetadata(false));

    public static bool GetShowClearButton(DependencyObject obj) => (bool)obj.GetValue(ShowClearButtonProperty);
    public static void SetShowClearButton(DependencyObject obj, bool value) => obj.SetValue(ShowClearButtonProperty, value);

    /// <summary>Marks a button inside a template as the clear button of its templated parent.</summary>
    public static readonly DependencyProperty IsClearButtonProperty = DependencyProperty.RegisterAttached(
        "IsClearButton", typeof(bool), typeof(ControlHelper), new PropertyMetadata(false, OnIsClearButtonChanged));

    public static bool GetIsClearButton(DependencyObject obj) => (bool)obj.GetValue(IsClearButtonProperty);
    public static void SetIsClearButton(DependencyObject obj, bool value) => obj.SetValue(IsClearButtonProperty, value);

    private static void OnIsClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button) return;
        button.Click -= OnClearButtonClick;
        if ((bool)e.NewValue) button.Click += OnClearButtonClick;
    }

    private static void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.TemplatedParent)
        {
            case TextBox textBox:
                textBox.Clear();
                textBox.Focus();
                break;
            case PasswordBox passwordBox:
                passwordBox.Clear();
                passwordBox.Focus();
                break;
            case ComboBox comboBox:
                comboBox.Text = string.Empty;
                comboBox.SelectedItem = null;
                break;
        }
    }

    #endregion

    #region Password monitoring

    /// <summary>Tracks <see cref="PasswordBox"/> length so templates can show a placeholder.</summary>
    public static readonly DependencyProperty MonitorPasswordProperty = DependencyProperty.RegisterAttached(
        "MonitorPassword", typeof(bool), typeof(ControlHelper), new PropertyMetadata(false, OnMonitorPasswordChanged));

    public static bool GetMonitorPassword(DependencyObject obj) => (bool)obj.GetValue(MonitorPasswordProperty);
    public static void SetMonitorPassword(DependencyObject obj, bool value) => obj.SetValue(MonitorPasswordProperty, value);

    public static readonly DependencyProperty PasswordLengthProperty = DependencyProperty.RegisterAttached(
        "PasswordLength", typeof(int), typeof(ControlHelper), new PropertyMetadata(0));

    public static int GetPasswordLength(DependencyObject obj) => (int)obj.GetValue(PasswordLengthProperty);
    public static void SetPasswordLength(DependencyObject obj, int value) => obj.SetValue(PasswordLengthProperty, value);

    private static void OnMonitorPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;
        passwordBox.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue)
        {
            passwordBox.PasswordChanged += OnPasswordChanged;
            SetPasswordLength(passwordBox, passwordBox.Password.Length);
        }
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var passwordBox = (PasswordBox)sender;
        SetPasswordLength(passwordBox, passwordBox.Password.Length);
    }

    #endregion
}
