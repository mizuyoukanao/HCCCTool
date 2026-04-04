using LutMatcher.Core.Models;

namespace LutMatcher.Core.Services;

public sealed class LutBaker
{
    public LutData Bake(ColorTransformModel model, int size)
    {
        var entries = new List<(float R, float G, float B)>(size * size * size);
        for (var b = 0; b < size; b++)
        {
            for (var g = 0; g < size; g++)
            {
                for (var r = 0; r < size; r++)
                {
                    var input = new[] { r / (float)(size - 1), g / (float)(size - 1), b / (float)(size - 1) };
                    var toned = new[]
                    {
                        ToneCurveFitter.Apply(model.ToneCurves[0], input[0]),
                        ToneCurveFitter.Apply(model.ToneCurves[1], input[1]),
                        ToneCurveFitter.Apply(model.ToneCurves[2], input[2])
                    };
                    var transformed = ColorMatrixFitter.Apply(toned, model.Matrix, model.Bias);
                    entries.Add((transformed[0], transformed[1], transformed[2]));
                }
            }
        }

        return new LutData { Size = size, Entries = entries };
    }
}
