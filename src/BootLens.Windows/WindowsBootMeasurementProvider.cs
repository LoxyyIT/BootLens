using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using BootLens.Core.Domain;
using BootLens.Core.Services;

namespace BootLens.Windows;

public sealed class WindowsBootMeasurementProvider : IBootMeasurementProvider
{
    private const string Channel = "Microsoft-Windows-Diagnostics-Performance/Operational";

    public Task<BootMeasurement?> GetLatestAsync(IReadOnlyCollection<StartupEntry> entries, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ReadLatest(entries, cancellationToken), cancellationToken);
    }

    private static BootMeasurement? ReadLatest(IReadOnlyCollection<StartupEntry> entries, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var query = new EventLogQuery(Channel, PathType.LogName, "*[System[Provider[@Name='Microsoft-Windows-Diagnostics-Performance'] and EventID=100]]")
            {
                ReverseDirection = true
            };
            using var reader = new EventLogReader(query);
            while (reader.ReadEvent() is { } record)
            {
                using (record)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var xml = record.ToXml();
                    var bootTime = ReadEventData(xml, "BootTime");
                    if (!double.TryParse(bootTime, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0) continue;
                    var startText = ReadEventData(xml, "BootStartTime");
                    if (!DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startedUtc))
                    {
                        var eventTime = record.TimeCreated;
                        if (eventTime is null) continue;
                        startedUtc = new DateTimeOffset(DateTime.SpecifyKind(eventTime.Value, DateTimeKind.Local)).ToUniversalTime();
                    }
                    return new BootMeasurement
                    {
                        StartedUtc = startedUtc.ToUniversalTime(),
                        DurationSeconds = milliseconds / 1000,
                        Source = $"{Channel} · Event 100 / BootTime",
                        ConfigurationFingerprint = Fingerprint(entries)
                    };
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (EventLogException) { }
        catch (UnauthorizedAccessException) { }
        catch (InvalidOperationException) { }
        return null;
    }

    private static string? ReadEventData(string xml, string name)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Data" && string.Equals((string?)element.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string Fingerprint(IEnumerable<StartupEntry> entries)
    {
        var configuration = string.Join('\n', entries.OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase).Select(entry => $"{entry.Id}\0{entry.State}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration))).ToLowerInvariant();
    }
}
