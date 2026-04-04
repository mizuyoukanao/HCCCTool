using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class ColorRangeService
{
    private const float VideoOffset = 16f / 255f;
    private const float VideoScale = 219f / 255f;

    public float NormalizeEncodedToWorking(float encoded, bool videoRange)
    {
        var normalized = videoRange ? (encoded - VideoOffset) / VideoScale : encoded;
        return Clamp01(normalized);
    }

    public float DenormalizeWorkingToEncoded(float working, bool videoRange)
    {
        var encoded = videoRange ? (working * VideoScale) + VideoOffset : working;
        return Clamp01(encoded);
    }

    public float[] NormalizeEncodedToWorking(float[] encodedRgb, bool videoRange)
    {
        return
        [
            NormalizeEncodedToWorking(encodedRgb[0], videoRange),
            NormalizeEncodedToWorking(encodedRgb[1], videoRange),
            NormalizeEncodedToWorking(encodedRgb[2], videoRange)
        ];
    }

    public float[] DenormalizeWorkingToEncoded(float[] workingRgb, bool videoRange)
    {
        return
        [
            DenormalizeWorkingToEncoded(workingRgb[0], videoRange),
            DenormalizeWorkingToEncoded(workingRgb[1], videoRange),
            DenormalizeWorkingToEncoded(workingRgb[2], videoRange)
        ];
    }

    public Mat ConvertBgrToWorkingRgb(Mat bgr, bool videoRange)
    {
        using var rgb8 = new Mat();
        Cv2.CvtColor(bgr, rgb8, ColorConversionCodes.BGR2RGB);
        var rgbFloat = new Mat();
        rgb8.ConvertTo(rgbFloat, MatType.CV_32FC3, 1.0 / 255.0);

        if (!videoRange)
        {
            return rgbFloat;
        }

        Cv2.Subtract(rgbFloat, new Scalar(VideoOffset, VideoOffset, VideoOffset), rgbFloat);
        Cv2.Divide(rgbFloat, new Scalar(VideoScale, VideoScale, VideoScale), rgbFloat);
        Cv2.Min(rgbFloat, new Scalar(1, 1, 1), rgbFloat);
        Cv2.Max(rgbFloat, new Scalar(0, 0, 0), rgbFloat);
        return rgbFloat;
    }

    public Mat ConvertWorkingRgbToBgr(Mat workingRgb, bool videoRange)
    {
        using var encodedRgb = workingRgb.Clone();
        if (videoRange)
        {
            Cv2.Multiply(encodedRgb, new Scalar(VideoScale, VideoScale, VideoScale), encodedRgb);
            Cv2.Add(encodedRgb, new Scalar(VideoOffset, VideoOffset, VideoOffset), encodedRgb);
        }

        Cv2.Min(encodedRgb, new Scalar(1, 1, 1), encodedRgb);
        Cv2.Max(encodedRgb, new Scalar(0, 0, 0), encodedRgb);

        using var rgb8 = new Mat();
        encodedRgb.ConvertTo(rgb8, MatType.CV_8UC3, 255.0);
        var bgr = new Mat();
        Cv2.CvtColor(rgb8, bgr, ColorConversionCodes.RGB2BGR);
        return bgr;
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
