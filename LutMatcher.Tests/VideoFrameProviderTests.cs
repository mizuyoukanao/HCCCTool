using LutMatcher.Core.Services;
using OpenCvSharp;

namespace LutMatcher.Tests;

public sealed class VideoFrameProviderTests
{
    [Fact]
    public void CanSeekByFrameAndTimestamp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lutmatcher_video_{Guid.NewGuid():N}.avi");
        try
        {
            using (var writer = new VideoWriter(path, FourCC.MJPG, 10, new Size(32, 32)))
            {
                for (var i = 0; i < 5; i++)
                {
                    using var frame = new Mat(new Size(32, 32), MatType.CV_8UC3, new Scalar(i * 40, 0, 0));
                    writer.Write(frame);
                }
            }

            using var provider = new VideoFrameProvider();
            provider.Open(path);

            using var byIndex = provider.GetFrameByIndex(3);
            var indexPixel = byIndex.At<Vec3b>(0, 0);
            Assert.InRange(indexPixel.Item0, 100, 140);

            using var byTime = provider.GetFrameByTimestamp(TimeSpan.FromMilliseconds(200));
            var timePixel = byTime.At<Vec3b>(0, 0);
            Assert.InRange(timePixel.Item0, 60, 100);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
