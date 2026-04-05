using System.IO.Compression;
using System.Text.Json;
using LutMatcher.Core.Models;
using OpenCvSharp;

namespace LutMatcher.Core.Services;

public sealed class SessionSerializer
{
    private const string SessionJsonName = "session.json";
    private const string ReferencePngName = "reference.png";
    private const string TargetPngName = "target.png";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Save(string path, SessionState state, Mat? reference, Mat? target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);

        WriteJsonEntry(archive, SessionJsonName, JsonSerializer.Serialize(state, JsonOptions));
        WriteMatEntry(archive, ReferencePngName, reference);
        WriteMatEntry(archive, TargetPngName, target);
    }

    public (SessionState State, Mat? Reference, Mat? Target) Load(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var file = File.OpenRead(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);

        var stateEntry = archive.GetEntry(SessionJsonName) ?? throw new InvalidOperationException("Session metadata is missing.");
        using var stateStream = stateEntry.Open();
        var state = JsonSerializer.Deserialize<SessionState>(stateStream, JsonOptions)
                    ?? throw new InvalidOperationException("Session metadata is invalid.");

        var reference = ReadMatEntry(archive, ReferencePngName);
        var target = ReadMatEntry(archive, TargetPngName);
        return (state, reference, target);
    }

    private static void WriteJsonEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static void WriteMatEntry(ZipArchive archive, string entryName, Mat? mat)
    {
        if (mat is null || mat.Empty())
        {
            return;
        }

        Cv2.ImEncode(".png", mat, out var data);
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static Mat? ReadMatEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        return bytes.Length == 0 ? null : Cv2.ImDecode(bytes, ImreadModes.Color);
    }
}
