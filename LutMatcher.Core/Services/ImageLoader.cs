using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class ImageLoader
{
    private static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".mov", ".mkv", ".avi"];

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

    public Mat LoadFromCamera(int deviceIndex)
    {
        using var cap = new VideoCapture(deviceIndex);
        if (!cap.IsOpened())
        {
            throw new InvalidOperationException($"Failed to open camera index {deviceIndex}.");
        }

        var frame = new Mat();
        cap.Read(frame);
        if (frame.Empty())
        {
            throw new InvalidOperationException("Camera frame is empty.");
        }

        return frame;
    }

    public List<int> ProbeCameraDevices(int maxProbe = 10)
    {
        var devices = new List<int>();
        for (var i = 0; i < maxProbe; i++)
        {
            using var cap = new VideoCapture(i);
            if (!cap.IsOpened())
            {
                continue;
            }

            var frame = new Mat();
            cap.Read(frame);
            if (!frame.Empty())
            {
                devices.Add(i);
            }
        }

        return devices;
    }
}
