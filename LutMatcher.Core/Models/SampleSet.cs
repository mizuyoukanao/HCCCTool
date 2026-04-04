namespace LutMatcher.Core.Models;

public sealed class SampleSet
{
    public required float[][] Reference { get; init; }
    public required float[][] Target { get; init; }
    public int Count => Reference.Length;
}
