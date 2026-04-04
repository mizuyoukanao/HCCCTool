using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class ImageLoader
{
    private static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".mov", ".mkv", ".avi"];
    private static readonly VideoCaptureAPIs[] CameraApis =
    [
        VideoCaptureAPIs.MSMF,
        VideoCaptureAPIs.DSHOW,
        VideoCaptureAPIs.ANY
    ];

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
}
