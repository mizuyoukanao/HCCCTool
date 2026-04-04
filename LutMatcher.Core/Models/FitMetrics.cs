namespace LutMatcher.Core.Models;

public sealed class FitMetrics
{
    public double Mae { get; init; }
    public double Rmse { get; init; }
    public double MaxAbsError { get; init; }
    public double ImprovementRatio { get; init; }

    public override string ToString() =>
        $"MAE={Mae:F6}, RMSE={Rmse:F6}, MaxAbs={MaxAbsError:F6}, Improvement={ImprovementRatio:F2}x";
}
