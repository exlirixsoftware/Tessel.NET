using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>One property of the object shown by a <see cref="PropertyGrid"/>.</summary>
public sealed class PropertyEntry
{
    internal PropertyEntry(PropertyInfo property)
    {
        Property = property;
        Name = property.Name;
        DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                      ?? property.GetCustomAttribute<DisplayAttribute>()?.GetName()
                      ?? Humanize(property.Name);
        Category = property.GetCustomAttribute<CategoryAttribute>()?.Category
                   ?? property.GetCustomAttribute<DisplayAttribute>()?.GetGroupName()
                   ?? "Misc";
        Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description
                      ?? property.GetCustomAttribute<DisplayAttribute>()?.GetDescription();
        var setter = property.GetSetMethod();
        IsReadOnly = setter == null || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true;
        var range = property.GetCustomAttribute<RangeAttribute>();
        if (range != null)
        {
            try
            {
                Minimum = Convert.ToDouble(range.Minimum, CultureInfo.InvariantCulture);
                Maximum = Convert.ToDouble(range.Maximum, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                // a range of something that is not a number
            }
        }
    }

    public PropertyInfo Property { get; }

    public string Name { get; }

    /// <summary>The label: [DisplayName], [Display(Name)] or the property name split into words ("FontSize" becomes "Font size").</summary>
    public string DisplayName { get; }

    /// <summary>The group: [Category], [Display(GroupName)] or "Misc".</summary>
    public string Category { get; }

    /// <summary>The tooltip: [Description] or [Display(Description)].</summary>
    public string? Description { get; }

    /// <summary>True without a public setter or with [ReadOnly(true)].</summary>
    public bool IsReadOnly { get; }

    /// <summary>The smallest value (from [Range]) or null.</summary>
    public double? Minimum { get; }

    public double? Maximum { get; }

    public Type PropertyType => Property.PropertyType;

    /// <summary>"FontSize" becomes "Font size", "URLPath" becomes "URL path".</summary>
    internal static string Humanize(string name)
    {
        var words = System.Text.RegularExpressions.Regex.Matches(name, @"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+").Select(m => m.Value).ToList();
        if (words.Count == 0) return name;
        for (var i = 0; i < words.Count; i++)
        {
            if (i == 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
            else if (!(words[i].Length > 1 && words[i].All(char.IsUpper))) words[i] = words[i].ToLowerInvariant();
        }
        return string.Join(' ', words);
    }
}

public sealed class PropertyValueChangedEventArgs(PropertyEntry entry, object? newValue) : EventArgs
{
    public PropertyEntry Entry { get; } = entry;

    public object? NewValue { get; } = newValue;
}

/// <summary>
/// An inspector for any object, like the Properties panel of an editor: it lists the public properties grouped by category and edits them
/// in place. Text, numbers (with [Range]), booleans, enums, colors, dates and anything with a type converter get a suitable editor; the
/// attributes [DisplayName], [Category], [Description], [ReadOnly] and [Browsable(false)] shape what is shown. Give it your own editors with
/// <see cref="EditorFactory"/>.
/// <code>Inspector.SelectedObject = selectedShape;</code>
/// </summary>
[TemplatePart(Name = "PART_Search", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Host", Type = typeof(Panel))]
public class PropertyGrid : Control
{
    private Panel? _host;
    private TextBox? _search;

    static PropertyGrid()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(typeof(PropertyGrid)));
        FocusableProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
        nameof(SelectedObject), typeof(object), typeof(PropertyGrid), new PropertyMetadata(null, (d, _) => ((PropertyGrid)d).OnSelectedObjectChanged()));

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText), typeof(string), typeof(PropertyGrid), new PropertyMetadata(string.Empty, (d, _) => ((PropertyGrid)d).Rebuild()));

    public static readonly DependencyProperty ShowSearchProperty = DependencyProperty.Register(
        nameof(ShowSearch), typeof(bool), typeof(PropertyGrid), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowCategoriesProperty = DependencyProperty.Register(
        nameof(ShowCategories), typeof(bool), typeof(PropertyGrid), new PropertyMetadata(true, (d, _) => ((PropertyGrid)d).Rebuild()));

    public static readonly DependencyProperty IsAlphabeticalProperty = DependencyProperty.Register(
        nameof(IsAlphabetical), typeof(bool), typeof(PropertyGrid), new PropertyMetadata(false, (d, _) => ((PropertyGrid)d).Rebuild()));

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
        nameof(LabelWidth), typeof(double), typeof(PropertyGrid), new PropertyMetadata(150d, (d, _) => ((PropertyGrid)d).Rebuild(), (_, v) => Math.Max(40, (double)v)));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PropertyGrid), new PropertyMetadata(null, (d, _) => ((PropertyGrid)d).UpdateHeader()));

    private static readonly DependencyPropertyKey HeaderTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HeaderText), typeof(string), typeof(PropertyGrid), new PropertyMetadata("Properties"));

    public static readonly DependencyProperty HeaderTextProperty = HeaderTextPropertyKey.DependencyProperty;

    /// <summary>The object whose properties are shown (null shows nothing).</summary>
    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    /// <summary>Only properties whose label or category contains this text are shown.</summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool ShowSearch
    {
        get => (bool)GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    /// <summary>Groups the properties by category (otherwise one flat list).</summary>
    public bool ShowCategories
    {
        get => (bool)GetValue(ShowCategoriesProperty);
        set => SetValue(ShowCategoriesProperty, value);
    }

    /// <summary>Sorts the properties by label (otherwise in the order they are declared).</summary>
    public bool IsAlphabetical
    {
        get => (bool)GetValue(IsAlphabeticalProperty);
        set => SetValue(IsAlphabeticalProperty, value);
    }

    /// <summary>The width of the label column.</summary>
    public double LabelWidth
    {
        get => (double)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    /// <summary>The heading (the type name of the object when null).</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The heading that is shown: <see cref="Title"/>, otherwise the type name of the object.</summary>
    public string HeaderText => (string)GetValue(HeaderTextProperty);

    private void UpdateHeader() => SetValue(HeaderTextPropertyKey, Title ?? SelectedObject?.GetType().Name ?? "Properties");

    /// <summary>
    /// Creates the editor of a property yourself: return an element bound to the property, or null to use the standard editor. The second
    /// argument is the object that is shown.
    /// </summary>
    public Func<PropertyEntry, object, FrameworkElement?>? EditorFactory { get; set; }

    /// <summary>Raised after the user changed a property (not when the object changes it).</summary>
    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    /// <summary>The properties that are shown for <paramref name="target"/>, in declaration order.</summary>
    public static IReadOnlyList<PropertyEntry> Describe(object target)
    {
        return target.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
            .Select(p => new PropertyEntry(p))
            .ToList();
    }

    /// <summary>The properties that match the search and are in the order the grid shows them.</summary>
    internal IReadOnlyList<PropertyEntry> VisibleEntries()
    {
        if (SelectedObject == null) return [];
        var entries = Describe(SelectedObject).AsEnumerable();
        var query = SearchText?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            entries = entries.Where(e => e.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                                         || e.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                                         || e.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }
        return IsAlphabetical ? entries.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList() : entries.ToList();
    }

    /// <summary>Reads the values again (needed for objects that do not raise PropertyChanged).</summary>
    public void Refresh() => Rebuild();

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Host") as Panel;
        _search = GetTemplateChild("PART_Search") as TextBox;
        if (_search != null)
        {
            _search.Text = SearchText;
            _search.TextChanged += (_, _) =>
            {
                if (SearchText != _search.Text) SearchText = _search.Text;
            };
        }
        Rebuild();
    }

    private void OnSelectedObjectChanged()
    {
        UpdateHeader();
        SetCurrentValue(SearchTextProperty, string.Empty);
        if (_search != null) _search.Text = string.Empty;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_host == null) return;
        _host.Children.Clear();

        if (SelectedObject == null)
        {
            _host.Children.Add(Message("Nothing is selected."));
            return;
        }

        var entries = VisibleEntries();
        if (entries.Count == 0)
        {
            _host.Children.Add(Message("No properties match."));
            return;
        }

        if (!ShowCategories)
        {
            var rows = new StackPanel();
            foreach (var entry in entries) rows.Children.Add(Row(entry));
            _host.Children.Add(rows);
            return;
        }

        foreach (var group in entries.GroupBy(e => e.Category))
        {
            var rows = new StackPanel();
            foreach (var entry in group) rows.Children.Add(Row(entry));
            var expander = new Expander { Header = group.Key, IsExpanded = true, Content = rows, Margin = new Thickness(0, 0, 0, 4) };
            _host.Children.Add(expander);
        }
    }

    private static FrameworkElement Message(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.TextTertiaryBrush");
        return block;
    }

    private FrameworkElement Row(PropertyEntry entry)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2), MinHeight = 32 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = entry.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 8, 0),
            ToolTip = entry.Description ?? entry.DisplayName,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.TextSecondaryBrush");
        row.Children.Add(label);

        var editor = CreateEditor(entry, SelectedObject!);
        editor.VerticalAlignment = VerticalAlignment.Center;
        editor.Margin = new Thickness(0, 0, 4, 0);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        return row;
    }

    #endregion

    #region Editors

    /// <summary>The editor for a property: the one <see cref="EditorFactory"/> gives, otherwise the one that fits the type.</summary>
    internal FrameworkElement CreateEditor(PropertyEntry entry, object target)
    {
        var custom = EditorFactory?.Invoke(entry, target);
        if (custom != null) return custom;

        var type = Nullable.GetUnderlyingType(entry.PropertyType) ?? entry.PropertyType;
        var readOnly = entry.IsReadOnly;

        FrameworkElement editor;
        if (type == typeof(bool))
        {
            var toggle = new ToggleSwitch { IsEnabled = !readOnly, HorizontalAlignment = HorizontalAlignment.Left };
            Bind(toggle, ToggleButton.IsCheckedProperty, entry, target, null, UpdateSourceTrigger.PropertyChanged);
            editor = toggle;
        }
        else if (IsNumber(type))
        {
            var integral = IsIntegral(type);
            var box = new NumberBox
            {
                IsEnabled = !readOnly,
                StringFormat = integral ? "0" : "0.###",
                SmallChange = integral ? 1 : 0.1,
                LargeChange = integral ? 10 : 1,
            };
            if (entry.Minimum is { } min) box.Minimum = min;
            if (entry.Maximum is { } max) box.Maximum = max;
            Bind(box, NumberBox.ValueProperty, entry, target, new NumberConverter(), UpdateSourceTrigger.PropertyChanged);
            editor = box;
        }
        else if (type.IsEnum)
        {
            var combo = new ComboBox { ItemsSource = Enum.GetValues(type), IsEnabled = !readOnly };
            Bind(combo, Selector.SelectedItemProperty, entry, target, null, UpdateSourceTrigger.PropertyChanged);
            editor = combo;
        }
        else if (type == typeof(DateTime))
        {
            var picker = new DatePicker { IsEnabled = !readOnly };
            Bind(picker, DatePicker.SelectedDateProperty, entry, target, null, UpdateSourceTrigger.PropertyChanged);
            editor = picker;
        }
        else if (type == typeof(Color) || type == typeof(SolidColorBrush) || type == typeof(Brush))
        {
            editor = ColorEditor(entry, target, type);
        }
        else if (type == typeof(string) || HasTextConverter(type))
        {
            var text = new TextBox { IsReadOnly = readOnly };
            Bind(text, TextBox.TextProperty, entry, target, type == typeof(string) ? null : new TextConverter(type), UpdateSourceTrigger.LostFocus);
            editor = text;
        }
        else
        {
            var block = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
            block.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.TextSecondaryBrush");
            var value = entry.Property.GetValue(target);
            block.Text = value switch
            {
                null => "(none)",
                string s => s,
                IEnumerable list => $"{list.Cast<object>().Count()} items",
                _ => $"({type.Name})",
            };
            editor = block;
        }
        return editor;
    }

    private FrameworkElement ColorEditor(PropertyEntry entry, object target, Type type)
    {
        var panel = new DockPanel();
        var swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        swatch.SetResourceReference(Border.BorderBrushProperty, "Tessel.BorderStrongBrush");
        DockPanel.SetDock(swatch, Dock.Left);

        var text = new TextBox { IsReadOnly = entry.IsReadOnly };
        var converter = new ColorConverter2(type);
        Bind(text, TextBox.TextProperty, entry, target, converter, UpdateSourceTrigger.LostFocus);
        var preview = new Binding(entry.Name) { Source = target, Mode = BindingMode.OneWay, Converter = new ColorToBrushConverter() };
        swatch.SetBinding(Border.BackgroundProperty, preview);

        panel.Children.Add(swatch);
        panel.Children.Add(text);
        text.SourceUpdated += (_, _) => RaiseChanged(entry, target);
        return panel;
    }

    private void Bind(FrameworkElement editor, DependencyProperty property, PropertyEntry entry, object target, IValueConverter? converter, UpdateSourceTrigger trigger)
    {
        var binding = new Binding(entry.Name)
        {
            Source = target,
            Mode = entry.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            UpdateSourceTrigger = trigger,
            NotifyOnSourceUpdated = true,
            ValidatesOnExceptions = true,
            Converter = converter,
        };
        editor.SetBinding(property, binding);
        editor.SourceUpdated += (_, _) => RaiseChanged(entry, target);
    }

    private void RaiseChanged(PropertyEntry entry, object target)
        => PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(entry, entry.Property.GetValue(target)));

    private static bool IsNumber(Type type) => IsIntegral(type) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    private static bool IsIntegral(Type type) => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
                                                 || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

    private static bool HasTextConverter(Type type)
    {
        var converter = TypeDescriptor.GetConverter(type);
        return converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string)) && !type.IsPrimitive;
    }

    private sealed class NumberConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value == null ? 0d : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var number = value is double d ? d : 0d;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return System.Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
            var rounded = Math.Round(number);
            return System.Convert.ChangeType(Math.Clamp(rounded, MinOf(type), MaxOf(type)), type, CultureInfo.InvariantCulture);
        }

        private static double MinOf(Type type) => (double)(type.GetField("MinValue")?.GetValue(null) is { } v ? System.Convert.ToDecimal(v, CultureInfo.InvariantCulture) : decimal.MinValue);

        private static double MaxOf(Type type) => (double)(type.GetField("MaxValue")?.GetValue(null) is { } v ? System.Convert.ToDecimal(v, CultureInfo.InvariantCulture) : decimal.MaxValue);
    }

    private sealed class TextConverter(Type type) : IValueConverter
    {
        private readonly TypeConverter _converter = TypeDescriptor.GetConverter(type);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value == null ? string.Empty : _converter.ConvertToString(null, culture, value);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string text ? _converter.ConvertFromString(null, culture, text) : Binding.DoNothing;
    }

    private sealed class ColorConverter2(Type type) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
        {
            Color color => ToHex(color),
            SolidColorBrush brush => ToHex(brush.Color),
            _ => value == null ? "(none)" : "(" + value.GetType().Name + ")",
        };

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string text) return Binding.DoNothing;
            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(text.Trim());
                return type == typeof(Color) ? color : new SolidColorBrush(color);
            }
            catch (FormatException)
            {
                return Binding.DoNothing;
            }
        }

        private static string ToHex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private sealed class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
        {
            Color color => new SolidColorBrush(color),
            Brush brush => brush,
            _ => null,
        };

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }

    #endregion
}
