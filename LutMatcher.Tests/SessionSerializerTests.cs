using LutMatcher.Core.Models;
using LutMatcher.Core.Services;
using OpenCvSharp;

namespace LutMatcher.Tests;

public sealed class SessionSerializerTests
{
    [Fact]
    public void SaveLoad_RoundTrip_PreservesState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lutmatcher_session_{Guid.NewGuid():N}.lmz");
        try
        {
            var state = new SessionState
            {
                ReferenceSourceType = SourceType.Video,
                TargetSourceType = SourceType.Camera,
                ReferenceFrameIndex = 12,
                TargetFrameIndex = 22,
                ReferenceTimestampMs = 1200,
                TargetTimestampMs = 2200,
                HasActiveRoi = true,
                RoiX = 1,
                RoiY = 2,
                RoiW = 10,
                RoiH = 20,
                IsFullRange = false,
                LutSize = 33,
                EnableAutoAlign = true,
                Model = new ColorTransformModel
                {
                    ToneCurves = [Identity(), Identity(), Identity()],
                    Matrix = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
                    Bias = [0, 0, 0]
                },
                Metrics = new FitMetrics { Mae = 0.1, Rmse = 0.2, MaxAbsError = 0.3, ImprovementRatio = 1.2 }
            };

            using var reference = new Mat(new Size(8, 8), MatType.CV_8UC3, new Scalar(10, 20, 30));
            using var target = new Mat(new Size(8, 8), MatType.CV_8UC3, new Scalar(50, 60, 70));

            var serializer = new SessionSerializer();
            serializer.Save(path, state, reference, target);
            var loaded = serializer.Load(path);

            Assert.Equal(SourceType.Video, loaded.State.ReferenceSourceType);
            Assert.Equal(12, loaded.State.ReferenceFrameIndex);
            Assert.True(loaded.State.HasActiveRoi);
            Assert.NotNull(loaded.State.Model);
            Assert.NotNull(loaded.Reference);
            Assert.NotNull(loaded.Target);
            Assert.Equal(8, loaded.Reference!.Rows);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static float[] Identity() => Enumerable.Range(0, 256).Select(i => i / 255f).ToArray();
}
