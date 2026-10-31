using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Tessel.NET.Mvvm;

namespace Tessel.NET.Controls;

/// <summary>How an <see cref="AnimationTimeline"/> labels time.</summary>
public enum TimelineTimeDisplay
{
    /// <summary>Frame numbers: 0, 12, 24, ... (pixel and frame-by-frame editors).</summary>
    Frames,

    /// <summary>Timecode: <c>00:02:12</c> (minutes, seconds, frames) or <c>1:00:02:12</c> with hours (video editors).</summary>
    Timecode,

    /// <summary>Seconds: <c>2.5s</c>.</summary>
    Seconds,
}

/// <summary>
/// A span on a track of an <see cref="AnimationTimeline"/>: a video or audio clip, a block of an effect, or a single cel/keyframe
/// (<see cref="Length"/> 1). Positions and lengths are in whole frames. Change <see cref="Start"/> and <see cref="Length"/> from code and the
/// timeline redraws.
/// </summary>
public sealed class AnimationClip : ObservableObject
{
    private string _name = string.Empty;
    private int _start;
    private int _length = 1;
    private Brush? _brush;
    private ImageSource? _thumbnail;
    private bool _isKeyframe;
    private bool _isSelected;
    private object? _tag;
    private IReadOnlyList<float>? _waveform;
    private int _mediaStart;
    private int _mediaLength;

    public AnimationClip()
    {
    }

    public AnimationClip(string name, int start, int length)
    {
        _name = name;
        _start = Math.Max(0, start);
        _length = Math.Max(1, length);
    }

    /// <summary>The label drawn inside the clip.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>The first frame of the clip (0 or more).</summary>
    public int Start
    {
        get => _start;
        set
        {
            if (SetProperty(ref _start, Math.Max(0, value))) OnPropertyChanged(nameof(End));
        }
    }

    /// <summary>The number of frames the clip covers (1 or more).</summary>
    public int Length
    {
        get => _length;
        set
        {
            if (SetProperty(ref _length, Math.Max(1, value))) OnPropertyChanged(nameof(End));
        }
    }

    /// <summary>The first frame after the clip (<see cref="Start"/> + <see cref="Length"/>).</summary>
    public int End => _start + _length;

    /// <summary>The fill of the clip. Null uses the color of its track.</summary>
    public Brush? Brush
    {
        get => _brush;
        set => SetProperty(ref _brush, value);
    }

    /// <summary>An image drawn at the left of the clip (a video frame, a waveform slice, ...).</summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>Draws the clip as a diamond at its first frame (an animation keyframe) instead of a bar.</summary>
    public bool IsKeyframe
    {
        get => _isKeyframe;
        set => SetProperty(ref _isKeyframe, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Your own data (the media file, the layer cel, ...).</summary>
    public object? Tag
    {
        get => _tag;
        set => SetProperty(ref _tag, value);
    }

    /// <summary>
    /// The audio waveform drawn inside the clip: peak amplitudes from 0 to 1, evenly spread over the media (see
    /// <see cref="AnimationWaveform"/> to create them from samples). Without <see cref="MediaLength"/> the peaks are stretched over the clip.
    /// </summary>
    public IReadOnlyList<float>? Waveform
    {
        get => _waveform;
        set => SetProperty(ref _waveform, value);
    }

    /// <summary>
    /// How many frames into the media the clip begins (0 or more). Trimming the left edge increases it, so the waveform scrolls with the
    /// trim instead of stretching. Only used when <see cref="MediaLength"/> is set.
    /// </summary>
    public int MediaStart
    {
        get => _mediaStart;
        set => SetProperty(ref _mediaStart, Math.Max(0, value));
    }

    /// <summary>
    /// The total length of the source media in frames (0 = unknown). When set, the clip cannot be trimmed beyond the start or the end
    /// of the media, and the <see cref="Waveform"/> covers the whole media so trimming reveals or hides parts of it.
    /// </summary>
    public int MediaLength
    {
        get => _mediaLength;
        set => SetProperty(ref _mediaLength, Math.Max(0, value));
    }

    public override string ToString() => $"{Name} [{Start}..{End})";
}

/// <summary>A row of an <see cref="AnimationTimeline"/>: a video/audio track, or a layer of a pixel editor.</summary>
public sealed class AnimationTrack : ObservableObject
{
    private string _name = string.Empty;
    private Brush? _brush;
    private double _height = 36;
    private bool _isVisible = true;
    private bool _isLocked;
    private object? _tag;
    private Brush? _displayBrush;

    public AnimationTrack()
    {
    }

    public AnimationTrack(string name) => _name = name;

    /// <summary>Shown in the track header.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>The default fill of the clips of this track. Null picks a color from a palette.</summary>
    public Brush? Brush
    {
        get => _brush;
        set => SetProperty(ref _brush, value);
    }

    /// <summary>The height of the row in device-independent pixels (18 or more).</summary>
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, Math.Max(18, value));
    }

    /// <summary>The "eye" toggle of the header. Hidden tracks are drawn faded; it is up to your renderer to skip them.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The "lock" toggle of the header. Clips of a locked track cannot be selected, moved or resized.</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    /// <summary>Your own data (the layer, the media bin entry, ...).</summary>
    public object? Tag
    {
        get => _tag;
        set => SetProperty(ref _tag, value);
    }

    /// <summary>The clips of the track (they may overlap).</summary>
    public ObservableCollection<AnimationClip> Clips { get; } = [];

    /// <summary>The brush the track really uses: <see cref="Brush"/> or a palette color. Set by the timeline, used by the header template.</summary>
    public Brush? DisplayBrush
    {
        get => _displayBrush;
        internal set => SetProperty(ref _displayBrush, value);
    }

    public override string ToString() => Name;
}

/// <summary>A named position on the ruler of an <see cref="AnimationTimeline"/> (a cue, a beat, a chapter).</summary>
public sealed class AnimationMarker : ObservableObject
{
    private int _frame;
    private string _name = string.Empty;
    private Brush? _brush;

    public AnimationMarker()
    {
    }

    public AnimationMarker(int frame, string name)
    {
        _frame = Math.Max(0, frame);
        _name = name;
    }

    public int Frame
    {
        get => _frame;
        set => SetProperty(ref _frame, Math.Max(0, value));
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>The color of the marker flag (accent by default).</summary>
    public Brush? Brush
    {
        get => _brush;
        set => SetProperty(ref _brush, value);
    }
}

/// <summary>What happened to a clip when the user moved or resized it.</summary>
public sealed class AnimationClipChange(
    AnimationClip clip,
    AnimationTrack oldTrack,
    AnimationTrack newTrack,
    int oldStart,
    int oldLength,
    int oldIndex = -1,
    int oldMediaStart = 0)
{
    public AnimationClip Clip { get; } = clip;

    public AnimationTrack OldTrack { get; } = oldTrack;

    /// <summary>The track the clip is on now (the same as <see cref="OldTrack"/> unless it was dragged to another track).</summary>
    public AnimationTrack NewTrack { get; } = newTrack;

    public int OldStart { get; } = oldStart;

    public int OldLength { get; } = oldLength;

    /// <summary>The position of the clip in <see cref="OldTrack"/>'s <see cref="AnimationTrack.Clips"/> before the change (-1 if unknown).</summary>
    public int OldIndex { get; } = oldIndex;

    /// <summary>The <see cref="AnimationClip.MediaStart"/> before the change.</summary>
    public int OldMediaStart { get; } = oldMediaStart;
}

public sealed class AnimationClipsChangedEventArgs(IReadOnlyList<AnimationClipChange> changes) : EventArgs
{
    public IReadOnlyList<AnimationClipChange> Changes { get; } = changes;
}

public sealed class AnimationClipsDeletingEventArgs(IReadOnlyList<AnimationClip> clips) : EventArgs
{
    public IReadOnlyList<AnimationClip> Clips { get; } = clips;

    /// <summary>Set to true to keep the clips (the timeline deletes them from their tracks otherwise).</summary>
    public bool Cancel { get; set; }
}

public sealed class AnimationClipEventArgs(AnimationClip clip, AnimationTrack track) : EventArgs
{
    public AnimationClip Clip { get; } = clip;

    public AnimationTrack Track { get; } = track;
}

/// <summary>Helpers that turn audio samples into the peak list drawn by <see cref="AnimationClip.Waveform"/>.</summary>
public static class AnimationWaveform
{
    /// <summary>Reduces floating point samples (-1 to 1) to <paramref name="bins"/> peaks (0 to 1).</summary>
    public static float[] FromSamples(IReadOnlyList<float> samples, int bins = 512)
    {
        bins = Math.Max(1, bins);
        var peaks = new float[bins];
        if (samples.Count == 0) return peaks;

        for (var bin = 0; bin < bins; bin++)
        {
            var from = (int)((long)bin * samples.Count / bins);
            var to = Math.Max(from + 1, (int)((long)(bin + 1) * samples.Count / bins));
            var peak = 0f;
            for (var i = from; i < to && i < samples.Count; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
            peaks[bin] = Math.Min(1f, peak);
        }
        return peaks;
    }

    /// <summary>Reduces 16-bit PCM samples to <paramref name="bins"/> peaks (0 to 1).</summary>
    public static float[] FromPcm16(IReadOnlyList<short> samples, int bins = 512)
    {
        var floats = new float[samples.Count];
        for (var i = 0; i < floats.Length; i++) floats[i] = samples[i] / 32768f;
        return FromSamples(floats, bins);
    }
}
