namespace LutMatcher.Core.Models;

public sealed class LutData
{
    public int Size { get; init; }
    public required List<(float R, float G, float B)> Entries { get; init; }
}
