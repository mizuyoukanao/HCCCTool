# HCCCTool / LutMatcher

Windows desktop application (WPF, .NET 8) to fit an OBS-compatible 3D LUT (`.cube`) that matches a **Target** input to a **Reference** input.

## Projects

- `LutMatcher.App`: WPF UI
- `LutMatcher.Core`: fitting pipeline, LUT bake, cube export
- `LutMatcher.Tests`: unit tests

## Requirements

- Windows 10/11
- Visual Studio 2022 (17.8+ recommended)
- .NET 8 SDK / runtime

## Build

1. Open `HCCCTool.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Build solution (`Debug` or `Release`, Any CPU).
4. Run `LutMatcher.App` as startup project.

## Usage (MVP)

1. Click **Load Reference** and select an image/video file.
2. Click **Load Target** and select a matching frame source.
   - For HDMI capture / UVC camera input, click **Refresh Devices**, choose device index, then use **Load Ref Camera** or **Load Target Camera**.
3. Choose range mode (**Full range** or **Video range**).
4. Optionally set ROI (`x, y, width, height`).
   - Set width/height <= 0 to use full frame.
5. Click **Auto Fit LUT**.
6. Check **Corrected** and **Diff** previews and metrics in log panel.
7. Click **Export LUT** and save a `.cube` file.

## Supported input formats

- Image: `png`, `jpg`, `bmp`, `tif`
- Video (first-frame MVP): `mp4`, `mov`, `mkv`, `avi`
- Camera/capture device input (single-frame capture in MVP) via OpenCV `VideoCapture` device index.

## Design note: fitting pipeline

The current MVP uses a two-step transform model:

1. **Per-channel 1D tone curve (256 bins)**
   - build paired sample mapping from corresponding pixels
   - trimmed mean aggregation per bin
   - missing-bin interpolation
   - monotonic enforcement
   - light smoothing

2. **3x3 + bias affine color transform**
   - least-squares fit on tone-corrected samples
   - optional iterative outlier rejection

The final model is baked into a 3D LUT (`17/33/65`, default `33`).
LUT writing follows standard `.cube` order (R fastest, B slowest) with 6-decimal precision.

## Notes

- MVP prioritizes paired image workflow.
- Initial implementation assumes SDR RGB workflow.
- Python is not used.
