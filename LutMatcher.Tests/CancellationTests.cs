using LutMatcher.Core.Models;
using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class CancellationTests
{
    [Fact]
    public void LutBaker_HonorsCancellation()
    {
        var model = new ColorTransformModel
        {
            ToneCurves = [Identity(), Identity(), Identity()],
            Matrix = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
            Bias = [0, 0, 0]
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new LutBaker().Bake(model, 65, cancellationToken: cts.Token));
    }

    private static float[] Identity() => Enumerable.Range(0, 256).Select(i => i / 255f).ToArray();
}
