using OpenCvSharp;

namespace LutMatcher.Core.Services;

public interface ICameraCaptureFactory
{
    VideoCapture? Create(int deviceIndex, VideoCaptureAPIs api);
}

public sealed class OpenCvCameraCaptureFactory : ICameraCaptureFactory
{
    public VideoCapture? Create(int deviceIndex, VideoCaptureAPIs api)
    {
        var capture = new VideoCapture(deviceIndex, api);
        return capture.IsOpened() ? capture : null;
    }
}

public sealed class CameraCaptureService : IDisposable
{
    private static readonly VideoCaptureAPIs[] CameraApis =
    [
        VideoCaptureAPIs.MSMF,
        VideoCaptureAPIs.DSHOW,
        VideoCaptureAPIs.ANY
    ];

    private readonly object _sync = new();
    private readonly ICameraCaptureFactory _factory;
    private VideoCapture? _referenceCapture;
    private VideoCapture? _targetCapture;
    private Mat? _latestReference;
    private Mat? _latestTarget;

    public CameraCaptureService(ICameraCaptureFactory? factory = null)
    {
        _factory = factory ?? new OpenCvCameraCaptureFactory();
    }

    public bool IsRunning { get; private set; }
    public string ReferenceDeviceInfo { get; private set; } = string.Empty;
    public string TargetDeviceInfo { get; private set; } = string.Empty;

    public void Start(int referenceDeviceIndex, int targetDeviceIndex, CancellationToken cancellationToken = default)
    {
        Stop();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _referenceCapture = OpenCapture(referenceDeviceIndex, out var refInfo);
            _targetCapture = OpenCapture(targetDeviceIndex, out var targetInfo);

            ReferenceDeviceInfo = refInfo;
            TargetDeviceInfo = targetInfo;

            Warmup(_referenceCapture, cancellationToken);
            Warmup(_targetCapture, cancellationToken);
            IsRunning = true;
            UpdateLatestPair(cancellationToken);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void UpdateLatestPair(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || _referenceCapture is null || _targetCapture is null)
        {
            throw new InvalidOperationException("Camera capture is not started.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var refMat = ReadFrame(_referenceCapture, cancellationToken);
        using var targetMat = ReadFrame(_targetCapture, cancellationToken);

        lock (_sync)
        {
            _latestReference?.Dispose();
            _latestTarget?.Dispose();
            _latestReference = refMat.Clone();
            _latestTarget = targetMat.Clone();
        }
    }

    public (Mat Reference, Mat Target) FreezePair(CancellationToken cancellationToken = default)
    {
        UpdateLatestPair(cancellationToken);
        lock (_sync)
        {
            if (_latestReference is null || _latestTarget is null)
            {
                throw new InvalidOperationException("No camera frames are available.");
            }

            return (_latestReference.Clone(), _latestTarget.Clone());
        }
    }

    public (Mat? Reference, Mat? Target) GetLatestPreview()
    {
        lock (_sync)
        {
            return (_latestReference?.Clone(), _latestTarget?.Clone());
        }
    }

    public List<ImageLoader.CameraInfo> GetAvailableCameras(int maxProbeCount = 10)
    {
        var devices = new List<ImageLoader.CameraInfo>();
        for (var index = 0; index < maxProbeCount; index++)
        {
            foreach (var api in CameraApis)
            {
                using var cap = _factory.Create(index, api);
                if (cap is null)
                {
                    continue;
                }

                devices.Add(new ImageLoader.CameraInfo(index, $"Device {index} ({api})"));
                break;
            }
        }

        return devices;
    }

    public void Stop()
    {
        IsRunning = false;
        DisposeCapture(ref _referenceCapture);
        DisposeCapture(ref _targetCapture);

        lock (_sync)
        {
            _latestReference?.Dispose();
            _latestTarget?.Dispose();
            _latestReference = null;
            _latestTarget = null;
        }
    }

    private static Mat ReadFrame(VideoCapture capture, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            frame.Dispose();
            throw new InvalidOperationException("Failed to read frame from capture device.");
        }

        return frame;
    }

    private VideoCapture OpenCapture(int deviceIndex, out string info)
    {
        foreach (var api in CameraApis)
        {
            var cap = _factory.Create(deviceIndex, api);
            if (cap is null)
            {
                continue;
            }

            info = $"Device {deviceIndex} ({api})";
            return cap;
        }

        throw new InvalidOperationException($"Failed to open camera/capture device index {deviceIndex}.");
    }

    private static void Warmup(VideoCapture capture, CancellationToken cancellationToken, int warmupFrames = 5)
    {
        using var frame = new Mat();
        for (var i = 0; i < warmupFrames; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture.Read(frame);
        }
    }

    private static void DisposeCapture(ref VideoCapture? capture)
    {
        capture?.Release();
        capture?.Dispose();
        capture = null;
    }

    public void Dispose() => Stop();
}
