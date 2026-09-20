using BootLens.Windows;

namespace BootLens.Windows.Tests;

public sealed class WindowsParsingTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Demo\\demo.exe\" --silent", "C:\\Program Files\\Demo\\demo.exe")]
    [InlineData("%WINDIR%\\System32\\demo.exe /x", "%WINDIR%\\System32\\demo.exe")]
    [InlineData("\\SystemRoot\\System32\\svchost.exe -k netsvcs", "System32\\svchost.exe")]
    public void Executable_path_is_extracted_without_running_the_command(string command, string expected)
    {
        var actual = WindowsStartupScanner.ExtractExecutablePath(command);
        Assert.NotNull(actual);
        Assert.EndsWith(expected.Replace("%WINDIR%", Environment.GetFolderPath(Environment.SpecialFolder.Windows)), actual, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Startup_scan_reports_valid_authenticode_signatures()
    {
        var entries = await new WindowsStartupScanner().ScanAsync();

        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.IsSigned);
    }
}
