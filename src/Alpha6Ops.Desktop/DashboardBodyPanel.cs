using System;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

// A scrollable dashboard has a natural minimum height, then shares spare viewport
// space between its three main sections. Text is never scaled down to make it fit.
public sealed class DashboardBodyPanel : Panel
{
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(nameof(ViewportHeight), typeof(double),
        typeof(DashboardBodyPanel), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double ViewportHeight { get => (double)GetValue(ViewportHeightProperty); set => SetValue(ViewportHeightProperty, value); }
    private double[] heights = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        heights = new double[InternalChildren.Count];
        double minimum = 0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            heights[i] = InternalChildren[i].DesiredSize.Height;
            minimum += heights[i];
        }
        var target = Math.Max(minimum, double.IsFinite(ViewportHeight) ? ViewportHeight : 0);
        var extra = target - minimum;
        double[] weights = [.4, .35, .25];
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            if (i < weights.Length) heights[i] += extra * weights[i];
            InternalChildren[i].Measure(new Size(availableSize.Width, heights[i]));
        }
        return new Size(availableSize.Width, target);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Arrange(new Rect(0, y, finalSize.Width, heights[i]));
            y += heights[i];
        }
        return finalSize;
    }
}
