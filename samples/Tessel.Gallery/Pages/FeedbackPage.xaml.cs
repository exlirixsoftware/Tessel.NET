using System;
using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Controls;
using Tessel.NET.Helpers;
using Tessel.NET.Markup;

namespace Tessel.Gallery.Pages;

public partial class FeedbackPage : UserControl
{
    public FeedbackPage() => InitializeComponent();

    private void OnShowFlyout(object sender, RoutedEventArgs e) => InfoFlyout.IsOpen = true;

    private void OnFlyoutConfirm(object sender, RoutedEventArgs e)
    {
        InfoFlyout.Hide();
        Snackbar.Show("Item deleted", severity: InfoBarSeverity.Success, owner: Window.GetWindow(this));
    }

    private void OnShowTip(object sender, RoutedEventArgs e) => Tip.IsOpen = true;

    private void OnTipAction(object? sender, EventArgs e) => Snackbar.Show("Great!", "You can reopen the tip any time.", owner: Window.GetWindow(this));

    private void OnReopenInfoBar(object sender, RoutedEventArgs e) => ErrorBar.IsOpen = true;

    private void OnSnackbar(object sender, RoutedEventArgs e)
    {
        var severity = Enum.Parse<InfoBarSeverity>((string)((Button)sender).Tag);
        var (title, message) = severity switch
        {
            InfoBarSeverity.Success => ("Upload complete", "3 files were uploaded to Documents."),
            InfoBarSeverity.Warning => ("Low storage", "You have less than 1 GB left."),
            InfoBarSeverity.Error => ("Sync failed", "Retrying in 30 seconds."),
            _ => ("Heads up", "Snackbars disappear after a few seconds."),
        };
        Snackbar.Show(title, message, severity, owner: Window.GetWindow(this));
    }

    private async void OnShowDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Save changes?",
            Content = "You have unsaved changes to “Quarterly report.docx”. Do you want to save them before closing?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't save",
            CloseButtonText = "Cancel",
        };
        var result = await dialog.ShowAsync(Window.GetWindow(this));
        DialogResult.Text = $"Result: {result}";
    }

    private void OnLoadToggle(object sender, RoutedEventArgs e)
    {
        var loaded = LoadToggle.IsChecked == true;
        SkeletonHost.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
        LoadedHost.Visibility = loaded ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnShowBusyDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Export report",
            Content = "Generate the quarterly report as a PDF?",
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
        };
        var cancelled = false;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true; // keep the dialog open while the work runs
            dialog.IsBusy = true;
            dialog.ProgressText = "Preparing…";
            for (var percent = 0; percent <= 100 && !cancelled; percent += 5)
            {
                if (percent > 0) dialog.ReportProgress(percent, $"Exporting… {percent}%");
                await System.Threading.Tasks.Task.Delay(120);
            }
            if (!cancelled) dialog.Hide(ContentDialogResult.Primary);
        };
        var result = await dialog.ShowAsync(Window.GetWindow(this));
        cancelled = true;
        DialogResult.Text = $"Result: {result}";
        if (result == ContentDialogResult.Primary) Snackbar.Show("Report exported", severity: InfoBarSeverity.Success, owner: Window.GetWindow(this));
    }

    private async void OnShowDestructiveDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete project?",
            Content = "This permanently deletes the project and all of its files. This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            IsPrimaryDestructive = true,
        };
        var result = await dialog.ShowAsync(Window.GetWindow(this));
        DialogResult.Text = $"Result: {result}";
        if (result == ContentDialogResult.Primary)
        {
            Snackbar.Show("Project deleted", severity: InfoBarSeverity.Success, owner: Window.GetWindow(this));
        }
    }

    private async void OnShowInputDialog(object sender, RoutedEventArgs e)
    {
        var name = new TextBox();
        ControlHelper.SetHeader(name, "Folder name");
        ControlHelper.SetPlaceholderText(name, "New folder");
        ControlHelper.SetIcon(name, GlyphExtension.ToGlyph(Symbol.Folder));

        var dialog = new ContentDialog
        {
            Title = "Create folder",
            Content = name,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = false,
        };
        name.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = name.Text.Trim().Length > 0;

        var result = await dialog.ShowAsync(Window.GetWindow(this));
        DialogResult.Text = result == ContentDialogResult.Primary ? $"Result: created “{name.Text.Trim()}”" : "Result: cancelled";
    }
}
