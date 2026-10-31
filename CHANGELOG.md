# Changelog

All notable changes to **Tessel.NET** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

## [1.2.0] - 2026-10-28

Adds the `Timeline` control and the `AnimationTimeline` editing control. This release is backward compatible: applications that merge `ThemeResources` need no changes.

### Added

#### New custom controls

| Control | Description |
| --- | --- |
| `Timeline` / `TimelineItem` | Chronological list of events (an activity feed, order tracking, a project history). Each entry has a marker on a connecting line, a `Header`, a description (the content), an optional `Timestamp` (any text, a date with `TimestampStringFormat`, or an element) and an optional `Icon` shown inside the marker (a dot when there is none). `Kind` colors the marker (`BadgeKind`: `Accent` by default, `Neutral`, `Success`, `Warning`, `Danger`, `Info`); `MarkerBrush` overrides it. `IsPending` shows an entry that has not happened yet as a hollow, dashed, faded marker. `Orientation` is `Vertical` (default) or `Horizontal`; `Layout` is `Standard` (all entries on one side, timestamps on the other) or `Alternate` (entries alternate between the two sides of the line). Horizontal timelines give each entry `ItemWidth` (default 200, `NaN` sizes to content), scroll sideways and respond to the mouse wheel. Timestamp columns are shared so the markers of a vertical timeline always line up. Works with `Items` and with `ItemsSource` (the data item is the content; bind `Header`, `Timestamp`, `Kind`, `Icon` with an `ItemContainerStyle`). Read-only `Side`, `Index`, `IsFirst` and `IsLast` are provided for custom templates. |
| `AnimationTimeline` | An editing timeline for video, audio and animation tools (a video editor, a pixel/sprite editor, a motion-graphics tool). Positions are whole frames, so one control covers both worlds: clips of many frames on a timecode ruler, or layers with one-frame cels and keyframe diamonds on a frame-number ruler. **Model:** `Tracks` of `AnimationTrack` (`Name`, `Brush`, `Height`, `IsVisible`, `IsLocked`, `Clips`) holding `AnimationClip`s (`Name`, `Start`, `Length`, `Brush`, `Thumbnail`, `IsKeyframe`, `IsSelected`, `Tag`, `Waveform`, `MediaStart`, `MediaLength`), plus `Markers` (`AnimationMarker`). All model classes raise `PropertyChanged`/`CollectionChanged` and the timeline redraws. **Time:** `FramesPerSecond` (default 24), `Duration` (frames), `CurrentFrame` (the playhead, two-way and clamped), `TimeDisplay` (`Timecode`, `Frames` or `Seconds`) and `FormatTime`. **Playback:** `Play`, `Pause`, `Stop`, `TogglePlayback`, `StepForward`/`StepBackward`, `PlaybackRate`, a loop region (`LoopStart`, `LoopEnd`, `IsLoopEnabled`; Shift-drag the ruler) and `FollowPlayhead`. **Editing with the mouse:** click or drag the ruler to scrub, click a marker to jump to it, click/Ctrl-click clips to select, drag a box on empty space to select every clip it touches (Ctrl/Shift keeps the current selection; clips of locked tracks are skipped), drag clips (also onto other tracks, several at once) and trim their edges, double-click a clip, Delete removes the selection. Dragged clips snap to the playhead, markers, the loop region and other clips' edges (`IsSnappingEnabled`); Escape cancels a drag; locked tracks cannot be edited and `IsEditable="False"` makes the timeline read-only. **Editing rules:** `PreventOverlap` stops clips at their neighbors (a dragged clip cannot jump over another clip, a trimmed edge stops at the next clip, a clip cannot be dropped on an occupied part of another track), `ConstrainToDuration` keeps clips inside `0..Duration`, and `AutoExtendDuration` grows `Duration` instead when a clip is moved, trimmed or added past the end. `AddClip(track, clip)` adds a clip under the same rules and returns `false` when there is no room. **Undo and redo:** every move, trim, delete and `AddClip` is recorded (`IsUndoEnabled`, `HistoryLimit` = 200); `Undo`, `Redo`, `ClearHistory`, `CanUndo`/`CanRedo`, `UndoDescription`/`RedoDescription` and `HistoryChanged` drive your own Edit menu, and Undo also restores an automatically extended `Duration`. `RecordHistory(description, undo, redo)` puts your own steps (a colour change, a split) on the same stack; replacing `Tracks` starts a new history. The transport bar has Undo and Redo buttons. **Waveforms:** `AnimationClip.Waveform` draws peak bars inside audio clips; `AnimationWaveform.FromSamples` and `FromPcm16` reduce raw audio to peaks. With `MediaLength` (the length of the source media in frames) and `MediaStart` (where in the media the clip begins) trimming scrolls the waveform instead of stretching it, a left trim advances `MediaStart`, and a clip cannot be trimmed beyond the start or the end of its media. **View:** `PixelsPerFrame` zoom (Ctrl + mouse wheel, `ZoomIn`/`ZoomOut`/`ZoomToFit`), horizontal and vertical scrolling that keeps the track names aligned, an adaptive ruler (seconds or frames depending on the zoom), `ShowTransport` (play, step, loop, snap and zoom buttons with a time display), `HeaderWidth`, `HeaderCorner` and a replaceable `TrackHeaderTemplate` (color bar, name, eye and lock toggles by default). **Events:** `CurrentFrameChanged`, `PlaybackStateChanged`, `SelectionChanged`, `ClipsMoved`, `ClipsResized` (both report old position/length/track), `ClipsDeleting` (cancelable), `ClipActivated` and `HistoryChanged`. Keyboard: Space, Left/Right (Ctrl = 10 frames), Home/End, Delete, Ctrl+A, Ctrl +/-, Ctrl+Z, Ctrl+Y / Ctrl+Shift+Z. |

#### Gallery

- The Navigation page has a **Timeline** card with a switch between the vertical, alternate, horizontal and horizontal alternate layouts, and a second one that builds a timeline from data with an "Add event" button.
- New **Animation** page with a video-editor example (five tracks, markers, a preview with the active clips, add/delete clip and marker buttons, waveforms on the audio tracks (one of them a trimmable part of a longer recording), switches for overlap prevention, keeping clips inside the project and growing the project automatically, a time-format switch and a live "last action" line) and a pixel-editor example (layers, one-frame cels, keyframe diamonds and a bouncing ball driven by the playhead).
- The Home page has a "What's new in 1.2" card.

### Changed

- The assembly and package version is now `1.2.0` (`Directory.Build.props`).
- The `Stepper` is still the control for progress through steps; for a plain list of past events use the new `Timeline` (the vertical `Stepper` remains available).

### Fixed

- No bug fixes to features of 1.1.0 in this release.

## [1.1.0] - 2026-10-04

Adds 15 new controls, styles for the remaining everyday WPF controls, and two upgrades to existing controls. This release is backward compatible: applications that merge `ThemeResources` need no changes.

### Highlights

- **Styles for `ListView`/`GridView`, `ToolBar`/`ToolBarTray`, `Frame` and plain `Window`.** Every common WPF control now follows the Tessel look.
- **Inputs:** `AutoSuggestBox`, `TimePicker`, `ColorPicker`, `RatingControl`.
- **Buttons:** `SplitButton`, `DropDownButton`, `ToggleSplitButton`, `Chip`.
- **Navigation:** `TabView`, `BreadcrumbBar`, `Pagination`, `Stepper`, `SegmentedControl`.
- **Popups and loading states:** `Flyout`, `TeachingTip`, `Skeleton`, a determinate `ProgressRing` and a progress state for `ContentDialog`.
- **Dialogs:** `MessageDialog`, `FileChooserDialog` and `FileSaverDialog`, built on a new reusable `FileBrowser` control.

### Added

#### Styles for more standard WPF controls

- `ListView`, `ListViewItem` and `GridViewColumnHeader`: list and grid-view (`ListView.View`) styling with the same hover/selection look as `ListBox`, resizable column headers and a header row that scrolls with the content.
- `ToolBar` and `ToolBarTray`: rounded bar with a drag grip and an overflow ("···") popup, plus styles for the buttons, toggle buttons and separators placed inside it (`ToolBar.ButtonStyleKey`, `ToggleButtonStyleKey`, `SeparatorStyleKey`).
- `Frame`: no built-in navigation chrome, just the hosted page.
- `Window`: plain WPF windows now pick up the background, foreground and font. `TesselWindow` keeps its own style.

#### New custom controls

| Control | Description |
| --- | --- |
| `AutoSuggestBox` | Text box with a drop-down of suggestions. Handle `TextChanged` (with `AutoSuggestionBoxTextChangeReason`: `UserInput`, `ProgrammaticChange`, `SuggestionChosen`) to update `ItemsSource`, and `QuerySubmitted` / `SuggestionChosen` to react. Up/Down move through the list, Enter submits, Escape closes. Supports `DisplayMemberPath`, `ItemTemplate`, `PlaceholderText`, `QueryIcon` and `ControlHelper.Header`. |
| `DropDownButton` | Button with a chevron that opens the `ContextMenu` set in `DropDownMenu` (`IsDropDownOpen` reflects its state). |
| `SplitButton` | Main-action button (normal `Click`/`Command`) with a separate chevron that opens `DropDownMenu`. |
| `ToggleSplitButton` | Like `SplitButton`, but the main part toggles `IsChecked` (accent colors when checked). |
| `Chip` | Compact tag or filter. `IsRemovable` shows an "x" and raises `Removed`; `IsSelectable` makes it toggle `IsChecked`; `Icon` adds a leading glyph. |
| `SegmentedControl` / `SegmentedItem` | Row of mutually exclusive options (a `ListBox`), with optional `Icon` per item. |
| `TabView` / `TabViewItem` | Document-style tabs: close button (also middle-click), drag to reorder (`CanReorderTabs`), "add tab" button (`AddTabButtonClick`, `IsAddTabButtonVisible`), `TabStripFooter`, `IsClosable`, `Icon`, horizontal scrolling of the strip. `TabCloseRequested` removes the tab unless `e.Cancel` is set. Works with `Items` and with a bound `ItemsSource` (an `IList`, ideally an `ObservableCollection`). |
| `BreadcrumbBar` / `BreadcrumbBarItem` | Path of clickable crumbs; the last one is the current location. `ItemClicked` gives the index and item. |
| `Pagination` | Previous/next buttons and a compact page strip with ellipses. `CurrentPage` (1-based, two-way), `PageCount`, `MaxVisiblePages`, `CurrentPageChanged`. |
| `Stepper` / `StepperItem` | Progress through steps, driven by `CurrentStep`; each step shows `Header`, `Description` and optional content. `StepState` (`Pending`, `Current`, `Completed`, `Error`) is computed; `IsError` marks a failed step. `Orientation="Vertical"` gives a timeline. |
| `TimePicker` | Hour / minute (/ AM-PM) columns in a drop-down. `SelectedTime` (`TimeSpan?`), `Is24Hour` (defaults to the culture), `MinuteIncrement`, `PlaceholderText`, `ControlHelper.Header`. |
| `ColorPicker` | Saturation/brightness field, hue strip, optional alpha strip and a hex box. `Color` (two-way), `IsAlphaEnabled`, `IsHexInputVisible`, `ColorChanged`. |
| `RatingControl` | Star rating with hover preview. `Value`, `MaxRating`, `IsReadOnly`, `IsClearEnabled`, `Caption`; arrow keys, Home/End and Delete work too. |
| `Flyout` | Light-dismiss popup with any content, anchored to `Target` (`Placement`: `Bottom`, `Top`, `Left`, `Right`; `StaysOpen`). Open with `IsOpen = true` or `ShowAt(element)`; `Opened`/`Closed` events. |
| `TeachingTip` | `Flyout` with an icon, title, subtitle, content, an action and a close button. `ActionButtonClick`, `CloseButtonClick`, `IsClosable`. |
| `Skeleton` | Shimmering placeholder for content that is loading (`IsActive`, `CornerRadius`, `IsCircle`). |
| `MessageDialog` | Ready-made message box on top of `ContentDialog`: title, message, optional icon (`MessageDialogIcon`: `Information`, `Success`, `Warning`, `Error`, `Question`) and standard buttons (`MessageDialogButtons`: `Ok`, `OkCancel`, `YesNo`, `YesNoCancel`, `RetryCancel`). `await MessageDialog.ShowAsync(...)` returns a `MessageDialogResult`; Escape returns the cancelling choice. `IsPrimaryDestructive`, custom button texts and extra `Details` content are supported. |
| `FileBrowser` | File system browser: places (Desktop, Documents, ..., This PC), back/forward/up, a clickable breadcrumb that turns into an editable path box, a sortable list (name, date modified, type, size) and a "New folder" button. **Search box** (`SearchText`): finds entries whose name contains the text in the open folder and all its subfolders (case-insensitive, up to 2000 results, results show their folder; Ctrl+F focuses it, Escape clears it, navigating clears it). **Thumbnail view** (`ViewMode`: `Details` or `Thumbnails`, switchable with the buttons next to the search box): tiles with an image preview for PNG, JPEG, GIF, BMP, TIFF and ICO files and a large icon for everything else; previews are decoded in the background, cached and limited to a few at a time (`FileEntry.Thumbnail`). Filtering with `FileFilter`, `Multiselect`, `FoldersOnly`, `ShowHidden`; loads folders asynchronously; `Navigate`, `GoUp`, `GoBack`, `GoForward`, `Refresh`, `SelectedEntries`, `DirectoryChanged`, `EntryActivated`. Keyboard: Enter, Backspace, Alt+Left/Right/Up, F5. |
| `FileChooserDialog` | Themed "Open" dialog shown inside the window. Pick one file, several files (`Multiselect`) or a folder (`SelectFolders`). Filters in the classic `"Images\|*.png;*.jpg\|All files\|*.*"` format or as `FileFilter` objects; `InitialDirectory`, `FilterIndex`, `ShowHiddenFiles`, `AllowNewFolder`, `CheckFileExists`. `await dialog.ShowAsync()` returns true when confirmed; read `FileName` / `FileNames`. Typed names (also several quoted names) are resolved against the open folder, typing a folder opens it. Includes the browser's search box and thumbnail view; `ViewMode` sets the initial view and holds the last one the user chose. |
| `FileSaverDialog` | Themed "Save as" dialog with a name box, file-type list and "New folder". `FileName` (suggested name in, full path out), `DefaultExtension` (falls back to the selected file type), `OverwritePrompt` (asks before replacing a file, on by default). Validates the folder and invalid characters. |

#### Existing controls

- `ContentDialog`: `DialogMaxWidth` (default 548) and `DialogWidth` make wide dialogs possible.
- `ProgressRing`: determinate mode. Set `IsIndeterminate="False"` and `Value` (with `Minimum`/`Maximum`, default 0-100) to draw a progress arc over a track.
- `ContentDialog`: progress state for long operations. `IsBusy` shows a progress bar and message and disables the primary and secondary buttons (close stays enabled); `ProgressValue`, `IsProgressIndeterminate`, `ProgressText` and `ReportProgress(percent, text)` drive it.

#### NuGet package

- `Tessel.NET` can be added from NuGet: `dotnet add package Tessel.NET`. `dotnet pack src/Tessel.NET -c Release` builds the package for `net10.0-windows` (WPF framework reference) with the XML documentation, README, icon, MIT license metadata, repository information and a `.snupkg` symbols package.

#### Gallery

- New **Navigation** page (breadcrumbs, tab view, pagination, segmented control, stepper).
- The Gallery shows the library version (read from the assembly) in the title bar and in Settings, and the Home page has a "What's new in 1.1" card with shortcuts to the new pages.
- New **Dialogs** page (message dialogs, file chooser, file saver and a standalone file browser).
- New demos on the Buttons (split/drop-down buttons, chips), Text & Input (auto-suggest, time picker, rating, color picker), Collections (`ListView`/`GridView`), Menus (`ToolBar`, `Frame`) and Feedback pages (flyout, teaching tip, skeleton, determinate progress ring, progress dialog).

### Changed

- The assembly and package version is now `1.1.0` (`Directory.Build.props`).
- `FileBrowser` has a minimum width of 600 so the path box, search box and view switch always fit.
- `Generic.xaml` and `Controls.xaml` merge the new template dictionaries (`Custom/Buttons`, `Popups`, `Pickers`, `Tabs`, `Wayfinding`, `Chips`, `Progress`, `ColorRating` and `Controls/ListView`, `ToolBar`). Nothing needs to change in applications that merge `ThemeResources`.
- `ProgressRing`'s template now reacts to `IsIndeterminate`; indeterminate rings look and spin exactly as before.
- `ContentDialog`'s template has an extra (initially collapsed) progress area; dialogs that do not use `IsBusy` look the same as before.

### Fixed

- `ToolBar`: a `ComboBox`, `TextBox`, `CheckBox` or `RadioButton` placed in a toolbar used the built-in WPF toolbar style (a white field with dark text in the dark theme) instead of the Tessel style. `ToolBar` assigns its own style keys to the items it hosts; these keys now point to the Tessel styles.

### Known limitations

- `BreadcrumbBar` does not collapse long paths into an overflow menu.
- `TeachingTip` has no pointer tail towards its target.
- `AutoSuggestBox.DisplayMemberPath` accepts simple property names only (no nested paths).
- `TabView` close and reorder need an `IList` when `ItemsSource` is used (an `ObservableCollection` is recommended).
- The file dialogs are Windows-oriented (drive letters, the Desktop/Documents/Downloads/Pictures/Music/Videos places) and have no details/preview pane.
- File search matches names only (no content search) and stops after 2000 results; thumbnails are shown for the picture formats WPF decodes natively (no WebP, SVG, PDF or video frames).
- `Stepper` covers both the stepper and the timeline use case (`Orientation="Vertical"`); there is no separate `Timeline` control.

## [1.0.0] - 2026-09-26

First public release of Tessel.NET, a modern, themeable UI framework for **WPF on .NET 10**.

### Highlights

- Light, Dark and System themes, switchable at runtime.
- Custom accent colors, or the Windows accent color.
- One merged dictionary (`ThemeResources`) restyles all standard WPF controls.
- 14 new custom controls, including a custom title-bar window, `NavigationView`, `ContentDialog` and `Snackbar`.
- MVVM helpers, value converters and a Segoe Fluent Icons glyph catalogue.
- `Tessel.Gallery`, a sample app that shows every control.

### Added

#### Theming

- `ThemeResources`: the `ResourceDictionary` to merge into `App.xaml`. It carries the color palette, typography and the styles of all standard controls, and exposes `Theme` and `Accent` properties.
- `AppTheme` enum: `System` (follows the Windows "app mode" setting), `Light` and `Dark`.
- `ThemeManager` static API:
  - `SetTheme(AppTheme)`
  - `SetAccent(Color?)`; passing `null` restores the built-in accent
  - `UseSystemAccent()`
  - `GetSystemTheme()` and `GetSystemAccentColor()`
  - `Theme`, `ActualTheme` and `Accent` properties
  - `ThemeChanged` event
- Base, Light and Dark palettes defined as resources. Use `DynamicResource` so values follow theme changes:
  - Surfaces: `Tessel.BackgroundBrush`, `Tessel.SurfaceBrush`, `Tessel.SurfaceAltBrush`, `Tessel.SubtleBrush`
  - Text: `Tessel.TextBrush`, `Tessel.TextSecondaryBrush`, `Tessel.TextTertiaryBrush`, `Tessel.TextDisabledBrush`
  - Strokes: `Tessel.BorderBrush`, `Tessel.BorderStrongBrush`
  - Control states: `Tessel.ControlBackgroundBrush`, `Tessel.ControlHoverBrush`, `Tessel.ControlPressedBrush`, `Tessel.InputBackgroundBrush`
  - Accent: `Tessel.AccentBrush`, `Tessel.AccentHoverBrush`, `Tessel.AccentPressedBrush`, `Tessel.AccentSubtleBrush`, `Tessel.OnAccentBrush`
  - Status: `Tessel.{Success,Warning,Danger,Info}Brush` and their `…SubtleBrush` variants
  - Effects: `Tessel.Shadow`, `Tessel.ShadowLarge`
  - Typography and shape: `Tessel.FontFamily`, `Tessel.IconFontFamily`, `Tessel.MonoFontFamily`, `Tessel.ControlCornerRadius`
- Text styles: `Tessel.Text.Display`, `.Title`, `.Subtitle`, `.BodyStrong`, `.Body`, `.Secondary`, `.Caption`, `.Code` and `.Icon`.

#### Custom controls

| Control | Description |
| --- | --- |
| `TesselWindow` | Window with a custom title bar and caption buttons. Properties: `TitleBarContent`, `TitleBarHeight`, `IconSize`, `ShowTitle`. |
| `NavigationView` | Side navigation with a collapsible pane (`IsPaneOpen`, `OpenPaneLength`, `CompactPaneLength`, `PaneTitle`, `PaneHeader`, `IsPaneToggleButtonVisible`), a menu list and a footer list. It hosts pages (`SelectedItem`, `Navigate(Type)`, `IsPageCacheEnabled`) and raises a selection-changed event with old and new item. `PageFactory` lets a DI container construct pages. |
| `NavigationViewItem` | Navigation entry with `Icon` and `TargetPageType`. Selecting it creates and caches the target page. |
| `NavigationViewItemHeader` | Non-focusable section header for the navigation menu. |
| `ContentDialog` | Modal dialog placed on the window's adorner layer. `Title`, `PrimaryButtonText`, `SecondaryButtonText`, `CloseButtonText`, `IsPrimaryButtonEnabled`, `IsPrimaryDestructive`. `ShowAsync(Window?)` returns a `ContentDialogResult` (`None`, `Primary` or `Secondary`). `Hide()` closes it. A button-click event args type allows cancelling the close. |
| `InfoBar` | Inline, persistent notification. `Title`, `Message`, `Severity` (`InfoBarSeverity`: `Informational`, `Success`, `Warning`, `Error`), `IsOpen`, `IsClosable`, `IsIconVisible`. `Content` is shown as an action area on the right. |
| `Snackbar` | Static helper for transient toasts stacked in the bottom-right corner of a window. `Snackbar.Show(title, message, severity, duration, owner)`. `DefaultDuration` is 4 seconds. A toast stays visible while the pointer is over it. |
| `NumberBox` | Numeric input with spin buttons. `Value`, `Minimum`, `Maximum`, `SmallChange`, `LargeChange`, `StringFormat`, `SpinButtonsVisibility`. |
| `ProgressRing` | Circular indeterminate progress indicator. `IsActive`, `Thickness`. |
| `ToggleSwitch` | On/off switch derived from `ToggleButton`. `OnContent`, `OffContent`. |
| `Card` | Headered container. `CornerRadius`, `IsElevated`. |
| `SettingsCard` | Settings row with `Icon`, `Header`, `Description` and a content slot. |
| `Badge` | Small status label. `Kind` (`BadgeKind`: `Neutral`, `Accent`, `Success`, `Warning`, `Danger`, `Info`) and `IsSolid`. |
| `Avatar` | Image or initials avatar. `DisplayName`, `ImageSource`, `IsColorFromName`. |
| `FontIcon` | Glyph icon from Segoe Fluent Icons or Segoe MDL2 Assets. `Glyph`, `Symbol`. |

#### Restyled standard WPF controls

All of the following are styled implicitly once `ThemeResources` is merged:

- **Buttons:** `Button`, `RepeatButton`, `ToggleButton`, `Hyperlink`
- **Selection:** `CheckBox`, `RadioButton`
- **Text input:** `TextBox`, `PasswordBox`, `RichTextBox`, `DatePickerTextBox`
- **Dropdowns and dates:** `ComboBox` (including editable mode), `Calendar`, `DatePicker`
- **Range and progress:** `Slider` (horizontal and vertical), `ProgressBar`
- **Scrolling:** `ScrollViewer`, `ScrollBar`
- **Collections:** `ListBox`, `DataGrid` (rows, cells, column headers and the column gripper), `TreeView`
- **Containers:** `TabControl`, `Expander`, `GroupBox`
- **Menus and popups:** `Menu`, `ContextMenu`, `MenuItem` (top-level and submenu variants), `Separator`, `ToolTip`
- **Other:** `Label`, `GridSplitter`, `StatusBar`

Button style variants: `Tessel.Button.Accent`, `Tessel.Button.Outline`, `Tessel.Button.Subtle`, `Tessel.Button.Danger`, `Tessel.Button.Icon` and `Tessel.Button.Link`.

#### Attached properties (`ControlHelper`)

| Property | Effect |
| --- | --- |
| `Header` | Label above the input |
| `PlaceholderText` | Hint shown while the input is empty |
| `Icon` | Leading icon (use `{tessel:Glyph Name}`) |
| `ShowClearButton` | Adds a button that clears the text |
| `CornerRadius` | Overrides the control's corner radius |
| `HoverBackground`, `PressedBackground` | Custom state colors for buttons |
| `MonitorPassword`, `PasswordLength` | Lets `PasswordBox` expose its length for bindings (clear button and placeholder) |

#### Icons and markup

- `Symbol` enum with 94 common Segoe Fluent Icons / Segoe MDL2 Assets glyphs.
- `{tessel:Glyph Name}` markup extension that resolves a `Symbol` to its glyph string.

#### Converters

- `BooleanToVisibilityConverter`
- `InverseBooleanConverter`
- `NullToVisibilityConverter`
- `EnumToBooleanConverter`
- `ColorToBrushConverter`
- `TreeViewItemIndentConverter`

#### MVVM helpers

- `ObservableObject`: base class implementing `INotifyPropertyChanged`.
- `RelayCommand`, `RelayCommand<T>` and `AsyncRelayCommand`: `ICommand` implementations.

#### XAML namespace

- All public namespaces map to a single XML namespace, `http://schemas.tessel.net/ui`, with the prefix `tessel`: `Tessel.NET.Controls`, `Converters`, `Helpers`, `Markup` and `Theming`.

#### Sample application

- `Tessel.Gallery` demonstrates every control, with these pages: Home, Buttons, Text & Input, Selection, Collections, Feedback, Layout, Menus & Tooltips, Colors & Type, Icons and Settings. The Settings page controls the theme and accent.

### Technical details

- Target framework: `net10.0-windows` (WPF).
- Nullable reference types enabled; implicit usings disabled; `LangVersion` is `latest`.
- XML documentation file generated for the library.
- Solution file: `Tessel.NET.slnx`.
- Package ID: `Tessel.NET`.
- Licensed under the [MIT License](LICENSE).

### Known limitations

- Windows only. The library depends on WPF and the Segoe Fluent Icons / Segoe MDL2 Assets fonts. On Windows 10, glyphs fall back to Segoe MDL2 Assets.

[Unreleased]: https://github.com/exlirixsoftware/Tessel.NET/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/exlirixsoftware/Tessel.NET/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/exlirixsoftware/Tessel.NET/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/exlirixsoftware/Tessel.NET/releases/tag/v1.0.0
