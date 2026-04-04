using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class ImageLoader
{
    public readonly record struct CameraInfo(int Index, string Name);

    private static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".mov", ".mkv", ".avi"];
    private static readonly VideoCaptureAPIs[] CameraApis =
    [
        VideoCaptureAPIs.MSMF,
        VideoCaptureAPIs.DSHOW,
        VideoCaptureAPIs.ANY
    ];
    private const int MaxCameraProbeCount = 10;

    public Mat LoadFirstFrame(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ImageExtensions.Contains(ext))
        {
            var image = Cv2.ImRead(path, ImreadModes.Color);
            if (image.Empty())
            {
                throw new InvalidOperationException("Failed to read image file.");
            }

            return image;
        }

        if (VideoExtensions.Contains(ext))
        {
            using var cap = new VideoCapture(path);
            if (!cap.IsOpened())
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            var frame = new Mat();
            cap.Read(frame);
            if (frame.Empty())
            {
                throw new InvalidOperationException("Video file has no readable frame.");
            }

            return frame;
        }

        throw new NotSupportedException("Unsupported input format.");
    }

    public Mat LoadFirstFrameFromCamera(int deviceIndex, int frameDelay = 0, int warmupFrames = 5)
    {
        if (frameDelay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameDelay), "Frame delay must be zero or greater.");
        }

        foreach (var api in CameraApis)
        {
            using var cap = new VideoCapture(deviceIndex, api);
            if (!cap.IsOpened())
            {
                continue;
            }

            var frame = new Mat();
            for (var i = 0; i < Math.Max(1, warmupFrames); i++)
            {
                cap.Read(frame);
            }

            for (var i = 0; i < frameDelay; i++)
            {
                cap.Read(frame);
            }

            cap.Read(frame);
            if (!frame.Empty())
            {
                return frame;
            }
        }

        throw new InvalidOperationException($"Failed to open camera/capture device index {deviceIndex}.");
    }

    public List<CameraInfo> GetAvailableCameras(int maxProbeCount = MaxCameraProbeCount)
    {
        var cameras = new List<CameraInfo>();
        for (var index = 0; index < maxProbeCount; index++)
        {
            foreach (var api in CameraApis)
            {
                using var cap = new VideoCapture(index, api);
                if (!cap.IsOpened())
                {
                    continue;
                }

                cameras.Add(new CameraInfo(index, $"Camera {index + 1} ({api})"));
                break;
            }
        }

        return cameras;
    }
}
