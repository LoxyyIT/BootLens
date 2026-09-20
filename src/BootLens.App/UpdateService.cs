using System.Net.Http.Headers;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace BootLens.App;

public sealed record UpdateCheckResult(bool Succeeded, bool HasUpdate, string? LatestVersion, string ReleaseUrl);

public sealed class UpdateService
{
    public const string RepositoryUrl = "https://github.com/LoxyyIT/BootLens";
    public const string ReleasesUrl = RepositoryUrl + "/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static string CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.4";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync("https://api.github.com/repos/LoxyyIT/BootLens/releases/latest", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new(true, false, null, ReleasesUrl);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagProperty) ? tagProperty.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlProperty) ? urlProperty.GetString() : ReleasesUrl;
            if (!TryParseVersion(tag, out var latest)) return new(true, false, tag, url ?? ReleasesUrl);
            var current = ParseCurrentVersion();
            return new(true, latest > current, latest.ToString(3), url ?? ReleasesUrl);
        }
        catch (OperationCanceledException)
        {
            return new(false, false, null, ReleasesUrl);
        }
        catch (HttpRequestException)
        {
            return new(false, false, null, ReleasesUrl);
        }
        catch (JsonException)
        {
            return new(false, false, null, ReleasesUrl);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BootLens", CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static Version ParseCurrentVersion() => Version.TryParse(CurrentVersion, out var version) ? version : new Version(0, 1, 4);

    private static bool TryParseVersion(string? value, out Version version)
    {
        var normalized = value?.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out version!);
    }
}
