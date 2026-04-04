using LutMatcher.Core.Models;

namespace LutMatcher.Core.Services;

public sealed class MetricsCalculator
{
    public FitMetrics Calculate(float[][] reference, float[][] before, float[][] after)
    {
        var beforeMae = 0.0;
        var afterMae = 0.0;
        var afterRmse = 0.0;
        var maxAbs = 0.0;

        for (var i = 0; i < reference.Length; i++)
        {
            for (var c = 0; c < 3; c++)
            {
                var b = Math.Abs(reference[i][c] - before[i][c]);
                var a = Math.Abs(reference[i][c] - after[i][c]);
                beforeMae += b;
                afterMae += a;
                afterRmse += a * a;
                maxAbs = Math.Max(maxAbs, a);
            }
        }

        var n = reference.Length * 3.0;
        return new FitMetrics
        {
            Mae = afterMae / n,
            Rmse = Math.Sqrt(afterRmse / n),
            MaxAbsError = maxAbs,
            ImprovementRatio = (beforeMae / n) / Math.Max(afterMae / n, 1e-9)
        };
    }
}
