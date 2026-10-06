using System;
using System.Linq;
using System.Windows.Controls;
using Tessel.NET.Controls;

namespace Tessel.Gallery.Pages;

public partial class InputsPage : UserControl
{
    private static readonly string[] Countries =
    [
        "Argentina", "Australia", "Austria", "Belgium", "Brazil", "Canada", "Chile", "Denmark", "Finland", "France",
        "Germany", "Greece", "Iceland", "India", "Ireland", "Italy", "Japan", "Mexico", "Netherlands", "Norway",
        "Poland", "Portugal", "Romania", "Spain", "Sweden", "Switzerland", "United Kingdom", "United States",
    ];

    public InputsPage() => InitializeComponent();

    private void OnCountryTextChanged(object? sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (e.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var text = CountryBox.Text.Trim();
        CountryBox.ItemsSource = text.Length == 0
            ? null
            : Countries.Where(c => c.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnCountryQuery(object? sender, AutoSuggestBoxQuerySubmittedEventArgs e)
        => CountryStatus.Text = $"Submitted: {e.QueryText}";
}
