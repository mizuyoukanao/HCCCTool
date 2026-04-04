# LUT Matcher Design Note

## Range handling

- `ColorRangeService` is used by sample extraction, corrected preview generation, and LUT baking.
- Encoded RGB is converted to working RGB `[0,1]` before fitting.
- Full range mode uses identity conversion.
- Video range mode uses legal-range conversion (`16/255` offset, `219/255` scale) for both normalize and denormalize.

## Alignment

- `AlignmentService` estimates translation shift via phase correlation.
- If enabled, target is aligned before sample extraction.
- On failure, the pipeline falls back to resize-only behavior.

## Fitting pipeline

1. Extract paired samples from corresponding pixels (`SampleExtractor`).
2. Fit per-channel tone curves (`ToneCurveFitter`).
3. Fit affine color matrix + bias (`ColorMatrixFitter`).
4. Evaluate fit metrics (`MetricsCalculator`).

## LUT baking behavior

- `LutBaker` always iterates `.cube` order (R fastest, B slowest).
- Full range mode:
  - input domain is direct `[0,1]`
  - output entries are direct encoded `[0,1]`
- Video range mode:
  - LUT input is interpreted as encoded legal range
  - values are normalized to working range before model application
  - transformed values are denormalized back to legal range before writing entries
