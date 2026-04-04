using LutMatcher.Core.Models;
using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class SampleExtractor
{
    public SampleSet Extract(Mat referenceBgr, Mat targetBgr, Rect? roi, bool videoRange, int maxSamples = 200000)
    {
        if (referenceBgr.Empty() || targetBgr.Empty())
        {
            throw new InvalidOperationException("Input image is empty.");
        }

        using var refRgb = ConvertToNormalizedRgb(referenceBgr, videoRange);
        using var tgtResized = targetBgr.Size() == referenceBgr.Size() ? targetBgr.Clone() : targetBgr.Resize(referenceBgr.Size());
        using var tgtRgb = ConvertToNormalizedRgb(tgtResized, videoRange);

        var safeRoi = NormalizeRoi(roi, refRgb.Width, refRgb.Height);
        using var refCrop = new Mat(refRgb, safeRoi);
        using var tgtCrop = new Mat(tgtRgb, safeRoi);

        var refSamples = new List<float[]>();
        var tgtSamples = new List<float[]>();
        var step = Math.Max(1, (int)Math.Sqrt((safeRoi.Width * safeRoi.Height) / (double)maxSamples));

        for (var y = 0; y < safeRoi.Height; y += step)
        {
            for (var x = 0; x < safeRoi.Width; x += step)
            {
                var r = refCrop.At<Vec3f>(y, x);
                var t = tgtCrop.At<Vec3f>(y, x);
                if (IsClipped(r) || IsClipped(t))
                {
                    continue;
                }

                refSamples.Add([r.Item0, r.Item1, r.Item2]);
                tgtSamples.Add([t.Item0, t.Item1, t.Item2]);
            }
        }

        if (refSamples.Count < 128)
        {
            throw new InvalidOperationException("Not enough valid samples after filtering.");
        }

        return new SampleSet { Reference = [.. refSamples], Target = [.. tgtSamples] };
    }

    private static Rect NormalizeRoi(Rect? roi, int width, int height)
    {
        if (roi is null)
        {
            return new Rect(0, 0, width, height);
        }

        var r = roi.Value;
        if (r.Width <= 0 || r.Height <= 0)
        {
            throw new ArgumentException("ROI size must be positive.");
        }

        var x = Math.Clamp(r.X, 0, width - 1);
        var y = Math.Clamp(r.Y, 0, height - 1);
        var w = Math.Clamp(r.Width, 1, width - x);
        var h = Math.Clamp(r.Height, 1, height - y);
        return new Rect(x, y, w, h);
    }

    private static bool IsClipped(Vec3f v)
    {
        const float epsilon = 0.01f;
        return v.Item0 < epsilon || v.Item1 < epsilon || v.Item2 < epsilon ||
               v.Item0 > 1f - epsilon || v.Item1 > 1f - epsilon || v.Item2 > 1f - epsilon;
    }

    private static Mat ConvertToNormalizedRgb(Mat bgr, bool videoRange)
    {
        using var floatBgr = new Mat();
        bgr.ConvertTo(floatBgr, MatType.CV_32FC3, 1.0 / 255.0);

        if (videoRange)
        {
            Cv2.Subtract(floatBgr, new Scalar(16.0 / 255.0, 16.0 / 255.0, 16.0 / 255.0), floatBgr);
            Cv2.Divide(floatBgr, new Scalar(219.0 / 255.0, 219.0 / 255.0, 219.0 / 255.0), floatBgr);
            Cv2.Min(floatBgr, new Scalar(1, 1, 1), floatBgr);
            Cv2.Max(floatBgr, new Scalar(0, 0, 0), floatBgr);
        }

        var rgb = new Mat();
        Cv2.CvtColor(floatBgr, rgb, ColorConversionCodes.BGR2RGB);
        return rgb;
    }
}
