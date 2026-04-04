using LutMatcher.Core.Models;
using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class CubeExporterTests
{
    [Fact]
    public void CubeHeader_IsValid()
    {
        var lut = new LutData
        {
            Size = 2,
            Entries =
            [
                (0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f), (1f, 1f, 0f),
                (0f, 0f, 1f), (1f, 0f, 1f), (0f, 1f, 1f), (1f, 1f, 1f)
            ]
        };
        var text = new CubeExporter().BuildText(lut, "Test");

        Assert.Contains("TITLE \"Test\"", text);
        Assert.Contains("LUT_3D_SIZE 2", text);
        Assert.Contains("DOMAIN_MIN 0.0 0.0 0.0", text);
        Assert.Contains("DOMAIN_MAX 1.0 1.0 1.0", text);
    }

    [Fact]
    public void CubeOrder_RedFastestBlueSlowest()
    {
        var model = new ColorTransformModel
        {
            ToneCurves = [Identity(), Identity(), Identity()],
            Matrix = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
            Bias = [0, 0, 0]
        };
        var lut = new LutBaker().Bake(model, 2);
        Assert.Equal((0f, 0f, 0f), lut.Entries[0]);
        Assert.Equal((1f, 0f, 0f), lut.Entries[1]);
        Assert.Equal((0f, 1f, 0f), lut.Entries[2]);
        Assert.Equal((0f, 0f, 1f), lut.Entries[4]);
    }

    private static float[] Identity() => Enumerable.Range(0, 256).Select(i => i / 255f).ToArray();
}
