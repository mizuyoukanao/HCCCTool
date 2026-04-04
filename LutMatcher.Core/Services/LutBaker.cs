using LutMatcher.Core.Models;

namespace LutMatcher.Core.Services;

public sealed class LutBaker
{
    private readonly ColorRangeService _range = new();

    public LutData Bake(ColorTransformModel model, int size, bool videoRange = false, CancellationToken cancellationToken = default)
    {
        var entries = new List<(float R, float G, float B)>(size * size * size);
        for (var b = 0; b < size; b++)
        {
            for (var g = 0; g < size; g++)
            {
                for (var r = 0; r < size; r++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var encodedInput = new[] { r / (float)(size - 1), g / (float)(size - 1), b / (float)(size - 1) };
                    var input = _range.NormalizeEncodedToWorking(encodedInput, videoRange);
                    var toned = new[]
                    {
                        ToneCurveFitter.Apply(model.ToneCurves[0], input[0]),
                        ToneCurveFitter.Apply(model.ToneCurves[1], input[1]),
                        ToneCurveFitter.Apply(model.ToneCurves[2], input[2])
                    };
                    var transformed = ColorMatrixFitter.Apply(toned, model.Matrix, model.Bias);
                    var encodedOutput = _range.DenormalizeWorkingToEncoded(transformed, videoRange);
                    entries.Add((encodedOutput[0], encodedOutput[1], encodedOutput[2]));
                }
            }
        }

        return new LutData { Size = size, Entries = entries };
    }
}
