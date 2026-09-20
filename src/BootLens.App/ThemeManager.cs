using System.Windows;
using System.Windows.Media;

namespace BootLens.App;

public sealed class ThemeManager
{
    public void Apply(string theme)
    {
        var resources = Application.Current.Resources;
        Set(resources, "WindowBackground", "#F7F5FC");
        Set(resources, "PanelBackground", "#FFFFFF");
        Set(resources, "PanelMuted", "#F3F0FA");
        Set(resources, "TextPrimary", "#241D36");
        Set(resources, "TextSecondary", "#756E85");
        Set(resources, "BorderColor", "#E6E0F0");
        Set(resources, "AccentColor", "#7C3AED");
        Set(resources, "AccentSoft", "#F0E8FF");
        Set(resources, "WarningSoft", "#FFF5DF");
        Set(resources, "DangerSoft", "#FFE9EC");
    }

    private static void Set(ResourceDictionary resources, string key, string value) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
}
