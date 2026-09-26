using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Tessel.UI.Converters;

/// <summary>true → Visible, false → Collapsed. Set <see cref="Invert"/> to flip.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is Visibility.Visible) ^ Invert;
}

/// <summary>Negates a boolean.</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>null (or empty string / empty collection) → Collapsed, otherwise Visible. Set <see cref="Invert"/> to flip.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value switch
        {
            null => true,
            string s => s.Length == 0,
            ICollection c => c.Count == 0,
            _ => false,
        };
        return isEmpty ^ Invert ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Compares an enum value with the converter parameter. Useful for binding RadioButtons to an enum:
/// <c>IsChecked="{Binding Mode, Converter={StaticResource EnumToBool}, ConverterParameter=Dark}"</c>.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
        {
            var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Enum.Parse(enumType, parameter.ToString()!, ignoreCase: true);
        }
        return Binding.DoNothing;
    }
}

/// <summary>Converts a <see cref="Color"/> to a <see cref="SolidColorBrush"/>.</summary>
[ValueConversion(typeof(Color), typeof(SolidColorBrush))]
public sealed class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color color ? new SolidColorBrush(color) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is SolidColorBrush brush ? brush.Color : null;
}

/// <summary>Produces a left margin proportional to a <see cref="TreeViewItem"/>'s depth (full-row selection).</summary>
public sealed class TreeViewItemIndentConverter : IValueConverter
{
    public double Indent { get; set; } = 16;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var depth = 0;
        DependencyObject? current = value as TreeViewItem;
        while (current != null && (current = ItemsControl.ItemsControlFromItemContainer(current)) is TreeViewItem)
        {
            depth++;
        }
        return new Thickness(depth * Indent, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
