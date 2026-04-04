using LutMatcher.Core.Models;

namespace LutMatcher.Core.Services;

public sealed class TransformPipeline
{
    private readonly ToneCurveFitter _toneCurveFitter = new();
    private readonly ColorMatrixFitter _matrixFitter = new();

    public ColorTransformModel Fit(SampleSet samples)
    {
        var curves = new float[3][];
        var toned = new float[samples.Count][];
        for (var i = 0; i < samples.Count; i++) toned[i] = new float[3];

        for (var c = 0; c < 3; c++)
        {
            curves[c] = _toneCurveFitter.Fit(samples.Target.Select(v => v[c]).ToArray(), samples.Reference.Select(v => v[c]).ToArray());
            for (var i = 0; i < samples.Count; i++) toned[i][c] = ToneCurveFitter.Apply(curves[c], samples.Target[i][c]);
        }

        var (m, b) = _matrixFitter.Fit(toned, samples.Reference);
        return new ColorTransformModel { ToneCurves = curves, Matrix = m, Bias = b };
    }

    public float[] Apply(ColorTransformModel model, float[] input)
    {
        var toned = new[]
        {
            ToneCurveFitter.Apply(model.ToneCurves[0], input[0]),
            ToneCurveFitter.Apply(model.ToneCurves[1], input[1]),
            ToneCurveFitter.Apply(model.ToneCurves[2], input[2])
        };
        return ColorMatrixFitter.Apply(toned, model.Matrix, model.Bias);
    }
}
