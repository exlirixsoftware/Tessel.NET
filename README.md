# Tessel.NET

A modern, themeable UI framework for **WPF on .NET 10**.

- Light, dark and system themes, switchable at runtime
- Custom accent colors (or the Windows accent)
- Restyled standard WPF controls — merge one dictionary and they're all styled
- Custom controls: `TesselWindow`, `NavigationView`, `ContentDialog`, `Snackbar`, `InfoBar`, `Card`, `SettingsCard`, `ToggleSwitch`, `NumberBox`, `ProgressRing`, `Badge`, `Avatar`, `FontIcon`, `AutoSuggestBox`, `TimePicker`, `ColorPicker`, `RatingControl`, `SplitButton`, `DropDownButton`, `ToggleSplitButton`, `Chip`, `SegmentedControl`, `TabView`, `BreadcrumbBar`, `Pagination`, `Stepper`, `Flyout`, `TeachingTip`, `Skeleton`, `MessageDialog`, `FileBrowser`, `FileChooserDialog`, `FileSaverDialog`
- MVVM helpers: `ObservableObject`, `RelayCommand`, `RelayCommand<T>`, `AsyncRelayCommand`
- Converters: bool/null → Visibility, inverse bool, enum ↔ bool, color → brush

```
Tessel.NET.slnx
├── src/Tessel.NET            the library
└── samples/Tessel.Gallery   a demo app showing every control
```

Run the gallery:

```bash
dotnet run --project samples/Tessel.Gallery
```

## Getting started

Install the [NuGet package](https://www.nuget.org/packages/Tessel.NET) in a WPF project that targets `net10.0-windows`:

```bash
dotnet add package Tessel.NET
```

or add it to the project file:

```xml
<PackageReference Include="Tessel.NET" Version="1.1.0" />
```

(To work with the source instead, reference `src/Tessel.NET/Tessel.NET.csproj`.) Then merge the theme into `App.xaml`:

```xml
<Application xmlns:tessel="http://schemas.tessel.net/ui" ...>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <tessel:ThemeResources Theme="System" Accent="#4F46E5" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

If you want the custom title bar, derive your windows from `TesselWindow`:

```xml
<tessel:TesselWindow x:Class="MyApp.MainWindow"
                     xmlns:tessel="http://schemas.tessel.net/ui" ...>
```

```csharp
public partial class MainWindow : TesselWindow { ... }
```

## Theming

```csharp
ThemeManager.SetTheme(AppTheme.Dark);          // Light, Dark or System
ThemeManager.SetAccent(Color.FromRgb(0, 120, 212));
ThemeManager.SetAccent(null);                  // back to the built-in accent
ThemeManager.UseSystemAccent();                // Windows accent color
ThemeManager.ThemeChanged += (_, _) => { ... };
```

Every color is a resource. Use `DynamicResource` so it follows theme changes:

| Resource | Use |
| --- | --- |
| `Tessel.BackgroundBrush`, `Tessel.SurfaceBrush`, `Tessel.SurfaceAltBrush`, `Tessel.SubtleBrush` | window, cards, layers |
| `Tessel.TextBrush`, `Tessel.TextSecondaryBrush`, `Tessel.TextTertiaryBrush`, `Tessel.TextDisabledBrush` | text |
| `Tessel.BorderBrush`, `Tessel.BorderStrongBrush` | strokes |
| `Tessel.ControlBackgroundBrush`, `Tessel.ControlHoverBrush`, `Tessel.ControlPressedBrush`, `Tessel.InputBackgroundBrush` | control states |
| `Tessel.AccentBrush`, `Tessel.AccentHoverBrush`, `Tessel.AccentPressedBrush`, `Tessel.AccentSubtleBrush`, `Tessel.OnAccentBrush` | accent |
| `Tessel.{Success,Warning,Danger,Info}Brush` and `…SubtleBrush` | status colors |
| `Tessel.Shadow`, `Tessel.ShadowLarge` | drop shadow effects |
| `Tessel.FontFamily`, `Tessel.IconFontFamily`, `Tessel.MonoFontFamily`, `Tessel.ControlCornerRadius`, … | typography & shape |

Text styles: `Tessel.Text.Display`, `.Title`, `.Subtitle`, `.BodyStrong`, `.Body`, `.Secondary`, `.Caption`, `.Code`, `.Icon`.

## Styled standard controls

Button, RepeatButton, ToggleButton, Hyperlink, CheckBox, RadioButton, TextBox, PasswordBox, RichTextBox, ComboBox, Slider, ProgressBar, ScrollViewer/ScrollBar, ListBox, ListView (with `GridView`), TabControl, Expander, GroupBox, Menu, ContextMenu, MenuItem, Separator, ToolTip, DataGrid, TreeView, Calendar, DatePicker, ToolBar/ToolBarTray, Label, GridSplitter, StatusBar, Frame, Window.

Button variants: `Tessel.Button.Accent`, `.Outline`, `.Subtle`, `.Danger`, `.Icon`, `.Link`.

### Attached properties (`tessel:ControlHelper`)

| Property | Applies to | Effect |
| --- | --- | --- |
| `Header` | TextBox, PasswordBox, ComboBox, DatePicker, NumberBox | label above the input |
| `PlaceholderText` | TextBox, PasswordBox, ComboBox | hint shown while empty |
| `Icon` | buttons, TextBox, PasswordBox | leading icon (`{tessel:Glyph Search}`) |
| `ShowClearButton` | TextBox, PasswordBox | "x" button that clears the text |
| `CornerRadius` | most controls | overrides the corner radius |
| `HoverBackground`, `PressedBackground` | buttons | custom state colors |

```xml
<TextBox tessel:ControlHelper.Header="Email"
         tessel:ControlHelper.PlaceholderText="name@example.com"
         tessel:ControlHelper.Icon="{tessel:Glyph Mail}"
         tessel:ControlHelper.ShowClearButton="True" />
```

## Custom controls

```xml
<!-- Navigation: items with TargetPageType create (and cache) that page when selected -->
<tessel:NavigationView PaneTitle="My App">
    <tessel:NavigationView.MenuItems>
        <tessel:NavigationViewItem Content="Home" Icon="{tessel:Glyph Home}" TargetPageType="{x:Type pages:HomePage}" />
        <tessel:NavigationViewItemHeader Content="Section" />
    </tessel:NavigationView.MenuItems>
    <tessel:NavigationView.FooterMenuItems>
        <tessel:NavigationViewItem Content="Settings" Icon="{tessel:Glyph Settings}" TargetPageType="{x:Type pages:SettingsPage}" />
    </tessel:NavigationView.FooterMenuItems>
</tessel:NavigationView>

<tessel:Card Header="Profile" IsElevated="True">...</tessel:Card>
<tessel:SettingsCard Header="Notifications" Description="…" Icon="{tessel:Glyph Ringer}">
    <tessel:ToggleSwitch IsChecked="True" />
</tessel:SettingsCard>
<tessel:InfoBar Severity="Warning" Title="Heads up" Message="…" />
<tessel:NumberBox Minimum="0" Maximum="100" Value="{Binding Quantity}" />
<tessel:ProgressRing IsActive="{Binding IsBusy}" />
<tessel:Badge Content="New" Kind="Success" />
<tessel:Avatar DisplayName="Ada Lovelace" />
<tessel:FontIcon Symbol="Save" />
```

Plug a DI container into `NavigationView.PageFactory` to construct pages.

### Inputs

```xml
<!-- Suggestions: update ItemsSource from TextChanged, run the search in QuerySubmitted -->
<tessel:AutoSuggestBox PlaceholderText="Search…" TextChanged="OnTextChanged" QuerySubmitted="OnQuery" />

<tessel:TimePicker SelectedTime="{Binding Alarm}" Is24Hour="True" MinuteIncrement="5" />
<tessel:ColorPicker Color="{Binding Accent}" IsAlphaEnabled="True" />
<tessel:RatingControl Value="{Binding Stars}" MaxRating="5" Caption="Your rating" />
<tessel:SegmentedControl SelectedIndex="1">
    <tessel:SegmentedItem Content="Day" />
    <tessel:SegmentedItem Content="Week" />
</tessel:SegmentedControl>
```

```csharp
void OnTextChanged(object? sender, AutoSuggestBoxTextChangedEventArgs e)
{
    if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        box.ItemsSource = Search(box.Text);
}
```

### Buttons

`DropDownButton`, `SplitButton` (main action + chevron) and `ToggleSplitButton` open the `ContextMenu` assigned to `DropDownMenu`:

```xml
<tessel:SplitButton Content="Send" Click="OnSend" tessel:ControlHelper.Icon="{tessel:Glyph Send}">
    <tessel:SplitButton.DropDownMenu>
        <ContextMenu>
            <MenuItem Header="Send later" />
            <MenuItem Header="Save as draft" />
        </ContextMenu>
    </tessel:SplitButton.DropDownMenu>
</tessel:SplitButton>

<tessel:Chip Content="WPF" IsRemovable="True" Removed="OnTagRemoved" />
<tessel:Chip Content="Unread" IsSelectable="True" />   <!-- filter chip: toggles IsChecked -->
```

### Navigation

```xml
<tessel:BreadcrumbBar ItemsSource="{Binding Path}" ItemClicked="OnCrumbClicked" />

<!-- Closable, draggable document tabs. Closing removes the tab unless the handler sets e.Cancel = true. -->
<tessel:TabView AddTabButtonClick="OnAddTab" TabCloseRequested="OnTabCloseRequested">
    <tessel:TabViewItem Header="Home" Icon="{tessel:Glyph Home}" IsClosable="False" />
</tessel:TabView>

<tessel:Pagination CurrentPage="{Binding Page}" PageCount="24" />

<tessel:Stepper CurrentStep="{Binding Step}">           <!-- Orientation="Vertical" for a timeline -->
    <tessel:StepperItem Header="Cart" Description="Review your items" />
    <tessel:StepperItem Header="Payment" IsError="True" />
</tessel:Stepper>
```

### Message and file dialogs

All dialogs open inside the window (like `ContentDialog`) and are awaited:

```csharp
// Message box
if (await MessageDialog.ShowAsync("Delete project?", "This can't be undone.",
        MessageDialogButtons.YesNo, MessageDialogIcon.Warning, destructive: true) == MessageDialogResult.Yes) { ... }

// Open
var open = new FileChooserDialog { Title = "Open images", Filter = "Images|*.png;*.jpg|All files|*.*", Multiselect = true };
if (await open.ShowAsync()) foreach (var path in open.FileNames) { ... }

var folder = new FileChooserDialog { Title = "Choose a folder", SelectFolders = true };
if (await folder.ShowAsync()) Use(folder.FileName);

// Save (asks before overwriting; adds the default extension)
var save = new FileSaverDialog { FileName = "report", DefaultExtension = ".pdf", Filter = "PDF|*.pdf|All files|*.*" };
if (await save.ShowAsync()) File.WriteAllBytes(save.FileName!, bytes);
```

Both dialogs have a search box (searches the open folder and its subfolders) and a Details / Thumbnails switch; set `ViewMode = FileBrowserView.Thumbnails` to start with image previews.

`FileBrowser` is the control behind them and can be used on its own: `<tessel:FileBrowser CurrentDirectory="C:\Projects" Multiselect="True" />`.

### Popups and loading states

```xml
<Button x:Name="Help" Content="Help" Click="OnHelp" />
<tessel:Flyout x:Name="HelpFlyout" Target="{Binding ElementName=Help}">…any content…</tessel:Flyout>
<tessel:TeachingTip x:Name="Tip" Target="{Binding ElementName=Help}" Title="New!" Subtitle="…" ActionButtonContent="Got it" />

<tessel:Skeleton Width="180" Height="16" />            <!-- shimmering placeholder; IsCircle="True" for avatars -->
<tessel:ProgressRing IsIndeterminate="False" Value="{Binding Percent}" />
```

`HelpFlyout.IsOpen = true;` (or `ShowAt(element)`) opens them. `ContentDialog` can show progress while a long operation runs:

```csharp
dialog.PrimaryButtonClick += async (_, e) =>
{
    e.Cancel = true;                       // keep the dialog open
    dialog.IsBusy = true;                  // progress bar + disabled primary/secondary buttons
    for (var i = 0; i <= 100; i += 10) { dialog.ReportProgress(i, $"Exporting… {i}%"); await Task.Delay(100); }
    dialog.Hide(ContentDialogResult.Primary);
};
```

Dialogs and snackbars work in any `Window` (they are placed on the window's adorner layer):

```csharp
var result = await new ContentDialog
{
    Title = "Delete file?",
    Content = "This can't be undone.",
    PrimaryButtonText = "Delete",
    CloseButtonText = "Cancel",
    IsPrimaryDestructive = true,
}.ShowAsync();

Snackbar.Show("Saved", "Your changes were saved.", InfoBarSeverity.Success);
```

## Icons

`Symbol` lists common Segoe Fluent Icons glyphs (with Segoe MDL2 Assets as a fallback on Windows 10). Use `{tessel:Glyph Name}` wherever a glyph string is expected, or `<tessel:FontIcon Symbol="Name" />`. The gallery's Icons page shows all of them.

## Building and publishing the NuGet package

```bash
dotnet pack src/Tessel.NET -c Release -o artifacts
```

This creates `artifacts/Tessel.NET.<version>.nupkg` (the library, its XML documentation, the README, the icon and the MIT license) and a `.snupkg` symbols package. The version comes from `<Version>` in `Directory.Build.props`. To publish to nuget.org, create an API key in your nuget.org account and run:

```bash
dotnet nuget push artifacts/Tessel.NET.<version>.nupkg --api-key <your key> --source https://api.nuget.org/v3/index.json
```

The symbols package is pushed together with it. Published versions cannot be changed, so bump the version (and add an entry to `CHANGELOG.md`) for every release.
