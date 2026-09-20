namespace BootLens.Core.Analysis;

public sealed class PrivacyNormalizer
{
    public string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var normalized = path.Replace(Environment.UserName, "%USERNAME%", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "%PROGRAMDATA%", StringComparison.OrdinalIgnoreCase);
        return normalized;
    }
}
