using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class AlignmentService
{
    public bool TryAlignByTranslation(Mat referenceBgr, Mat targetBgr, out Mat alignedTargetBgr, out Point2d shift, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        alignedTargetBgr = targetBgr.Size() == referenceBgr.Size() ? targetBgr.Clone() : targetBgr.Resize(referenceBgr.Size());
        shift = new Point2d(0, 0);

        try
        {
            using var referenceGray = BuildGrayFloat(referenceBgr);
            using var targetGray = BuildGrayFloat(alignedTargetBgr);
            cancellationToken.ThrowIfCancellationRequested();
            shift = Cv2.PhaseCorrelate(targetGray, referenceGray, null, out _);

            using var affine = Mat.Eye(2, 3, MatType.CV_64FC1).ToMat();
            affine.Set<double>(0, 2, shift.X);
            affine.Set<double>(1, 2, shift.Y);

            var warped = new Mat();
            Cv2.WarpAffine(
                alignedTargetBgr,
                warped,
                affine,
                referenceBgr.Size(),
                InterpolationFlags.Linear,
                BorderTypes.Reflect101);
            alignedTargetBgr.Dispose();
            alignedTargetBgr = warped;
            return true;
        }
        catch
        {
            shift = new Point2d(0, 0);
            return false;
        }
    }

    private static Mat BuildGrayFloat(Mat bgr)
    {
        using var gray8 = new Mat();
        Cv2.CvtColor(bgr, gray8, ColorConversionCodes.BGR2GRAY);
        var gray32 = new Mat();
        gray8.ConvertTo(gray32, MatType.CV_32FC1, 1.0 / 255.0);
        return gray32;
    }
}
