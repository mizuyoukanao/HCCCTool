# LUT Matcher Design Note

## Video workflow

- `VideoFrameProvider` owns one `VideoCapture` per side.
- It exposes:
  - open from file path
  - metadata (`FrameCount`, `Fps`, `Duration`)
  - seek by frame index
  - seek by timestamp
  - current frame/timestamp reporting
- `MainWindowViewModel` keeps independent Reference/Target selection state.
- Fitting always uses the currently selected in-memory Reference/Target frames (not fixed first frame).
- Optional manual offset can shift target selection by frames or milliseconds.

## Camera workflow

- `CameraCaptureService` starts two capture devices simultaneously (Reference + Target).
- It supports:
  - discover/refresh device list
  - start/stop pair capture
  - refresh latest preview pair
  - `FreezePair` for near-simultaneous paired capture
- Service disposes `VideoCapture`/`Mat` resources on stop/reset and repeated start/stop cycles.

## Session format

- `SessionSerializer` stores a `.lmz` zip package.
- Package entries:
  - `session.json`: source type, source paths, video selection, ROI/range/LUT size/alignment, optional model and metrics.
  - `reference.png`: current reference frame snapshot (if available).
  - `target.png`: current target frame snapshot (if available).
- Loading restores the saved state and previews so calibration can continue.

## Cancellation behavior

- Long operations run with cancellation tokens:
  - video seek/extract tasks
  - fit image loops
  - LUT bake/export
  - session save/load
  - camera start warmup/update paths
- UI catches `OperationCanceledException` and keeps app state consistent.
