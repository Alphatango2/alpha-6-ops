using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class ResponsiveSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory, Action<bool, string> check)
    {
        var savedWidth = window.Width; var savedHeight = window.Height;
        foreach (var (width, height) in new[] { (640, 480), (720, 480), (900, 700), (1024, 768), (1149, 768), (1150, 768), (1366, 768), (1920, 1080), (2560, 1080), (2560, 1392), (900, 1200) })
        {
            window.Width = width; window.Height = height;
            window.DashboardScroll.ScrollToTop();
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            check(window.NavigationColumn.ActualWidth == (width < 1250 ? 64 : 204), $"Navigation adapts at {width}x{height}");
            check(Grid.GetRow(window.AlertsPanel) == (width < 1250 ? 1 : 0), $"Alerts reflow at {width}x{height}");
            check(Grid.GetRow(window.NetworkPanel) == (width < 1500 ? 1 : 0), $"Network map reflows at {width}x{height}");
            check(Grid.GetRow(window.MessagesPanel) == (width < 900 ? 2 : width < 1500 ? 1 : 0), $"Summary panels reflow at {width}x{height}");
            var weatherBounds = window.LocalWeatherButton.TransformToAncestor(window.DashboardRoot).TransformBounds(new Rect(window.LocalWeatherButton.RenderSize));
            check(weatherBounds.Right <= window.DashboardRoot.ActualWidth && weatherBounds.Left >= 0 && window.WeatherIcon.ActualWidth == 30,
                $"Weather and vector icon remain inside the header at {width}x{height}");
            var headerFits = true;
            foreach (var text in Descendants<TextBlock>(window.HeaderStatusGrid)) headerFits &= TextFits(text);
            check(headerFits, $"Every header label and clock fits at {width}x{height}");
            var firstCard = window.ConnectionBadge.TransformToAncestor(window.DashboardRoot).TransformBounds(new Rect(window.ConnectionBadge.RenderSize));
            var lastCard = window.HeaderClockPanel.TransformToAncestor(window.DashboardRoot).TransformBounds(new Rect(window.HeaderClockPanel.RenderSize));
            check(Math.Abs(firstCard.Left - (window.HeaderLogo.ActualWidth + 22)) <= 1 && Math.Abs(lastCard.Right - window.DashboardRoot.ActualWidth) <= 1,
                $"Header uses the full width after the logo without an empty spacer at {width}x{height}");
            check(Grid.GetRow(window.ZuluClockGroup) == (width < 1150 ? 1 : 0) && Grid.GetColumn(window.ZuluClockGroup) == (width < 1150 ? 0 : 2),
                $"Local and Zulu clocks use the appropriate side-by-side or stacked layout at {width}x{height}");
            var exitBounds = window.ExitOpsButton.TransformToAncestor(window).TransformBounds(new Rect(window.ExitOpsButton.RenderSize));
            check(exitBounds.Bottom <= window.ActualHeight && exitBounds.Right <= window.ActualWidth && window.ExitOpsButton.ActualHeight >= 32,
                $"Exit OPS remains in the window with a usable click target at {width}x{height}");
            check(window.HeroPanel.ActualWidth >= 490, $"Flight details retain readable width at {width}x{height}");
            check(window.DashboardFlightsGrid.Columns[1].ActualWidth >= 80, $"Route column retains readable width at {width}x{height}");
            check(AutomationProperties.GetName(window.DashboardNavButton) == "DASHBOARD", $"Compact navigation retains accessible names at {width}x{height}");
            var tile = (FrameworkElement)window.ModuleTiles.ItemContainerGenerator.ContainerFromIndex(0);
            check(tile.ActualWidth >= 180, $"Module tiles remain readable at {width}x{height}");
            check(window.NetworkPanel.ActualHeight >= (width < 1500 ? 270 : 240), $"Network map preserves its minimum height at {width}x{height}");
            foreach (var (content, card) in new (FrameworkElement, FrameworkElement)[] {
                (window.ConnectionCardContent, window.ConnectionBadge), (window.DispatchCardContent, window.HeaderDispatchButton),
                (window.WeatherCardContent, window.LocalWeatherButton), (window.ClockCardContent, window.HeaderClockPanel) })
            {
                var bounds = content.TransformToAncestor(card).TransformBounds(new Rect(content.RenderSize));
                check(Math.Abs(bounds.Left + bounds.Width / 2 - card.ActualWidth / 2) <= 2 && Math.Abs(bounds.Top + bounds.Height / 2 - card.ActualHeight / 2) <= 2,
                    $"{content.Name} stays centered at {width}x{height}");
            }
            var clippedCaptions = Descendants<TextBlock>(window.ModuleTiles).Where(text => !TextFits(text)).Select(text => text.Text).ToArray();
            check(clippedCaptions.Length == 0, $"Every module label and description fits without text clipping at {width}x{height}: {string.Join(", ", clippedCaptions)}");
            if (width >= 1920)
            {
                var summary = window.SummaryLayout.TransformToAncestor(window.DashboardBody).TransformBounds(new Rect(window.SummaryLayout.RenderSize));
                check(window.DashboardScroll.ScrollableHeight <= 1 && Math.Abs(summary.Bottom - window.DashboardScroll.ViewportHeight) <= 2,
                    $"Dashboard fills the viewport down to the footer at {width}x{height}; scroll {window.DashboardScroll.ScrollableHeight}, summary bottom {summary.Bottom}, viewport {window.DashboardScroll.ViewportHeight}, header {window.HeaderRow.ActualHeight}");
                check(window.HeroPanel.ActualHeight >= 353 && window.OperationsPanel.ActualHeight >= 225 && tile.ActualHeight >= 125,
                    $"Expanded sections preserve their minimum sizes at {width}x{height}");
                check(Math.Abs(window.ModuleTiles.ActualHeight - window.NetworkPanel.ActualHeight) <= 1,
                    $"Module tiles and network map share aligned edges at {width}x{height}");
            }
            DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, $"responsive-{width}x{height}.png"));
            if (width == 720)
            {
                window.DashboardScroll.ScrollToBottom();window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                check(window.DashboardScroll.VerticalOffset > 0 && window.MessagesPanel.ActualHeight >= 225,
                    "Small windows can scroll through all stacked dashboard sections");
                DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, "responsive-720-lower.png"));
            }
        }
        window.Width = 640; window.Height = 700;
        window.SetHeaderWeather(new LocalWeather("San Luis Obispo", -5, 57, DateTimeOffset.UtcNow));
        window.RenderLocalWeather(DateTimeOffset.UtcNow);
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        check(TextFits(window.WeatherLocationText) && TextFits(window.WeatherConditionText), "Long weather location and condition wrap without clipping");
        DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, "responsive-long-weather.png"));
        window.SetHeaderWeather(new LocalWeather("Milwaukee", 25, 3, DateTimeOffset.UtcNow));
        window.RenderLocalWeather(DateTimeOffset.UtcNow);
        foreach (var (pixelsWide, pixelsHigh, scale) in new[] { (1920, 1040, 1.5), (1920, 1040, 2.0), (1366, 728, 1.5), (1280, 680, 2.0) })
        {
            var available = new Size(pixelsWide / scale, pixelsHigh / scale);
            var size = MainWindow.FitWindowSize(new Size(2560, 1440), available);
            check(size.Width <= available.Width && size.Height <= available.Height,
                $"Saved window fits {pixelsWide}x{pixelsHigh} work area at {scale:P0} scaling");
        }
        check(window.DashboardScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden,
            "Dashboard keeps overflow available to wheel and touch without a permanent scrollbar");
        check(MainWindow.ShouldOptimizeToMonitor(null, new Size(1920, 1040)) &&
              MainWindow.ShouldOptimizeToMonitor(new UserPreferences(1366, 768, false, false, EmbeddedReplay.Fixtures[0]), new Size(1920, 1040)) &&
              !MainWindow.ShouldOptimizeToMonitor(new UserPreferences(1700, 950, false, false, EmbeddedReplay.Fixtures[0]), new Size(1920, 1040)),
            "Launch optimization expands undersized saved windows while preserving an already useful custom size");
        window.Width = 960; window.Height = 700; window.DashboardScroll.ScrollToTop(); window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, "responsive-150-percent.png"), 1.5);
        DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, "responsive-200-percent.png"), 2);
        window.Width = savedWidth; window.Height = savedHeight; window.DashboardScroll.ScrollToTop(); window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static bool TextFits(TextBlock text)
    {
        if (string.IsNullOrEmpty(text.Text)) return true;
        if (text.ActualWidth <= 0 || text.ActualHeight <= 0) return false;
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        if (text.TextWrapping != TextWrapping.NoWrap) formatted.MaxTextWidth = text.ActualWidth;
        return formatted.Height <= text.ActualHeight + 3 && (text.TextWrapping != TextWrapping.NoWrap || formatted.Width <= text.ActualWidth + 3);
    }
}
