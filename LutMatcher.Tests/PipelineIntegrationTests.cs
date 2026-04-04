using LutMatcher.Core.Models;
using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class PipelineIntegrationTests
{
    [Fact]
    public void FitPipeline_ImprovesRmse_OnSyntheticTransform()
    {
        var rng = new Random(7);
        var reference = new List<float[]>();
        var target = new List<float[]>();
        for (var i = 0; i < 2000; i++)
        {
            var t = new[] { (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() };
            target.Add(t);
            reference.Add([
                Math.Clamp(0.92f * t[0] + 0.04f * t[1] + 0.01f, 0f, 1f),
                Math.Clamp(0.06f * t[0] + 0.86f * t[1] + 0.02f, 0f, 1f),
                Math.Clamp(0.08f * t[2] + 0.85f * t[1] + 0.02f, 0f, 1f)
            ]);
        }

        var samples = new SampleSet { Reference = [.. reference], Target = [.. target] };
        var pipeline = new TransformPipeline();
        var model = pipeline.Fit(samples);

        var before = samples.Target;
        var after = samples.Target.Select(v => pipeline.Apply(model, v)).ToArray();
        var metrics = new MetricsCalculator().Calculate(samples.Reference, before, after);

        Assert.True(metrics.ImprovementRatio > 2.0);
        Assert.True(metrics.Rmse < 0.03);
    }
}
