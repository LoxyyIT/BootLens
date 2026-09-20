using System.Text;
using System.Text.Json;
using BootLens.Core.Domain;

namespace BootLens.Core.Export;

public enum ExportPathMode
{
    Full,
    Anonymized,
    Excluded
}

public sealed class ReportExporter
{
    public string ToJson(IReadOnlyCollection<StartupEntry> entries, ExportPathMode pathMode)
    {
        var payload = entries.Select(entry => new { entry.Id, entry.DisplayName, entry.Mechanism, entry.State, entry.Publisher, ExecutablePath = FormatPath(entry.ExecutablePath, pathMode), entry.CommandLine, entry.Version, entry.Sha256, entry.IsSigned, entry.IsMicrosoft, entry.IsCritical, entry.IsBroken }).ToArray();
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ToCsv(IReadOnlyCollection<StartupEntry> entries, ExportPathMode pathMode)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DisplayName,Mechanism,State,Publisher,ExecutablePath,Version,Sha256,IsSigned,IsCritical,IsBroken");
        foreach (var entry in entries) builder.AppendLine(string.Join(',', Escape(entry.DisplayName), Escape(entry.Mechanism.ToString()), Escape(entry.State.ToString()), Escape(entry.Publisher), Escape(FormatPath(entry.ExecutablePath, pathMode)), Escape(entry.Version), Escape(entry.Sha256), entry.IsSigned, entry.IsCritical, entry.IsBroken));
        return builder.ToString();
    }

    public string ToHtml(IReadOnlyCollection<StartupEntry> entries, ExportPathMode pathMode, string title = "BootLens startup report")
    {
        var rows = string.Join(Environment.NewLine, entries.Select(entry => $"<tr><td>{Html(entry.DisplayName)}</td><td>{Html(entry.Mechanism.ToString())}</td><td>{Html(entry.State.ToString())}</td><td>{Html(entry.Publisher)}</td><td>{Html(FormatPath(entry.ExecutablePath, pathMode))}</td></tr>"));
        return $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{Html(title)}</title><style>body{{font-family:Segoe UI,Arial,sans-serif;background:#f5f6f8;color:#111827;padding:32px}}main{{max-width:1200px;margin:auto;background:#fff;border:1px solid #e4e7ec;border-radius:16px;padding:24px}}table{{width:100%;border-collapse:collapse}}th,td{{padding:12px;border-bottom:1px solid #e4e7ec;text-align:left}}th{{color:#667085;font-size:12px;text-transform:uppercase}}</style></head><body><main><h1>{Html(title)}</h1><table><thead><tr><th>Name</th><th>Mechanism</th><th>State</th><th>Publisher</th><th>Path</th></tr></thead><tbody>{rows}</tbody></table></main></body></html>";
    }

    private static string? FormatPath(string? path, ExportPathMode mode) => mode switch { ExportPathMode.Excluded => null, ExportPathMode.Anonymized => Anonymize(path), _ => path };
    private static string? Anonymize(string? path) => string.IsNullOrWhiteSpace(path) ? path : path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase).Replace(Environment.UserName, "%USERNAME%", StringComparison.OrdinalIgnoreCase);
    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
