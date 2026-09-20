using System.Windows;
using BootLens.Core.Analysis;
using BootLens.Core.Localization;
using BootLens.Core.Services;
using BootLens.Data;
using BootLens.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BootLens.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var collection = new ServiceCollection();
        collection.AddLogging(builder => builder.AddDebug());
        collection.AddSingleton<ThemeManager>();
        collection.AddSingleton<LocalizationService>();
        collection.AddSingleton<StartupScoring>();
        collection.AddSingleton<StartupAnalysisService>();
        collection.AddSingleton<ChangeDetection>();
        collection.AddSingleton<IBootLensRepository>(_ => new SqliteBootLensRepository());
        collection.AddSingleton<IStartupScanner, WindowsStartupScanner>();
        collection.AddSingleton<IStartupModifier, WindowsStartupModifier>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainWindow>();
        _services = collection.BuildServiceProvider();
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        await window.InitializeAsync();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
