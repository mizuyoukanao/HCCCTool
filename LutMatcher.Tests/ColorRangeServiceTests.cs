using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class ColorRangeServiceTests
{
    private readonly ColorRangeService _service = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizeDenormalize_RoundTrip_IsStable(bool videoRange)
    {
        var samples = videoRange
            ? new[] { 16f / 255f, 32f / 255f, 64f / 255f, 128f / 255f, 200f / 255f, 235f / 255f }
            : new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f };
        foreach (var source in samples)
        {
            var working = _service.NormalizeEncodedToWorking(source, videoRange);
            var encoded = _service.DenormalizeWorkingToEncoded(working, videoRange);
            Assert.InRange(Math.Abs(encoded - source), 0, 0.0001);
        }
    }

    [Fact]
    public void VideoRange_BlackAndWhite_MapToLegalRange()
    {
        var black = _service.DenormalizeWorkingToEncoded(0f, true);
        var white = _service.DenormalizeWorkingToEncoded(1f, true);
        Assert.InRange(black, 16f / 255f - 1e-6f, 16f / 255f + 1e-6f);
        Assert.InRange(white, 235f / 255f - 1e-6f, 235f / 255f + 1e-6f);
    }
}
