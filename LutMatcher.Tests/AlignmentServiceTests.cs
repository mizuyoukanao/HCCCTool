using LutMatcher.Core.Services;
using OpenCvSharp;

namespace LutMatcher.Tests;

public sealed class AlignmentServiceTests
{
    [Fact]
    public void Alignment_ReducesShiftError()
    {
        using var reference = new Mat(new Size(128, 128), MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(reference, new Rect(30, 40, 30, 20), new Scalar(255, 128, 32), -1);

        using var shifted = new Mat();
        var matrix = Mat.Eye(2, 3, MatType.CV_64FC1);
        matrix.Set(0, 2, 4);
        matrix.Set(1, 2, -3);
        Cv2.WarpAffine(reference, shifted, matrix, reference.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

        using var beforeDiff = new Mat();
        Cv2.Absdiff(reference, shifted, beforeDiff);
        var before = Cv2.Mean(beforeDiff).Val0;

        var service = new AlignmentService();
        var ok = service.TryAlignByTranslation(reference, shifted, out var aligned, out _);
        Assert.True(ok);

        using (aligned)
        using var afterDiff = new Mat();
        Cv2.Absdiff(reference, aligned, afterDiff);
        var after = Cv2.Mean(afterDiff).Val0;
        Assert.True(after < before);
    }
}
