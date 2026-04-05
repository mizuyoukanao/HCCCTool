using LutMatcher.Core.Services;

namespace LutMatcher.Tests;

public sealed class CameraCaptureServiceTests
{
    [Fact]
    public void FreezePair_WithoutStart_Throws()
    {
        using var service = new CameraCaptureService();
        Assert.Throws<InvalidOperationException>(() => service.FreezePair());
    }

    [Fact]
    public void Stop_CanBeCalledRepeatedly()
    {
        using var service = new CameraCaptureService();
        service.Stop();
        service.Stop();
        Assert.False(service.IsRunning);
    }
}
