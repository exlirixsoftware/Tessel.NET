using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tessel.NET.Theming;

namespace Tessel.NET.Controls;

/// <summary>
/// An editing timeline for video, audio and animation tools: a ruler with a playhead, tracks (rows) with clips, markers, a loop region,
/// zoom, snapping and playback. Positions are whole frames, so the same control serves a video editor (clips of many frames on a
/// timecode ruler) and a pixel editor (layers as tracks, one-frame cels or keyframes on a frame-number ruler).
/// <code>
/// var timeline = new AnimationTimeline { FramesPerSecond = 24, Duration = 240 };
/// var video = new AnimationTrack("Video 1");
/// video.Clips.Add(new AnimationClip("Intro.mp4", start: 0, length: 96));
/// timeline.Tracks.Add(video);
/// timeline.Play();
/// </code>
/// The user can scrub the ruler, drag clips (also to other tracks), trim their edges, Shift-drag the ruler to set a loop region, and zoom with
/// Ctrl + mouse wheel. Everything the user changes is written to the model (<see cref="AnimationClip.Start"/> etc.) and reported by events.
/// </summary>
[TemplatePart(Name = "PART_Surface", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_HorizontalScrollBar", Type = typeof(ScrollBar))]
[TemplatePart(Name = "PART_VerticalScrollBar", Type = typeof(ScrollBar))]
[TemplatePart(Name = "PART_Headers", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PlayButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ToStartButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ToEndButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_StepBackButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_StepForwardButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ZoomInButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ZoomOutButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ZoomFitButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_UndoButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_RedoButton", Type = typeof(ButtonBase))]
public class AnimationTimeline : Control
{
    /// <summary>Height of the ruler in device-independent pixels (the header column leaves the same space).</summary>
    internal const double RulerHeight = 38;

    internal const double MinPixelsPerFrame = 0.25;
    internal const double MaxPixelsPerFrame = 96;

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x4F, 0x46, 0xE5), Color.FromRgb(0x0E, 0x8F, 0xC4), Color.FromRgb(0x0F, 0x9D, 0x6E), Color.FromRgb(0xD9, 0x7B, 0x06),
        Color.FromRgb(0xDC, 0x3F, 0x3F), Color.FromRgb(0x84, 0x4F, 0xE0), Color.FromRgb(0xD6, 0x3D, 0x8E), Color.FromRgb(0x0E, 0x9A, 0x9A),
    ];

    private AnimationTimelineSurface? _surface;
    private ScrollBar? _hBar;
    private ScrollBar? _vBar;
    private FrameworkElement? _headers;
    private readonly TranslateTransform _headerTransform = new();
    private readonly List<ButtonBase> _buttons = [];
    private bool _updatingScrollBars;

    private readonly DispatcherTimer _playTimer;
    private readonly Stopwatch _clock = new();
    private double _playOriginFrame;
    private bool _ticking;

    private readonly HashSet<AnimationTrack> _watchedTracks = [];
    private readonly HashSet<AnimationClip> _watchedClips = [];

    static AnimationTimeline()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AnimationTimeline), new FrameworkPropertyMetadata(typeof(AnimationTimeline)));
        FocusableProperty.OverrideMetadata(typeof(AnimationTimeline), new FrameworkPropertyMetadata(true));
    }

    public AnimationTimeline()
    {
        _playTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(8) };
        _playTimer.Tick += OnPlayTick;

        SetValue(TracksProperty, new ObservableCollection<AnimationTrack>());
        SetValue(MarkersProperty, new ObservableCollection<AnimationMarker>());
        UpdateTexts();

        Loaded += (_, _) => ThemeManager.ThemeChanged += OnThemeChanged;
        Unloaded += (_, _) =>
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            Pause();
        };
    }

    #region Dependency properties

    public static readonly DependencyProperty TracksProperty = DependencyProperty.Register(
        nameof(Tracks), typeof(ObservableCollection<AnimationTrack>), typeof(AnimationTimeline), new PropertyMetadata(null, OnTracksChanged));

    /// <summary>The rows of the timeline. Replace the collection to bind it, or add tracks to the default one.</summary>
    public ObservableCollection<AnimationTrack> Tracks
    {
        get => (ObservableCollection<AnimationTrack>)GetValue(TracksProperty);
        set => SetValue(TracksProperty, value);
    }

    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
        nameof(Markers), typeof(ObservableCollection<AnimationMarker>), typeof(AnimationTimeline), new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Named positions drawn on the ruler. Clicking one moves the playhead to it.</summary>
    public ObservableCollection<AnimationMarker> Markers
    {
        get => (ObservableCollection<AnimationMarker>)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public static readonly DependencyProperty FramesPerSecondProperty = DependencyProperty.Register(
        nameof(FramesPerSecond), typeof(double), typeof(AnimationTimeline),
        new PropertyMetadata(24d, OnTimeSettingsChanged, (_, v) => Math.Clamp((double)v, 1d, 240d)));

    /// <summary>The frame rate (1 to 240, default 24). Used for playback speed and for the time labels.</summary>
    public double FramesPerSecond
    {
        get => (double)GetValue(FramesPerSecondProperty);
        set => SetValue(FramesPerSecondProperty, value);
    }

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(int), typeof(AnimationTimeline),
        new PropertyMetadata(240, OnDurationChanged, (_, v) => Math.Max(1, (int)v)));

    /// <summary>The length of the project in frames (default 240). The playhead cannot go past it. Use <see cref="FitDurationToContent"/> to match the clips.</summary>
    public int Duration
    {
        get => (int)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly DependencyProperty CurrentFrameProperty = DependencyProperty.Register(
        nameof(CurrentFrame), typeof(int), typeof(AnimationTimeline),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentFrameChanged, CoerceCurrentFrame));

    /// <summary>The frame under the playhead (0 to <see cref="Duration"/>). Bind it to show the matching frame in your preview.</summary>
    public int CurrentFrame
    {
        get => (int)GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    public static readonly DependencyProperty PixelsPerFrameProperty = DependencyProperty.Register(
        nameof(PixelsPerFrame), typeof(double), typeof(AnimationTimeline),
        new FrameworkPropertyMetadata(8d, OnZoomChanged, (_, v) => Math.Clamp((double)v, MinPixelsPerFrame, MaxPixelsPerFrame)));

    /// <summary>The zoom: how many pixels one frame is wide (0.25 to 96, default 8). Ctrl + mouse wheel changes it.</summary>
    public double PixelsPerFrame
    {
        get => (double)GetValue(PixelsPerFrameProperty);
        set => SetValue(PixelsPerFrameProperty, value);
    }

    public static readonly DependencyProperty TimeDisplayProperty = DependencyProperty.Register(
        nameof(TimeDisplay), typeof(TimelineTimeDisplay), typeof(AnimationTimeline),
        new PropertyMetadata(TimelineTimeDisplay.Timecode, OnTimeSettingsChanged));

    /// <summary>Frame numbers, timecode (default) or seconds.</summary>
    public TimelineTimeDisplay TimeDisplay
    {
        get => (TimelineTimeDisplay)GetValue(TimeDisplayProperty);
        set => SetValue(TimeDisplayProperty, value);
    }

    public static readonly DependencyProperty LoopStartProperty = DependencyProperty.Register(
        nameof(LoopStart), typeof(int?), typeof(AnimationTimeline), new PropertyMetadata(null, OnLoopChanged));

    /// <summary>The first frame of the loop (work area) region, or null.</summary>
    public int? LoopStart
    {
        get => (int?)GetValue(LoopStartProperty);
        set => SetValue(LoopStartProperty, value);
    }

    public static readonly DependencyProperty LoopEndProperty = DependencyProperty.Register(
        nameof(LoopEnd), typeof(int?), typeof(AnimationTimeline), new PropertyMetadata(null, OnLoopChanged));

    /// <summary>The frame after the last frame of the loop region, or null. Shift-drag on the ruler sets the region.</summary>
    public int? LoopEnd
    {
        get => (int?)GetValue(LoopEndProperty);
        set => SetValue(LoopEndProperty, value);
    }

    public static readonly DependencyProperty IsLoopEnabledProperty = DependencyProperty.Register(
        nameof(IsLoopEnabled), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false, OnLoopChanged));

    /// <summary>Playback repeats: inside the loop region when there is one, otherwise the whole project.</summary>
    public bool IsLoopEnabled
    {
        get => (bool)GetValue(IsLoopEnabledProperty);
        set => SetValue(IsLoopEnabledProperty, value);
    }

    public static readonly DependencyProperty PlaybackRateProperty = DependencyProperty.Register(
        nameof(PlaybackRate), typeof(double), typeof(AnimationTimeline),
        new PropertyMetadata(1d, OnPlaybackRateChanged, (_, v) => Math.Clamp((double)v, 0.1d, 8d)));

    /// <summary>Playback speed (0.1 to 8, default 1).</summary>
    public double PlaybackRate
    {
        get => (double)GetValue(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, value);
    }

    public static readonly DependencyProperty IsSnappingEnabledProperty = DependencyProperty.Register(
        nameof(IsSnappingEnabled), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(true));

    /// <summary>Dragged clips snap to the playhead, markers, the loop region and the edges of other clips.</summary>
    public bool IsSnappingEnabled
    {
        get => (bool)GetValue(IsSnappingEnabledProperty);
        set => SetValue(IsSnappingEnabledProperty, value);
    }

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(true));

    /// <summary>Allows moving, trimming and deleting clips. Selecting and scrubbing always work.</summary>
    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public static readonly DependencyProperty FollowPlayheadProperty = DependencyProperty.Register(
        nameof(FollowPlayhead), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(true));

    /// <summary>Scrolls horizontally to keep the playhead visible during playback.</summary>
    public bool FollowPlayhead
    {
        get => (bool)GetValue(FollowPlayheadProperty);
        set => SetValue(FollowPlayheadProperty, value);
    }

    public static readonly DependencyProperty PreventOverlapProperty = DependencyProperty.Register(
        nameof(PreventOverlap), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false));

    /// <summary>
    /// Clips of one track may not overlap: dragging a clip onto another one stops in front of it, and trimming stops at the neighbor.
    /// <see cref="AddClip"/> refuses a clip that does not fit. Off by default (video editors stack clips on layers).
    /// </summary>
    public bool PreventOverlap
    {
        get => (bool)GetValue(PreventOverlapProperty);
        set => SetValue(PreventOverlapProperty, value);
    }

    public static readonly DependencyProperty ConstrainToDurationProperty = DependencyProperty.Register(
        nameof(ConstrainToDuration), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false));

    /// <summary>Clips cannot be moved or trimmed past <see cref="Duration"/>.</summary>
    public bool ConstrainToDuration
    {
        get => (bool)GetValue(ConstrainToDurationProperty);
        set => SetValue(ConstrainToDurationProperty, value);
    }

    public static readonly DependencyProperty AutoExtendDurationProperty = DependencyProperty.Register(
        nameof(AutoExtendDuration), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false));

    /// <summary>
    /// <see cref="Duration"/> grows when a clip is moved, trimmed or added past the end (the change can be undone together with the edit).
    /// Ignored when <see cref="ConstrainToDuration"/> is on.
    /// </summary>
    public bool AutoExtendDuration
    {
        get => (bool)GetValue(AutoExtendDurationProperty);
        set => SetValue(AutoExtendDurationProperty, value);
    }

    public static readonly DependencyProperty IsUndoEnabledProperty = DependencyProperty.Register(
        nameof(IsUndoEnabled), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(true, (d, _) => ((AnimationTimeline)d).ClearHistory()));

    /// <summary>Records moves, trims, deletions and <see cref="AddClip"/> so they can be undone (Ctrl+Z) and redone (Ctrl+Y). Turning it off clears the history.</summary>
    public bool IsUndoEnabled
    {
        get => (bool)GetValue(IsUndoEnabledProperty);
        set => SetValue(IsUndoEnabledProperty, value);
    }

    public static readonly DependencyProperty HistoryLimitProperty = DependencyProperty.Register(
        nameof(HistoryLimit), typeof(int), typeof(AnimationTimeline), new PropertyMetadata(200, null, (_, v) => Math.Max(1, (int)v)));

    /// <summary>The number of undo steps that are kept (default 200).</summary>
    public int HistoryLimit
    {
        get => (int)GetValue(HistoryLimitProperty);
        set => SetValue(HistoryLimitProperty, value);
    }

    private static readonly DependencyPropertyKey CanUndoPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanUndo), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false));

    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    public bool CanUndo
    {
        get => (bool)GetValue(CanUndoProperty);
        private set => SetValue(CanUndoPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CanRedoPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanRedo), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false));

    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    public bool CanRedo
    {
        get => (bool)GetValue(CanRedoProperty);
        private set => SetValue(CanRedoPropertyKey, value);
    }

    private static readonly DependencyPropertyKey UndoDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(UndoDescription), typeof(string), typeof(AnimationTimeline), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty UndoDescriptionProperty = UndoDescriptionPropertyKey.DependencyProperty;

    /// <summary>What the next <see cref="Undo"/> would revert (for example "Move clip"), or empty.</summary>
    public string UndoDescription
    {
        get => (string)GetValue(UndoDescriptionProperty);
        private set => SetValue(UndoDescriptionPropertyKey, value);
    }

    private static readonly DependencyPropertyKey RedoDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(RedoDescription), typeof(string), typeof(AnimationTimeline), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RedoDescriptionProperty = RedoDescriptionPropertyKey.DependencyProperty;

    /// <summary>What the next <see cref="Redo"/> would apply, or empty.</summary>
    public string RedoDescription
    {
        get => (string)GetValue(RedoDescriptionProperty);
        private set => SetValue(RedoDescriptionPropertyKey, value);
    }

    public static readonly DependencyProperty ShowTransportProperty = DependencyProperty.Register(
        nameof(ShowTransport), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(true));

    /// <summary>Shows the bar with the play, step, loop, snap and zoom buttons and the time display.</summary>
    public bool ShowTransport
    {
        get => (bool)GetValue(ShowTransportProperty);
        set => SetValue(ShowTransportProperty, value);
    }

    public static readonly DependencyProperty HeaderWidthProperty = DependencyProperty.Register(
        nameof(HeaderWidth), typeof(double), typeof(AnimationTimeline), new PropertyMetadata(180d));

    /// <summary>Width of the column with the track names.</summary>
    public double HeaderWidth
    {
        get => (double)GetValue(HeaderWidthProperty);
        set => SetValue(HeaderWidthProperty, value);
    }

    public static readonly DependencyProperty TrackHeaderTemplateProperty = DependencyProperty.Register(
        nameof(TrackHeaderTemplate), typeof(DataTemplate), typeof(AnimationTimeline), new PropertyMetadata(null));

    /// <summary>
    /// The template of a track header (the data context is the <see cref="AnimationTrack"/>). Give it the track's
    /// <see cref="AnimationTrack.Height"/> so it lines up with the row.
    /// </summary>
    public DataTemplate? TrackHeaderTemplate
    {
        get => (DataTemplate?)GetValue(TrackHeaderTemplateProperty);
        set => SetValue(TrackHeaderTemplateProperty, value);
    }

    public static readonly DependencyProperty HeaderCornerProperty = DependencyProperty.Register(
        nameof(HeaderCorner), typeof(object), typeof(AnimationTimeline), new PropertyMetadata(null));

    /// <summary>Content shown in the top-left corner above the track names (a title, an "add track" button, ...).</summary>
    public object? HeaderCorner
    {
        get => GetValue(HeaderCornerProperty);
        set => SetValue(HeaderCornerProperty, value);
    }

    private static readonly DependencyPropertyKey IsPlayingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPlaying), typeof(bool), typeof(AnimationTimeline), new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty IsPlayingProperty = IsPlayingPropertyKey.DependencyProperty;

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        private set => SetValue(IsPlayingPropertyKey, value);
    }

    private static readonly DependencyPropertyKey HorizontalOffsetPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HorizontalOffset), typeof(double), typeof(AnimationTimeline), new PropertyMetadata(0d));

    public static readonly DependencyProperty HorizontalOffsetProperty = HorizontalOffsetPropertyKey.DependencyProperty;

    /// <summary>How far the timeline is scrolled to the right, in pixels.</summary>
    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        private set => SetValue(HorizontalOffsetPropertyKey, value);
    }

    private static readonly DependencyPropertyKey VerticalOffsetPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VerticalOffset), typeof(double), typeof(AnimationTimeline), new PropertyMetadata(0d));

    public static readonly DependencyProperty VerticalOffsetProperty = VerticalOffsetPropertyKey.DependencyProperty;

    /// <summary>How far the tracks are scrolled down, in pixels.</summary>
    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        private set => SetValue(VerticalOffsetPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CurrentTimeTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CurrentTimeText), typeof(string), typeof(AnimationTimeline), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CurrentTimeTextProperty = CurrentTimeTextPropertyKey.DependencyProperty;

    /// <summary>The playhead position formatted according to <see cref="TimeDisplay"/>.</summary>
    public string CurrentTimeText
    {
        get => (string)GetValue(CurrentTimeTextProperty);
        private set => SetValue(CurrentTimeTextPropertyKey, value);
    }

    private static readonly DependencyPropertyKey DurationTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DurationText), typeof(string), typeof(AnimationTimeline), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DurationTextProperty = DurationTextPropertyKey.DependencyProperty;

    /// <summary>The project length formatted according to <see cref="TimeDisplay"/>.</summary>
    public string DurationText
    {
        get => (string)GetValue(DurationTextProperty);
        private set => SetValue(DurationTextPropertyKey, value);
    }

    public static readonly RoutedEvent CurrentFrameChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(CurrentFrameChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<int>), typeof(AnimationTimeline));

    /// <summary>The playhead moved (by the user, by playback or from code).</summary>
    public event RoutedPropertyChangedEventHandler<int> CurrentFrameChanged
    {
        add => AddHandler(CurrentFrameChangedEvent, value);
        remove => RemoveHandler(CurrentFrameChangedEvent, value);
    }

    #endregion

    #region Events

    /// <summary>Playback started or stopped.</summary>
    public event EventHandler? PlaybackStateChanged;

    /// <summary>The set of selected clips changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The user finished moving clips (also to other tracks). The model has already been updated.</summary>
    public event EventHandler<AnimationClipsChangedEventArgs>? ClipsMoved;

    /// <summary>The user finished trimming a clip. The model has already been updated.</summary>
    public event EventHandler<AnimationClipsChangedEventArgs>? ClipsResized;

    /// <summary>Delete was pressed (or <see cref="DeleteSelectedClips"/> called). Set <c>Cancel</c> to handle the deletion yourself.</summary>
    public event EventHandler<AnimationClipsDeletingEventArgs>? ClipsDeleting;

    /// <summary>A clip was double-clicked.</summary>
    public event EventHandler<AnimationClipEventArgs>? ClipActivated;

    /// <summary>The undo or redo history changed (a step was recorded, undone, redone or cleared).</summary>
    public event EventHandler? HistoryChanged;

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        DetachParts();
        base.OnApplyTemplate();

        _surface = GetTemplateChild("PART_Surface") as AnimationTimelineSurface;
        _hBar = GetTemplateChild("PART_HorizontalScrollBar") as ScrollBar;
        _vBar = GetTemplateChild("PART_VerticalScrollBar") as ScrollBar;
        _headers = GetTemplateChild("PART_Headers") as FrameworkElement;

        if (_surface != null) _surface.Owner = this;
        if (_hBar != null) _hBar.ValueChanged += OnHorizontalScroll;
        if (_vBar != null) _vBar.ValueChanged += OnVerticalScroll;
        if (_headers != null) _headers.RenderTransform = _headerTransform;

        Wire("PART_PlayButton", (_, _) => TogglePlayback());
        Wire("PART_ToStartButton", (_, _) => CurrentFrame = LoopStartOrZero());
        Wire("PART_ToEndButton", (_, _) => CurrentFrame = Duration);
        Wire("PART_StepBackButton", (_, _) => StepBackward());
        Wire("PART_StepForwardButton", (_, _) => StepForward());
        Wire("PART_ZoomInButton", (_, _) => ZoomIn());
        Wire("PART_ZoomOutButton", (_, _) => ZoomOut());
        Wire("PART_ZoomFitButton", (_, _) => ZoomToFit());
        Wire("PART_UndoButton", (_, _) => Undo());
        Wire("PART_RedoButton", (_, _) => Redo());

        UpdateScrollBars();
    }

    private void Wire(string name, RoutedEventHandler handler)
    {
        if (GetTemplateChild(name) is not ButtonBase button) return;
        button.Click += handler;
        _buttons.Add(button);
        _wired.Add((button, handler));
    }

    private readonly List<(ButtonBase Button, RoutedEventHandler Handler)> _wired = [];

    private void DetachParts()
    {
        foreach (var (button, handler) in _wired) button.Click -= handler;
        _wired.Clear();
        _buttons.Clear();
        if (_hBar != null) _hBar.ValueChanged -= OnHorizontalScroll;
        if (_vBar != null) _vBar.ValueChanged -= OnVerticalScroll;
        if (_surface != null) _surface.Owner = null;
    }

    private int LoopStartOrZero() => IsLoopEnabled && LoopRegion is { } region ? region.Start : 0;

    #endregion

    #region Playback

    /// <summary>Starts playing from the playhead (from the start when it is at the end).</summary>
    public void Play()
    {
        if (IsPlaying) return;
        var (start, end) = PlaybackRange;
        if (CurrentFrame >= end || CurrentFrame < start) SetCurrentValue(CurrentFrameProperty, start);
        _playOriginFrame = CurrentFrame;
        _clock.Restart();
        IsPlaying = true;
        _playTimer.Start();
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        _playTimer.Stop();
        _clock.Stop();
        IsPlaying = false;
    }

    /// <summary>Pauses and returns to the first frame (or the start of the loop region).</summary>
    public void Stop()
    {
        Pause();
        SetCurrentValue(CurrentFrameProperty, LoopStartOrZero());
    }

    public void TogglePlayback()
    {
        if (IsPlaying) Pause();
        else Play();
    }

    public void StepForward(int frames = 1) => SetCurrentValue(CurrentFrameProperty, CurrentFrame + frames);

    public void StepBackward(int frames = 1) => SetCurrentValue(CurrentFrameProperty, CurrentFrame - frames);

    /// <summary>The frames playback runs through: the loop region when looping inside one, otherwise the whole project.</summary>
    internal (int Start, int End) PlaybackRange => IsLoopEnabled && LoopRegion is { } region ? region : (0, Duration);

    /// <summary>The loop region as (start, end) when it is set and at least two frames long.</summary>
    internal (int Start, int End)? LoopRegion
    {
        get
        {
            if (LoopStart is not { } start || LoopEnd is not { } end) return null;
            var (a, b) = (Math.Min(start, end), Math.Max(start, end));
            return b - a >= 2 ? (Math.Max(0, a), Math.Min(b, Duration)) : null;
        }
    }

    private void OnPlayTick(object? sender, EventArgs e)
    {
        var (start, end) = PlaybackRange;
        var position = _playOriginFrame + _clock.Elapsed.TotalSeconds * FramesPerSecond * PlaybackRate;

        if (position >= end)
        {
            if (IsLoopEnabled && end > start)
            {
                position = start + (position - start) % (end - start);
                _playOriginFrame = position;
                _clock.Restart();
            }
            else
            {
                _ticking = true;
                SetCurrentValue(CurrentFrameProperty, end);
                _ticking = false;
                Pause();
                return;
            }
        }

        var frame = (int)Math.Floor(position);
        if (frame == CurrentFrame) return;
        _ticking = true;
        try
        {
            SetCurrentValue(CurrentFrameProperty, frame);
        }
        finally
        {
            _ticking = false;
        }
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AnimationTimeline)d).PlaybackStateChanged?.Invoke(d, EventArgs.Empty);

    private static void OnPlaybackRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        if (!timeline.IsPlaying) return;
        timeline._playOriginFrame = timeline.CurrentFrame;
        timeline._clock.Restart();
    }

    #endregion

    #region Zoom and scrolling

    public void ZoomIn() => ZoomAt(PixelsPerFrame * 1.25, ViewportWidth / 2);

    public void ZoomOut() => ZoomAt(PixelsPerFrame / 1.25, ViewportWidth / 2);

    /// <summary>Zooms so the whole project fits the visible width.</summary>
    public void ZoomToFit()
    {
        var width = ViewportWidth - 56;
        if (width <= 0) return;
        SetCurrentValue(PixelsPerFrameProperty, width / Duration);
        SetHorizontalOffset(0);
    }

    /// <summary>Sets the zoom and keeps the frame under <paramref name="anchorX"/> (a position inside the track area) where it is.</summary>
    internal void ZoomAt(double pixelsPerFrame, double anchorX)
    {
        var anchorFrame = (anchorX + HorizontalOffset) / PixelsPerFrame;
        SetCurrentValue(PixelsPerFrameProperty, pixelsPerFrame);
        SetHorizontalOffset(anchorFrame * PixelsPerFrame - anchorX);
    }

    /// <summary>Scrolls so that <paramref name="frame"/> is visible.</summary>
    public void ScrollIntoView(int frame)
    {
        var x = frame * PixelsPerFrame;
        var margin = 24d;
        if (x < HorizontalOffset + margin) SetHorizontalOffset(x - margin);
        else if (x > HorizontalOffset + ViewportWidth - margin) SetHorizontalOffset(x - ViewportWidth + margin);
    }

    internal double ViewportWidth => _surface?.ActualWidth ?? 0;

    internal double ViewportHeight => Math.Max(0, (_surface?.ActualHeight ?? 0) - RulerHeight);

    internal double ContentWidth => Duration * PixelsPerFrame + 48;

    internal double ContentHeight => Tracks?.Sum(t => t.Height) ?? 0;

    internal void SetHorizontalOffset(double value)
    {
        var max = Math.Max(0, ContentWidth - ViewportWidth);
        value = Math.Clamp(value, 0, max);
        if (Math.Abs(value - HorizontalOffset) < 0.01) return;
        HorizontalOffset = value;
        if (_hBar != null && !_updatingScrollBars && Math.Abs(_hBar.Value - value) > 0.01) _hBar.Value = value;
        _surface?.InvalidateVisual();
    }

    internal void SetVerticalOffset(double value)
    {
        var max = Math.Max(0, ContentHeight - ViewportHeight);
        value = Math.Clamp(value, 0, max);
        if (Math.Abs(value - VerticalOffset) < 0.01) return;
        VerticalOffset = value;
        _headerTransform.Y = -value;
        if (_vBar != null && !_updatingScrollBars && Math.Abs(_vBar.Value - value) > 0.01) _vBar.Value = value;
        _surface?.InvalidateVisual();
    }

    internal void UpdateScrollBars()
    {
        if (_surface == null) return;
        _updatingScrollBars = true;
        try
        {
            var maxH = Math.Max(0, ContentWidth - ViewportWidth);
            var maxV = Math.Max(0, ContentHeight - ViewportHeight);
            if (_hBar != null)
            {
                _hBar.Maximum = maxH;
                _hBar.ViewportSize = ViewportWidth;
                _hBar.LargeChange = Math.Max(24, ViewportWidth * 0.9);
                _hBar.SmallChange = 24;
                _hBar.Visibility = maxH > 0 ? Visibility.Visible : Visibility.Hidden;
            }
            if (_vBar != null)
            {
                _vBar.Maximum = maxV;
                _vBar.ViewportSize = ViewportHeight;
                _vBar.LargeChange = Math.Max(24, ViewportHeight * 0.9);
                _vBar.SmallChange = 24;
                _vBar.Visibility = maxV > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }
        finally
        {
            _updatingScrollBars = false;
        }
        SetHorizontalOffset(HorizontalOffset);
        SetVerticalOffset(VerticalOffset);
        if (_hBar != null) _hBar.Value = HorizontalOffset;
        if (_vBar != null) _vBar.Value = VerticalOffset;
    }

    private void OnHorizontalScroll(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingScrollBars) SetHorizontalOffset(e.NewValue);
    }

    private void OnVerticalScroll(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingScrollBars) SetVerticalOffset(e.NewValue);
    }

    #endregion

    #region Time formatting

    /// <summary>Formats a frame according to <see cref="TimeDisplay"/> (for example <c>00:02:12</c>, <c>60</c> or <c>2.5s</c>).</summary>
    public string FormatTime(int frame) => FormatTime(frame, TimeDisplay, FramesPerSecond);

    /// <summary>Formats a frame in a specific format, whatever <see cref="TimeDisplay"/> is.</summary>
    public string FormatTime(int frame, TimelineTimeDisplay display) => FormatTime(frame, display, FramesPerSecond);

    internal static string FormatTime(int frame, TimelineTimeDisplay display, double framesPerSecond)
    {
        var fps = Math.Max(1, (int)Math.Round(framesPerSecond));
        switch (display)
        {
            case TimelineTimeDisplay.Frames:
                return frame.ToString(CultureInfo.InvariantCulture);
            case TimelineTimeDisplay.Seconds:
                return (frame / framesPerSecond).ToString("0.##", CultureInfo.InvariantCulture) + "s";
            default:
                var frames = frame % fps;
                var totalSeconds = frame / fps;
                var seconds = totalSeconds % 60;
                var minutes = totalSeconds / 60 % 60;
                var hours = totalSeconds / 3600;
                return hours > 0
                    ? $"{hours}:{minutes:00}:{seconds:00}:{frames:00}"
                    : $"{minutes:00}:{seconds:00}:{frames:00}";
        }
    }

    /// <summary>Distance between labelled and unlabelled ticks of the ruler (in frames) for the current zoom and frame rate.</summary>
    internal (int Major, int Minor) GetTickSteps()
    {
        var fps = Math.Max(1, (int)Math.Round(FramesPerSecond));
        var candidates = new SortedSet<int> { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 5000, 10000 };
        foreach (var seconds in new[] { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1800, 3600 }) candidates.Add(seconds * fps);

        var major = candidates.FirstOrDefault(c => c * PixelsPerFrame >= 72);
        if (major == 0) major = candidates.Max;
        var minor = major % 5 == 0 ? major / 5 : major % 2 == 0 ? major / 2 : major;
        return (major, Math.Max(1, minor));
    }

    private void UpdateTexts()
    {
        CurrentTimeText = FormatTime(CurrentFrame);
        DurationText = FormatTime(Duration);
    }

    #endregion

    #region Selection and editing

    /// <summary>The selected clips (in track order).</summary>
    public IReadOnlyList<AnimationClip> SelectedClips => Tracks.SelectMany(t => t.Clips).Where(c => c.IsSelected).ToList();

    public void ClearSelection() => SetSelection([]);

    public void SelectAll() => SetSelection(Tracks.Where(t => !t.IsLocked).SelectMany(t => t.Clips));

    /// <summary>Selects exactly these clips.</summary>
    public void SelectClips(IEnumerable<AnimationClip> clips) => SetSelection(clips);

    internal void SetSelection(IEnumerable<AnimationClip> clips)
    {
        var wanted = new HashSet<AnimationClip>(clips);
        var changed = false;
        foreach (var clip in Tracks.SelectMany(t => t.Clips))
        {
            var selected = wanted.Contains(clip);
            if (clip.IsSelected == selected) continue;
            clip.IsSelected = selected;
            changed = true;
        }
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes the selected clips from their tracks (after <see cref="ClipsDeleting"/> allowed it).</summary>
    public void DeleteSelectedClips()
    {
        if (!IsEditable) return;
        var clips = SelectedClips.Where(c => !TrackOf(c)!.IsLocked).ToList();
        if (clips.Count == 0) return;

        var args = new AnimationClipsDeletingEventArgs(clips);
        ClipsDeleting?.Invoke(this, args);
        if (args.Cancel) return;

        RemoveClipsRecorded(clips, clips.Count == 1 ? "Delete clip" : "Delete clips");
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets <see cref="Duration"/> to the end of the last clip (at least one frame).</summary>
    public void FitDurationToContent()
        => Duration = Math.Max(1, Tracks.SelectMany(t => t.Clips).Select(c => c.End).DefaultIfEmpty(1).Max());

    /// <summary>Removes the loop region.</summary>
    public void ClearLoopRegion()
    {
        LoopStart = null;
        LoopEnd = null;
    }

    internal AnimationTrack? TrackOf(AnimationClip clip) => Tracks.FirstOrDefault(t => t.Clips.Contains(clip));

    internal void RaiseClipsMoved(IReadOnlyList<AnimationClipChange> changes) => ClipsMoved?.Invoke(this, new AnimationClipsChangedEventArgs(changes));

    internal void RaiseClipsResized(IReadOnlyList<AnimationClipChange> changes) => ClipsResized?.Invoke(this, new AnimationClipsChangedEventArgs(changes));

    internal void RaiseClipActivated(AnimationClip clip, AnimationTrack track) => ClipActivated?.Invoke(this, new AnimationClipEventArgs(clip, track));

    #endregion

    #region Undo, redo and clip editing

    private sealed record HistoryStep(string Description, Action Undo, Action Redo);

    private readonly List<HistoryStep> _undoSteps = [];
    private readonly List<HistoryStep> _redoSteps = [];

    /// <summary>Reverts the last recorded edit (move, trim, delete, <see cref="AddClip"/> or your own <see cref="RecordHistory"/> step).</summary>
    public void Undo()
    {
        if (_undoSteps.Count == 0) return;
        var step = _undoSteps[^1];
        _undoSteps.RemoveAt(_undoSteps.Count - 1);
        step.Undo();
        _redoSteps.Add(step);
        UpdateHistoryState();
    }

    public void Redo()
    {
        if (_redoSteps.Count == 0) return;
        var step = _redoSteps[^1];
        _redoSteps.RemoveAt(_redoSteps.Count - 1);
        step.Redo();
        _undoSteps.Add(step);
        UpdateHistoryState();
    }

    /// <summary>Forgets all undo and redo steps.</summary>
    public void ClearHistory()
    {
        if (_undoSteps.Count == 0 && _redoSteps.Count == 0) return;
        _undoSteps.Clear();
        _redoSteps.Clear();
        UpdateHistoryState();
    }

    /// <summary>
    /// Adds your own step to the history (for example for an edit you made yourself). <paramref name="undo"/> and <paramref name="redo"/>
    /// are called by <see cref="Undo"/> and <see cref="Redo"/>; the edit itself must already have been applied.
    /// </summary>
    public void RecordHistory(string description, Action undo, Action redo)
    {
        if (!IsUndoEnabled) return;
        _undoSteps.Add(new HistoryStep(description, undo, redo));
        _redoSteps.Clear();
        var limit = HistoryLimit;
        if (_undoSteps.Count > limit) _undoSteps.RemoveRange(0, _undoSteps.Count - limit);
        UpdateHistoryState();
    }

    private void UpdateHistoryState()
    {
        CanUndo = _undoSteps.Count > 0;
        CanRedo = _redoSteps.Count > 0;
        UndoDescription = _undoSteps.Count > 0 ? _undoSteps[^1].Description : string.Empty;
        RedoDescription = _redoSteps.Count > 0 ? _redoSteps[^1].Description : string.Empty;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Records a finished move or trim (the model has already been changed) so it can be undone.
    /// </summary>
    internal void RecordChange(string description, IReadOnlyList<AnimationClipChange> changes, int oldDuration)
    {
        if (!IsUndoEnabled) return;
        var newDuration = Duration;
        var after = changes.Select(c => (c.Clip, c.OldTrack, c.NewTrack, Start: c.Clip.Start, Length: c.Clip.Length, Media: c.Clip.MediaStart)).ToList();

        RecordHistory(description,
            undo: () =>
            {
                foreach (var change in changes.Where(c => !ReferenceEquals(c.OldTrack, c.NewTrack)).OrderBy(c => c.OldIndex))
                {
                    change.NewTrack.Clips.Remove(change.Clip);
                    change.OldTrack.Clips.Insert(Math.Clamp(change.OldIndex, 0, change.OldTrack.Clips.Count), change.Clip);
                }
                foreach (var change in changes)
                {
                    change.Clip.Start = change.OldStart;
                    change.Clip.Length = change.OldLength;
                    change.Clip.MediaStart = change.OldMediaStart;
                }
                SetCurrentValue(DurationProperty, oldDuration);
                SetSelection(changes.Select(c => c.Clip));
            },
            redo: () =>
            {
                foreach (var step in after)
                {
                    if (!ReferenceEquals(step.OldTrack, step.NewTrack))
                    {
                        step.OldTrack.Clips.Remove(step.Clip);
                        step.NewTrack.Clips.Add(step.Clip);
                    }
                    step.Clip.Start = step.Start;
                    step.Clip.Length = step.Length;
                    step.Clip.MediaStart = step.Media;
                }
                SetCurrentValue(DurationProperty, newDuration);
                SetSelection(after.Select(a => a.Clip));
            });
    }

    /// <summary>Removes clips from their tracks and records the deletion.</summary>
    private void RemoveClipsRecorded(IReadOnlyList<AnimationClip> clips, string description)
    {
        var removed = clips
            .Select(clip => TrackOf(clip) is { } track ? (Clip: clip, Track: track, Index: track.Clips.IndexOf(clip)) : default)
            .Where(entry => entry.Track != null)
            .ToList();
        foreach (var entry in removed) entry.Track.Clips.Remove(entry.Clip);

        RecordHistory(description,
            undo: () =>
            {
                foreach (var entry in removed.OrderBy(e => e.Index)) entry.Track.Clips.Insert(Math.Clamp(entry.Index, 0, entry.Track.Clips.Count), entry.Clip);
                SetSelection(removed.Select(e => e.Clip));
            },
            redo: () =>
            {
                foreach (var entry in removed) entry.Track.Clips.Remove(entry.Clip);
                SetSelection([]);
            });
    }

    /// <summary>
    /// Adds a clip to a track and records it for undo. Returns false (and adds nothing) when <see cref="PreventOverlap"/> is on and the clip
    /// would overlap another one, or when <see cref="ConstrainToDuration"/> is on and it ends after <see cref="Duration"/>.
    /// </summary>
    public bool AddClip(AnimationTrack track, AnimationClip clip)
    {
        if (!Tracks.Contains(track)) return false;
        if (PreventOverlap && !IsFree(track, clip.Start, clip.Length, null)) return false;
        if (ConstrainToDuration && clip.End > Duration) return false;

        var oldDuration = Duration;
        track.Clips.Add(clip);
        ExtendDurationIfNeeded();
        var newDuration = Duration;
        var index = track.Clips.Count - 1;

        RecordHistory("Add clip",
            undo: () =>
            {
                track.Clips.Remove(clip);
                SetCurrentValue(DurationProperty, oldDuration);
            },
            redo: () =>
            {
                track.Clips.Insert(Math.Clamp(index, 0, track.Clips.Count), clip);
                SetCurrentValue(DurationProperty, newDuration);
            });
        return true;
    }

    /// <summary>True when no clip of <paramref name="track"/> (other than <paramref name="ignore"/>) overlaps the given frames.</summary>
    internal bool IsFree(AnimationTrack track, int start, int length, ISet<AnimationClip>? ignore)
        => !track.Clips.Any(c => (ignore == null || !ignore.Contains(c)) && c.Start < start + length && c.End > start);

    /// <summary>The end of the closest clip before <paramref name="clip"/> on its track (0 when there is none).</summary>
    internal int PreviousEnd(AnimationTrack track, AnimationClip clip)
        => track.Clips.Where(c => !ReferenceEquals(c, clip) && c.End <= clip.Start).Select(c => c.End).DefaultIfEmpty(0).Max();

    /// <summary>The start of the closest clip after <paramref name="clip"/> on its track (<see cref="int.MaxValue"/> when there is none).</summary>
    internal int NextStart(AnimationTrack track, AnimationClip clip)
        => track.Clips.Where(c => !ReferenceEquals(c, clip) && c.Start >= clip.End).Select(c => c.Start).DefaultIfEmpty(int.MaxValue).Min();

    internal int MaxClipEnd => Tracks.SelectMany(t => t.Clips).Select(c => c.End).DefaultIfEmpty(0).Max();

    internal void ExtendDurationIfNeeded()
    {
        if (!AutoExtendDuration || ConstrainToDuration) return;
        var end = MaxClipEnd;
        if (end > Duration) SetCurrentValue(DurationProperty, end);
    }

    #endregion

    #region Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayback();
                break;
            case Key.Left:
                StepBackward(ctrl ? 10 : 1);
                break;
            case Key.Right:
                StepForward(ctrl ? 10 : 1);
                break;
            case Key.Home:
                SetCurrentValue(CurrentFrameProperty, 0);
                break;
            case Key.End:
                SetCurrentValue(CurrentFrameProperty, Duration);
                break;
            case Key.Delete:
                DeleteSelectedClips();
                break;
            case Key.A when ctrl:
                SelectAll();
                break;
            case Key.Z when ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                Redo();
                break;
            case Key.Z when ctrl:
                Undo();
                break;
            case Key.Y when ctrl:
                Redo();
                break;
            case Key.Escape:
                _surface?.CancelDrag();
                break;
            case Key.OemPlus or Key.Add when ctrl:
                ZoomIn();
                break;
            case Key.OemMinus or Key.Subtract when ctrl:
                ZoomOut();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    #endregion

    #region Change handling

    private static object CoerceCurrentFrame(DependencyObject d, object value) => Math.Clamp((int)value, 0, ((AnimationTimeline)d).Duration);

    private static void OnCurrentFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        timeline.CurrentTimeText = timeline.FormatTime((int)e.NewValue);

        // A seek during playback continues from the new position.
        if (timeline.IsPlaying && !timeline._ticking)
        {
            timeline._playOriginFrame = (int)e.NewValue;
            timeline._clock.Restart();
        }
        if (timeline.IsPlaying && timeline.FollowPlayhead) timeline.ScrollIntoView((int)e.NewValue);

        timeline._surface?.InvalidateVisual();
        timeline.RaiseEvent(new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue, CurrentFrameChangedEvent));
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        timeline.CoerceValue(CurrentFrameProperty);
        timeline.UpdateTexts();
        timeline.UpdateScrollBars();
        timeline._surface?.InvalidateVisual();
    }

    private static void OnTimeSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        timeline.UpdateTexts();
        timeline._surface?.InvalidateVisual();
    }

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        timeline.UpdateScrollBars();
        timeline._surface?.InvalidateVisual();
    }

    private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AnimationTimeline)d)._surface?.InvalidateVisual();

    private void OnThemeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => _surface?.InvalidateVisual());

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        if (e.OldValue is ObservableCollection<AnimationMarker> old)
        {
            old.CollectionChanged -= timeline.OnMarkersCollectionChanged;
            foreach (var marker in old) marker.PropertyChanged -= timeline.OnMarkerPropertyChanged;
        }
        if (e.NewValue is ObservableCollection<AnimationMarker> added)
        {
            added.CollectionChanged += timeline.OnMarkersCollectionChanged;
            foreach (var marker in added) marker.PropertyChanged += timeline.OnMarkerPropertyChanged;
        }
        timeline._surface?.InvalidateVisual();
    }

    private void OnMarkersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (AnimationMarker marker in e.OldItems) marker.PropertyChanged -= OnMarkerPropertyChanged;
        if (e.NewItems != null) foreach (AnimationMarker marker in e.NewItems) marker.PropertyChanged += OnMarkerPropertyChanged;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var marker in Markers) marker.PropertyChanged -= OnMarkerPropertyChanged;
            foreach (var marker in Markers) marker.PropertyChanged += OnMarkerPropertyChanged;
        }
        _surface?.InvalidateVisual();
    }

    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e) => _surface?.InvalidateVisual();

    private static void OnTracksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (AnimationTimeline)d;
        if (e.OldValue is ObservableCollection<AnimationTrack> old) old.CollectionChanged -= timeline.OnTracksCollectionChanged;
        if (e.NewValue is ObservableCollection<AnimationTrack> added) added.CollectionChanged += timeline.OnTracksCollectionChanged;
        timeline.RewatchModel();
        timeline.ClearHistory();
    }

    private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RewatchModel();

    /// <summary>Re-subscribes to every track and clip (the model is small enough that this is cheaper than diffing).</summary>
    private void RewatchModel()
    {
        foreach (var track in _watchedTracks)
        {
            track.PropertyChanged -= OnTrackPropertyChanged;
            track.Clips.CollectionChanged -= OnClipsCollectionChanged;
        }
        foreach (var clip in _watchedClips) clip.PropertyChanged -= OnClipPropertyChanged;
        _watchedTracks.Clear();
        _watchedClips.Clear();

        if (Tracks != null)
        {
            var index = 0;
            foreach (var track in Tracks)
            {
                track.PropertyChanged += OnTrackPropertyChanged;
                track.Clips.CollectionChanged += OnClipsCollectionChanged;
                _watchedTracks.Add(track);
                track.DisplayBrush = track.Brush ?? PaletteBrush(index++);
                foreach (var clip in track.Clips) WatchClip(clip);
            }
        }
        UpdateScrollBars();
        _surface?.InvalidateVisual();
    }

    private void WatchClip(AnimationClip clip)
    {
        if (_watchedClips.Add(clip)) clip.PropertyChanged += OnClipPropertyChanged;
    }

    private void OnClipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (AnimationClip clip in e.OldItems)
            {
                clip.PropertyChanged -= OnClipPropertyChanged;
                _watchedClips.Remove(clip);
            }
        }
        if (e.NewItems != null) foreach (AnimationClip clip in e.NewItems) WatchClip(clip);
        if (e.Action == NotifyCollectionChangedAction.Reset && sender is ObservableCollection<AnimationClip> list)
        {
            foreach (var clip in list) WatchClip(clip);
        }
        _surface?.InvalidateVisual();
    }

    private void OnTrackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AnimationTrack track && e.PropertyName == nameof(AnimationTrack.Brush))
        {
            track.DisplayBrush = track.Brush ?? PaletteBrush(Tracks.IndexOf(track));
        }
        if (e.PropertyName == nameof(AnimationTrack.Height)) UpdateScrollBars();
        _surface?.InvalidateVisual();
    }

    private void OnClipPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimationClip.IsSelected)) SelectionChanged?.Invoke(this, EventArgs.Empty);
        _surface?.InvalidateVisual();
    }

    internal static Brush PaletteBrush(int index)
    {
        var brush = new SolidColorBrush(Palette[Math.Abs(index) % Palette.Length]);
        brush.Freeze();
        return brush;
    }

    #endregion
}
