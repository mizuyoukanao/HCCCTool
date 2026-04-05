using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class RoiStateServiceTests
{
    [Fact]
    public void ApplyAndClear_ChangesState()
    {
        var service = new RoiStateService();
        var roi = service.Apply(1, 2, 3, 4);
        Assert.Equal(1, roi.X);
        Assert.True(service.ActiveRoi.HasValue);

        service.Clear();
        Assert.False(service.ActiveRoi.HasValue);
    }
}
