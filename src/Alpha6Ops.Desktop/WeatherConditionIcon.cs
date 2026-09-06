using System;
using System.Windows;
using System.Windows.Media;

namespace Alpha6Ops.Desktop;

// Vector artwork in a 32x32 coordinate space; no font/emoji or bitmap dependencies.
public sealed class WeatherConditionIcon : FrameworkElement
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(int),
        typeof(WeatherConditionIcon), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));
    public int Code { get => (int)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    internal static string Kind(int code) => code switch
    {
        0 or 1 => "sun", 2 => "partly-cloudy", 3 => "cloud",
        45 or 48 => "fog", 51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
        71 or 73 or 75 or 77 or 85 or 86 => "snow", 95 or 96 or 99 => "storm", _ => "unknown"
    };

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = Math.Min(ActualWidth, ActualHeight) / 32;
        dc.PushTransform(new TranslateTransform((ActualWidth - 32 * scale) / 2, (ActualHeight - 32 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var yellow = new SolidColorBrush(Color.FromRgb(255, 218, 0));
        var white = new SolidColorBrush(Color.FromRgb(203, 221, 232));
        var blue = new SolidColorBrush(Color.FromRgb(84, 184, 239));
        var kind = Kind(Code);
        if (kind is "sun" or "partly-cloudy")
        {
            var center = kind == "sun" ? new Point(16, 16) : new Point(11, 10);
            dc.DrawEllipse(yellow, null, center, 6, 6);
            var pen = new Pen(yellow, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4;
                dc.DrawLine(pen, new Point(center.X + 9 * Math.Cos(angle), center.Y + 9 * Math.Sin(angle)),
                    new Point(center.X + 12 * Math.Cos(angle), center.Y + 12 * Math.Sin(angle)));
            }
        }
        if (kind is not "sun" and not "unknown")
        {
            dc.DrawGeometry(white, null, Geometry.Parse("M 7,22 C 0,22 0,12 7,12 C 8,3 22,3 24,12 C 33,11 34,22 25,22 Z"));
            if (kind == "rain")
                for (var x = 9; x <= 25; x += 8) dc.DrawLine(new Pen(blue, 2), new Point(x, 25), new Point(x - 3, 30));
            if (kind == "snow")
                for (var x = 8; x <= 26; x += 9)
                {
                    dc.DrawLine(new Pen(white, 1.5), new Point(x, 25), new Point(x, 31));
                    dc.DrawLine(new Pen(white, 1.5), new Point(x - 3, 26.5), new Point(x + 3, 29.5));
                    dc.DrawLine(new Pen(white, 1.5), new Point(x - 3, 29.5), new Point(x + 3, 26.5));
                }
            if (kind == "storm") dc.DrawGeometry(yellow, null, Geometry.Parse("M 17,18 L 12,26 17,26 14,32 24,23 19,23 22,18 Z"));
            if (kind == "fog")
            {
                dc.DrawLine(new Pen(white, 2), new Point(4, 26), new Point(28, 26));
                dc.DrawLine(new Pen(white, 2), new Point(8, 30), new Point(24, 30));
            }
        }
        if (kind == "unknown")
        {
            dc.DrawEllipse(null, new Pen(white, 2), new Point(16, 16), 11, 11);
            dc.DrawLine(new Pen(white, 2), new Point(10, 16), new Point(22, 16));
        }
        dc.Pop(); dc.Pop();
    }
}
