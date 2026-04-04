using System.Text;
using LutMatcher.Core.Models;

namespace LutMatcher.Core.Services;

public sealed class CubeExporter
{
    public string BuildText(LutData lut, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TITLE \"{title}\"");
        sb.AppendLine($"LUT_3D_SIZE {lut.Size}");
        sb.AppendLine("DOMAIN_MIN 0.0 0.0 0.0");
        sb.AppendLine("DOMAIN_MAX 1.0 1.0 1.0");

        foreach (var (r, g, b) in lut.Entries)
        {
            sb.AppendLine($"{r:F6} {g:F6} {b:F6}");
        }

        return sb.ToString();
    }

    public async Task ExportAsync(string path, LutData lut, string title, CancellationToken cancellationToken)
    {
        var text = BuildText(lut, title);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false), cancellationToken);
    }
}
