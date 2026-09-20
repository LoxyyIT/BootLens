using BootLens.Core.Domain;
using BootLens.Core.Export;

namespace BootLens.Core.Tests;

public sealed class ExportTests
{
    [Fact]
    public void Export_can_exclude_paths_and_escape_html()
    {
        var entry = new StartupEntry { Id = "x", DisplayName = "<Demo>", Mechanism = StartupMechanism.RegistryRun, State = StartupState.Enabled, ExecutablePath = "C:\\Users\\Person\\demo.exe" };
        var exporter = new ReportExporter();
        Assert.DoesNotContain("demo.exe", exporter.ToJson([entry], ExportPathMode.Excluded));
        Assert.Contains("&lt;Demo&gt;", exporter.ToHtml([entry], ExportPathMode.Full));
    }
}
