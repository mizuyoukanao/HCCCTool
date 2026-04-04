namespace LutMatcher.Core.Services;

public sealed class ToneCurveFitter
{
    public float[] Fit(float[] source, float[] target, int bins = 256)
    {
        var buckets = Enumerable.Range(0, bins).Select(_ => new List<float>()).ToArray();
        for (var i = 0; i < source.Length; i++)
        {
            var idx = Math.Clamp((int)(source[i] * (bins - 1)), 0, bins - 1);
            buckets[idx].Add(target[i]);
        }

        var curve = new float[bins];
        for (var i = 0; i < bins; i++)
        {
            curve[i] = buckets[i].Count == 0 ? float.NaN : TrimmedMean(buckets[i], 0.1f);
        }

        InterpolateMissing(curve);
        EnforceMonotonic(curve);
        Smooth(curve);
        EnforceMonotonic(curve);
        return curve;
    }

    public static float Apply(float[] curve, float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        var pos = x * (curve.Length - 1);
        var i = (int)pos;
        var j = Math.Min(i + 1, curve.Length - 1);
        var t = pos - i;
        return curve[i] * (1f - t) + curve[j] * t;
    }

    private static float TrimmedMean(List<float> values, float trimRatio)
    {
        values.Sort();
        var trim = (int)(values.Count * trimRatio);
        var start = Math.Min(trim, values.Count - 1);
        var end = Math.Max(start + 1, values.Count - trim);
        var span = values.GetRange(start, end - start);
        return span.Average();
    }

    private static void InterpolateMissing(float[] curve)
    {
        var lastValid = -1;
        for (var i = 0; i < curve.Length; i++)
        {
            if (float.IsNaN(curve[i]))
            {
                continue;
            }

            if (lastValid + 1 < i)
            {
                var start = lastValid >= 0 ? curve[lastValid] : curve[i];
                var gap = i - lastValid;
                for (var k = lastValid + 1; k < i; k++)
                {
                    var t = (float)(k - lastValid) / gap;
                    curve[k] = start + (curve[i] - start) * t;
                }
            }

            lastValid = i;
        }

        if (lastValid < 0)
        {
            for (var i = 0; i < curve.Length; i++) curve[i] = i / (float)(curve.Length - 1);
            return;
        }

        for (var i = 0; i < curve.Length; i++)
        {
            if (float.IsNaN(curve[i])) curve[i] = curve[lastValid];
        }
    }

    private static void EnforceMonotonic(float[] curve)
    {
        curve[0] = Math.Clamp(curve[0], 0f, 1f);
        for (var i = 1; i < curve.Length; i++)
        {
            curve[i] = Math.Clamp(Math.Max(curve[i], curve[i - 1]), 0f, 1f);
        }
    }

    private static void Smooth(float[] curve)
    {
        var copy = (float[])curve.Clone();
        for (var i = 1; i < curve.Length - 1; i++)
        {
            curve[i] = (copy[i - 1] + 2f * copy[i] + copy[i + 1]) / 4f;
        }
    }
}
