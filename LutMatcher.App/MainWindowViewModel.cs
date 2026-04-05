using System.Windows.Threading;
using LutMatcher.Core.Models;
using LutMatcher.Core.Services;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Rect = OpenCvSharp.Rect;

namespace LutMatcher.App;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ImageLoader _loader = new();
    private readonly SampleExtractor _extractor = new();
    private readonly TransformPipeline _pipeline = new();
    private readonly MetricsCalculator _metrics = new();
    private readonly LutBaker _baker = new();
    private readonly CubeExporter _exporter = new();
    private readonly ColorRangeService _range = new();
    private readonly AlignmentService _alignment = new();
    private readonly VideoFrameProvider _referenceVideo = new();
    private readonly VideoFrameProvider _targetVideo = new();
    private readonly CameraCaptureService _cameraService = new();
    private readonly SessionSerializer _sessionSerializer = new();
    private readonly DispatcherTimer _cameraPreviewTimer;

    private Mat? _reference;
    private Mat? _target;
    private Mat? _corrected;
    private ColorTransformModel? _model;
    private FitMetrics? _lastMetrics;
    private CancellationTokenSource? _cts;
    private List<ImageLoader.CameraInfo> _cameraOptions = [];
    private bool _isBusy;
    private Rect? _activeRoi;
    private Point2d? _lastShift;
    private SourceType _referenceSourceType;
    private SourceType _targetSourceType;
    private string? _referencePath;
    private string? _targetPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? ReferencePreview { get; private set; }
    public object? TargetPreview { get; private set; }
    public object? CorrectedPreview { get; private set; }
    public object? DiffPreview { get; private set; }
    public string MetricsText { get; private set; } = "No metrics yet.";
    public string ReferenceSourceText => $"Reference Source: {_referenceSourceType}";
    public string TargetSourceText => $"Target Source: {_targetSourceType}";

    public bool IsFullRange { get; set; } = true;
    public bool IsVideoRange
    {
        get => !IsFullRange;
        set
        {
            IsFullRange = !value;
            OnPropertyChanged(nameof(IsFullRange));
            OnPropertyChanged();
        }
    }

    public List<int> LutSizes { get; } = [17, 33, 65];
    public int SelectedLutSize { get; set; } = 33;

    public string RoiX { get; set; } = "0";
    public string RoiY { get; set; } = "0";
    public string RoiW { get; set; } = "0";
    public string RoiH { get; set; } = "0";
    public bool HasActiveRoi => _activeRoi.HasValue;
    public string ActiveRoiText => _activeRoi is { } roi ? $"Active ROI: x={roi.X}, y={roi.Y}, w={roi.Width}, h={roi.Height}" : "Active ROI: none";

    public bool EnableAutoAlign { get; set; } = true;
    public List<ImageLoader.CameraInfo> CameraOptions
    {
        get => _cameraOptions;
        private set { _cameraOptions = value; OnPropertyChanged(); }
    }

    public ImageLoader.CameraInfo? SelectedReferenceCamera { get; set; }
    public ImageLoader.CameraInfo? SelectedTargetCamera { get; set; }

    public int ReferenceVideoFrameIndex { get; set; }
    public int TargetVideoFrameIndex { get; set; }
    public string ReferenceVideoTimeMs { get; set; } = "0";
    public string TargetVideoTimeMs { get; set; } = "0";
    public string ManualOffsetFrames { get; set; } = "0";
    public string ManualOffsetMs { get; set; } = "0";
    public string ReferenceVideoInfo => BuildVideoInfo(_referenceVideo);
    public string TargetVideoInfo => BuildVideoInfo(_targetVideo);

    private string _logText = "Ready.";
    public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); NotifyButtons(); }
    }

    public ICommand LoadReferenceCommand { get; }
    public ICommand LoadTargetCommand { get; }
    public ICommand AutoFitCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCameraListCommand { get; }
    public ICommand ApplyRoiCommand { get; }
    public ICommand ClearRoiCommand { get; }
    public ICommand SeekReferenceFrameCommand { get; }
    public ICommand SeekTargetFrameCommand { get; }
    public ICommand SeekReferenceTimeCommand { get; }
    public ICommand SeekTargetTimeCommand { get; }
    public ICommand UseCurrentFramesCommand { get; }
    public ICommand StartCamerasCommand { get; }
    public ICommand StopCamerasCommand { get; }
    public ICommand FreezePairCommand { get; }
    public ICommand SaveSessionCommand { get; }
    public ICommand LoadSessionCommand { get; }

    public MainWindowViewModel()
    {
        LoadReferenceCommand = new RelayCommand(() => LoadSource(true), () => !IsBusy);
        LoadTargetCommand = new RelayCommand(() => LoadSource(false), () => !IsBusy);
        AutoFitCommand = new RelayCommand(async () => await FitAsync(), () => !IsBusy && _reference is not null && _target is not null);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !IsBusy && _model is not null);
        ResetCommand = new RelayCommand(Reset, () => !IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RefreshCameraListCommand = new RelayCommand(RefreshCameraList, () => !IsBusy);
        ApplyRoiCommand = new RelayCommand(ApplyRoi, () => !IsBusy);
        ClearRoiCommand = new RelayCommand(ClearRoi, () => !IsBusy);
        SeekReferenceFrameCommand = new RelayCommand(async () => await SeekVideoAsync(true, true), () => !IsBusy && _referenceVideo.IsOpen);
        SeekTargetFrameCommand = new RelayCommand(async () => await SeekVideoAsync(false, true), () => !IsBusy && _targetVideo.IsOpen);
        SeekReferenceTimeCommand = new RelayCommand(async () => await SeekVideoAsync(true, false), () => !IsBusy && _referenceVideo.IsOpen);
        SeekTargetTimeCommand = new RelayCommand(async () => await SeekVideoAsync(false, false), () => !IsBusy && _targetVideo.IsOpen);
        UseCurrentFramesCommand = new RelayCommand(UseCurrentFrames, () => !IsBusy && (_referenceVideo.IsOpen || _targetVideo.IsOpen));
        StartCamerasCommand = new RelayCommand(StartCameras, () => !IsBusy && !_cameraService.IsRunning);
        StopCamerasCommand = new RelayCommand(StopCameras, () => _cameraService.IsRunning);
        FreezePairCommand = new RelayCommand(FreezePair, () => !IsBusy && _cameraService.IsRunning);
        SaveSessionCommand = new RelayCommand(async () => await SaveSessionAsync(), () => !IsBusy);
        LoadSessionCommand = new RelayCommand(async () => await LoadSessionAsync(), () => !IsBusy);

        _cameraPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _cameraPreviewTimer.Tick += (_, _) => RefreshCameraPreview();

        RefreshCameraList();
    }

    private void LoadSource(bool reference)
    {
        var dlg = new OpenFileDialog { Filter = "Supported|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.mp4;*.mov;*.mkv;*.avi" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (_loader.IsVideoPath(dlg.FileName))
            {
                var provider = reference ? _referenceVideo : _targetVideo;
                provider.Open(dlg.FileName);
                UpdateVideoProperties(reference, 0, TimeSpan.Zero);
                using var frame = provider.GetFrameByIndex(0);
                SetFrame(reference, frame, SourceType.Video, dlg.FileName);
                AppendLog($"Loaded {(reference ? "Reference" : "Target")} video: {dlg.FileName}");
                return;
            }

            var mat = _loader.LoadFirstFrame(dlg.FileName);
            SetFrame(reference, mat, SourceType.Image, dlg.FileName, ownsMat: true);
            AppendLog($"Loaded {(reference ? "Reference" : "Target")} image: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            AppendLog($"Load failed: {ex.Message}");
        }
    }

    private async Task SeekVideoAsync(bool reference, bool byFrame)
    {
        var provider = reference ? _referenceVideo : _targetVideo;
        if (!provider.IsOpen) return;

        await RunBusyAsync(async token =>
        {
            Mat frame;
            if (byFrame)
            {
                var index = reference ? ReferenceVideoFrameIndex : TargetVideoFrameIndex;
                frame = await Task.Run(() => provider.GetFrameByIndex(index, token), token);
            }
            else
            {
                var timeText = reference ? ReferenceVideoTimeMs : TargetVideoTimeMs;
                if (!double.TryParse(timeText, out var ms) || ms < 0) throw new InvalidOperationException("Video time must be a non-negative number.");
                frame = await Task.Run(() => provider.GetFrameByTimestamp(TimeSpan.FromMilliseconds(ms), token), token);
            }

            using (frame)
            {
                UpdateVideoProperties(reference, provider.GetCurrentFrameIndex(), provider.GetCurrentTimestamp());
                SetFrame(reference, frame, SourceType.Video, provider.SourcePath);
            }
        }, "Video seek cancelled.");
    }

    private void UseCurrentFrames()
    {
        if (_referenceVideo.IsOpen)
        {
            using var frame = _referenceVideo.GetFrameByIndex(_referenceVideo.GetCurrentFrameIndex());
            SetFrame(true, frame, SourceType.Video, _referenceVideo.SourcePath);
        }

        if (_targetVideo.IsOpen)
        {
            using var frame = _targetVideo.GetFrameByIndex(_targetVideo.GetCurrentFrameIndex());
            SetFrame(false, frame, SourceType.Video, _targetVideo.SourcePath);
        }

        ApplyManualOffsetForVideos();
        AppendLog("Current video frames applied.");
    }

    private void ApplyManualOffsetForVideos()
    {
        if (!int.TryParse(ManualOffsetFrames, out var offsetFrames)) offsetFrames = 0;
        if (!double.TryParse(ManualOffsetMs, out var offsetMs)) offsetMs = 0;

        if (_targetVideo.IsOpen)
        {
            if (offsetFrames != 0)
            {
                var idx = Math.Max(0, _targetVideo.GetCurrentFrameIndex() + offsetFrames);
                using var frame = _targetVideo.GetFrameByIndex(idx);
                SetFrame(false, frame, SourceType.Video, _targetVideo.SourcePath);
                UpdateVideoProperties(false, _targetVideo.GetCurrentFrameIndex(), _targetVideo.GetCurrentTimestamp());
            }
            else if (Math.Abs(offsetMs) > 0.1)
            {
                var ts = _targetVideo.GetCurrentTimestamp() + TimeSpan.FromMilliseconds(offsetMs);
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
                using var frame = _targetVideo.GetFrameByTimestamp(ts);
                SetFrame(false, frame, SourceType.Video, _targetVideo.SourcePath);
                UpdateVideoProperties(false, _targetVideo.GetCurrentFrameIndex(), _targetVideo.GetCurrentTimestamp());
            }
        }
    }

    private void StartCameras()
    {
        if (SelectedReferenceCamera is not { } refCam || SelectedTargetCamera is not { } targetCam)
        {
            AppendLog("Select both camera devices first.");
            return;
        }

        try
        {
            _cameraService.Start(refCam.Index, targetCam.Index);
            _cameraPreviewTimer.Start();
            _referenceSourceType = SourceType.Camera;
            _targetSourceType = SourceType.Camera;
            OnPropertyChanged(nameof(ReferenceSourceText));
            OnPropertyChanged(nameof(TargetSourceText));
            AppendLog($"Cameras started. Ref={_cameraService.ReferenceDeviceInfo}, Target={_cameraService.TargetDeviceInfo}");
            NotifyButtons();
        }
        catch (Exception ex)
        {
            AppendLog($"Camera start failed: {ex.Message}");
        }
    }

    private void StopCameras()
    {
        _cameraPreviewTimer.Stop();
        _cameraService.Stop();
        AppendLog("Cameras stopped.");
        NotifyButtons();
    }

    private void FreezePair()
    {
        try
        {
            using var pair = new DisposablePair(_cameraService.FreezePair());
            SetFrame(true, pair.Reference, SourceType.Camera);
            SetFrame(false, pair.Target, SourceType.Camera);
            AppendLog("Camera pair frozen.");
        }
        catch (Exception ex)
        {
            AppendLog($"Freeze failed: {ex.Message}");
        }
    }

    private void RefreshCameraPreview()
    {
        if (!_cameraService.IsRunning) return;

        var preview = _cameraService.GetLatestPreview();
        using (preview.Reference)
        {
            if (preview.Reference is not null)
            {
                ReferencePreview = preview.Reference.ToWriteableBitmap();
                OnPropertyChanged(nameof(ReferencePreview));
            }
        }

        using (preview.Target)
        {
            if (preview.Target is not null)
            {
                TargetPreview = preview.Target.ToWriteableBitmap();
                OnPropertyChanged(nameof(TargetPreview));
            }
        }
    }

    private async Task FitAsync()
    {
        if (_reference is null || _target is null) return;

        await RunBusyAsync(async token =>
        {
            AppendLog("Fitting started...");
            var alignmentResult = await Task.Run(() => BuildAlignedTarget(_reference, _target, token), token);
            using var alignedTarget = alignmentResult.Target;
            _lastShift = alignmentResult.Shift;
            AppendLog(alignmentResult.Message);

            var sampleSet = await Task.Run(() => _extractor.Extract(_reference, alignedTarget, _activeRoi, !IsFullRange, cancellationToken: token), token);
            _model = await Task.Run(() => _pipeline.Fit(sampleSet), token);
            _corrected?.Dispose();
            _corrected = await Task.Run(() => ApplyModelToImage(alignedTarget, _model, !IsFullRange, token), token);
            CorrectedPreview = _corrected.ToWriteableBitmap();
            OnPropertyChanged(nameof(CorrectedPreview));

            using var diff = BuildDiff(_reference, _corrected);
            DiffPreview = diff.ToWriteableBitmap();
            OnPropertyChanged(nameof(DiffPreview));

            var before = sampleSet.Target;
            var after = sampleSet.Target.Select(v => _pipeline.Apply(_model, v)).ToArray();
            _lastMetrics = _metrics.Calculate(sampleSet.Reference, before, after);
            MetricsText = $"MAE: {_lastMetrics.Mae:F6}\nRMSE: {_lastMetrics.Rmse:F6}\nMax Abs: {_lastMetrics.MaxAbsError:F6}\nImprovement: {_lastMetrics.ImprovementRatio:F2}x";
            OnPropertyChanged(nameof(MetricsText));
            AppendLog($"Fit complete. {_lastMetrics}");
            NotifyButtons();
        }, "Operation cancelled.");
    }

    private async Task ExportAsync()
    {
        if (_model is null) return;
        var dlg = new SaveFileDialog { Filter = "Cube LUT|*.cube", FileName = "LutMatcher.cube" };
        if (dlg.ShowDialog() != true) return;

        await RunBusyAsync(async token =>
        {
            AppendLog("Export started...");
            var lut = await Task.Run(() => _baker.Bake(_model, SelectedLutSize, !IsFullRange, token), token);
            await _exporter.ExportAsync(dlg.FileName, lut, "LutMatcher", token);
            AppendLog($"Exported: {dlg.FileName}");
        }, "Export cancelled.");
    }

    private async Task SaveSessionAsync()
    {
        var dlg = new SaveFileDialog { Filter = "LUT Matcher Session|*.lmz", FileName = "session.lmz" };
        if (dlg.ShowDialog() != true) return;

        await RunBusyAsync(async token =>
        {
            var state = new SessionState
            {
                ReferenceSourceType = _referenceSourceType,
                TargetSourceType = _targetSourceType,
                ReferenceSourcePath = _referencePath,
                TargetSourcePath = _targetPath,
                ReferenceFrameIndex = ReferenceVideoFrameIndex,
                TargetFrameIndex = TargetVideoFrameIndex,
                ReferenceTimestampMs = ParseDoubleOrDefault(ReferenceVideoTimeMs),
                TargetTimestampMs = ParseDoubleOrDefault(TargetVideoTimeMs),
                ManualOffsetFrames = ParseIntOrDefault(ManualOffsetFrames),
                ManualOffsetMs = ParseDoubleOrDefault(ManualOffsetMs),
                IsFullRange = IsFullRange,
                LutSize = SelectedLutSize,
                EnableAutoAlign = EnableAutoAlign,
                EstimatedShiftX = _lastShift?.X,
                EstimatedShiftY = _lastShift?.Y,
                RoiX = ParseIntOrDefault(RoiX),
                RoiY = ParseIntOrDefault(RoiY),
                RoiW = ParseIntOrDefault(RoiW),
                RoiH = ParseIntOrDefault(RoiH),
                HasActiveRoi = _activeRoi.HasValue,
                Model = _model,
                Metrics = _lastMetrics
            };

            await Task.Run(() => _sessionSerializer.Save(dlg.FileName, state, _reference, _target, token), token);
            AppendLog($"Session saved: {dlg.FileName}");
        }, "Save session cancelled.");
    }

    private async Task LoadSessionAsync()
    {
        var dlg = new OpenFileDialog { Filter = "LUT Matcher Session|*.lmz" };
        if (dlg.ShowDialog() != true) return;

        await RunBusyAsync(async token =>
        {
            var loaded = await Task.Run(() => _sessionSerializer.Load(dlg.FileName, token), token);
            var state = loaded.State;

            IsFullRange = state.IsFullRange;
            OnPropertyChanged(nameof(IsFullRange));
            OnPropertyChanged(nameof(IsVideoRange));
            SelectedLutSize = state.LutSize;
            EnableAutoAlign = state.EnableAutoAlign;
            _lastShift = state.EstimatedShiftX.HasValue && state.EstimatedShiftY.HasValue
                ? new Point2d(state.EstimatedShiftX.Value, state.EstimatedShiftY.Value)
                : null;

            RoiX = state.RoiX.ToString(); RoiY = state.RoiY.ToString(); RoiW = state.RoiW.ToString(); RoiH = state.RoiH.ToString();
            _activeRoi = state.HasActiveRoi ? new Rect(state.RoiX, state.RoiY, state.RoiW, state.RoiH) : null;
            OnPropertyChanged(nameof(RoiX)); OnPropertyChanged(nameof(RoiY)); OnPropertyChanged(nameof(RoiW)); OnPropertyChanged(nameof(RoiH));
            OnPropertyChanged(nameof(HasActiveRoi)); OnPropertyChanged(nameof(ActiveRoiText));

            ReferenceVideoFrameIndex = state.ReferenceFrameIndex;
            TargetVideoFrameIndex = state.TargetFrameIndex;
            ReferenceVideoTimeMs = state.ReferenceTimestampMs.ToString("F0");
            TargetVideoTimeMs = state.TargetTimestampMs.ToString("F0");
            ManualOffsetFrames = state.ManualOffsetFrames.ToString();
            ManualOffsetMs = state.ManualOffsetMs.ToString("F0");

            _model = state.Model;
            _lastMetrics = state.Metrics;
            if (_lastMetrics is not null)
            {
                MetricsText = $"MAE: {_lastMetrics.Mae:F6}\nRMSE: {_lastMetrics.Rmse:F6}\nMax Abs: {_lastMetrics.MaxAbsError:F6}\nImprovement: {_lastMetrics.ImprovementRatio:F2}x";
                OnPropertyChanged(nameof(MetricsText));
            }

            SetFrame(true, loaded.Reference, state.ReferenceSourceType, state.ReferenceSourcePath, ownsMat: true);
            SetFrame(false, loaded.Target, state.TargetSourceType, state.TargetSourcePath, ownsMat: true);
            AppendLog($"Session loaded: {dlg.FileName}");
            NotifyButtons();
        }, "Load session cancelled.");
    }

    private (Mat Target, string Message, Point2d? Shift) BuildAlignedTarget(Mat reference, Mat target, CancellationToken cancellationToken)
    {
        if (!EnableAutoAlign)
        {
            var resized = target.Size() == reference.Size() ? target.Clone() : target.Resize(reference.Size());
            return (resized, "Auto align disabled.", null);
        }

        if (_alignment.TryAlignByTranslation(reference, target, out var aligned, out var shift, cancellationToken))
        {
            return (aligned, $"Auto align shift: dx={shift.X:F2}, dy={shift.Y:F2}", shift);
        }

        var fallback = target.Size() == reference.Size() ? target.Clone() : target.Resize(reference.Size());
        return (fallback, "Auto align failed. Falling back to simple resize.", null);
    }

    private Mat ApplyModelToImage(Mat bgr, ColorTransformModel model, bool videoRange, CancellationToken cancellationToken)
    {
        using var rgbFloat = _range.ConvertBgrToWorkingRgb(bgr, videoRange);
        using var output = new Mat(rgbFloat.Rows, rgbFloat.Cols, MatType.CV_32FC3);

        for (var y = 0; y < rgbFloat.Rows; y++)
        {
            if ((y & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < rgbFloat.Cols; x++)
            {
                var p = rgbFloat.At<Vec3f>(y, x);
                var corrected = ColorMatrixFitter.Apply([
                    ToneCurveFitter.Apply(model.ToneCurves[0], p.Item0),
                    ToneCurveFitter.Apply(model.ToneCurves[1], p.Item1),
                    ToneCurveFitter.Apply(model.ToneCurves[2], p.Item2)
                ], model.Matrix, model.Bias);
                output.Set(y, x, new Vec3f(corrected[0], corrected[1], corrected[2]));
            }
        }

        return _range.ConvertWorkingRgbToBgr(output, videoRange);
    }

    private static Mat BuildDiff(Mat reference, Mat corrected)
    {
        using var correctedResized = corrected.Resize(reference.Size());
        using var ref32 = new Mat(); using var corr32 = new Mat();
        reference.ConvertTo(ref32, MatType.CV_32FC3, 1.0 / 255.0);
        correctedResized.ConvertTo(corr32, MatType.CV_32FC3, 1.0 / 255.0);
        using var diff = new Mat();
        Cv2.Absdiff(ref32, corr32, diff);
        var channels = diff.Split();
        using var err = new Mat();
        Cv2.AddWeighted(channels[0], 1.0 / 3.0, channels[1], 1.0 / 3.0, 0, err);
        Cv2.AddWeighted(err, 1.0, channels[2], 1.0 / 3.0, 0, err);
        foreach (var channel in channels) channel.Dispose();

        using var err8 = new Mat();
        Cv2.Normalize(err, err, 0, 255, NormTypes.MinMax);
        err.ConvertTo(err8, MatType.CV_8UC1);
        var outMat = new Mat();
        Cv2.ApplyColorMap(err8, outMat, ColormapTypes.Turbo);
        return outMat;
    }

    private void ApplyRoi()
    {
        if (!TryParseRoi(out var roi, out var error))
        {
            AppendLog($"ROI apply failed: {error}");
            return;
        }

        _activeRoi = roi;
        OnPropertyChanged(nameof(HasActiveRoi));
        OnPropertyChanged(nameof(ActiveRoiText));
        AppendLog($"ROI applied: x={roi.X}, y={roi.Y}, w={roi.Width}, h={roi.Height}");
    }

    private void ClearRoi()
    {
        RoiX = "0"; RoiY = "0"; RoiW = "0"; RoiH = "0";
        _activeRoi = null;
        OnPropertyChanged(nameof(RoiX)); OnPropertyChanged(nameof(RoiY)); OnPropertyChanged(nameof(RoiW)); OnPropertyChanged(nameof(RoiH));
        OnPropertyChanged(nameof(HasActiveRoi));
        OnPropertyChanged(nameof(ActiveRoiText));
        AppendLog("ROI cleared.");
    }

    private bool TryParseRoi(out Rect roi, out string error)
    {
        roi = default;
        error = string.Empty;
        if (!int.TryParse(RoiW, out var w) || !int.TryParse(RoiH, out var h) || w <= 0 || h <= 0)
        {
            error = "Width and height must be positive integers.";
            return false;
        }

        if (!int.TryParse(RoiX, out var x)) x = 0;
        if (!int.TryParse(RoiY, out var y)) y = 0;
        roi = new Rect(x, y, w, h);
        return true;
    }

    private void SetFrame(bool reference, Mat? input, SourceType sourceType, string? path = null, bool ownsMat = false)
    {
        if (input is null || input.Empty()) return;
        var mat = ownsMat ? input : input.Clone();

        if (reference)
        {
            _reference?.Dispose();
            _reference = mat;
            ReferencePreview = mat.ToWriteableBitmap();
            _referenceSourceType = sourceType;
            _referencePath = path;
            OnPropertyChanged(nameof(ReferencePreview));
            OnPropertyChanged(nameof(ReferenceSourceText));
        }
        else
        {
            _target?.Dispose();
            _target = mat;
            TargetPreview = mat.ToWriteableBitmap();
            _targetSourceType = sourceType;
            _targetPath = path;
            OnPropertyChanged(nameof(TargetPreview));
            OnPropertyChanged(nameof(TargetSourceText));
        }

        NotifyButtons();
    }

    private void Reset()
    {
        _cameraPreviewTimer.Stop();
        _cameraService.Stop();
        _referenceVideo.Dispose();
        _targetVideo.Dispose();
        _reference?.Dispose(); _target?.Dispose(); _corrected?.Dispose();
        _reference = null; _target = null; _corrected = null;
        _model = null; _lastMetrics = null; _activeRoi = null; _lastShift = null;
        _referenceSourceType = SourceType.None; _targetSourceType = SourceType.None;
        ReferencePreview = null; TargetPreview = null; CorrectedPreview = null; DiffPreview = null;
        MetricsText = "No metrics yet."; LogText = "Reset complete.";
        OnPropertyChanged(nameof(ReferencePreview)); OnPropertyChanged(nameof(TargetPreview));
        OnPropertyChanged(nameof(CorrectedPreview)); OnPropertyChanged(nameof(DiffPreview));
        OnPropertyChanged(nameof(MetricsText));
        OnPropertyChanged(nameof(ReferenceSourceText)); OnPropertyChanged(nameof(TargetSourceText));
        OnPropertyChanged(nameof(HasActiveRoi)); OnPropertyChanged(nameof(ActiveRoiText));
        NotifyButtons();
    }

    private void RefreshCameraList()
    {
        var options = _cameraService.GetAvailableCameras().ToList();
        CameraOptions = options;
        if (options.Count == 0)
        {
            SelectedReferenceCamera = null; SelectedTargetCamera = null;
            AppendLog("No cameras detected.");
            return;
        }

        if (SelectedReferenceCamera is null || options.All(v => v.Index != SelectedReferenceCamera.Value.Index))
        {
            SelectedReferenceCamera = options[0]; OnPropertyChanged(nameof(SelectedReferenceCamera));
        }

        if (SelectedTargetCamera is null || options.All(v => v.Index != SelectedTargetCamera.Value.Index))
        {
            SelectedTargetCamera = options.Count > 1 ? options[1] : options[0]; OnPropertyChanged(nameof(SelectedTargetCamera));
        }

        AppendLog($"Camera list refreshed: {string.Join(", ", options.Select(v => v.Name))}");
    }

    private void UpdateVideoProperties(bool reference, int index, TimeSpan ts)
    {
        if (reference)
        {
            ReferenceVideoFrameIndex = index;
            ReferenceVideoTimeMs = ts.TotalMilliseconds.ToString("F0");
            OnPropertyChanged(nameof(ReferenceVideoFrameIndex));
            OnPropertyChanged(nameof(ReferenceVideoTimeMs));
            OnPropertyChanged(nameof(ReferenceVideoInfo));
        }
        else
        {
            TargetVideoFrameIndex = index;
            TargetVideoTimeMs = ts.TotalMilliseconds.ToString("F0");
            OnPropertyChanged(nameof(TargetVideoFrameIndex));
            OnPropertyChanged(nameof(TargetVideoTimeMs));
            OnPropertyChanged(nameof(TargetVideoInfo));
        }
    }

    private static string BuildVideoInfo(VideoFrameProvider p) => !p.IsOpen ? "No video loaded" : $"Frames: {p.FrameCount}, FPS: {p.Fps:F2}, Duration: {p.Duration}";

    private async Task RunBusyAsync(Func<CancellationToken, Task> work, string canceledMessage)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await work(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog(canceledMessage);
        }
        catch (Exception ex)
        {
            AppendLog($"Operation failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyButtons()
    {
        (LoadReferenceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadTargetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (AutoFitCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ExportCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ResetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RefreshCameraListCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyRoiCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ClearRoiCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekReferenceFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekTargetFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekReferenceTimeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekTargetTimeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (UseCurrentFramesCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StartCamerasCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StopCamerasCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (FreezePairCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SaveSessionCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadSessionCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void AppendLog(string message) => LogText += Environment.NewLine + $"[{DateTime.Now:HH:mm:ss}] {message}";
    private static int ParseIntOrDefault(string value) => int.TryParse(value, out var v) ? v : 0;
    private static double ParseDoubleOrDefault(string value) => double.TryParse(value, out var v) ? v : 0;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _cameraPreviewTimer.Stop();
        _cameraService.Dispose();
        _referenceVideo.Dispose();
        _targetVideo.Dispose();
        _reference?.Dispose();
        _target?.Dispose();
        _corrected?.Dispose();
    }

    private sealed class DisposablePair((Mat Reference, Mat Target) pair) : IDisposable
    {
        public Mat Reference { get; } = pair.Reference;
        public Mat Target { get; } = pair.Target;
        public void Dispose() { Reference.Dispose(); Target.Dispose(); }
    }
}
