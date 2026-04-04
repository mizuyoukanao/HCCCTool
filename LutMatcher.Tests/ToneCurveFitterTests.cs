using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class ToneCurveFitterTests
{
    [Fact]
    public void ToneCurve_IsMonotonic()
    {
        var source = Enumerable.Range(0, 4096).Select(i => i / 4095f).ToArray();
        var target = source.Select(v => MathF.Pow(v, 0.9f)).ToArray();

        var curve = new ToneCurveFitter().Fit(source, target);
        for (var i = 1; i < curve.Length; i++)
        {
            Assert.True(curve[i] >= curve[i - 1]);
        }
    }
}
