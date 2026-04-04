namespace LutMatcher.Core.Models;

public sealed class ColorTransformModel
{
    public required float[][] ToneCurves { get; init; }
    public required double[,] Matrix { get; init; }
    public required double[] Bias { get; init; }
}
