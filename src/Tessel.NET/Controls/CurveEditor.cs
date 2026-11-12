using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace Tessel.NET.Controls;

/// <summary>
/// Edits an <see cref="AnimationCurve"/>: drag the keys, drag the Bezier handles of the selected key, double-click to add a key, press Delete to
/// remove one. A playhead (<see cref="CurrentTime"/>, scrubbed on the strip above the graph) shows the value of the curve at that time.
/// Use it for easing curves, volume envelopes, motion paths or any value-over-time graph; it works next to an <see cref="AnimationTimeline"/>.
/// </summary>
public class CurveEditor : Control
{
    private const double LeftMargin = 46;
    private const double RightMargin = 16;
    private const double TopMargin = 26;
    private const double BottomMargin = 26;
    private const double KeyRadius = 6;
    private const double HandleRadius = 5;
    private const double HitRadius = 9;

    private enum DragKind
    {
        None,
        Key,
        InHandle,
        OutHandle,
        Scrub,
        Marquee,
        Pan,
    }

    private sealed record Snapshot(AnimationCurve.KeyState[] Keys, int[] Selected, int Primary);

    private AnimationCurve? _subscribed;
    private DragKind _drag;
    private CurveKey? _hoverKey;
    private DragKind _hoverKind;
    private readonly HashSet<CurveKey> _selection = [];
    private CurveKey? _primary;
    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private Snapshot? _before;
    private Dictionary<CurveKey, (double Time, double Value)> _origins = [];
    private (double Time, double Value) _pressData;
    private Point _marqueeStart;
    private Point _marqueeEnd;
    private HashSet<CurveKey> _marqueeBase = [];
    private Point _panLast;

    private const double MinSpan = 1e-3;
    private const double MaxSpan = 1e6;

    static CurveEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CurveEditor), new FrameworkPropertyMetadata(typeof(CurveEditor)));
        FocusableProperty.OverrideMetadata(typeof(CurveEditor), new FrameworkPropertyMetadata(true));
    }

    public CurveEditor()
    {
        SetCurrentValue(CurveProperty, AnimationCurve.CreateDefault());
    }

    #region Dependency properties

    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(
        nameof(Curve), typeof(AnimationCurve), typeof(CurveEditor),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((CurveEditor)d).OnCurveChanged((AnimationCurve?)e.OldValue, (AnimationCurve?)e.NewValue)));

    public static readonly DependencyProperty MinTimeProperty = DependencyProperty.Register(
        nameof(MinTime), typeof(double), typeof(CurveEditor), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxTimeProperty = DependencyProperty.Register(
        nameof(MaxTime), typeof(double), typeof(CurveEditor), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(
        nameof(MinValue), typeof(double), typeof(CurveEditor), new FrameworkPropertyMetadata(-0.25, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue), typeof(double), typeof(CurveEditor), new FrameworkPropertyMetadata(1.25, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentTimeProperty = DependencyProperty.Register(
        nameof(CurrentTime), typeof(double), typeof(CurveEditor),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ShowPlayheadProperty = DependencyProperty.Register(
        nameof(ShowPlayhead), typeof(bool), typeof(CurveEditor), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(CurveEditor), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSnappingEnabledProperty = DependencyProperty.Register(
        nameof(IsSnappingEnabled), typeof(bool), typeof(CurveEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty SnapTimeProperty = DependencyProperty.Register(
        nameof(SnapTime), typeof(double), typeof(CurveEditor), new PropertyMetadata(0.05));

    public static readonly DependencyProperty SnapValueProperty = DependencyProperty.Register(
        nameof(SnapValue), typeof(double), typeof(CurveEditor), new PropertyMetadata(0.05));

    public static readonly DependencyProperty MinimumKeysProperty = DependencyProperty.Register(
        nameof(MinimumKeys), typeof(int), typeof(CurveEditor), new PropertyMetadata(2));

    public static readonly DependencyProperty CurveBrushProperty = DependencyProperty.Register(
        nameof(CurveBrush), typeof(Brush), typeof(CurveEditor), new FrameworkPropertyMetadata(Brushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextFormatProperty = DependencyProperty.Register(
        nameof(TextFormat), typeof(string), typeof(CurveEditor), new FrameworkPropertyMetadata("0.##", FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyPropertyKey SelectedKeyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SelectedKey), typeof(CurveKey), typeof(CurveEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedKeyProperty = SelectedKeyPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsUndoEnabledProperty = DependencyProperty.Register(
        nameof(IsUndoEnabled), typeof(bool), typeof(CurveEditor), new PropertyMetadata(true, (d, _) => ((CurveEditor)d).ClearHistory()));

    public static readonly DependencyProperty HistoryLimitProperty = DependencyProperty.Register(
        nameof(HistoryLimit), typeof(int), typeof(CurveEditor), new PropertyMetadata(100, null, (_, v) => Math.Max(1, (int)v)));

    private static readonly DependencyPropertyKey CanUndoPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanUndo), typeof(bool), typeof(CurveEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey CanRedoPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanRedo), typeof(bool), typeof(CurveEditor), new PropertyMetadata(false));

    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    /// <summary>Records every edit made in the editor (drags, added or deleted keys, easings) so Undo and Redo can take it back.</summary>
    public bool IsUndoEnabled
    {
        get => (bool)GetValue(IsUndoEnabledProperty);
        set => SetValue(IsUndoEnabledProperty, value);
    }

    /// <summary>The number of steps that are remembered (100 by default).</summary>
    public int HistoryLimit
    {
        get => (int)GetValue(HistoryLimitProperty);
        set => SetValue(HistoryLimitProperty, value);
    }

    public bool CanUndo => (bool)GetValue(CanUndoProperty);

    public bool CanRedo => (bool)GetValue(CanRedoProperty);

    /// <summary>The curve being edited (a smooth curve from (0, 0) to (1, 1) at first).</summary>
    public AnimationCurve? Curve
    {
        get => (AnimationCurve?)GetValue(CurveProperty);
        set => SetValue(CurveProperty, value);
    }

    /// <summary>The time range shown on the horizontal axis.</summary>
    public double MinTime
    {
        get => (double)GetValue(MinTimeProperty);
        set => SetValue(MinTimeProperty, value);
    }

    public double MaxTime
    {
        get => (double)GetValue(MaxTimeProperty);
        set => SetValue(MaxTimeProperty, value);
    }

    /// <summary>The value range shown on the vertical axis (the default leaves room for overshooting curves).</summary>
    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>The playhead position (two-way). Drag on the strip above the graph to scrub it.</summary>
    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public bool ShowPlayhead
    {
        get => (bool)GetValue(ShowPlayheadProperty);
        set => SetValue(ShowPlayheadProperty, value);
    }

    /// <summary>False makes the graph read-only (the playhead can still be scrubbed).</summary>
    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>Keys and handles snap to multiples of <see cref="SnapTime"/> and <see cref="SnapValue"/> while they are dragged.</summary>
    public bool IsSnappingEnabled
    {
        get => (bool)GetValue(IsSnappingEnabledProperty);
        set => SetValue(IsSnappingEnabledProperty, value);
    }

    public double SnapTime
    {
        get => (double)GetValue(SnapTimeProperty);
        set => SetValue(SnapTimeProperty, value);
    }

    public double SnapValue
    {
        get => (double)GetValue(SnapValueProperty);
        set => SetValue(SnapValueProperty, value);
    }

    /// <summary>Delete does not remove keys below this count (2 by default, so a curve always has a start and an end).</summary>
    public int MinimumKeys
    {
        get => (int)GetValue(MinimumKeysProperty);
        set => SetValue(MinimumKeysProperty, value);
    }

    /// <summary>The color of the curve, the keys and the playhead.</summary>
    public Brush CurveBrush
    {
        get => (Brush)GetValue(CurveBrushProperty);
        set => SetValue(CurveBrushProperty, value);
    }

    /// <summary>The number format of the axis labels and of the value shown while dragging.</summary>
    public string TextFormat
    {
        get => (string)GetValue(TextFormatProperty);
        set => SetValue(TextFormatProperty, value);
    }

    /// <summary>The key that was selected last (null when nothing is selected). Assigning selects only that key; see <see cref="SelectedKeys"/>.</summary>
    public CurveKey? SelectedKey
    {
        get => (CurveKey?)GetValue(SelectedKeyProperty);
        private set => SelectOnly(value);
    }

    #endregion

    /// <summary>Raised after the curve changed (keys added, removed or dragged, easing applied...).</summary>
    public event EventHandler? CurveChanged;

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.</summary>
    public event EventHandler? HistoryChanged;

    /// <summary>The value of the curve at <paramref name="time"/>.</summary>
    public double Evaluate(double time) => Curve?.Evaluate(time) ?? 0;

    #region Selection

    /// <summary>The selected keys in time order.</summary>
    public IReadOnlyList<CurveKey> SelectedKeys => Curve == null ? [] : Curve.Keys.Where(_selection.Contains).ToList();

    /// <summary>Selects only this key (null clears the selection).</summary>
    public void SelectKey(CurveKey? key) => SelectOnly(key != null && Curve != null && Curve.Keys.Contains(key) ? key : null);

    /// <summary>Selects exactly these keys. Click a key with Ctrl or Shift, or drag a box on the graph, to select several by mouse.</summary>
    public void SelectKeys(IEnumerable<CurveKey> keys)
    {
        var valid = Curve == null ? [] : keys.Where(Curve.Keys.Contains).ToList();
        Select(valid, null);
    }

    public void SelectAll() => Select(Curve == null ? [] : Curve.Keys.ToList(), null);

    public void ClearSelection() => SelectOnly(null);

    private void SelectOnly(CurveKey? key) => Select(key == null ? [] : [key], key);

    private void Select(IEnumerable<CurveKey> keys, CurveKey? primary)
    {
        var set = new HashSet<CurveKey>(keys);
        if (primary != null && !set.Contains(primary)) primary = null;
        if (primary == null && set.Count > 0 && Curve != null) primary = Curve.Keys.LastOrDefault(set.Contains);

        var changed = !set.SetEquals(_selection) || !ReferenceEquals(primary, _primary);
        _selection.Clear();
        foreach (var key in set) _selection.Add(key);
        _primary = primary;
        SetValue(SelectedKeyPropertyKey, primary);
        InvalidateVisual();
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Commands

    /// <summary>Removes the selected keys unless that would leave fewer than <see cref="MinimumKeys"/>.</summary>
    public bool DeleteSelectedKey()
    {
        if (!IsEditable || Curve == null || _selection.Count == 0) return false;
        var doomed = SelectedKeys;
        if (Curve.Keys.Count - doomed.Count < Math.Max(1, MinimumKeys)) return false;
        var curve = Curve;
        Edit(() => curve.Batch(() =>
        {
            foreach (var key in doomed) curve.Keys.Remove(key);
        }));
        ClearSelection();
        return true;
    }

    /// <summary>Adds a key on the graph at (<paramref name="time"/>, <paramref name="value"/>) and selects it.</summary>
    public CurveKey? AddKey(double time, double value)
    {
        if (Curve == null) return null;
        var curve = Curve;
        CurveKey? key = null;
        Edit(() => key = curve.AddKey(time, value));
        SelectOnly(key);
        return key;
    }

    /// <summary>
    /// Gives the segment after each selected key (or before it, for the last key) one of the preset shapes. With several keys selected every
    /// one of those segments is changed.
    /// </summary>
    public bool ApplyEasingToSelected(CurveEasing easing)
    {
        var segments = SelectedSegments;
        if (Curve == null || segments.Count == 0) return false;
        var curve = Curve;
        Edit(() => curve.Batch(() =>
        {
            foreach (var segment in segments) curve.ApplyEasing(segment, easing);
        }));
        return true;
    }

    /// <summary>Changes how the segments of the selected keys are drawn (see <see cref="ApplyEasingToSelected"/>).</summary>
    public bool SetSelectedInterpolation(CurveInterpolation interpolation)
    {
        var segments = SelectedSegments;
        if (Curve == null || segments.Count == 0) return false;
        var curve = Curve;
        Edit(() => curve.Batch(() =>
        {
            foreach (var segment in segments) curve.Keys[segment].Interpolation = interpolation;
        }));
        return true;
    }

    /// <summary>The indexes of the keys that start the segments of the selected keys.</summary>
    public IReadOnlyList<int> SelectedSegments
    {
        get
        {
            if (Curve == null || Curve.Keys.Count < 2) return [];
            return Curve.Keys.Select((key, index) => (key, index))
                .Where(x => _selection.Contains(x.key))
                .Select(x => Math.Min(x.index, Curve.Keys.Count - 2))
                .Distinct()
                .ToList();
        }
    }

    /// <summary>The index of the key that starts the segment of the last selected key, or -1.</summary>
    public int SelectedSegment
    {
        get
        {
            if (Curve == null || SelectedKey == null || Curve.Keys.Count < 2) return -1;
            var index = Curve.Keys.IndexOf(SelectedKey);
            return index < 0 ? -1 : Math.Min(index, Curve.Keys.Count - 2);
        }
    }

    /// <summary>Changes the value range so the whole curve is visible (and the time range to the first and last key).</summary>
    public void FitToCurve()
    {
        if (Curve == null || Curve.Keys.Count == 0) return;
        var samples = Curve.Sample(200);
        double min = double.MaxValue, max = double.MinValue;
        foreach (var point in samples)
        {
            min = Math.Min(min, point.Y);
            max = Math.Max(max, point.Y);
        }
        foreach (var key in Curve.Keys)
        {
            min = Math.Min(min, key.Value);
            max = Math.Max(max, key.Value);
        }
        var pad = Math.Max((max - min) * 0.12, 0.05);
        MinValue = min - pad;
        MaxValue = max + pad;

        var first = Curve.Keys[0].Time;
        var last = Curve.Keys[^1].Time;
        if (last > first)
        {
            MinTime = first;
            MaxTime = last;
        }
    }

    #endregion

    #region View

    /// <summary>Zooms both axes around the center of the graph. Ctrl + mouse wheel zooms around the pointer (Ctrl+Shift: time only, Ctrl+Alt: value only).</summary>
    public void ZoomIn() => ZoomAt(Center, 1.25, 1.25);

    public void ZoomOut() => ZoomAt(Center, 0.8, 0.8);

    /// <summary>Shows time 0 to 1 and values -0.25 to 1.25 again.</summary>
    public void ResetView()
    {
        MinTime = 0;
        MaxTime = 1;
        MinValue = -0.25;
        MaxValue = 1.25;
    }

    private Point Center => new(Plot.Left + Plot.Width / 2, Plot.Top + Plot.Height / 2);

    /// <summary>Zooms in (factor above 1) or out (below 1) keeping the graph point under <paramref name="anchor"/> where it is.</summary>
    internal void ZoomAt(Point anchor, double factorX, double factorY)
    {
        var (time, value) = FromScreen(anchor);
        var ratioX = (time - MinTime) / TimeSpan;
        var ratioY = (value - MinValue) / ValueSpan;
        var spanX = Math.Clamp(TimeSpan / factorX, MinSpan, MaxSpan);
        var spanY = Math.Clamp(ValueSpan / factorY, MinSpan, MaxSpan);
        var minTime = time - ratioX * spanX;
        var minValue = value - ratioY * spanY;
        MinTime = minTime;
        MaxTime = minTime + spanX;
        MinValue = minValue;
        MaxValue = minValue + spanY;
    }

    /// <summary>Moves the view by a distance in pixels (a drag with the middle button, or with Space held, does this).</summary>
    internal void PanBy(double dx, double dy)
    {
        var plot = Plot;
        var time = -dx / plot.Width * TimeSpan;
        var value = dy / plot.Height * ValueSpan;
        MinTime += time;
        MaxTime += time;
        MinValue += value;
        MaxValue += value;
    }

    internal void BeginPan(Point point)
    {
        _drag = DragKind.Pan;
        _panLast = point;
    }

    #endregion

    #region History

    /// <summary>Takes back the last edit made in the editor (also Ctrl+Z). Changes made to the curve from code are not recorded.</summary>
    public void Undo()
    {
        if (Curve == null || _undo.Count == 0) return;
        var current = CaptureHistory();
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        RestoreHistory(step);
        UpdateHistoryState();
    }

    /// <summary>Applies the edit that was taken back (also Ctrl+Y or Ctrl+Shift+Z).</summary>
    public void Redo()
    {
        if (Curve == null || _redo.Count == 0) return;
        var current = CaptureHistory();
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        RestoreHistory(step);
        UpdateHistoryState();
    }

    public void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
        UpdateHistoryState();
    }

    private Snapshot CaptureHistory()
    {
        var curve = Curve!;
        var selected = curve.Keys.Select((key, index) => (key, index)).Where(x => _selection.Contains(x.key)).Select(x => x.index).ToArray();
        return new Snapshot(curve.Capture(), selected, _primary == null ? -1 : curve.Keys.IndexOf(_primary));
    }

    private void RestoreHistory(Snapshot step)
    {
        var curve = Curve!;
        curve.Restore(step.Keys);
        var keys = step.Selected.Where(i => i < curve.Keys.Count).Select(i => curve.Keys[i]).ToList();
        Select(keys, step.Primary >= 0 && step.Primary < curve.Keys.Count ? curve.Keys[step.Primary] : null);
    }

    /// <summary>Runs an edit and records it as one undo step (when it changed something).</summary>
    private void Edit(Action change)
    {
        var before = Curve != null && IsUndoEnabled ? CaptureHistory() : null;
        change();
        if (before != null) Commit(before);
    }

    private void Commit(Snapshot before)
    {
        if (Curve == null || !IsUndoEnabled) return;
        if (before.Keys.AsSpan().SequenceEqual(Curve.Capture())) return;
        _undo.Add(before);
        while (_undo.Count > HistoryLimit) _undo.RemoveAt(0);
        _redo.Clear();
        UpdateHistoryState();
    }

    private void UpdateHistoryState()
    {
        SetValue(CanUndoPropertyKey, _undo.Count > 0);
        SetValue(CanRedoPropertyKey, _redo.Count > 0);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Geometry

    private Rect Plot => new(LeftMargin, TopMargin, Math.Max(1, ActualWidth - LeftMargin - RightMargin), Math.Max(1, ActualHeight - TopMargin - BottomMargin));

    private double TimeSpan => Math.Max(MaxTime - MinTime, 1e-9);

    private double ValueSpan => Math.Max(MaxValue - MinValue, 1e-9);

    internal Point ToScreen(double time, double value)
    {
        var plot = Plot;
        return new Point(plot.Left + (time - MinTime) / TimeSpan * plot.Width, plot.Bottom - (value - MinValue) / ValueSpan * plot.Height);
    }

    internal (double Time, double Value) FromScreen(Point point)
    {
        var plot = Plot;
        return (MinTime + (point.X - plot.Left) / plot.Width * TimeSpan, MinValue + (plot.Bottom - point.Y) / plot.Height * ValueSpan);
    }

    private double SnapTo(double value, double step) => IsSnappingEnabled && step > 0 ? Math.Round(value / step) * step : value;

    #endregion

    #region Curve subscription

    private void OnCurveChanged(AnimationCurve? oldCurve, AnimationCurve? newCurve)
    {
        if (oldCurve != null) oldCurve.Changed -= OnCurveEdited;
        _subscribed = newCurve;
        if (newCurve != null) newCurve.Changed += OnCurveEdited;
        ClearHistory();
        Select([], null);
        InvalidateVisual();
    }

    private void OnCurveEdited(object? sender, EventArgs e)
    {
        if (_selection.Count > 0 && _subscribed != null && _selection.Any(key => !_subscribed.Keys.Contains(key)))
        {
            Select(_selection.Where(_subscribed.Keys.Contains).ToList(), _primary != null && _subscribed.Keys.Contains(_primary) ? _primary : null);
        }
        InvalidateVisual();
        CurveChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Rendering

    private FormattedText Text(string text, Brush brush, double size = 10.5)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static double NiceStep(double range, int target)
    {
        var raw = range / Math.Max(1, target);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var nice = normalized < 1.5 ? 1 : normalized < 3.5 ? 2 : normalized < 7.5 ? 5 : 10;
        return nice * magnitude;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < LeftMargin + RightMargin + 20 || height < TopMargin + BottomMargin + 20) return;

        var plot = Plot;
        var border = BorderBrush ?? Brushes.Gray;
        var text = Foreground ?? Brushes.Gray;
        var curveBrush = CurveBrush;
        var background = Background ?? Brushes.Transparent;

        dc.DrawRoundedRectangle(background, new Pen(border, 1), new Rect(0.5, 0.5, width - 1, height - 1), 6, 6);

        // grid and axis labels
        var gridPen = new Pen(border, 1) { DashStyle = DashStyles.Solid };
        dc.PushOpacity(0.45);
        var timeStep = NiceStep(TimeSpan, Math.Max(2, (int)(plot.Width / 80)));
        for (var t = Math.Ceiling(MinTime / timeStep) * timeStep; t <= MaxTime + 1e-9; t += timeStep)
        {
            var x = ToScreen(t, 0).X;
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
        var valueStep = NiceStep(ValueSpan, Math.Max(2, (int)(plot.Height / 40)));
        for (var v = Math.Ceiling(MinValue / valueStep) * valueStep; v <= MaxValue + 1e-9; v += valueStep)
        {
            var y = ToScreen(0, v).Y;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
        dc.Pop();

        var format = string.IsNullOrEmpty(TextFormat) ? "0.##" : TextFormat;
        for (var t = Math.Ceiling(MinTime / timeStep) * timeStep; t <= MaxTime + 1e-9; t += timeStep)
        {
            var label = Text(t.ToString(format, CultureInfo.CurrentCulture), text);
            dc.DrawText(label, new Point(ToScreen(t, 0).X - label.Width / 2, plot.Bottom + 5));
        }
        for (var v = Math.Ceiling(MinValue / valueStep) * valueStep; v <= MaxValue + 1e-9; v += valueStep)
        {
            var label = Text(v.ToString(format, CultureInfo.CurrentCulture), text);
            dc.DrawText(label, new Point(plot.Left - label.Width - 7, ToScreen(0, v).Y - label.Height / 2));
        }

        // the zero and one lines are drawn a little stronger
        var strongPen = new Pen(border, 1.2);
        foreach (var v in new[] { 0d, 1d })
        {
            if (v < MinValue || v > MaxValue) continue;
            var y = ToScreen(0, v).Y;
            dc.DrawLine(strongPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        // ruler strip for the playhead
        dc.DrawRectangle(border, null, new Rect(plot.Left, 4, plot.Width, 1));

        dc.PushClip(new RectangleGeometry(new Rect(plot.Left - 1, plot.Top - 1, plot.Width + 2, plot.Height + 2)));
        DrawCurve(dc, curveBrush);
        dc.Pop();

        if (ShowPlayhead) DrawPlayhead(dc, plot, curveBrush, text);
        DrawKeys(dc, curveBrush, background, text);
    }

    private void DrawCurve(DrawingContext dc, Brush brush)
    {
        if (Curve is not { Keys.Count: > 0 } curve) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = curve.Keys[0];
            var plot = Plot;
            ctx.BeginFigure(new Point(plot.Left - 2, ToScreen(0, first.Value).Y), false, false);
            ctx.LineTo(ToScreen(first.Time, first.Value), true, true);

            for (var i = 0; i < curve.Keys.Count - 1; i++)
            {
                var from = curve.Keys[i];
                var to = curve.Keys[i + 1];
                switch (from.Interpolation)
                {
                    case CurveInterpolation.Step:
                        ctx.LineTo(ToScreen(to.Time, from.Value), true, true);
                        ctx.LineTo(ToScreen(to.Time, to.Value), true, true);
                        break;
                    case CurveInterpolation.Linear:
                        ctx.LineTo(ToScreen(to.Time, to.Value), true, true);
                        break;
                    default:
                        var (_, p1, p2, p3) = curve.Segment(i);
                        ctx.BezierTo(ToScreen(p1.X, p1.Y), ToScreen(p2.X, p2.Y), ToScreen(p3.X, p3.Y), true, true);
                        break;
                }
            }

            var last = curve.Keys[^1];
            ctx.LineTo(new Point(plot.Right + 2, ToScreen(0, last.Value).Y), true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(brush, 2.25) { LineJoin = PenLineJoin.Round }, geometry);
    }

    private void DrawPlayhead(DrawingContext dc, Rect plot, Brush brush, Brush text)
    {
        var time = Math.Clamp(CurrentTime, MinTime, MaxTime);
        var x = ToScreen(time, 0).X;
        dc.PushOpacity(0.8);
        dc.DrawLine(new Pen(brush, 1.2), new Point(x, 8), new Point(x, plot.Bottom));
        dc.Pop();

        var flag = new StreamGeometry();
        using (var ctx = flag.Open())
        {
            ctx.BeginFigure(new Point(x - 5, 5), true, true);
            ctx.LineTo(new Point(x + 5, 5), true, false);
            ctx.LineTo(new Point(x, 13), true, false);
        }
        flag.Freeze();
        dc.DrawGeometry(brush, null, flag);

        if (Curve is { Keys.Count: > 0 } curve)
        {
            var value = curve.Evaluate(time);
            var dot = ToScreen(time, value);
            if (dot.Y >= plot.Top && dot.Y <= plot.Bottom)
            {
                dc.DrawEllipse(brush, new Pen(Background ?? Brushes.White, 2), dot, 4, 4);
                var format = string.IsNullOrEmpty(TextFormat) ? "0.##" : TextFormat;
                var label = Text(value.ToString(format, CultureInfo.CurrentCulture), text);
                var labelX = dot.X + 10 + label.Width > plot.Right ? dot.X - 10 - label.Width : dot.X + 10;
                dc.DrawText(label, new Point(labelX, Math.Clamp(dot.Y - label.Height - 4, plot.Top, plot.Bottom - label.Height)));
            }
        }
    }

    private void DrawKeys(DrawingContext dc, Brush curveBrush, Brush background, Brush text)
    {
        if (Curve is not { } curve) return;
        var plot = Plot;
        var single = _selection.Count == 1 ? SelectedKey : null;
        var singleIndex = single == null ? -1 : curve.Keys.IndexOf(single);

        if (single != null && singleIndex >= 0 && IsEditable)
        {
            var center = ToScreen(single.Time, single.Value);
            var handlePen = new Pen(curveBrush, 1.2);
            if (singleIndex > 0)
            {
                var handle = HandlePoint(single, false);
                dc.DrawLine(handlePen, center, handle);
                dc.DrawEllipse(background, new Pen(curveBrush, 2), handle, HandleRadius, HandleRadius);
            }
            if (singleIndex < curve.Keys.Count - 1)
            {
                var handle = HandlePoint(single, true);
                dc.DrawLine(handlePen, center, handle);
                dc.DrawEllipse(background, new Pen(curveBrush, 2), handle, HandleRadius, HandleRadius);
            }
        }

        foreach (var key in curve.Keys)
        {
            var point = ToScreen(key.Time, key.Value);
            if (point.X < plot.Left - 8 || point.X > plot.Right + 8) continue;
            var isSelected = _selection.Contains(key);
            var radius = KeyRadius + (ReferenceEquals(key, _hoverKey) && _hoverKind == DragKind.Key ? 1.5 : 0);
            dc.DrawEllipse(isSelected ? curveBrush : background, new Pen(isSelected ? text : curveBrush, 2), point, radius, radius);
            if (isSelected && _selection.Count > 1 && ReferenceEquals(key, _primary))
            {
                dc.DrawEllipse(null, new Pen(curveBrush, 1), point, radius + 3, radius + 3);
            }
        }

        if (_primary != null && _drag is DragKind.Key or DragKind.InHandle or DragKind.OutHandle)
        {
            var format = string.IsNullOrEmpty(TextFormat) ? "0.##" : TextFormat;
            var point = ToScreen(_primary.Time, _primary.Value);
            var label = Text($"{_primary.Time.ToString(format, CultureInfo.CurrentCulture)}, {_primary.Value.ToString(format, CultureInfo.CurrentCulture)}", text);
            var x = Math.Clamp(point.X + 12, plot.Left, Math.Max(plot.Left, plot.Right - label.Width - 8));
            var y = Math.Clamp(point.Y - label.Height - 12, plot.Top, Math.Max(plot.Top, plot.Bottom - label.Height - 4));
            dc.DrawRoundedRectangle(background, new Pen(curveBrush, 1), new Rect(x - 4, y - 2, label.Width + 8, label.Height + 4), 3, 3);
            dc.DrawText(label, new Point(x, y));
        }

        if (_drag == DragKind.Marquee)
        {
            var box = new Rect(_marqueeStart, _marqueeEnd);
            dc.PushOpacity(0.15);
            dc.DrawRectangle(curveBrush, null, box);
            dc.Pop();
            dc.DrawRectangle(null, new Pen(curveBrush, 1) { DashStyle = DashStyles.Dash }, box);
        }
    }

    private Point HandlePoint(CurveKey key, bool outgoing) => outgoing
        ? ToScreen(key.Time + key.OutDx, key.Value + key.OutDy)
        : ToScreen(key.Time + key.InDx, key.Value + key.InDy);

    #endregion

    #region Input

    private (DragKind Kind, CurveKey? Key) HitTest(Point point)
    {
        if (Curve is not { } curve) return (DragKind.None, null);

        if (IsEditable && _selection.Count == 1 && SelectedKey is { } selected)
        {
            var index = curve.Keys.IndexOf(selected);
            if (index >= 0)
            {
                if (index < curve.Keys.Count - 1 && Distance(point, HandlePoint(selected, true)) <= HitRadius) return (DragKind.OutHandle, selected);
                if (index > 0 && Distance(point, HandlePoint(selected, false)) <= HitRadius) return (DragKind.InHandle, selected);
            }
        }

        CurveKey? best = null;
        var bestDistance = HitRadius + 2;
        foreach (var key in curve.Keys)
        {
            var distance = Distance(point, ToScreen(key.Time, key.Value));
            if (distance <= bestDistance)
            {
                best = key;
                bestDistance = distance;
            }
        }
        if (best != null) return (DragKind.Key, best);

        if (point.Y < TopMargin && point.X >= LeftMargin - 4 && point.X <= ActualWidth - RightMargin + 4) return (DragKind.Scrub, null);
        return (DragKind.None, null);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private void BeginEditDrag(DragKind kind, Point point)
    {
        _drag = kind;
        _before = IsUndoEnabled ? CaptureHistory() : null;
        _pressData = FromScreen(point);
        _origins = _selection.ToDictionary(key => key, key => (key.Time, key.Value));
    }

    /// <summary>Handles a press at <paramref name="point"/> (also called by tests). Returns true when a drag started.</summary>
    internal bool PointerDown(Point point, ModifierKeys modifiers, int clickCount)
    {
        if (Curve == null) return false;
        var (kind, key) = HitTest(point);

        if (kind == DragKind.Scrub)
        {
            _drag = DragKind.Scrub;
            ScrubTo(point);
            return true;
        }

        if (!IsEditable) return false;
        var additive = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;

        if (kind == DragKind.Key && key != null)
        {
            if (additive)
            {
                var set = new HashSet<CurveKey>(_selection);
                var added = !set.Remove(key);
                if (added) set.Add(key);
                Select(set, added ? key : null);
                if (!added) return false;
            }
            else if (!_selection.Contains(key))
            {
                SelectOnly(key);
            }
            else
            {
                Select(_selection.ToList(), key);
            }
            BeginEditDrag(DragKind.Key, point);
            return true;
        }

        if (kind is DragKind.InHandle or DragKind.OutHandle)
        {
            BeginEditDrag(kind, point);
            return true;
        }

        if (Plot.Contains(point) && clickCount >= 2)
        {
            var (time, value) = FromScreen(point);
            AddKey(SnapTo(Math.Clamp(time, MinTime, MaxTime), SnapTime), SnapTo(value, SnapValue));
            return false;
        }

        _marqueeBase = additive ? new HashSet<CurveKey>(_selection) : [];
        if (!additive) Select([], null);
        if (!Plot.Contains(point)) return false;

        _drag = DragKind.Marquee;
        _marqueeStart = _marqueeEnd = point;
        InvalidateVisual();
        return true;
    }

    internal void PointerMove(Point point)
    {
        if (Curve == null) return;

        switch (_drag)
        {
            case DragKind.None:
                var (kind, key) = HitTest(point);
                if (kind != _hoverKind || !ReferenceEquals(key, _hoverKey))
                {
                    _hoverKind = kind;
                    _hoverKey = key;
                    Cursor = kind is DragKind.Key or DragKind.InHandle or DragKind.OutHandle ? Cursors.Hand : null;
                    InvalidateVisual();
                }
                return;
            case DragKind.Scrub:
                ScrubTo(point);
                return;
            case DragKind.Pan:
                PanBy(point.X - _panLast.X, point.Y - _panLast.Y);
                _panLast = point;
                return;
            case DragKind.Marquee:
                _marqueeEnd = point;
                var box = new Rect(_marqueeStart, _marqueeEnd);
                Select(_marqueeBase.Concat(Curve.Keys.Where(k => box.Contains(ToScreen(k.Time, k.Value)))).ToList(), null);
                InvalidateVisual();
                return;
        }

        if (_primary is not { } selected) return;
        var (time, value) = FromScreen(point);

        if (_drag == DragKind.Key)
        {
            MoveSelection(selected, time, value);
            return;
        }

        var index = Curve.Keys.IndexOf(selected);
        if (index < 0) return;

        // a handle: stored relative to its key; the time offset stays inside the segment
        var outgoing = _drag == DragKind.OutHandle;
        var dx = SnapTo(time, SnapTime) - selected.Time;
        var dy = SnapTo(value, SnapValue) - selected.Value;
        if (outgoing)
        {
            var span = index < Curve.Keys.Count - 1 ? Curve.Keys[index + 1].Time - selected.Time : 0;
            selected.IsSmooth = false;
            selected.OutDx = Math.Clamp(dx, 0, span);
            selected.OutDy = dy;
        }
        else
        {
            var span = index > 0 ? selected.Time - Curve.Keys[index - 1].Time : 0;
            selected.IsSmooth = false;
            selected.InDx = Math.Clamp(dx, -span, 0);
            selected.InDy = dy;
        }
        if (index < Curve.Keys.Count - 1 && outgoing) selected.Interpolation = CurveInterpolation.Bezier;
        else if (index > 0 && !outgoing) Curve.Keys[index - 1].Interpolation = CurveInterpolation.Bezier;
    }

    /// <summary>Moves every selected key by the distance the primary one was dragged; keys cannot pass the unselected neighbors.</summary>
    private void MoveSelection(CurveKey primary, double time, double value)
    {
        if (Curve is not { } curve || !_origins.TryGetValue(primary, out var origin)) return;

        var dt = SnapTo(origin.Time + (time - _pressData.Time), SnapTime) - origin.Time;
        var dv = SnapTo(origin.Value + (value - _pressData.Value), SnapValue) - origin.Value;

        var keys = curve.Keys;
        var epsilon = TimeSpan * 0.001;
        double lowest = double.NegativeInfinity, highest = double.PositiveInfinity;
        double lowestValue = double.NegativeInfinity, highestValue = double.PositiveInfinity;
        for (var i = 0; i < keys.Count; i++)
        {
            if (!_origins.TryGetValue(keys[i], out var o)) continue;
            if (i > 0 && !_origins.ContainsKey(keys[i - 1])) lowest = Math.Max(lowest, keys[i - 1].Time + epsilon - o.Time);
            else if (i == 0) lowest = Math.Max(lowest, Math.Min(0, MinTime - o.Time));
            if (i < keys.Count - 1 && !_origins.ContainsKey(keys[i + 1])) highest = Math.Min(highest, keys[i + 1].Time - epsilon - o.Time);
            else if (i == keys.Count - 1) highest = Math.Min(highest, Math.Max(0, MaxTime - o.Time));
            lowestValue = Math.Max(lowestValue, Math.Min(0, MinValue - o.Value));
            highestValue = Math.Min(highestValue, Math.Max(0, MaxValue - o.Value));
        }
        if (highest < lowest) highest = lowest;
        if (highestValue < lowestValue) highestValue = lowestValue;
        dt = Math.Clamp(dt, lowest, highest);
        dv = Math.Clamp(dv, lowestValue, highestValue);

        var origins = _origins;
        curve.Batch(() =>
        {
            foreach (var (key, o) in origins)
            {
                key.Time = o.Time + dt;
                key.Value = o.Value + dv;
            }
        });
    }

    internal void PointerUp()
    {
        if (_drag == DragKind.None) return;
        var drag = _drag;
        _drag = DragKind.None;
        if (drag is DragKind.Key or DragKind.InHandle or DragKind.OutHandle && _before != null) Commit(_before);
        _before = null;
        _origins = [];
        InvalidateVisual();
    }

    private void ScrubTo(Point point)
    {
        var (time, _) = FromScreen(point);
        CurrentTime = Math.Clamp(SnapTo(time, SnapTime), MinTime, MaxTime);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var position = e.GetPosition(this);
        if (Keyboard.IsKeyDown(Key.Space))
        {
            BeginPan(position);
            CaptureMouse();
        }
        else if (PointerDown(position, Keyboard.Modifiers, e.ClickCount))
        {
            CaptureMouse();
        }
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Middle) return;
        Focus();
        BeginPan(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointerMove(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        PointerUp();
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Middle) return;
        PointerUp();
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        PointerUp();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Control) == 0) return; // without Ctrl the wheel scrolls the page the editor is in

        var factor = e.Delta > 0 ? 1.2 : 1 / 1.2;
        var timeOnly = (modifiers & ModifierKeys.Shift) != 0;
        var valueOnly = (modifiers & ModifierKeys.Alt) != 0;
        ZoomAt(e.GetPosition(this), valueOnly ? 1 : factor, timeOnly ? 1 : factor);
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverKind != DragKind.None)
        {
            _hoverKind = DragKind.None;
            _hoverKey = null;
            Cursor = null;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (e.Key == Key.Delete && DeleteSelectedKey()) e.Handled = true;
        else if (control && e.Key == Key.Z && !shift && IsEditable) { Undo(); e.Handled = true; }
        else if (control && (e.Key == Key.Y || (e.Key == Key.Z && shift)) && IsEditable) { Redo(); e.Handled = true; }
        else if (control && e.Key == Key.A && IsEditable) { SelectAll(); e.Handled = true; }
    }

    #endregion
}
