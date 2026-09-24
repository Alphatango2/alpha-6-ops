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
        // Native shell dimensions are independent of the preserved legacy page surface.
        foreach (var (width, height) in new[] { (640, 480), (720, 480), (900, 700), (1024, 768), (1149, 768), (1150, 768), (1366, 768), (1920, 1080), (2560, 1080), (2560, 1440), (900, 1200) })
        {
            window.Width=width;window.Height=height;window.ShowDashboard();window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();
            var shell=window.Shell;
            check(shell.RailColumn.ActualWidth==(shell.IsCompact?64:208),$"Native rail adapts to {width}x{height}");
            check(Descendants<Viewbox>(shell).All(v=>!IsAncestor(v,shell.WorkspacePresenter)),$"Workspace has no scaling Viewbox at {width}x{height}");
            check(shell.WorkspaceTitle.FontSize==22&&shell.ConnectionLabel.FontSize==12,$"Shell text retains native readable sizes at {width}x{height}");
            foreach(var control in new FrameworkElement[]{shell.BrandMark,shell.SearchButton,shell.ConnectionButton,shell.MessagesButton,shell.ProfileButton,shell.WorkspacePresenter,window.ExitOpsButton})
            {
                var b=control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
                check(b.Left>=0&&b.Right<=window.ActualWidth+1&&b.Top>=0&&b.Bottom<=window.ActualHeight+1&&b.Width>0&&b.Height>0,$"{control.Name} stays reachable at {width}x{height}");
            }
            check(TextFits(shell.ConnectionLabel)&&TextFits(shell.WorkspaceTitle),$"Header text fits at {width}x{height}");
            check(Descendants<Button>(shell.PrimaryNavigation).All(b=>!string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))&&b.ActualHeight>=40&&b.ActualWidth>=40&&Descendants<TextBlock>(b).Where(t=>t.IsVisible).All(TextFits)),$"Navigation has named usable targets at {width}x{height}");
            check(window.LegacyViewport.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto&&window.LegacyViewport.VerticalScrollBarVisibility==ScrollBarVisibility.Auto,$"Legacy content can scroll instead of shrinking at {width}x{height}");
            var tile=(FrameworkElement)window.ModuleTiles.ItemContainerGenerator.ContainerFromIndex(0);
            check(window.HeroPanel.ActualWidth>=490&&window.DashboardFlightsGrid.Columns[1].ActualWidth>=80&&tile.ActualWidth>=180,$"Legacy operational content retains readable width at {width}x{height}");
            check(Grid.GetRow(window.AlertsPanel)==0&&Grid.GetRow(window.NetworkPanel)==0&&Grid.GetRow(window.MessagesPanel)==0&&window.NetworkPanel.ActualHeight>=240,$"Legacy panel layout is retained at {width}x{height}");
            check(Descendants<TextBlock>(window.ModuleTiles).All(TextFits),$"Legacy module captions fit at {width}x{height}");
            check(window.HeroPanel.ActualHeight>=353&&window.OperationsPanel.ActualHeight>=225&&tile.ActualHeight>=125,$"Legacy section minimums hold at {width}x{height}");
            check(Math.Abs(window.ModuleTiles.ActualHeight-window.NetworkPanel.ActualHeight)<=1,$"Legacy modules and map stay aligned at {width}x{height}");
            DashboardSmokeTest.Capture(window,Path.Combine(outputDirectory,$"responsive-{width}x{height}.png"));
            window.LegacyViewport.ScrollToBottom();window.LegacyViewport.ScrollToHorizontalOffset(window.LegacyViewport.ScrollableWidth);window.UpdateLayout();
            check(Math.Abs(window.LegacyViewport.VerticalOffset-window.LegacyViewport.ScrollableHeight)<1&&Math.Abs(window.LegacyViewport.HorizontalOffset-window.LegacyViewport.ScrollableWidth)<1,$"Entire legacy workspace is reachable by scrolling at {width}x{height}");
            window.LegacyViewport.ScrollToTop();window.LegacyViewport.ScrollToHorizontalOffset(0);
            shell.OpenSearch();window.UpdateLayout();
            var searchBounds=shell.SearchPanel.TransformToAncestor(window).TransformBounds(new Rect(shell.SearchPanel.RenderSize));
            check(searchBounds.Bottom<=window.ActualHeight&&shell.WorkspaceSearch.IsKeyboardFocused,$"Search fits and receives keyboard focus at {width}x{height}");
            if(width==640)DashboardSmokeTest.Capture(window,Path.Combine(outputDirectory,"sierra-search-640.png"));
            shell.CloseOverlays();
        }
        window.Width = 640; window.Height = 700;
        window.Shell.ShowUtilities();
        window.SetHeaderWeather(new LocalWeather("San Luis Obispo", -5, 57, DateTimeOffset.UtcNow));
        window.RenderLocalWeather(DateTimeOffset.UtcNow);
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        check(TextFits(window.WeatherLocationText) && TextFits(window.WeatherConditionText), "Long weather location and condition wrap without clipping");
        DashboardSmokeTest.Capture(window, Path.Combine(outputDirectory, "responsive-long-weather.png"));
        window.SetHeaderWeather(new LocalWeather("Milwaukee", 25, 3, DateTimeOffset.UtcNow));
        window.RenderLocalWeather(DateTimeOffset.UtcNow);
        window.Shell.CloseOverlays();
        foreach (var (pixelsWide, pixelsHigh, scale) in new[] { (1920, 1040, 1.5), (1920, 1040, 2.0), (1366, 728, 1.5), (1280, 680, 2.0) })
        {
            var available = new Size(pixelsWide / scale, pixelsHigh / scale);
            var size = MainWindow.FitWindowSize(new Size(2560, 1440), available);
            check(size.Width <= available.Width && size.Height <= available.Height,
                $"Saved window fits {pixelsWide}x{pixelsHigh} work area at {scale:P0} scaling");
        }
        check(window.DashboardScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled && window.DashboardScroll.PanningMode == PanningMode.None,
            "Dashboard disables scrollbar, mouse-wheel, and touch-panning movement");
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

    private static bool IsAncestor(DependencyObject candidate,DependencyObject child)
    {
        for(var p=VisualTreeHelper.GetParent(child);p is not null;p=VisualTreeHelper.GetParent(p))if(p==candidate)return true;
        return false;
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
