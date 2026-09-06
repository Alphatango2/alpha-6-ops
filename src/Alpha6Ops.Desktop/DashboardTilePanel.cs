using System;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public sealed class DashboardTilePanel : Panel
{
    private const double MinimumTileHeight = 125;
    private const double Gap = 12;
    internal static int ColumnsFor(double width) => Math.Clamp((int)((width + Gap) / (185 + Gap)), 2, 4);
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        var columns = ColumnsFor(width);
        var rows = Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)columns));
        var tileWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var tileHeight = MinimumTileHeight;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(tileWidth, double.PositiveInfinity));
            tileHeight = Math.Max(tileHeight, child.DesiredSize.Height);
        }
        var height = rows * tileHeight + (rows - 1) * Gap;
        if (double.IsFinite(availableSize.Height)) height = Math.Max(height, availableSize.Height);
        tileHeight = (height - (rows - 1) * Gap) / rows;
        foreach (UIElement child in InternalChildren) child.Measure(new Size(tileWidth, tileHeight));
        return new Size(width, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        var rows = Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)columns));
        var tileWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var tileHeight = Math.Max(MinimumTileHeight, (finalSize.Height - Gap * (rows - 1)) / rows);
        for (var i = 0; i < InternalChildren.Count; i++)
            InternalChildren[i].Arrange(new Rect(i % columns * (tileWidth + Gap), i / columns * (tileHeight + Gap), tileWidth, tileHeight));
        return finalSize;
    }
}
