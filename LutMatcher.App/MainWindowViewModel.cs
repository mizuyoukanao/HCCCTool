using LutMatcher.Core.Models;
using LutMatcher.Core.Services;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Rect = OpenCvSharp.Rect;

namespace LutMatcher.App;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ImageLoader _loader = new();
    private readonly SampleExtractor _extractor = new();
    private readonly TransformPipeline _pipeline = new();
    private readonly MetricsCalculator _metrics = new();
    private readonly LutBaker _baker = new();
    private readonly CubeExporter _exporter = new();
    private readonly ColorRangeService _range = new();
    private readonly AlignmentService _alignment = new();

    private Mat? _reference;
    private Mat? _target;
    private Mat? _corrected;
    private ColorTransformModel? _model;
    private CancellationTokenSource? _cts;
    private List<ImageLoader.CameraInfo> _cameraOptions = [];
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? ReferencePreview { get; private set; }
    public object? TargetPreview { get; private set; }
    public object? CorrectedPreview { get; private set; }
    public object? DiffPreview { get; private set; }
    public string MetricsText { get; private set; } = "No metrics yet.";

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
    public bool EnableAutoAlign { get; set; } = true;
    public List<ImageLoader.CameraInfo> CameraOptions
    {
        get => _cameraOptions;
        private set
        {
            _cameraOptions = value;
            OnPropertyChanged();
        }
    }
    public ImageLoader.CameraInfo? SelectedReferenceCamera { get; set; }
    public ImageLoader.CameraInfo? SelectedTargetCamera { get; set; }
    public string ReferenceFrameDelay { get; set; } = "0";
    public string TargetFrameDelay { get; set; } = "0";

    private string _logText = "Ready.";
    public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            NotifyButtons();
        }
    }

    public ICommand LoadReferenceCommand { get; }
    public ICommand LoadTargetCommand { get; }
    public ICommand LoadReferenceCameraCommand { get; }
    public ICommand LoadTargetCameraCommand { get; }
    public ICommand AutoFitCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCameraListCommand { get; }
    public ICommand ApplyRoiCommand { get; }
    public ICommand ClearRoiCommand { get; }

    public MainWindowViewModel()
    {
        LoadReferenceCommand = new RelayCommand(() => LoadImage(true), () => !IsBusy);
        LoadTargetCommand = new RelayCommand(() => LoadImage(false), () => !IsBusy);
        LoadReferenceCameraCommand = new RelayCommand(() => LoadCamera(true), () => !IsBusy);
        LoadTargetCameraCommand = new RelayCommand(() => LoadCamera(false), () => !IsBusy);
        AutoFitCommand = new RelayCommand(async () => await FitAsync(), () => !IsBusy && _reference is not null && _target is not null);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !IsBusy && _model is not null);
        ResetCommand = new RelayCommand(Reset, () => !IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RefreshCameraListCommand = new RelayCommand(RefreshCameraList, () => !IsBusy);
        ApplyRoiCommand = new RelayCommand(ApplyRoi, () => !IsBusy);
        ClearRoiCommand = new RelayCommand(ClearRoi, () => !IsBusy);
        RefreshCameraList();
    }

    private void LoadImage(bool reference)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Supported|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.mp4;*.mov;*.mkv;*.avi"
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var mat = _loader.LoadFirstFrame(dlg.FileName);
            if (reference)
            {
                _reference?.Dispose();
                _reference = mat;
                ReferencePreview = mat.ToWriteableBitmap();
                OnPropertyChanged(nameof(ReferencePreview));
            }
            else
            {
                _target?.Dispose();
                _target = mat;
                TargetPreview = mat.ToWriteableBitmap();
                OnPropertyChanged(nameof(TargetPreview));
            }

            AppendLog($"Loaded {(reference ? "Reference" : "Target")}: {dlg.FileName}");
            NotifyButtons();
        }
        catch (Exception ex)
        {
            AppendLog($"Load failed: {ex.Message}");
        }
    }

    private void LoadCamera(bool reference)
    {
        RefreshCameraList();
        var rawDelay = reference ? ReferenceFrameDelay : TargetFrameDelay;
        var selectedCamera = reference ? SelectedReferenceCamera : SelectedTargetCamera;
        if (selectedCamera is not { } camera)
        {
            AppendLog($"No {(reference ? "Reference" : "Target")} camera selected.");
            return;
        }
        var deviceIndex = camera.Index;
        if (!int.TryParse(rawDelay, out var frameDelay) || frameDelay < 0)
        {
            AppendLog($"Frame delay is invalid: {rawDelay}");
            return;
        }

        try
        {
            var mat = _loader.LoadFirstFrameFromCamera(deviceIndex, frameDelay);
            if (reference)
            {
                _reference?.Dispose();
                _reference = mat;
                ReferencePreview = mat.ToWriteableBitmap();
                OnPropertyChanged(nameof(ReferencePreview));
            }
            else
            {
                _target?.Dispose();
                _target = mat;
                TargetPreview = mat.ToWriteableBitmap();
                OnPropertyChanged(nameof(TargetPreview));
            }

            AppendLog($"Loaded {(reference ? "Reference" : "Target")} from {camera.Name} (delay: {frameDelay} frames).");
            NotifyButtons();
        }
        catch (Exception ex)
        {
            AppendLog($"Camera load failed: {ex.Message}");
        }
    }

    private async Task FitAsync()
    {
        if (_reference is null || _target is null)
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            AppendLog("Fitting started...");
            var roi = ParseRoi();
            var alignmentResult = await Task.Run(
                () => BuildAlignedTarget(_reference, _target, _cts.Token),
                _cts.Token);
            using var alignedTarget = alignmentResult.Target;
            AppendLog(alignmentResult.Message);
            var sampleSet = await Task.Run(() => _extractor.Extract(_reference, alignedTarget, roi, !IsFullRange, cancellationToken: _cts.Token), _cts.Token);
            _model = await Task.Run(() => _pipeline.Fit(sampleSet), _cts.Token);
            _corrected?.Dispose();
            _corrected = await Task.Run(() => ApplyModelToImage(alignedTarget, _model, !IsFullRange, _cts.Token), _cts.Token);
            CorrectedPreview = _corrected.ToWriteableBitmap();
            OnPropertyChanged(nameof(CorrectedPreview));

            using var diff = BuildDiff(_reference, _corrected);
            DiffPreview = diff.ToWriteableBitmap();
            OnPropertyChanged(nameof(DiffPreview));

            var before = sampleSet.Target;
            var after = sampleSet.Target.Select(v => _pipeline.Apply(_model, v)).ToArray();
            var metrics = _metrics.Calculate(sampleSet.Reference, before, after);
            MetricsText = $"MAE: {metrics.Mae:F6}\nRMSE: {metrics.Rmse:F6}\nMax Abs: {metrics.MaxAbsError:F6}\nImprovement: {metrics.ImprovementRatio:F2}x";
            OnPropertyChanged(nameof(MetricsText));
            AppendLog($"Fit complete. {metrics}");
            NotifyButtons();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog($"Fit failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (_model is null)
        {
            return;
        }

        var dlg = new SaveFileDialog { Filter = "Cube LUT|*.cube", FileName = "LutMatcher.cube" };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            AppendLog("Export started...");
            var lut = await Task.Run(() => _baker.Bake(_model, SelectedLutSize, !IsFullRange, _cts.Token), _cts.Token);
            await _exporter.ExportAsync(dlg.FileName, lut, "LutMatcher", _cts.Token);
            AppendLog($"Exported: {dlg.FileName}");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Export cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog($"Export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private (Mat Target, string Message) BuildAlignedTarget(Mat reference, Mat target, CancellationToken cancellationToken)
    {
        if (!EnableAutoAlign)
        {
            var resized = target.Size() == reference.Size() ? target.Clone() : target.Resize(reference.Size());
            return (resized, "Auto align disabled.");
        }

        if (_alignment.TryAlignByTranslation(reference, target, out var aligned, out var shift, cancellationToken))
        {
            return (aligned, $"Auto align shift: dx={shift.X:F2}, dy={shift.Y:F2}");
        }

        var fallback = target.Size() == reference.Size() ? target.Clone() : target.Resize(reference.Size());
        return (fallback, "Auto align failed. Falling back to simple resize.");
    }

    private Mat ApplyModelToImage(Mat bgr, ColorTransformModel model, bool videoRange, CancellationToken cancellationToken)
    {
        using var rgbFloat = _range.ConvertBgrToWorkingRgb(bgr, videoRange);

        using var output = new Mat(rgbFloat.Rows, rgbFloat.Cols, MatType.CV_32FC3);
        for (var y = 0; y < rgbFloat.Rows; y++)
        {
            if ((y & 15) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

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
        using var ref32 = new Mat();
        using var corr32 = new Mat();
        reference.ConvertTo(ref32, MatType.CV_32FC3, 1.0 / 255.0);
        correctedResized.ConvertTo(corr32, MatType.CV_32FC3, 1.0 / 255.0);
        using var diff = new Mat();
        Cv2.Absdiff(ref32, corr32, diff);
        var channels = diff.Split();
        using var err = new Mat();
        Cv2.AddWeighted(channels[0], 1.0 / 3.0, channels[1], 1.0 / 3.0, 0, err);
        Cv2.AddWeighted(err, 1.0, channels[2], 1.0 / 3.0, 0, err);
        foreach (var channel in channels)
        {
            channel.Dispose();
        }

        using var err8 = new Mat();
        Cv2.Normalize(err, err, 0, 255, NormTypes.MinMax);
        err.ConvertTo(err8, MatType.CV_8UC1);
        var outMat = new Mat();
        Cv2.ApplyColorMap(err8, outMat, ColormapTypes.Turbo);
        return outMat;
    }

    private Rect? ParseRoi()
    {
        if (!int.TryParse(RoiW, out var w) || !int.TryParse(RoiH, out var h) || w <= 0 || h <= 0)
        {
            return null;
        }

        if (!int.TryParse(RoiX, out var x)) x = 0;
        if (!int.TryParse(RoiY, out var y)) y = 0;
        return new Rect(x, y, w, h);
    }

    private void Reset()
    {
        _reference?.Dispose();
        _target?.Dispose();
        _corrected?.Dispose();
        _reference = null;
        _target = null;
        _corrected = null;
        _model = null;
        ReferencePreview = null;
        TargetPreview = null;
        CorrectedPreview = null;
        DiffPreview = null;
        MetricsText = "No metrics yet.";
        LogText = "Reset complete.";
        OnPropertyChanged(nameof(ReferencePreview));
        OnPropertyChanged(nameof(TargetPreview));
        OnPropertyChanged(nameof(CorrectedPreview));
        OnPropertyChanged(nameof(DiffPreview));
        OnPropertyChanged(nameof(MetricsText));
        NotifyButtons();
    }

    private void ApplyRoi()
    {
        ParseRoi();
        AppendLog("ROI applied.");
    }

    private void ClearRoi()
    {
        RoiX = "0";
        RoiY = "0";
        RoiW = "0";
        RoiH = "0";
        OnPropertyChanged(nameof(RoiX));
        OnPropertyChanged(nameof(RoiY));
        OnPropertyChanged(nameof(RoiW));
        OnPropertyChanged(nameof(RoiH));
        AppendLog("ROI cleared.");
    }

    private void NotifyButtons()
    {
        (LoadReferenceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadTargetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadReferenceCameraCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadTargetCameraCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (AutoFitCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ExportCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ResetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RefreshCameraListCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyRoiCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ClearRoiCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void RefreshCameraList()
    {
        var options = _loader.GetAvailableCameras()
            .ToList();
        CameraOptions = options;

        if (options.Count == 0)
        {
            SelectedReferenceCamera = null;
            SelectedTargetCamera = null;
            AppendLog("No cameras detected.");
            return;
        }

        if (SelectedReferenceCamera is not { } selectedReferenceCamera || options.All(v => v.Index != selectedReferenceCamera.Index))
        {
            SelectedReferenceCamera = options[0];
            OnPropertyChanged(nameof(SelectedReferenceCamera));
        }

        if (SelectedTargetCamera is not { } selectedTargetCamera || options.All(v => v.Index != selectedTargetCamera.Index))
        {
            SelectedTargetCamera = options.Count > 1 ? options[1] : options[0];
            OnPropertyChanged(nameof(SelectedTargetCamera));
        }

        AppendLog($"Camera list refreshed: {string.Join(", ", options.Select(v => v.Name))}");
    }

    private void AppendLog(string message) => LogText += Environment.NewLine + $"[{DateTime.Now:HH:mm:ss}] {message}";

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
