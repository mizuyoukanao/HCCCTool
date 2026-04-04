namespace LutMatcher.Core.Services;

public sealed class ColorMatrixFitter
{
    public (double[,] Matrix, double[] Bias) Fit(float[][] sourceRgb, float[][] targetRgb, int iterations = 2, double sigma = 2.5)
    {
        var indices = Enumerable.Range(0, sourceRgb.Length).ToList();
        double[,] matrix = new double[3, 3];
        double[] bias = [0, 0, 0];

        for (var iter = 0; iter < iterations; iter++)
        {
            (matrix, bias) = SolveLeastSquares(sourceRgb, targetRgb, indices);
            if (iter == iterations - 1)
            {
                break;
            }

            var errors = indices.Select(i => Residual(sourceRgb[i], targetRgb[i], matrix, bias)).ToArray();
            var mean = errors.Average();
            var std = Math.Sqrt(errors.Select(v => Math.Pow(v - mean, 2)).Average() + 1e-9);
            var threshold = mean + sigma * std;
            indices = indices.Where((_, idx) => errors[idx] <= threshold).ToList();
            if (indices.Count < 16)
            {
                break;
            }
        }

        return (matrix, bias);
    }

    public static float[] Apply(float[] rgb, double[,] m, double[] b)
    {
        var output = new float[3];
        for (var c = 0; c < 3; c++)
        {
            var v = b[c];
            for (var k = 0; k < 3; k++) v += m[c, k] * rgb[k];
            output[c] = Math.Clamp((float)v, 0f, 1f);
        }

        return output;
    }

    private static (double[,], double[]) SolveLeastSquares(float[][] src, float[][] dst, List<int> indices)
    {
        var xtx = new double[4, 4];
        var xty = new double[3, 4];

        foreach (var idx in indices)
        {
            var x = new[] { (double)src[idx][0], src[idx][1], src[idx][2], 1.0 };
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    xtx[r, c] += x[r] * x[c];
                }

                for (var ch = 0; ch < 3; ch++)
                {
                    xty[ch, r] += x[r] * dst[idx][ch];
                }
            }
        }

        var inv = Invert4x4(xtx);
        var m = new double[3, 3];
        var b = new double[3];
        for (var ch = 0; ch < 3; ch++)
        {
            var coeff = new double[4];
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++) coeff[r] += inv[r, c] * xty[ch, c];
            }

            for (var k = 0; k < 3; k++) m[ch, k] = coeff[k];
            b[ch] = coeff[3];
        }

        return (m, b);
    }

    private static double Residual(float[] x, float[] y, double[,] m, double[] b)
    {
        var pred = Apply(x, m, b);
        return Math.Sqrt(Math.Pow(pred[0] - y[0], 2) + Math.Pow(pred[1] - y[1], 2) + Math.Pow(pred[2] - y[2], 2));
    }

    private static double[,] Invert4x4(double[,] a)
    {
        var n = 4;
        var aug = new double[n, n * 2];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++) aug[r, c] = a[r, c];
            aug[r, n + r] = 1;
        }

        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < n; r++) if (Math.Abs(aug[r, col]) > Math.Abs(aug[pivot, col])) pivot = r;
            if (Math.Abs(aug[pivot, col]) < 1e-9) throw new InvalidOperationException("Matrix inversion failed.");
            if (pivot != col) SwapRows(aug, pivot, col);
            var div = aug[col, col];
            for (var c = 0; c < n * 2; c++) aug[col, c] /= div;
            for (var r = 0; r < n; r++)
            {
                if (r == col) continue;
                var factor = aug[r, col];
                for (var c = 0; c < n * 2; c++) aug[r, c] -= factor * aug[col, c];
            }
        }

        var result = new double[n, n];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++) result[r, c] = aug[r, n + c];
        }

        return result;
    }

    private static void SwapRows(double[,] matrix, int a, int b)
    {
        for (var c = 0; c < matrix.GetLength(1); c++)
        {
            (matrix[a, c], matrix[b, c]) = (matrix[b, c], matrix[a, c]);
        }
    }
}
