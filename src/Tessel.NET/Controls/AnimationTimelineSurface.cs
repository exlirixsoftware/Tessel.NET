using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>
/// The drawing and mouse-handling surface of <see cref="AnimationTimeline"/> (ruler, tracks, clips, playhead). It is only
/// public so the control template can create it; use <see cref="AnimationTimeline"/> instead.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AnimationTimelineSurface : FrameworkElement
{
    private const double EdgeGrip = 6;
    private const double SnapPixels = 8;

    private enum DragMode
    {
        None,
        Scrub,
        Loop,
        Move,
        ResizeStart,
        ResizeEnd,
        Marquee,
    }

    private enum Region
    {
        Empty,
        Ruler,
        Marker,
        Clip,
        ClipStart,
        ClipEnd,
    }

    private readonly record struct Hit(Region Region, int TrackIndex, AnimationClip? Clip, AnimationMarker? Marker);

    private readonly record struct Origin(AnimationClip Clip, AnimationTrack Track, int TrackIndex, int Start, int Length);

    private DragMode _mode;
    private Point _downPoint;
    private int _loopAnchor;
    private bool _dragged;
    private bool _pressed;
    private bool _resumePlaybackOnRelease;
    private bool _deselectOnRelease;
    private bool _selectOnlyOnRelease;
    private AnimationClip? _primary;
    private List<Origin> _origins = [];
    private int _deltaFrames;
    private int _deltaTracks;
    private int _resizeStart;
    private int _resizeLength;
    private int? _snapFrame;
    private AnimationClip? _hover;
    private Point _marqueeStart;
    private Rect? _marquee;
    private bool _marqueeAdditive;
    private HashSet<AnimationClip> _baseSelection = [];

    public AnimationTimelineSurface()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Focusable = false;
    }

    internal AnimationTimeline? Owner { get; set; }

    #region Geometry helpers

    private double Zoom => Owner?.PixelsPerFrame ?? 1;

    private double FrameToX(double frame) => frame * Zoom - (Owner?.HorizontalOffset ?? 0);

    private int FrameAt(double x) => (int)Math.Round((x + (Owner?.HorizontalOffset ?? 0)) / Zoom);

    private double TrackTop(int index)
    {
        var o = Owner!;
        var y = AnimationTimeline.RulerHeight - o.VerticalOffset;
        for (var i = 0; i < index && i < o.Tracks.Count; i++) y += o.Tracks[i].Height;
        return y;
    }

    /// <summary>The track row at a vertical position, or -1 (above the first / below the last track).</summary>
    private int TrackIndexAt(double y)
    {
        var o = Owner!;
        var top = AnimationTimeline.RulerHeight - o.VerticalOffset;
        for (var i = 0; i < o.Tracks.Count; i++)
        {
            var height = o.Tracks[i].Height;
            if (y >= top && y < top + height) return i;
            top += height;
        }
        return -1;
    }

    private Rect ClipRect(int trackIndex, int start, int length)
    {
        var o = Owner!;
        var track = o.Tracks[trackIndex];
        var top = TrackTop(trackIndex);
        var x = FrameToX(start);
        var width = Math.Max(3, length * Zoom);
        return new Rect(x, top + 3, width, track.Height - 7);
    }

    private Hit HitTest(Point p)
    {
        var o = Owner!;
        if (p.Y < AnimationTimeline.RulerHeight)
        {
            if (p.Y > AnimationTimeline.RulerHeight / 2)
            {
                foreach (var marker in o.Markers)
                {
                    if (Math.Abs(FrameToX(marker.Frame) - p.X) <= 6) return new Hit(Region.Marker, -1, null, marker);
                }
            }
            return new Hit(Region.Ruler, -1, null, null);
        }

        var index = TrackIndexAt(p.Y);
        if (index < 0) return new Hit(Region.Empty, -1, null, null);

        var track = o.Tracks[index];
        if (track.IsLocked) return new Hit(Region.Empty, index, null, null);

        // Later clips are drawn on top, so test them first.
        for (var i = track.Clips.Count - 1; i >= 0; i--)
        {
            var clip = track.Clips[i];
            var rect = ClipRect(index, clip.Start, clip.Length);
            if (clip.IsKeyframe)
            {
                var center = rect.Left + Zoom / 2;
                if (Math.Abs(p.X - center) <= 8 && p.Y >= rect.Top && p.Y <= rect.Bottom) return new Hit(Region.Clip, index, clip, null);
                continue;
            }
            if (!rect.Contains(p)) continue;

            var grip = Math.Min(EdgeGrip, rect.Width / 3);
            if (p.X - rect.Left <= grip) return new Hit(Region.ClipStart, index, clip, null);
            if (rect.Right - p.X <= grip) return new Hit(Region.ClipEnd, index, clip, null);
            return new Hit(Region.Clip, index, clip, null);
        }
        return new Hit(Region.Empty, index, null, null);
    }

    #endregion

    #region Rendering

    private sealed class Look
    {
        public Brush Surface = Brushes.White;
        public Brush SurfaceAlt = Brushes.WhiteSmoke;
        public Brush Subtle = Brushes.LightGray;
        public Brush Border = Brushes.LightGray;
        public Brush BorderStrong = Brushes.Gray;
        public Brush Text = Brushes.Black;
        public Brush TextSecondary = Brushes.DimGray;
        public Brush TextTertiary = Brushes.Gray;
        public Brush Accent = Brushes.RoyalBlue;
        public Brush Danger = Brushes.Red;
    }

    private Look LoadColors()
    {
        Brush Get(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
        return new Look
        {
            Surface = Get("Tessel.SurfaceBrush", Brushes.White),
            SurfaceAlt = Get("Tessel.SurfaceAltBrush", Brushes.WhiteSmoke),
            Subtle = Get("Tessel.SubtleBrush", Brushes.LightGray),
            Border = Get("Tessel.BorderBrush", Brushes.LightGray),
            BorderStrong = Get("Tessel.BorderStrongBrush", Brushes.Gray),
            Text = Get("Tessel.TextBrush", Brushes.Black),
            TextSecondary = Get("Tessel.TextSecondaryBrush", Brushes.DimGray),
            TextTertiary = Get("Tessel.TextTertiaryBrush", Brushes.Gray),
            Accent = Get("Tessel.AccentBrush", Brushes.RoyalBlue),
            Danger = Get("Tessel.DangerBrush", Brushes.Red),
        };
    }

    private FormattedText Text(string text, Brush brush, double size, bool bold = false, double maxWidth = double.PositiveInfinity)
    {
        var family = Owner?.FontFamily ?? new FontFamily("Segoe UI");
        var typeface = new Typeface(family, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (!double.IsInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(1, maxWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        return formatted;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var o = Owner;
        double width = ActualWidth, height = ActualHeight;
        if (o == null || width <= 0 || height <= 0) return;

        var c = LoadColors();
        var zoom = o.PixelsPerFrame;
        var ruler = AnimationTimeline.RulerHeight;
        var firstFrame = (int)Math.Floor(o.HorizontalOffset / zoom);
        var lastFrame = (int)Math.Ceiling((o.HorizontalOffset + width) / zoom);
        var (major, minor) = o.GetTickSteps();

        dc.DrawRectangle(c.Surface, null, new Rect(0, 0, width, height));

        // Rows
        var y = ruler - o.VerticalOffset;
        for (var i = 0; i < o.Tracks.Count; i++)
        {
            var track = o.Tracks[i];
            if (y + track.Height >= ruler && y <= height)
            {
                if (i % 2 == 1)
                {
                    dc.PushOpacity(0.5);
                    dc.DrawRectangle(c.SurfaceAlt, null, new Rect(0, y, width, track.Height));
                    dc.Pop();
                }
                dc.DrawRectangle(c.Border, null, new Rect(0, y + track.Height - 1, width, 1));
            }
            y += track.Height;
        }
        var tracksBottom = Math.Max(y, ruler);

        // Everything after the end of the project
        var endX = FrameToX(o.Duration);
        if (endX < width)
        {
            dc.PushOpacity(0.55);
            dc.DrawRectangle(c.Subtle, null, new Rect(Math.Max(0, endX), ruler, width - Math.Max(0, endX), height - ruler));
            dc.Pop();
            dc.DrawRectangle(c.BorderStrong, null, new Rect(Math.Round(endX), ruler, 1, height - ruler));
        }

        // Vertical grid lines: every frame when zoomed in, then minor and major ticks
        if (zoom >= 14)
        {
            dc.PushOpacity(0.22);
            for (var f = firstFrame; f <= lastFrame; f++) dc.DrawRectangle(c.Border, null, new Rect(Math.Round(FrameToX(f)), ruler, 1, height - ruler));
            dc.Pop();
        }
        dc.PushOpacity(0.45);
        for (var f = firstFrame - firstFrame % minor; f <= lastFrame; f += minor)
        {
            if (f < 0 || f % major == 0) continue;
            dc.DrawRectangle(c.Border, null, new Rect(Math.Round(FrameToX(f)), ruler, 1, height - ruler));
        }
        dc.Pop();
        dc.PushOpacity(0.9);
        for (var f = firstFrame - firstFrame % major; f <= lastFrame; f += major)
        {
            if (f >= 0) dc.DrawRectangle(c.Border, null, new Rect(Math.Round(FrameToX(f)), ruler, 1, height - ruler));
        }
        dc.Pop();

        // Loop region over the tracks
        if (o.LoopRegion is { } loop)
        {
            var x0 = FrameToX(loop.Start);
            var x1 = FrameToX(loop.End);
            dc.PushOpacity(o.IsLoopEnabled ? 0.10 : 0.05);
            dc.DrawRectangle(c.Accent, null, new Rect(x0, ruler, Math.Max(0, x1 - x0), height - ruler));
            dc.Pop();
        }

        // Clips
        DrawClips(dc, c, o, width, height);

        // Rubber band
        if (_mode == DragMode.Marquee && _marquee is { } band)
        {
            var rect = new Rect(band.X - o.HorizontalOffset, band.Y + ruler - o.VerticalOffset, band.Width, band.Height);
            dc.PushOpacity(0.14);
            dc.DrawRectangle(c.Accent, null, rect);
            dc.Pop();
            dc.DrawRectangle(null, new Pen(c.Accent, 1), rect);
        }

        // Ruler (drawn over the tracks so clips scroll underneath it)
        DrawRuler(dc, c, o, width, firstFrame, lastFrame, major, minor);

        // Snap guide
        if (_snapFrame is { } snap)
        {
            var x = Math.Round(FrameToX(snap)) + 0.5;
            var pen = new Pen(c.Accent, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(pen, new Point(x, ruler), new Point(x, height));
        }

        // Playhead
        var playheadX = FrameToX(o.CurrentFrame);
        if (playheadX >= -8 && playheadX <= width + 8)
        {
            var px = Math.Round(playheadX);
            dc.DrawRectangle(c.Accent, null, new Rect(px - 0.75, ruler - 2, 1.5, height - ruler + 2));
            var handle = new StreamGeometry();
            using (var ctx = handle.Open())
            {
                ctx.BeginFigure(new Point(px - 6.5, ruler - 14), true, true);
                ctx.LineTo(new Point(px + 6.5, ruler - 14), true, false);
                ctx.LineTo(new Point(px + 6.5, ruler - 7), true, false);
                ctx.LineTo(new Point(px, ruler - 1), true, false);
                ctx.LineTo(new Point(px - 6.5, ruler - 7), true, false);
            }
            handle.Freeze();
            dc.DrawGeometry(c.Accent, null, handle);
        }
    }

    private void DrawRuler(DrawingContext dc, Look c, AnimationTimeline o, double width, int firstFrame, int lastFrame, int major, int minor)
    {
        var ruler = AnimationTimeline.RulerHeight;
        dc.DrawRectangle(c.SurfaceAlt, null, new Rect(0, 0, width, ruler));
        dc.DrawRectangle(c.BorderStrong, null, new Rect(0, ruler - 1, width, 1));

        var zoom = o.PixelsPerFrame;
        if (zoom >= 14)
        {
            dc.PushOpacity(0.5);
            for (var f = Math.Max(0, firstFrame); f <= lastFrame; f++) dc.DrawRectangle(c.TextTertiary, null, new Rect(Math.Round(FrameToX(f)), ruler - 4, 1, 3));
            dc.Pop();
        }
        for (var f = Math.Max(0, firstFrame - firstFrame % minor); f <= lastFrame; f += minor)
        {
            var isMajor = f % major == 0;
            var x = Math.Round(FrameToX(f));
            dc.DrawRectangle(isMajor ? c.TextSecondary : c.TextTertiary, null, new Rect(x, ruler - (isMajor ? 9 : 5), 1, isMajor ? 8 : 4));
            if (isMajor && x > -60 && x < width)
            {
                var label = Text(o.FormatTime(f), c.TextSecondary, 11);
                dc.DrawText(label, new Point(x + 4, 4));
            }
        }

        // Loop region strip
        if (o.LoopRegion is { } loop)
        {
            var x0 = FrameToX(loop.Start);
            var x1 = FrameToX(loop.End);
            dc.PushOpacity(o.IsLoopEnabled ? 0.45 : 0.2);
            dc.DrawRectangle(c.Accent, null, new Rect(x0, ruler - 6, Math.Max(0, x1 - x0), 5));
            dc.Pop();
        }

        // Markers (a name is only drawn when it fits before the next marker)
        var ordered = o.Markers.OrderBy(m => m.Frame).ToList();
        for (var m = 0; m < ordered.Count; m++)
        {
            var marker = ordered[m];
            var x = Math.Round(FrameToX(marker.Frame));
            var room = m + 1 < ordered.Count ? FrameToX(ordered[m + 1].Frame) - x - 14 : 140;
            if (x < -80 || x > width + 8) continue;
            var brush = marker.Brush ?? c.Danger;
            var flag = new StreamGeometry();
            using (var ctx = flag.Open())
            {
                ctx.BeginFigure(new Point(x - 4.5, ruler - 20), true, true);
                ctx.LineTo(new Point(x + 4.5, ruler - 20), true, false);
                ctx.LineTo(new Point(x + 4.5, ruler - 14), true, false);
                ctx.LineTo(new Point(x, ruler - 9), true, false);
                ctx.LineTo(new Point(x - 4.5, ruler - 14), true, false);
            }
            flag.Freeze();
            dc.DrawGeometry(brush, null, flag);
            if (!string.IsNullOrEmpty(marker.Name) && room >= 28)
            {
                dc.DrawText(Text(marker.Name, c.Text, 10, maxWidth: Math.Min(140, room)), new Point(x + 7, ruler - 21));
            }
        }
    }

    private void DrawClips(DrawingContext dc, Look c, AnimationTimeline o, double width, double height)
    {
        var ruler = AnimationTimeline.RulerHeight;
        var dragging = _mode is DragMode.Move or DragMode.ResizeStart or DragMode.ResizeEnd && _dragged;

        for (var i = 0; i < o.Tracks.Count; i++)
        {
            var track = o.Tracks[i];
            var top = TrackTop(i);
            if (top + track.Height < ruler || top > height) continue;

            var fallback = track.DisplayBrush ?? track.Brush ?? AnimationTimeline.PaletteBrush(i);
            var opacity = !track.IsVisible ? 0.4 : track.IsLocked ? 0.7 : 1.0;

            foreach (var clip in track.Clips)
            {
                var rect = ClipRect(i, clip.Start, clip.Length);
                var visibleRect = clip.IsKeyframe ? new Rect(rect.Left - 8, rect.Top, Zoom + 16, rect.Height) : rect;
                if (visibleRect.Right < 0 || visibleRect.Left > width) continue;

                var isBeingDragged = dragging && _origins.Any(origin => origin.Clip == clip);
                dc.PushOpacity(isBeingDragged ? opacity * 0.35 : opacity);
                DrawClip(dc, c, clip, rect, clip.Brush ?? fallback, clip.IsSelected, ReferenceEquals(clip, _hover) && !dragging);
                dc.Pop();
            }
        }

        if (!dragging) return;

        // The clips being dragged, at their provisional position
        foreach (var origin in _origins)
        {
            if (_mode == DragMode.Move)
            {
                var index = Math.Clamp(origin.TrackIndex + _deltaTracks, 0, o.Tracks.Count - 1);
                var track = o.Tracks[index];
                var rect = ClipRect(index, Math.Max(0, origin.Start + _deltaFrames), origin.Length);
                var fallback = track.DisplayBrush ?? track.Brush ?? AnimationTimeline.PaletteBrush(index);
                DrawClip(dc, c, origin.Clip, rect, origin.Clip.Brush ?? fallback, true, false);
            }
            else
            {
                var rect = ClipRect(origin.TrackIndex, _resizeStart, _resizeLength);
                var track = o.Tracks[origin.TrackIndex];
                var fallback = track.DisplayBrush ?? track.Brush ?? AnimationTimeline.PaletteBrush(origin.TrackIndex);
                var media = _mode == DragMode.ResizeStart && origin.Clip.MediaLength > 0 ? origin.Clip.MediaStart + (_resizeStart - origin.Start) : origin.Clip.MediaStart;
                DrawClip(dc, c, origin.Clip, rect, origin.Clip.Brush ?? fallback, true, false, _resizeLength, media);
            }
        }
    }

    private void DrawClip(DrawingContext dc, Look c, AnimationClip clip, Rect rect, Brush brush, bool selected, bool hover, int? length = null, int? mediaStart = null)
    {
        if (clip.IsKeyframe)
        {
            var center = new Point(rect.Left + Zoom / 2, rect.Top + rect.Height / 2);
            var size = Math.Min(8.5, rect.Height / 2);
            var diamond = new StreamGeometry();
            using (var ctx = diamond.Open())
            {
                ctx.BeginFigure(new Point(center.X, center.Y - size), true, true);
                ctx.LineTo(new Point(center.X + size, center.Y), true, false);
                ctx.LineTo(new Point(center.X, center.Y + size), true, false);
                ctx.LineTo(new Point(center.X - size, center.Y), true, false);
            }
            diamond.Freeze();
            dc.DrawGeometry(brush, new Pen(selected ? c.Text : c.Surface, selected ? 2 : 1.5), diamond);
            return;
        }

        var rounded = new RectangleGeometry(rect, 4, 4);
        dc.DrawGeometry(brush, null, rounded);
        if (hover && !selected)
        {
            dc.PushOpacity(0.18);
            dc.DrawGeometry(Brushes.White, null, rounded);
            dc.Pop();
        }

        var textLeft = rect.Left + 7;
        dc.PushClip(rounded);
        if (clip.Waveform is { Count: > 0 }) DrawWaveform(dc, clip, rect, length ?? clip.Length, mediaStart ?? clip.MediaStart);
        if (clip.Thumbnail != null && rect.Width > rect.Height + 12)
        {
            dc.DrawImage(clip.Thumbnail, new Rect(rect.Left, rect.Top, rect.Height, rect.Height));
            textLeft = rect.Left + rect.Height + 6;
        }
        if (!string.IsNullOrEmpty(clip.Name) && rect.Right - textLeft > 18)
        {
            var label = Text(clip.Name, Brushes.White, 11.5, bold: true, maxWidth: rect.Right - textLeft - 6);
            var labelTop = rect.Top + Math.Max(2, (rect.Height - label.Height) / 2);
            if (clip.Waveform is { Count: > 0 })
            {
                // keep the name readable on top of the waveform bars
                dc.DrawRoundedRectangle(PlateBrush, null, new Rect(textLeft - 4, labelTop - 1, label.Width + 8, label.Height + 2), 3, 3);
            }
            dc.DrawText(label, new Point(textLeft, labelTop));
        }
        if ((selected || hover) && rect.Width > 24)
        {
            var grip = new Pen(Brushes.White, 2);
            dc.PushOpacity(0.75);
            dc.DrawLine(grip, new Point(rect.Left + 3, rect.Top + rect.Height / 2 - 5), new Point(rect.Left + 3, rect.Top + rect.Height / 2 + 5));
            dc.DrawLine(grip, new Point(rect.Right - 3, rect.Top + rect.Height / 2 - 5), new Point(rect.Right - 3, rect.Top + rect.Height / 2 + 5));
            dc.Pop();
        }
        dc.Pop();

        if (selected) dc.DrawRoundedRectangle(null, new Pen(c.Text, 2), rect, 4, 4);
        else dc.DrawRoundedRectangle(null, new Pen(Brushes.Black, 1) { Brush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)) }, rect, 4, 4);
    }

    private static readonly Brush WaveformBrush = CreateWaveformBrush();

    private static readonly Brush PlateBrush = CreatePlateBrush();

    private static Brush CreatePlateBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateWaveformBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
        brush.Freeze();
        return brush;
    }

    /// <summary>Draws the peaks as mirrored bars, one bar per two pixels, only for the part of the clip that is on screen.</summary>
    private void DrawWaveform(DrawingContext dc, AnimationClip clip, Rect rect, int length, int mediaStart)
    {
        var peaks = clip.Waveform!;
        var count = peaks.Count;
        double fractionStart = 0, fractionSpan = 1;
        if (clip.MediaLength > 0)
        {
            fractionStart = (double)mediaStart / clip.MediaLength;
            fractionSpan = (double)length / clip.MediaLength;
        }

        var from = Math.Max(rect.Left, 0);
        var to = Math.Min(rect.Right, ActualWidth);
        if (to <= from || rect.Width <= 0) return;

        var middle = rect.Top + rect.Height / 2;
        var maxHeight = Math.Max(1, rect.Height / 2 - 3);
        const double step = 2;
        for (var x = from; x < to; x += step)
        {
            var t0 = (x - rect.Left) / rect.Width;
            var t1 = Math.Min(1, (x + step - rect.Left) / rect.Width);
            var first = Math.Clamp((int)Math.Floor((fractionStart + t0 * fractionSpan) * count), 0, count - 1);
            var last = Math.Clamp((int)Math.Ceiling((fractionStart + t1 * fractionSpan) * count), first + 1, count);

            var peak = 0f;
            for (var i = first; i < last; i++) peak = Math.Max(peak, Math.Abs(peaks[i]));
            var h = Math.Max(1, Math.Min(1, peak) * maxHeight);
            dc.DrawRectangle(WaveformBrush, null, new Rect(x, middle - h, 1.4, h * 2));
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Owner?.UpdateScrollBars();
    }

    #endregion

    #region Mouse

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Owner == null) return;

        Owner.Focus();
        PointerDown(e.GetPosition(this), Keyboard.Modifiers, e.ClickCount);
        if (_pressed) CaptureMouse();
        e.Handled = true;
    }

    /// <summary>The primary button went down at <paramref name="p"/> (also used by tests, which cannot move the real pointer).</summary>
    internal void PointerDown(Point p, ModifierKeys modifiers, int clickCount)
    {
        var o = Owner;
        if (o == null) return;

        var hit = HitTest(p);
        var additive = modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Shift);
        _pressed = true;
        _downPoint = p;
        _dragged = false;
        _deselectOnRelease = false;
        _selectOnlyOnRelease = false;
        _resumePlaybackOnRelease = false;

        switch (hit.Region)
        {
            case Region.Marker:
                BeginScrub(o);
                o.CurrentFrame = hit.Marker!.Frame;
                break;

            case Region.Ruler when modifiers.HasFlag(ModifierKeys.Shift):
                _mode = DragMode.Loop;
                _loopAnchor = Math.Clamp(FrameAt(p.X), 0, o.Duration);
                break;

            case Region.Ruler:
                BeginScrub(o);
                o.CurrentFrame = FrameAt(p.X);
                break;

            case Region.Clip or Region.ClipStart or Region.ClipEnd:
                var clip = hit.Clip!;
                if (clickCount == 2)
                {
                    o.RaiseClipActivated(clip, o.Tracks[hit.TrackIndex]);
                    _mode = DragMode.None;
                    break;
                }

                if (!clip.IsSelected)
                {
                    if (additive) o.SetSelection(o.SelectedClips.Append(clip));
                    else o.SetSelection([clip]);
                }
                else if (additive) _deselectOnRelease = true;
                else if (o.SelectedClips.Count > 1) _selectOnlyOnRelease = true;

                _primary = clip;
                if (!o.IsEditable)
                {
                    _mode = DragMode.None;
                    break;
                }
                _mode = hit.Region == Region.ClipStart ? DragMode.ResizeStart : hit.Region == Region.ClipEnd ? DragMode.ResizeEnd : DragMode.Move;
                _origins = (_mode == DragMode.Move
                        ? o.SelectedClips.Where(s => o.TrackOf(s) is { IsLocked: false })
                        : new[] { clip })
                    .Select(s => new Origin(s, o.TrackOf(s)!, o.Tracks.IndexOf(o.TrackOf(s)!), s.Start, s.Length))
                    .ToList();
                _deltaFrames = 0;
                _deltaTracks = 0;
                _resizeStart = clip.Start;
                _resizeLength = clip.Length;
                break;

            default:
                if (!additive) o.ClearSelection();
                _mode = DragMode.Marquee;
                _marqueeStart = ToContent(p);
                _marqueeAdditive = additive;
                _baseSelection = additive ? new HashSet<AnimationClip>(o.SelectedClips) : [];
                _marquee = null;
                break;
        }
    }

    private Point ToContent(Point p) => new(p.X + Owner!.HorizontalOffset, p.Y - AnimationTimeline.RulerHeight + Owner.VerticalOffset);

    /// <summary>Selects the clips that touch the rubber band (and, with Ctrl/Shift, keeps the earlier selection).</summary>
    private void UpdateMarquee(AnimationTimeline o, Point p)
    {
        var rect = new Rect(_marqueeStart, ToContent(p));
        _marquee = rect;

        var hits = new List<AnimationClip>();
        double top = 0;
        foreach (var track in o.Tracks)
        {
            var height = track.Height;
            if (!track.IsLocked && rect.Bottom >= top && rect.Top <= top + height)
            {
                foreach (var clip in track.Clips)
                {
                    var x = clip.Start * Zoom;
                    var clipRect = clip.IsKeyframe
                        ? new Rect(x + Zoom / 2 - 8, top + 3, 16, height - 7)
                        : new Rect(x, top + 3, Math.Max(3, clip.Length * Zoom), height - 7);
                    if (clipRect.IntersectsWith(rect)) hits.Add(clip);
                }
            }
            top += height;
        }
        o.SetSelection(_marqueeAdditive ? _baseSelection.Concat(hits) : hits);
    }

    private void BeginScrub(AnimationTimeline o)
    {
        _mode = DragMode.Scrub;
        if (!o.IsPlaying) return;
        _resumePlaybackOnRelease = true;
        o.Pause();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Owner == null) return;
        var p = e.GetPosition(this);

        if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            UpdateHover(p);
            return;
        }
        PointerMove(p);
    }

    /// <summary>The pointer moved to <paramref name="p"/> while the primary button is down.</summary>
    internal void PointerMove(Point p)
    {
        var o = Owner;
        if (o == null || !_pressed) return;

        switch (_mode)
        {
            case DragMode.Scrub:
                o.CurrentFrame = FrameAt(p.X);
                AutoScroll(o, p.X);
                break;

            case DragMode.Loop:
                var frame = Math.Clamp(FrameAt(p.X), 0, o.Duration);
                o.LoopStart = Math.Min(_loopAnchor, frame);
                o.LoopEnd = Math.Max(_loopAnchor, frame);
                AutoScroll(o, p.X);
                break;

            case DragMode.Move or DragMode.ResizeStart or DragMode.ResizeEnd:
                if (!_dragged && Math.Abs(p.X - _downPoint.X) < 3 && Math.Abs(p.Y - _downPoint.Y) < 3) return;
                _dragged = true;
                UpdateDrag(o, p);
                AutoScroll(o, p.X);
                InvalidateVisual();
                break;

            case DragMode.Marquee:
                if (!_dragged && Math.Abs(p.X - _downPoint.X) < 3 && Math.Abs(p.Y - _downPoint.Y) < 3) return;
                _dragged = true;
                AutoScroll(o, p.X);
                UpdateMarquee(o, p);
                InvalidateVisual();
                break;
        }
    }

    private void UpdateDrag(AnimationTimeline o, Point p)
    {
        var delta = (int)Math.Round((p.X - _downPoint.X) / Zoom);
        _snapFrame = null;
        var primary = _origins.First(origin => origin.Clip == _primary);
        var moved = new HashSet<AnimationClip>(_origins.Select(origin => origin.Clip));

        if (_mode == DragMode.Move)
        {
            var minDelta = -_origins.Min(origin => origin.Start);
            var maxDelta = o.ConstrainToDuration ? o.Duration - _origins.Max(origin => origin.Start + origin.Length) : int.MaxValue;
            maxDelta = Math.Max(maxDelta, minDelta); // clips that already stick out can still be dragged back
            delta = Math.Clamp(delta, minDelta, maxDelta);

            if (o.IsSnappingEnabled)
            {
                var offset = SnapOffset(o, new[] { primary.Start + delta, primary.Start + primary.Length + delta });
                if (_snapFrame != null)
                {
                    var snapped = delta + offset;
                    if (snapped >= minDelta && snapped <= maxDelta) delta = snapped;
                    else _snapFrame = null;
                }
            }

            var target = TrackIndexAt(p.Y);
            var tracksDelta = target < 0 ? 0 : target - primary.TrackIndex;
            if (tracksDelta != 0 && !_origins.All(origin =>
                {
                    var index = origin.TrackIndex + tracksDelta;
                    return index >= 0 && index < o.Tracks.Count && !o.Tracks[index].IsLocked;
                }))
            {
                tracksDelta = 0;
            }

            if (o.PreventOverlap && tracksDelta == 0 && delta != 0)
            {
                // Walk away from where the clips started and stop at the first obstacle: clips cannot jump over their neighbors.
                var step = delta > 0 ? 1 : -1;
                var reached = 0;
                for (var d = step; Math.Abs(d) <= Math.Abs(delta) && MoveIsFree(o, moved, d, 0); d += step) reached = d;
                if (reached != delta)
                {
                    delta = reached;
                    _snapFrame = null;
                }
            }
            else if (o.PreventOverlap && !MoveIsFree(o, moved, delta, tracksDelta))
            {
                // Dropped on another track: slide back towards where the clips started until they fit.
                var fitted = 0;
                var found = false;
                if (delta != 0)
                {
                    var step = delta > 0 ? -1 : 1;
                    for (var d = delta + step; d != 0; d += step)
                    {
                        if (!MoveIsFree(o, moved, d, tracksDelta)) continue;
                        fitted = d;
                        found = true;
                        break;
                    }
                }
                if (!found && !MoveIsFree(o, moved, 0, tracksDelta)) tracksDelta = 0;
                delta = fitted;
                _snapFrame = null;
            }

            _deltaFrames = delta;
            _deltaTracks = tracksDelta;
            return;
        }

        var media = primary.Clip.MediaLength;
        if (_mode == DragMode.ResizeStart)
        {
            var end = primary.Start + primary.Length;
            var start = primary.Start + delta;
            if (o.IsSnappingEnabled) start += SnapOffset(o, new[] { start });

            var minStart = 0;
            if (media > 0) minStart = Math.Max(minStart, primary.Start - primary.Clip.MediaStart);
            if (o.PreventOverlap) minStart = Math.Max(minStart, o.PreviousEnd(primary.Track, primary.Clip));
            minStart = Math.Min(minStart, end - 1);

            start = Math.Clamp(start, minStart, end - 1);
            _resizeStart = start;
            _resizeLength = end - start;
            return;
        }

        var newEnd = primary.Start + primary.Length + delta;
        if (o.IsSnappingEnabled) newEnd += SnapOffset(o, new[] { newEnd });

        var maxEnd = int.MaxValue;
        if (o.ConstrainToDuration) maxEnd = Math.Max(o.Duration, primary.Start + primary.Length);
        if (media > 0) maxEnd = Math.Min(maxEnd, primary.Start + (media - primary.Clip.MediaStart));
        if (o.PreventOverlap) maxEnd = Math.Min(maxEnd, o.NextStart(primary.Track, primary.Clip));
        maxEnd = Math.Max(maxEnd, primary.Start + 1);

        newEnd = Math.Clamp(newEnd, primary.Start + 1, maxEnd);
        _resizeStart = primary.Start;
        _resizeLength = newEnd - primary.Start;
    }

    private bool MoveIsFree(AnimationTimeline o, HashSet<AnimationClip> moved, int delta, int tracksDelta)
        => _origins.All(origin => o.IsFree(o.Tracks[origin.TrackIndex + tracksDelta], origin.Start + delta, origin.Length, moved));

    /// <summary>The offset (in frames) that moves one of <paramref name="edges"/> onto the nearest snap point, or 0 if none is close.</summary>
    private int SnapOffset(AnimationTimeline o, int[] edges)
    {
        var threshold = SnapPixels / Zoom;
        var dragged = new HashSet<AnimationClip>(_origins.Select(origin => origin.Clip));
        var points = new List<int> { 0, o.CurrentFrame, o.Duration };
        points.AddRange(o.Markers.Select(m => m.Frame));
        if (o.LoopRegion is { } loop)
        {
            points.Add(loop.Start);
            points.Add(loop.End);
        }
        foreach (var clip in o.Tracks.SelectMany(t => t.Clips))
        {
            if (dragged.Contains(clip)) continue;
            points.Add(clip.Start);
            points.Add(clip.End);
        }

        var best = double.MaxValue;
        var offset = 0;
        foreach (var edge in edges)
        {
            foreach (var point in points)
            {
                var distance = Math.Abs(point - edge);
                if (distance > threshold || distance >= best) continue;
                best = distance;
                offset = point - edge;
                _snapFrame = point;
            }
        }
        return offset;
    }

    private void AutoScroll(AnimationTimeline o, double x)
    {
        if (x < 24) o.SetHorizontalOffset(o.HorizontalOffset - 14);
        else if (x > ActualWidth - 24) o.SetHorizontalOffset(o.HorizontalOffset + 14);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_pressed) return;
        if (IsMouseCaptured) ReleaseMouseCapture();
        PointerUp();
    }

    /// <summary>The primary button was released.</summary>
    internal void PointerUp()
    {
        var o = Owner;
        _pressed = false;
        if (o == null) return;

        switch (_mode)
        {
            case DragMode.Scrub:
                if (_resumePlaybackOnRelease) o.Play();
                break;

            case DragMode.Loop:
                if (o.LoopRegion != null) o.IsLoopEnabled = true;
                else o.ClearLoopRegion();
                break;

            case DragMode.Move or DragMode.ResizeStart or DragMode.ResizeEnd:
                if (_dragged) CommitDrag(o);
                else if (_deselectOnRelease && _primary != null) o.SetSelection(o.SelectedClips.Where(c => c != _primary));
                else if (_selectOnlyOnRelease && _primary != null) o.SetSelection([_primary]);
                break;

            default:
                if (_deselectOnRelease && _primary != null) o.SetSelection(o.SelectedClips.Where(c => c != _primary));
                break;
        }
        ResetDrag();
        InvalidateVisual();
    }

    private void CommitDrag(AnimationTimeline o)
    {
        var changes = new List<AnimationClipChange>();
        var oldDuration = o.Duration;

        if (_mode == DragMode.Move)
        {
            // Remember where every clip sat before any of them is taken out of its track.
            var indices = _origins.ToDictionary(origin => origin.Clip, origin => origin.Track.Clips.IndexOf(origin.Clip));
            foreach (var origin in _origins)
            {
                var newIndex = Math.Clamp(origin.TrackIndex + _deltaTracks, 0, o.Tracks.Count - 1);
                var newTrack = o.Tracks[newIndex];
                var newStart = Math.Max(0, origin.Start + _deltaFrames);
                if (newStart == origin.Start && ReferenceEquals(newTrack, origin.Track)) continue;

                origin.Clip.Start = newStart;
                if (!ReferenceEquals(newTrack, origin.Track))
                {
                    origin.Track.Clips.Remove(origin.Clip);
                    newTrack.Clips.Add(origin.Clip);
                }
                changes.Add(new AnimationClipChange(origin.Clip, origin.Track, newTrack, origin.Start, origin.Length, indices[origin.Clip], origin.Clip.MediaStart));
            }
            if (changes.Count == 0) return;

            o.ExtendDurationIfNeeded();
            o.RecordChange(changes.Count == 1 ? "Move clip" : "Move clips", changes, oldDuration);
            o.RaiseClipsMoved(changes);
            return;
        }

        var primary = _origins.First(origin => origin.Clip == _primary);
        if (_resizeStart == primary.Start && _resizeLength == primary.Length) return;

        var oldMedia = primary.Clip.MediaStart;
        var oldIndex = primary.Track.Clips.IndexOf(primary.Clip);
        primary.Clip.Start = _resizeStart;
        primary.Clip.Length = _resizeLength;
        // Trimming the left edge moves the clip's window into the media.
        if (_mode == DragMode.ResizeStart && primary.Clip.MediaLength > 0) primary.Clip.MediaStart = oldMedia + (_resizeStart - primary.Start);
        changes.Add(new AnimationClipChange(primary.Clip, primary.Track, primary.Track, primary.Start, primary.Length, oldIndex, oldMedia));

        o.ExtendDurationIfNeeded();
        o.RecordChange("Trim clip", changes, oldDuration);
        o.RaiseClipsResized(changes);
    }

    /// <summary>Abandons a drag in progress without changing the model (Escape).</summary>
    internal void CancelDrag()
    {
        if (!_pressed) return;
        _pressed = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        ResetDrag();
        InvalidateVisual();
    }

    private void ResetDrag()
    {
        _mode = DragMode.None;
        _origins = [];
        _primary = null;
        _dragged = false;
        _snapFrame = null;
        _deltaFrames = 0;
        _deltaTracks = 0;
        _marquee = null;
        _baseSelection = [];
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var o = Owner;
        if (o == null) return;
        o.Focus();
        var hit = HitTest(e.GetPosition(this));
        if (hit.Clip != null && !hit.Clip.IsSelected) o.SetSelection([hit.Clip]);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover == null || _pressed) return;
        _hover = null;
        Cursor = null;
        InvalidateVisual();
    }

    private void UpdateHover(Point p)
    {
        var hit = HitTest(p);
        var cursor = hit.Region switch
        {
            Region.ClipStart or Region.ClipEnd when Owner!.IsEditable => Cursors.SizeWE,
            Region.Marker => Cursors.Hand,
            Region.Ruler => Cursors.IBeam,
            _ => null,
        };
        if (!Equals(Cursor, cursor)) Cursor = cursor;
        if (ReferenceEquals(_hover, hit.Clip)) return;
        _hover = hit.Clip;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var o = Owner;
        if (o == null || e.Handled) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            o.ZoomAt(o.PixelsPerFrame * (e.Delta > 0 ? 1.2 : 1 / 1.2), e.GetPosition(this).X);
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || o.ContentHeight <= o.ViewportHeight)
        {
            if (o.ContentWidth <= o.ViewportWidth) return;
            o.SetHorizontalOffset(o.HorizontalOffset - e.Delta);
        }
        else
        {
            o.SetVerticalOffset(o.VerticalOffset - e.Delta / 2.0);
        }
        e.Handled = true;
    }

    #endregion
}
