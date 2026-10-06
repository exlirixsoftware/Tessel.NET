using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Tessel.NET.Controls;

/// <summary>The buttons of a <see cref="MessageDialog"/>.</summary>
public enum MessageDialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
    RetryCancel,
}

/// <summary>The button the user chose in a <see cref="MessageDialog"/>.</summary>
public enum MessageDialogResult
{
    /// <summary>The dialog was closed without choosing (it also reports the "safe" choice, see <see cref="MessageDialog"/>).</summary>
    None,
    Ok,
    Cancel,
    Yes,
    No,
    Retry,
}

/// <summary>The icon shown next to the message of a <see cref="MessageDialog"/>.</summary>
public enum MessageDialogIcon
{
    None,
    Information,
    Success,
    Warning,
    Error,
    Question,
}

/// <summary>
/// A ready-made message box built on <see cref="ContentDialog"/>: a title, a message, an optional icon and a standard set of buttons.
/// Closing the dialog with Escape returns the cancelling choice (<c>Ok</c> for <see cref="MessageDialogButtons.Ok"/>,
/// <c>No</c> for <see cref="MessageDialogButtons.YesNo"/>, otherwise <c>Cancel</c>).
/// <code>
/// if (await MessageDialog.ShowAsync("Delete file?", "This can't be undone.", MessageDialogButtons.YesNo, MessageDialogIcon.Warning, destructive: true) == MessageDialogResult.Yes) { ... }
/// </code>
/// </summary>
public class MessageDialog
{
    public string? Title { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Additional content shown below the message (for example a details text box).</summary>
    public object? Details { get; set; }

    public MessageDialogButtons Buttons { get; set; } = MessageDialogButtons.Ok;

    public MessageDialogIcon Icon { get; set; }

    /// <summary>Uses the danger (red) style for the first button.</summary>
    public bool IsPrimaryDestructive { get; set; }

    /// <summary>Overrides the text of the first button (OK / Yes / Retry).</summary>
    public string? PrimaryButtonText { get; set; }

    /// <summary>Overrides the text of the second button (No).</summary>
    public string? SecondaryButtonText { get; set; }

    /// <summary>Overrides the text of the cancel button (Cancel).</summary>
    public string? CloseButtonText { get; set; }

    /// <summary>Shows a message in the active window (or <paramref name="owner"/>).</summary>
    public static Task<MessageDialogResult> ShowAsync(
        string title,
        string message,
        MessageDialogButtons buttons = MessageDialogButtons.Ok,
        MessageDialogIcon icon = MessageDialogIcon.None,
        Window? owner = null,
        bool destructive = false)
        => new MessageDialog { Title = title, Message = message, Buttons = buttons, Icon = icon, IsPrimaryDestructive = destructive }.ShowAsync(owner);

    public async Task<MessageDialogResult> ShowAsync(Window? owner = null)
    {
        var (primary, secondary, close) = Buttons switch
        {
            MessageDialogButtons.OkCancel => ("OK", "", "Cancel"),
            MessageDialogButtons.YesNo => ("Yes", "No", ""),
            MessageDialogButtons.YesNoCancel => ("Yes", "No", "Cancel"),
            MessageDialogButtons.RetryCancel => ("Retry", "", "Cancel"),
            _ => ("OK", "", ""),
        };

        var dialog = new ContentDialog
        {
            Title = Title,
            Content = BuildContent(),
            PrimaryButtonText = PrimaryButtonText ?? primary,
            SecondaryButtonText = secondary.Length == 0 ? "" : SecondaryButtonText ?? secondary,
            CloseButtonText = close.Length == 0 ? "" : CloseButtonText ?? close,
            IsPrimaryDestructive = IsPrimaryDestructive,
        };

        var result = await dialog.ShowAsync(owner);
        return result switch
        {
            ContentDialogResult.Primary => Buttons switch
            {
                MessageDialogButtons.YesNo or MessageDialogButtons.YesNoCancel => MessageDialogResult.Yes,
                MessageDialogButtons.RetryCancel => MessageDialogResult.Retry,
                _ => MessageDialogResult.Ok,
            },
            ContentDialogResult.Secondary => MessageDialogResult.No,
            _ => Buttons switch
            {
                MessageDialogButtons.Ok => MessageDialogResult.Ok,
                MessageDialogButtons.YesNo => MessageDialogResult.No,
                _ => MessageDialogResult.Cancel,
            },
        };
    }

    private UIElement BuildContent()
    {
        var panel = new StackPanel();

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        var (glyph, brushKey) = Icon switch
        {
            MessageDialogIcon.Information => ("", "Tessel.InfoBrush"),
            MessageDialogIcon.Success => ("", "Tessel.SuccessBrush"),
            MessageDialogIcon.Warning => ("", "Tessel.WarningBrush"),
            MessageDialogIcon.Error => ("", "Tessel.DangerBrush"),
            MessageDialogIcon.Question => ("", "Tessel.AccentBrush"),
            _ => ("", ""),
        };
        if (glyph.Length > 0)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontSize = 28,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            icon.SetResourceReference(TextBlock.FontFamilyProperty, "Tessel.IconFontFamily");
            icon.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            row.Children.Add(icon);
        }

        var message = new TextBlock { Text = Message, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(message, 1);
        row.Children.Add(message);
        panel.Children.Add(row);

        if (Details is UIElement details)
        {
            if (details is FrameworkElement element) element.Margin = new Thickness(0, 16, 0, 0);
            panel.Children.Add(details);
        }
        else if (Details != null)
        {
            panel.Children.Add(new ContentPresenter { Content = Details, Margin = new Thickness(0, 16, 0, 0) });
        }

        return panel;
    }
}
