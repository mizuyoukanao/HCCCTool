# HCCCTool / LutMatcher

Windows desktop application (WPF, .NET 8) to fit an OBS-compatible 3D LUT (`.cube`) that matches a **Target** source to a **Reference** source.

## Projects

- `LutMatcher.App`: WPF UI orchestration
- `LutMatcher.Core`: fitting pipeline and source/session services
- `LutMatcher.Tests`: unit tests

## Supported workflows

1. **Paired image mode**
   - Load Reference/Target still images.
   - Fit model from the loaded pair.

2. **Paired video frame selection mode**
   - Load Reference/Target video files.
   - Seek each side by frame index or timestamp.
   - Optionally apply manual target offset (frames or ms).
   - Click **Use Current Frames** and fit from the selected pair.

3. **Paired live camera/capture freeze mode**
   - Select both capture devices.
   - Start both with **Start Cameras**.
   - Live preview updates both panes.
   - Capture near-simultaneous pair with **Freeze Pair**.

4. **Session save/load**
   - Save current calibration state with **Save Session** (`.lmz`).
   - Restore and continue later with **Load Session**.

## Build

1. Open `HCCCTool.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Build solution (`Debug` or `Release`, Any CPU).
4. Run `LutMatcher.App`.

## Calibration flow

1. Load Reference and Target sources (image/video/camera freeze).
2. Choose range mode (**Full range** / **Video range**).
3. Optional ROI: enter values, click **Apply ROI**.
4. Optional alignment: toggle **Enable Auto Align**.
5. Click **Auto Fit LUT**.
6. Review **Corrected**, **Diff**, and metrics.
7. Export with **Export LUT**.

## Notes

- `.cube` export remains OBS-compatible.
- Video workflow is manual sync in this implementation.
- Camera device discovery is index-based and refreshable.
