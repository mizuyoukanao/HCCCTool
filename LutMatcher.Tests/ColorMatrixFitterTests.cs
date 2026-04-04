using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class ColorMatrixFitterTests
{
    [Fact]
    public void MatrixFitting_RecoversKnownTransform()
    {
        var rng = new Random(0);
        var src = new List<float[]>();
        var dst = new List<float[]>();

        for (var i = 0; i < 200; i++)
        {
            var s = new[] { (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() };
            src.Add(s);
            dst.Add([
                0.9f * s[0] + 0.05f * s[1] + 0.01f,
                0.1f * s[0] + 0.8f * s[1] + 0.02f,
                0.05f * s[2] + 0.9f * s[1] + 0.03f
            ]);
        }

        var (m, b) = new ColorMatrixFitter().Fit([.. src], [.. dst], 1);
        var p = ColorMatrixFitter.Apply([0.4f, 0.5f, 0.6f], m, b);

        Assert.InRange(p[0], 0.395f, 0.405f);
        Assert.InRange(p[1], 0.455f, 0.465f);
        Assert.InRange(p[2], 0.505f, 0.515f);
    }

    [Fact]
    public void ClampBehavior_IsApplied()
    {
        var v = ColorMatrixFitter.Apply([2f, -1f, 0.5f], new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, [0, 0, 0]);
        Assert.Equal(1f, v[0]);
        Assert.Equal(0f, v[1]);
    }
}
