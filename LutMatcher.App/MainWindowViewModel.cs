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

    private Mat? _reference;
    private Mat? _target;
    private Mat? _corrected;
    private ColorTransformModel? _model;
    private CancellationTokenSource? _cts;

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? ReferencePreview { get; private set; }
    public object? TargetPreview { get; private set; }
    public object? CorrectedPreview { get; private set; }
    public object? DiffPreview { get; private set; }

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
    public string ReferenceCameraIndex { get; set; } = "0";
    public string TargetCameraIndex { get; set; } = "1";
    public string ReferenceFrameDelay { get; set; } = "0";
    public string TargetFrameDelay { get; set; } = "0";

    private string _logText = "Ready.";
    public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }

    public ICommand LoadReferenceCommand { get; }
    public ICommand LoadTargetCommand { get; }
    public ICommand LoadReferenceCameraCommand { get; }
    public ICommand LoadTargetCameraCommand { get; }
    public ICommand AutoFitCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }

    public MainWindowViewModel()
    {
        LoadReferenceCommand = new RelayCommand(() => LoadImage(true));
        LoadTargetCommand = new RelayCommand(() => LoadImage(false));
        LoadReferenceCameraCommand = new RelayCommand(() => LoadCamera(true));
        LoadTargetCameraCommand = new RelayCommand(() => LoadCamera(false));
        AutoFitCommand = new RelayCommand(async () => await FitAsync(), () => _reference is not null && _target is not null);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => _model is not null);
        ResetCommand = new RelayCommand(Reset);
        CancelCommand = new RelayCommand(() => _cts?.Cancel());
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
        var rawIndex = reference ? ReferenceCameraIndex : TargetCameraIndex;
        var rawDelay = reference ? ReferenceFrameDelay : TargetFrameDelay;
        if (!int.TryParse(rawIndex, out var deviceIndex) || deviceIndex < 0)
        {
            AppendLog($"Camera index is invalid: {rawIndex}");
            return;
        }
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

            AppendLog($"Loaded {(reference ? "Reference" : "Target")} from camera index {deviceIndex} (delay: {frameDelay} frames).");
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

        try
        {
            AppendLog("Fitting started...");
            var roi = ParseRoi();
            var sampleSet = await Task.Run(() => _extractor.Extract(_reference, _target, roi, !IsFullRange), _cts.Token);
            _model = await Task.Run(() => _pipeline.Fit(sampleSet), _cts.Token);
            _corrected?.Dispose();
            _corrected = await Task.Run(() => ApplyModelToImage(_target, _model), _cts.Token);
            CorrectedPreview = _corrected.ToWriteableBitmap();
            OnPropertyChanged(nameof(CorrectedPreview));

            using var diff = BuildDiff(_reference, _corrected);
            DiffPreview = diff.ToWriteableBitmap();
            OnPropertyChanged(nameof(DiffPreview));

            var before = sampleSet.Target;
            var after = sampleSet.Target.Select(v => _pipeline.Apply(_model, v)).ToArray();
            var metrics = _metrics.Calculate(sampleSet.Reference, before, after);
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

        try
        {
            AppendLog("Export started...");
            var lut = await Task.Run(() => _baker.Bake(_model, SelectedLutSize), _cts.Token);
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
    }

    private static Mat ApplyModelToImage(Mat bgr, ColorTransformModel model)
    {
        using var rgb = new Mat();
        Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);
        using var rgbFloat = new Mat();
        rgb.ConvertTo(rgbFloat, MatType.CV_32FC3, 1.0 / 255.0);

        var output = new Mat(rgbFloat.Rows, rgbFloat.Cols, MatType.CV_32FC3);
        for (var y = 0; y < rgbFloat.Rows; y++)
        {
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

        using var out8 = new Mat();
        output.ConvertTo(out8, MatType.CV_8UC3, 255.0);
        var outBgr = new Mat();
        Cv2.CvtColor(out8, outBgr, ColorConversionCodes.RGB2BGR);
        return outBgr;
    }

    private static Mat BuildDiff(Mat reference, Mat corrected)
    {
        using var diff = new Mat();
        Cv2.Absdiff(reference, corrected.Resize(reference.Size()), diff);
        var outMat = new Mat();
        Cv2.ApplyColorMap(diff, outMat, ColormapTypes.Jet);
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
        LogText = "Reset complete.";
        OnPropertyChanged(nameof(ReferencePreview));
        OnPropertyChanged(nameof(TargetPreview));
        OnPropertyChanged(nameof(CorrectedPreview));
        OnPropertyChanged(nameof(DiffPreview));
        NotifyButtons();
    }

    private void NotifyButtons()
    {
        (AutoFitCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ExportCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void AppendLog(string message) => LogText += Environment.NewLine + $"[{DateTime.Now:HH:mm:ss}] {message}";

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
