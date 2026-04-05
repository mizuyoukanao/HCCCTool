using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class RoiStateService
{
    public Rect? ActiveRoi { get; private set; }

    public Rect Apply(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "ROI width and height must be positive.");
        }

        ActiveRoi = new Rect(x, y, width, height);
        return ActiveRoi.Value;
    }

    public void Clear() => ActiveRoi = null;
}
