using System.Windows;
using System.Windows.Media;

namespace BootLens.App;

public sealed class LanguageFlag : FrameworkElement
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(string), typeof(LanguageFlag), new FrameworkPropertyMetadata("en", FrameworkPropertyMetadataOptions.AffectsRender));

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth > 0 ? ActualWidth : 22;
        var height = ActualHeight > 0 ? ActualHeight : 14;
        var bounds = new Rect(0, 0, width, height);
        drawingContext.PushClip(new RectangleGeometry(bounds, 3, 3));
        var code = Code.ToLowerInvariant();
        if (code == "it" || code == "fr")
        {
            var colors = code == "it" ? new[] { Color.FromRgb(0, 146, 70), Colors.White, Color.FromRgb(206, 43, 55) } : new[] { Color.FromRgb(0, 35, 149), Colors.White, Color.FromRgb(237, 41, 57) };
            for (var index = 0; index < 3; index++) drawingContext.DrawRectangle(new SolidColorBrush(colors[index]), null, new Rect(index * width / 3, 0, width / 3 + 1, height));
        }
        else if (code == "es")
        {
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(198, 40, 40)), null, new Rect(0, 0, width, height));
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(255, 196, 0)), null, new Rect(0, height * 0.25, width, height * 0.5));
        }
        else
        {
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 58, 138)), null, bounds);
            var whitePen = new Pen(Brushes.White, Math.Max(1, height * 0.16));
            var redPen = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), Math.Max(1, height * 0.08));
            drawingContext.DrawLine(whitePen, new Point(width * 0.08, height * 0.18), new Point(width * 0.92, height * 0.82));
            drawingContext.DrawLine(whitePen, new Point(width * 0.92, height * 0.18), new Point(width * 0.08, height * 0.82));
            drawingContext.DrawLine(redPen, new Point(width * 0.08, height * 0.18), new Point(width * 0.92, height * 0.82));
            drawingContext.DrawLine(redPen, new Point(width * 0.92, height * 0.18), new Point(width * 0.08, height * 0.82));
            drawingContext.DrawLine(whitePen, new Point(width * 0.5, 0), new Point(width * 0.5, height));
            drawingContext.DrawLine(whitePen, new Point(0, height * 0.5), new Point(width, height * 0.5));
            drawingContext.DrawLine(redPen, new Point(width * 0.5, 0), new Point(width * 0.5, height));
            drawingContext.DrawLine(redPen, new Point(0, height * 0.5), new Point(width, height * 0.5));
        }
        drawingContext.Pop();
    }
}
