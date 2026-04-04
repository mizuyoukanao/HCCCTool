using LutMatcher.Core.Services;
using OpenCvSharp;

namespace LutMatcher.Tests;

public sealed class SampleExtractorTests
{
    [Fact]
    public void Extract_RejectsClippedPixels_AndRespectsMaxSamples()
    {
        using var reference = new Mat(new Size(64, 64), MatType.CV_8UC3, new Scalar(128, 128, 128));
        using var target = reference.Clone();
        Cv2.Rectangle(reference, new Rect(0, 0, 10, 10), Scalar.White, -1);
        Cv2.Rectangle(target, new Rect(0, 0, 10, 10), Scalar.Black, -1);

        var extractor = new SampleExtractor();
        var sampleSet = extractor.Extract(reference, target, null, videoRange: false, maxSamples: 300, randomSeed: 1);

        Assert.InRange(sampleSet.Count, 128, 300);
        Assert.DoesNotContain(sampleSet.Reference, s => s.Any(v => v < 0.01f || v > 0.99f));
        Assert.DoesNotContain(sampleSet.Target, s => s.Any(v => v < 0.01f || v > 0.99f));
    }
}
