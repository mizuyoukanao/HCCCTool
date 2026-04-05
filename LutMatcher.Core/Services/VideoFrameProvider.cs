using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class VideoFrameProvider : IDisposable
{
    private VideoCapture? _capture;

    public string? SourcePath { get; private set; }
    public int FrameCount { get; private set; }
    public double Fps { get; private set; }
    public TimeSpan Duration { get; private set; }

    public bool IsOpen => _capture is not null && _capture.IsOpened();

    public void Open(string path)
    {
        DisposeCapture();

        var capture = new VideoCapture(path);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            throw new InvalidOperationException("Failed to open video file.");
        }

        _capture = capture;
        SourcePath = path;
        FrameCount = Math.Max(0, (int)capture.Get(VideoCaptureProperties.FrameCount));
        Fps = Math.Max(0, capture.Get(VideoCaptureProperties.Fps));
        Duration = Fps > 0 && FrameCount > 0
            ? TimeSpan.FromSeconds(FrameCount / Fps)
            : TimeSpan.Zero;
    }

    public Mat GetFrameByIndex(int frameIndex, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        var clamped = Math.Max(0, FrameCount > 0 ? Math.Min(frameIndex, FrameCount - 1) : frameIndex);
        _capture!.Set(VideoCaptureProperties.PosFrames, clamped);

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            throw new InvalidOperationException($"Failed to read frame index {clamped}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return frame.Clone();
    }

    public Mat GetFrameByTimestamp(TimeSpan timestamp, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        _capture!.Set(VideoCaptureProperties.PosMsec, Math.Max(0, timestamp.TotalMilliseconds));

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            throw new InvalidOperationException($"Failed to read frame at {timestamp.TotalMilliseconds:F0} ms.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return frame.Clone();
    }

    public int GetCurrentFrameIndex()
    {
        EnsureOpen();
        return Math.Max(0, (int)_capture!.Get(VideoCaptureProperties.PosFrames) - 1);
    }

    public TimeSpan GetCurrentTimestamp()
    {
        EnsureOpen();
        return TimeSpan.FromMilliseconds(Math.Max(0, _capture!.Get(VideoCaptureProperties.PosMsec)));
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Video source is not opened.");
        }
    }

    private void DisposeCapture()
    {
        _capture?.Dispose();
        _capture = null;
        SourcePath = null;
        FrameCount = 0;
        Fps = 0;
        Duration = TimeSpan.Zero;
    }

    public void Dispose() => DisposeCapture();
}
