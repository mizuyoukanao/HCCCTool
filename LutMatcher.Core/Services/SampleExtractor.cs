using LutMatcher.Core.Models;
using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class SampleExtractor
{
    private readonly ColorRangeService _range = new();

    public SampleSet Extract(
        Mat referenceBgr,
        Mat targetBgr,
        Rect? roi,
        bool videoRange,
        int maxSamples = 200000,
        int randomSeed = 1234,
        CancellationToken cancellationToken = default)
    {
        if (referenceBgr.Empty() || targetBgr.Empty())
        {
            throw new InvalidOperationException("Input image is empty.");
        }

        using var refRgb = _range.ConvertBgrToWorkingRgb(referenceBgr, videoRange);
        using var tgtResized = targetBgr.Size() == referenceBgr.Size() ? targetBgr.Clone() : targetBgr.Resize(referenceBgr.Size());
        using var tgtRgb = _range.ConvertBgrToWorkingRgb(tgtResized, videoRange);

        var safeRoi = NormalizeRoi(roi, refRgb.Width, refRgb.Height);
        using var refCrop = new Mat(refRgb, safeRoi);
        using var tgtCrop = new Mat(tgtRgb, safeRoi);

        var buckets = Enumerable.Range(0, 8).Select(_ => new List<(float[] r, float[] t)>()).ToArray();

        for (var y = 0; y < safeRoi.Height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var x = 0; x < safeRoi.Width; x++)
            {
                var r = refCrop.At<Vec3f>(y, x);
                var t = tgtCrop.At<Vec3f>(y, x);
                if (IsClipped(r) || IsClipped(t))
                {
                    continue;
                }

                var rr = new[] { r.Item0, r.Item1, r.Item2 };
                var tt = new[] { t.Item0, t.Item1, t.Item2 };
                var luma = (rr[0] + rr[1] + rr[2]) / 3f;
                var bucket = Math.Clamp((int)(luma * buckets.Length), 0, buckets.Length - 1);
                buckets[bucket].Add((rr, tt));
            }
        }

        var refSamples = new List<float[]>(Math.Min(maxSamples, safeRoi.Width * safeRoi.Height));
        var tgtSamples = new List<float[]>(Math.Min(maxSamples, safeRoi.Width * safeRoi.Height));
        var rng = new Random(randomSeed);
        var targetPerBucket = Math.Max(1, maxSamples / buckets.Length);
        var leftovers = new List<(float[] r, float[] t)>();
        foreach (var bucket in buckets)
        {
            var shuffled = bucket.OrderBy(_ => rng.Next()).ToList();
            var take = Math.Min(targetPerBucket, shuffled.Count);
            for (var i = 0; i < take; i++)
            {
                var sample = shuffled[i];
                refSamples.Add(sample.r);
                tgtSamples.Add(sample.t);
            }

            for (var i = take; i < shuffled.Count; i++)
            {
                leftovers.Add(shuffled[i]);
            }
        }

        foreach (var sample in leftovers.OrderBy(_ => rng.Next()))
        {
            if (refSamples.Count >= maxSamples)
            {
                break;
            }

            refSamples.Add(sample.r);
            tgtSamples.Add(sample.t);
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
}
