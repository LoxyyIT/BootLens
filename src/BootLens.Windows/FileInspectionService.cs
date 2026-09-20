using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace BootLens.Windows;

public sealed record FileInspectionResult(string Path, string? Product, string? Publisher, string? Version, long SizeBytes, string Sha256, bool Exists);

public sealed class FileInspectionService
{
    public FileInspectionResult Inspect(string path)
    {
        if (!File.Exists(path)) return new FileInspectionResult(path, null, null, null, 0, string.Empty, false);
        var info = new FileInfo(path);
        var version = FileVersionInfo.GetVersionInfo(path);
        return new FileInspectionResult(path, version.ProductName, version.CompanyName, version.FileVersion, info.Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), true);
    }
}
