using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Tessel.NET.Controls;

namespace Tessel.Gallery.Pages;

public partial class AnimationPage : UserControl
{
    private int _clipCounter = 1;

    public AnimationPage()
    {
        InitializeComponent();
        BuildVideoProject();
        BuildPixelProject();
        UpdateVideoPreview();
        UpdatePixelPreview();

        // Fit both projects to the width once the page has been laid out.
        Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            VideoTimeline.ZoomToFit();
            PixelTimeline.ZoomToFit();
        });
    }

    private static SolidColorBrush Solid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    #region Video editor

    private void BuildVideoProject()
    {
        var titles = new AnimationTrack("Titles") { Height = 34, Brush = Solid(0xD6, 0x3D, 0x8E) };
        titles.Clips.Add(new AnimationClip("Title card", 12, 72));
        titles.Clips.Add(new AnimationClip("Credits", 396, 84));

        var overlay = new AnimationTrack("Video 2") { Brush = Solid(0x84, 0x4F, 0xE0) };
        overlay.Clips.Add(new AnimationClip("Lower third.png", 130, 96));
        overlay.Clips.Add(new AnimationClip("Logo.png", 330, 72));

        var video = new AnimationTrack("Video 1") { Height = 44, Brush = Solid(0x0E, 0x8F, 0xC4) };
        video.Clips.Add(new AnimationClip("Intro.mp4", 0, 120));
        video.Clips.Add(new AnimationClip("Interview.mp4", 120, 180));
        video.Clips.Add(new AnimationClip("B-roll.mp4", 300, 108));
        video.Clips.Add(new AnimationClip("Outro.mp4", 408, 72));

        var music = new AnimationTrack("Audio 1") { Height = 44, Brush = Solid(0x0F, 0x9D, 0x6E) };
        music.Clips.Add(new AnimationClip("Music.wav", 0, 480) { Waveform = FakeWaveform(960, 7, 0.09), MediaLength = 480 });

        // A recording of 240 frames of which 168 are used (it starts 30 frames into the file): trimming reveals the rest.
        var voice = new AnimationTrack("Audio 2") { Height = 44, Brush = Solid(0xD9, 0x7B, 0x06) };
        voice.Clips.Add(new AnimationClip("Voice-over.wav", 132, 168) { Waveform = FakeWaveform(480, 21, 0.33), MediaLength = 240, MediaStart = 30 });

        foreach (var track in new[] { titles, overlay, video, music, voice }) VideoTimeline.Tracks.Add(track);

        VideoTimeline.Markers.Add(new AnimationMarker(0, "Start"));
        VideoTimeline.Markers.Add(new AnimationMarker(120, "Interview"));
        VideoTimeline.Markers.Add(new AnimationMarker(300, "B-roll"));
        VideoTimeline.Markers.Add(new AnimationMarker(408, "Outro"));
    }

    /// <summary>A believable-looking waveform: a slow envelope (phrases) times random peaks. Use AnimationWaveform.FromSamples for real audio.</summary>
    private static float[] FakeWaveform(int bins, int seed, double rhythm)
    {
        var random = new Random(seed);
        var peaks = new float[bins];
        for (var i = 0; i < bins; i++)
        {
            var envelope = 0.3 + 0.7 * Math.Abs(Math.Sin(i * rhythm));
            peaks[i] = (float)Math.Min(1, envelope * (0.35 + 0.65 * random.NextDouble()));
        }
        return peaks;
    }

    private void UpdateVideoPreview()
    {
        VideoPreviewTime.Text = VideoTimeline.FormatTime(VideoTimeline.CurrentFrame, TimelineTimeDisplay.Timecode);
        var active = VideoTimeline.Tracks
            .Where(t => t.IsVisible)
            .SelectMany(t => t.Clips)
            .Where(c => c.Start <= VideoTimeline.CurrentFrame && VideoTimeline.CurrentFrame < c.End)
            .Select(c => c.Name);
        var names = string.Join("  •  ", active);
        VideoPreviewClips.Text = names.Length == 0 ? "(no clips here)" : names;
    }

    private void OnVideoFrameChanged(object sender, RoutedPropertyChangedEventArgs<int> e) => UpdateVideoPreview();

    private void OnVideoClipsMoved(object? sender, AnimationClipsChangedEventArgs e)
    {
        var first = e.Changes[0];
        VideoStatus.Text = e.Changes.Count == 1
            ? $"Last action: moved “{first.Clip.Name}” from frame {first.OldStart} to {first.Clip.Start}" + (first.OldTrack != first.NewTrack ? $" and to track “{first.NewTrack.Name}”" : "")
            : $"Last action: moved {e.Changes.Count} clips";
        UpdateVideoPreview();
    }

    private void OnVideoClipsResized(object? sender, AnimationClipsChangedEventArgs e)
    {
        var change = e.Changes[0];
        VideoStatus.Text = $"Last action: trimmed “{change.Clip.Name}” to frames {change.Clip.Start}–{change.Clip.End} ({change.Clip.Length} frames)";
        UpdateVideoPreview();
    }

    private void OnVideoClipActivated(object? sender, AnimationClipEventArgs e)
        => VideoStatus.Text = $"Last action: opened “{e.Clip.Name}” on track “{e.Track.Name}” (double-click)";

    private void OnAddClip(object sender, RoutedEventArgs e)
    {
        var track = VideoTimeline.Tracks.First(t => t.Name == "Video 1");
        var clip = new AnimationClip($"New clip {_clipCounter++}", VideoTimeline.CurrentFrame, 48);
        if (VideoTimeline.AddClip(track, clip))
        {
            VideoTimeline.SelectClips([clip]);
            VideoStatus.Text = $"Last action: added “{clip.Name}” at frame {clip.Start} (Ctrl+Z undoes it)";
        }
        else
        {
            VideoStatus.Text = "Last action: there is no room for a new clip at the playhead (overlap or project length)";
        }
        UpdateVideoPreview();
    }

    private void OnAddMarker(object sender, RoutedEventArgs e)
    {
        VideoTimeline.Markers.Add(new AnimationMarker(VideoTimeline.CurrentFrame, $"Marker {VideoTimeline.Markers.Count + 1}"));
        VideoStatus.Text = $"Last action: added a marker at frame {VideoTimeline.CurrentFrame}";
    }

    private void OnDeleteClips(object sender, RoutedEventArgs e)
    {
        var count = VideoTimeline.SelectedClips.Count;
        VideoTimeline.DeleteSelectedClips();
        VideoStatus.Text = count == 0 ? "Last action: nothing selected" : $"Last action: deleted {count} clip(s)";
        UpdateVideoPreview();
    }

    private void OnFitDuration(object sender, RoutedEventArgs e) => VideoTimeline.FitDurationToContent();

    private void OnTimeDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VideoTimeline == null || sender is not Selector { SelectedIndex: >= 0 } selector) return;
        VideoTimeline.TimeDisplay = selector.SelectedIndex switch
        {
            0 => TimelineTimeDisplay.Timecode,
            1 => TimelineTimeDisplay.Frames,
            _ => TimelineTimeDisplay.Seconds,
        };
    }

    #endregion

    #region Pixel editor

    private static readonly Brush[] CelBrushes =
    [
        Solid(0x4F, 0x46, 0xE5), Solid(0xD6, 0x3D, 0x8E), Solid(0x0F, 0x9D, 0x6E), Solid(0xD9, 0x7B, 0x06),
    ];

    private void BuildPixelProject()
    {
        var position = new AnimationTrack("Position") { Height = 30, Brush = Solid(0x64, 0x74, 0x8B) };
        for (var frame = 0; frame < 24; frame += 6) position.Clips.Add(new AnimationClip(string.Empty, frame, 1) { IsKeyframe = true });
        position.Clips.Add(new AnimationClip(string.Empty, 23, 1) { IsKeyframe = true });

        var ball = new AnimationTrack("Ball") { Height = 34, Brush = Solid(0x4F, 0x46, 0xE5) };
        for (var frame = 0; frame < 24; frame++)
        {
            ball.Clips.Add(new AnimationClip((frame + 1).ToString(), frame, 1) { Brush = CelBrushes[frame / 6 % CelBrushes.Length], Tag = CelBrushes[frame / 6 % CelBrushes.Length] });
        }

        var shadow = new AnimationTrack("Shadow") { Height = 30, Brush = Solid(0x47, 0x55, 0x69) };
        for (var frame = 0; frame < 24; frame += 6) shadow.Clips.Add(new AnimationClip("hold", frame, 6));

        var ground = new AnimationTrack("Background") { Height = 30, Brush = Solid(0x0F, 0x9D, 0x6E) };
        ground.Clips.Add(new AnimationClip("ground", 0, 24));

        foreach (var track in new[] { position, ball, shadow, ground }) PixelTimeline.Tracks.Add(track);
    }

    private void OnPixelFrameChanged(object sender, RoutedPropertyChangedEventArgs<int> e) => UpdatePixelPreview();

    private void UpdatePixelPreview()
    {
        if (PixelTimeline == null || PixelBall == null) return;
        var frame = Math.Min(PixelTimeline.CurrentFrame, 23);
        var bounce = Math.Abs(Math.Sin(Math.PI * frame / 6.0));
        Canvas.SetLeft(PixelBall, 18 + frame * 11.5);
        Canvas.SetTop(PixelBall, 112 - 88 * bounce);
        Canvas.SetLeft(PixelShadow, 13 + frame * 11.5);
        PixelShadow.Opacity = 1 - bounce * 0.6;

        var cel = PixelTimeline.Tracks.FirstOrDefault(t => t.Name == "Ball")?.Clips.FirstOrDefault(c => c.Start <= frame && frame < c.End);
        if (cel?.Tag is Brush brush) PixelBall.Fill = brush;

        PixelInfo.Text = $"Frame {PixelTimeline.CurrentFrame} of {PixelTimeline.Duration}  •  {PixelTimeline.FramesPerSecond:0} fps";
    }

    #endregion
}
