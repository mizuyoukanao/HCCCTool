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

## Usage

1. Click **Load Reference** and select an image/video file.
2. Click **Load Target** and select a matching frame source.
3. Choose range mode (**Full range** or **Video range**).
4. Optionally set ROI (`x, y, width, height`).
   - Set width/height <= 0 to use full frame.
5. Click **Auto Fit LUT**.
6. Optionally toggle **Enable Auto Align** to estimate and apply translation shift.
7. Check **Corrected** and **Diff** previews and the metrics block.
8. Click **Export LUT** and save a `.cube` file.

## Supported input formats

- Image: `png`, `jpg`, `bmp`, `tif`
- Video: `mp4`, `mov`, `mkv`, `avi` (single-frame extraction path in current UI)
- Camera: OpenCV `VideoCapture` device loading

## Design note

The fitting pipeline uses a two-step transform model:

1. **Per-channel 1D tone curve (256 bins)**
   - build paired sample mapping from corresponding pixels
   - trimmed mean aggregation per bin
   - missing-bin interpolation
   - monotonic enforcement
   - light smoothing

2. **3x3 + bias affine color transform**
   - least-squares fit on tone-corrected samples
   - optional iterative outlier rejection

### Range handling

- `ColorRangeService` is the single conversion layer for encoded RGB and working RGB.
- Full range mode keeps encoded values in `[0,1]`.
- Video range mode normalizes encoded legal range (`16..235`) into working `[0,1]` and denormalizes output back to legal range.

### Alignment

- `AlignmentService` provides optional translation-only alignment before sample extraction.
- Estimated `dx/dy` shift is logged and the aligned frame is used for fitting and corrected preview generation.

### LUT baking

- LUT entries are generated in `.cube` order (R fastest, B slowest).
- Full range bake uses direct encoded domain/output.
- Video range bake normalizes encoded input -> applies fitted model -> denormalizes output.

## Notes

- The app is optimized for paired frame calibration workflows.
- Initial implementation assumes SDR RGB workflow.
- Python is not used.
