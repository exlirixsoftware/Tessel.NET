using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Tessel.NET.Mvvm;

namespace Tessel.NET.Controls;

/// <summary>How the value moves from a <see cref="CurveKey"/> to the next one.</summary>
public enum CurveInterpolation
{
    /// <summary>A straight line.</summary>
    Linear,

    /// <summary>Holds the value of the key until the next key, then jumps.</summary>
    Step,

    /// <summary>A cubic Bezier curve shaped by the handles of the two keys.</summary>
    Bezier,
}

/// <summary>Ready-made shapes for a segment (see <see cref="AnimationCurve.ApplyEasing"/>); they are the usual CSS cubic-bezier easings.</summary>
public enum CurveEasing
{
    Linear,
    Ease,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,
}

/// <summary>A point of an <see cref="AnimationCurve"/>: a value at a time, with the Bezier handles that leave and enter it.</summary>
public sealed class CurveKey : ObservableObject
{
    private double _time;
    private double _value;
    private CurveInterpolation _interpolation = CurveInterpolation.Bezier;
    private bool _isSmooth = true;
    private double _inDx;
    private double _inDy;
    private double _outDx;
    private double _outDy;

    public CurveKey()
    {
    }

    public CurveKey(double time, double value, CurveInterpolation interpolation = CurveInterpolation.Bezier)
    {
        _time = time;
        _value = value;
        _interpolation = interpolation;
    }

    public double Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>The shape of the segment that starts at this key.</summary>
    public CurveInterpolation Interpolation
    {
        get => _interpolation;
        set => SetProperty(ref _interpolation, value);
    }

    /// <summary>
    /// True: the handles are computed from the neighboring keys (flat at peaks and at the ends, so the curve never overshoots). Dragging a
    /// handle, or applying an easing, makes the handles manual (false).
    /// </summary>
    public bool IsSmooth
    {
        get => _isSmooth;
        set => SetProperty(ref _isSmooth, value);
    }

    /// <summary>The handle that enters this key, relative to it (the time offset is zero or negative).</summary>
    public double InDx
    {
        get => _inDx;
        set => SetProperty(ref _inDx, value);
    }

    public double InDy
    {
        get => _inDy;
        set => SetProperty(ref _inDy, value);
    }

    /// <summary>The handle that leaves this key, relative to it (the time offset is zero or positive).</summary>
    public double OutDx
    {
        get => _outDx;
        set => SetProperty(ref _outDx, value);
    }

    public double OutDy
    {
        get => _outDy;
        set => SetProperty(ref _outDy, value);
    }

    public override string ToString() => $"({Time:0.###}, {Value:0.###})";
}

/// <summary>
/// A value that changes over time, described by <see cref="Keys"/>: an easing curve for a UI animation, a volume envelope, a motion path.
/// <see cref="Evaluate"/> gives the value at any time. Edit it with a <see cref="CurveEditor"/> or in code; <see cref="Changed"/> is raised
/// after every change.
/// </summary>
public sealed class AnimationCurve
{
    private bool _updating;

    public AnimationCurve()
    {
        Keys.CollectionChanged += OnKeysChanged;
    }

    /// <summary>The keys in time order. Use <see cref="AddKey"/> to insert one in the right place.</summary>
    public ObservableCollection<CurveKey> Keys { get; } = [];

    /// <summary>Raised after the keys or one of their values changed.</summary>
    public event EventHandler? Changed;

    /// <summary>A smooth curve from (0, 0) to (1, 1).</summary>
    public static AnimationCurve CreateDefault()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, 0);
        curve.AddKey(1, 1);
        return curve;
    }

    /// <summary>A curve made from one of the easing presets, from (0, 0) to (1, 1).</summary>
    public static AnimationCurve FromEasing(CurveEasing easing)
    {
        var curve = CreateDefault();
        curve.ApplyEasing(0, easing);
        return curve;
    }

    /// <summary>Adds a key at <paramref name="time"/> (or returns the key already there) and keeps the keys sorted.</summary>
    public CurveKey AddKey(double time, double value, CurveInterpolation interpolation = CurveInterpolation.Bezier)
    {
        foreach (var existing in Keys)
        {
            if (Math.Abs(existing.Time - time) < 1e-9) return existing;
        }

        var key = new CurveKey(time, value, interpolation);
        var index = 0;
        while (index < Keys.Count && Keys[index].Time < time) index++;
        Keys.Insert(index, key);
        return key;
    }

    public bool RemoveKey(CurveKey key) => Keys.Remove(key);

    /// <summary>The value at <paramref name="time"/>. Before the first key and after the last one the curve is flat.</summary>
    public double Evaluate(double time)
    {
        var count = Keys.Count;
        if (count == 0) return 0;
        if (time <= Keys[0].Time) return Keys[0].Value;
        if (time >= Keys[count - 1].Time) return Keys[count - 1].Value;

        var i = 0;
        while (i < count - 2 && time >= Keys[i + 1].Time) i++;
        var from = Keys[i];
        var to = Keys[i + 1];

        switch (from.Interpolation)
        {
            case CurveInterpolation.Step:
                return from.Value;
            case CurveInterpolation.Linear:
                return from.Value + (to.Value - from.Value) * (time - from.Time) / (to.Time - from.Time);
        }

        var (p0, p1, p2, p3) = Segment(i);
        double lo = 0, hi = 1;
        for (var iteration = 0; iteration < 40; iteration++)
        {
            var mid = (lo + hi) / 2;
            if (Bezier(p0.X, p1.X, p2.X, p3.X, mid) < time) lo = mid;
            else hi = mid;
        }
        return Bezier(p0.Y, p1.Y, p2.Y, p3.Y, (lo + hi) / 2);
    }

    /// <summary>The four Bezier points of the segment from key <paramref name="index"/> to the next one (handles limited to the segment).</summary>
    internal (Point P0, Point P1, Point P2, Point P3) Segment(int index)
    {
        var from = Keys[index];
        var to = Keys[index + 1];
        var span = to.Time - from.Time;
        var outDx = Math.Clamp(from.OutDx, 0, span);
        var inDx = Math.Clamp(to.InDx, -span, 0);
        return (
            new Point(from.Time, from.Value),
            new Point(from.Time + outDx, from.Value + from.OutDy),
            new Point(to.Time + inDx, to.Value + to.InDy),
            new Point(to.Time, to.Value));
    }

    private static double Bezier(double a, double b, double c, double d, double s)
    {
        var u = 1 - s;
        return u * u * u * a + 3 * u * u * s * b + 3 * u * s * s * c + s * s * s * d;
    }

    /// <summary>Gives the segment that starts at key <paramref name="segment"/> one of the preset shapes (and manual handles).</summary>
    public void ApplyEasing(int segment, CurveEasing easing)
    {
        if (segment < 0 || segment >= Keys.Count - 1) throw new ArgumentOutOfRangeException(nameof(segment));
        var from = Keys[segment];
        var to = Keys[segment + 1];

        var wasUpdating = _updating;
        _updating = true;
        try
        {
            from.IsSmooth = false;
            to.IsSmooth = false;
            if (easing == CurveEasing.Linear)
            {
                from.Interpolation = CurveInterpolation.Linear;
                from.OutDx = (to.Time - from.Time) / 3;
                from.OutDy = (to.Value - from.Value) / 3;
                to.InDx = -(to.Time - from.Time) / 3;
                to.InDy = -(to.Value - from.Value) / 3;
            }
            else
            {
                var (x1, y1, x2, y2) = Parameters(easing);
                var span = to.Time - from.Time;
                var rise = to.Value - from.Value;
                from.Interpolation = CurveInterpolation.Bezier;
                from.OutDx = x1 * span;
                from.OutDy = y1 * rise;
                to.InDx = (x2 - 1) * span;
                to.InDy = (y2 - 1) * rise;
            }
        }
        finally
        {
            _updating = wasUpdating;
        }
        Refresh();
    }

    private static (double X1, double Y1, double X2, double Y2) Parameters(CurveEasing easing) => easing switch
    {
        CurveEasing.Ease => (0.25, 0.1, 0.25, 1),
        CurveEasing.EaseIn => (0.42, 0, 1, 1),
        CurveEasing.EaseOut => (0, 0, 0.58, 1),
        CurveEasing.EaseInOut => (0.42, 0, 0.58, 1),
        CurveEasing.EaseInBack => (0.36, 0, 0.66, -0.56),
        CurveEasing.EaseOutBack => (0.34, 1.56, 0.64, 1),
        CurveEasing.EaseInOutBack => (0.68, -0.6, 0.32, 1.6),
        _ => (1d / 3, 1d / 3, 2d / 3, 2d / 3),
    };

    /// <summary>Recalculates the automatic handles and raises <see cref="Changed"/> (called after every edit).</summary>
    public void Refresh()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            SortKeys();
            for (var i = 0; i < Keys.Count; i++)
            {
                if (Keys[i].IsSmooth) UpdateSmoothHandles(i);
            }
        }
        finally
        {
            _updating = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SortKeys()
    {
        for (var i = 1; i < Keys.Count; i++)
        {
            var j = i;
            while (j > 0 && Keys[j - 1].Time > Keys[j].Time)
            {
                Keys.Move(j, j - 1);
                j--;
            }
        }
    }

    private void UpdateSmoothHandles(int i)
    {
        var key = Keys[i];
        var hasPrevious = i > 0;
        var hasNext = i < Keys.Count - 1;
        var previous = hasPrevious ? Keys[i - 1] : null;
        var next = hasNext ? Keys[i + 1] : null;

        var slope = 0d;
        if (previous != null && next != null)
        {
            var peak = (key.Value - previous.Value) * (next.Value - key.Value) <= 0;
            if (!peak && next.Time > previous.Time) slope = (next.Value - previous.Value) / (next.Time - previous.Time);
        }

        var outDx = next != null ? (next.Time - key.Time) / 3 : 0;
        var inDx = previous != null ? (key.Time - previous.Time) / 3 : 0;
        key.OutDx = outDx;
        key.OutDy = slope * outDx;
        key.InDx = -inDx;
        key.InDy = -slope * inDx;
    }

    private void OnKeysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CurveKey key in e.OldItems) key.PropertyChanged -= OnKeyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (CurveKey key in e.NewItems) key.PropertyChanged += OnKeyChanged;
        }
        if (e.Action == NotifyCollectionChangedAction.Move && _updating) return;
        Refresh();
    }

    private void OnKeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updating) Refresh();
    }

    /// <summary>A key and everything that defines it, for undo and redo (the key object itself is kept, so references to it stay valid).</summary>
    internal readonly record struct KeyState(CurveKey Key, double Time, double Value, CurveInterpolation Interpolation, bool IsSmooth, double InDx, double InDy, double OutDx, double OutDy);

    internal KeyState[] Capture()
        => Keys.Select(k => new KeyState(k, k.Time, k.Value, k.Interpolation, k.IsSmooth, k.InDx, k.InDy, k.OutDx, k.OutDy)).ToArray();

    /// <summary>Puts the curve back into a captured state: the same key objects, in the same order, with the same values.</summary>
    internal void Restore(KeyState[] states)
    {
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            if (!Keys.SequenceEqual(states.Select(state => state.Key)))
            {
                while (Keys.Count > 0) Keys.RemoveAt(Keys.Count - 1);
                foreach (var state in states) Keys.Add(state.Key);
            }
            foreach (var state in states)
            {
                var key = state.Key;
                key.Time = state.Time;
                key.Value = state.Value;
                key.Interpolation = state.Interpolation;
                key.IsSmooth = state.IsSmooth;
                key.InDx = state.InDx;
                key.InDy = state.InDy;
                key.OutDx = state.OutDx;
                key.OutDy = state.OutDy;
            }
        }
        finally
        {
            _updating = wasUpdating;
        }
        Refresh();
    }

    /// <summary>Runs several edits and recalculates the handles and raises <see cref="Changed"/> once at the end.</summary>
    internal void Batch(Action edits)
    {
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            edits();
        }
        finally
        {
            _updating = wasUpdating;
        }
        Refresh();
    }

    /// <summary>The curve as points, for drawing or exporting: <paramref name="samples"/> values from the first to the last key.</summary>
    public IReadOnlyList<Point> Sample(int samples = 64)
    {
        samples = Math.Max(2, samples);
        var points = new List<Point>(samples);
        if (Keys.Count == 0) return points;
        var start = Keys[0].Time;
        var end = Keys[^1].Time;
        for (var i = 0; i < samples; i++)
        {
            var time = start + (end - start) * i / (samples - 1);
            points.Add(new Point(time, Evaluate(time)));
        }
        return points;
    }
}
