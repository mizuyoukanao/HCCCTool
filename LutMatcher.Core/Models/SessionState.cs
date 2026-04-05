namespace LutMatcher.Core.Models;

public sealed class SessionState
{
    public SourceType ReferenceSourceType { get; set; }
    public SourceType TargetSourceType { get; set; }
    public string? ReferenceSourcePath { get; set; }
    public string? TargetSourcePath { get; set; }
    public int ReferenceFrameIndex { get; set; }
    public int TargetFrameIndex { get; set; }
    public double ReferenceTimestampMs { get; set; }
    public double TargetTimestampMs { get; set; }
    public int ManualOffsetFrames { get; set; }
    public double ManualOffsetMs { get; set; }
    public bool IsFullRange { get; set; }
    public int LutSize { get; set; }
    public bool EnableAutoAlign { get; set; }
    public double? EstimatedShiftX { get; set; }
    public double? EstimatedShiftY { get; set; }

    public int RoiX { get; set; }
    public int RoiY { get; set; }
    public int RoiW { get; set; }
    public int RoiH { get; set; }
    public bool HasActiveRoi { get; set; }

    public ColorTransformModel? Model { get; set; }
    public FitMetrics? Metrics { get; set; }
}
