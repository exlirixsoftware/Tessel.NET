using System;
using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Controls;

namespace Tessel.Gallery.Pages;

public partial class DialogsPage : UserControl
{
    public DialogsPage() => InitializeComponent();

    private async void OnMessage(object sender, RoutedEventArgs e)
    {
        var kind = (string)((Button)sender).Tag;
        var dialog = kind switch
        {
            "Ok" => new MessageDialog { Title = "Update installed", Message = "Tessel was updated to version 1.1.0.", Icon = MessageDialogIcon.Success },
            "OkCancel" => new MessageDialog { Title = "Restart required", Message = "The application needs to restart to apply the new theme.", Buttons = MessageDialogButtons.OkCancel, Icon = MessageDialogIcon.Information },
            "YesNo" => new MessageDialog { Title = "Enable notifications?", Message = "Do you want to be notified when a build finishes?", Buttons = MessageDialogButtons.YesNo, Icon = MessageDialogIcon.Question },
            "YesNoCancel" => new MessageDialog { Title = "Unsaved changes", Message = "Save your changes before closing?", Buttons = MessageDialogButtons.YesNoCancel, Icon = MessageDialogIcon.Warning },
            "RetryCancel" => new MessageDialog { Title = "Connection failed", Message = "The server did not respond. Check your network and try again.", Buttons = MessageDialogButtons.RetryCancel, Icon = MessageDialogIcon.Error },
            _ => new MessageDialog { Title = "Delete project?", Message = "This permanently deletes the project and its files.", Buttons = MessageDialogButtons.YesNo, Icon = MessageDialogIcon.Warning, IsPrimaryDestructive = true },
        };
        var result = await dialog.ShowAsync(Window.GetWindow(this));
        MessageResult.Text = $"Result: {result}";
    }

    private async void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new FileChooserDialog
        {
            Title = "Open a file",
            Filter = "All files|*.*|Text files|*.txt;*.md|Source code|*.cs;*.xaml",
        };
        ChooserResult.Text = await dialog.ShowAsync(Window.GetWindow(this)) ? $"Selected: {dialog.FileName}" : "Selected: — (cancelled)";
    }

    private async void OnOpenImages(object sender, RoutedEventArgs e)
    {
        var dialog = new FileChooserDialog
        {
            Title = "Choose pictures",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        ChooserResult.Text = await dialog.ShowAsync(Window.GetWindow(this))
            ? $"Selected {dialog.FileNames.Count} file(s): {string.Join(", ", dialog.FileNames)}"
            : "Selected: — (cancelled)";
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new FileChooserDialog { Title = "Choose a folder", SelectFolders = true, PrimaryButtonText = "Select folder" };
        ChooserResult.Text = await dialog.ShowAsync(Window.GetWindow(this)) ? $"Selected folder: {dialog.FileName}" : "Selected: — (cancelled)";
    }

    private async void OnSaveFile(object sender, RoutedEventArgs e)
    {
        var dialog = new FileSaverDialog
        {
            Title = "Save document",
            FileName = "Untitled",
            DefaultExtension = ".txt",
            Filter = "Text document|*.txt|Markdown|*.md|All files|*.*",
        };
        SaverResult.Text = await dialog.ShowAsync(Window.GetWindow(this)) ? $"Save path: {dialog.FileName} (nothing is written to disk)" : "Save path: — (cancelled)";
    }

    private async void OnSaveImage(object sender, RoutedEventArgs e)
    {
        var dialog = new FileSaverDialog
        {
            Title = "Export image",
            FileName = "diagram",
            Filter = "PNG image|*.png|JPEG image|*.jpg",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            PrimaryButtonText = "Export",
        };
        SaverResult.Text = await dialog.ShowAsync(Window.GetWindow(this)) ? $"Save path: {dialog.FileName} (nothing is written to disk)" : "Save path: — (cancelled)";
    }
}
