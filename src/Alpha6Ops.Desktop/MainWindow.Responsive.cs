using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private (bool Rail, bool Header, bool Hero, bool Map, int Summary)? responsiveMode;

    private void UpdateResponsiveLayout()
    {
        if (DashboardRoot is null || ActualWidth <= 0) return;
        // DashboardRoot is the 1920x1080 reference surface. The containing Viewbox scales
        // that complete surface to the monitor, so its internal layout must remain in the
        // full desktop mode instead of stacking panels and creating vertical overflow.
        var width = DashboardRoot.Width;
        var mode = (Rail: width < 1250, Header: width < 1150, Hero: width < 1250, Map: width < 1500, Summary: width < 900 ? 2 : width < 1500 ? 1 : 0);
        if (responsiveMode == mode) return;
        responsiveMode = mode;
        NavigationColumn.Width = new GridLength(mode.Rail ? 64 : 204);
        NavigationGap.Width = new GridLength(mode.Rail ? 12 : 16);
        NavigationProfile.Visibility = mode.Rail ? Visibility.Collapsed : Visibility.Visible;
        foreach (Button button in NavigationButtons.Children)
        {
            if (button.Content is not StackPanel panel || panel.Children.Count < 2 || panel.Children[1] is not TextBlock label) continue;
            button.ToolTip = label.Text;
            AutomationProperties.SetName(button, label.Text);
            label.Visibility = mode.Rail ? Visibility.Collapsed : Visibility.Visible;
            if (panel.Children[0] is TextBlock glyph) glyph.Width = mode.Rail ? 28 : 43;
            else if (panel.Children[0] is FrameworkElement icon)
                icon.Margin = new Thickness(0, 0, mode.Rail ? 4 : 19, 0);
            button.Padding = new Thickness(mode.Rail ? 8 : 15, 12, mode.Rail ? 8 : 15, 12);
        }

        HeaderRow.Height = GridLength.Auto;
        HeaderRow.MinHeight = mode.Header ? 210 : 110;
        HeaderStatusGrid.MinHeight = mode.Header ? 198 : 98;
        Grid.SetColumn(HeaderStatusGrid, 0);
        Grid.SetColumnSpan(HeaderStatusGrid, 3);
        HeaderStatusGrid.Margin = new Thickness(276, 0, 0, 12);
        HeaderStatusGrid.ColumnDefinitions.Clear();
        foreach (var weight in mode.Header ? new double[] { 1, 1 } : new double[] { 1, .95, 1.25, 2 })
            HeaderStatusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
        HeaderStatusGrid.RowDefinitions.Clear();
        HeaderStatusGrid.RowDefinitions.Add(new RowDefinition());
        if (mode.Header) HeaderStatusGrid.RowDefinitions.Add(new RowDefinition());
        Place(ConnectionBadge, 0, 0); Place(HeaderDispatchButton, 0, 1);
        Place(LocalWeatherButton, mode.Header ? 1 : 0, mode.Header ? 0 : 2);
        Place(HeaderClockPanel, mode.Header ? 1 : 0, mode.Header ? 1 : 3);
        ConnectionBadge.Margin = new Thickness(0, 0, 10, mode.Header ? 8 : 0);
        HeaderDispatchButton.Margin = mode.Header ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 10, 0);
        SetGrid(ClockCardContent, mode.Header ? [1] : [1, 0, 1], mode.Header ? 2 : 1);
        if (!mode.Header) ClockCardContent.ColumnDefinitions[1].Width = new GridLength(21);
        Place(LocalClockGroup, 0, 0);
        Place(ZuluClockGroup, mode.Header ? 1 : 0, mode.Header ? 0 : 2);
        ClockDivider.Visibility = mode.Header ? Visibility.Collapsed : Visibility.Visible;
        ZuluClockGroup.Margin = new Thickness(0, mode.Header ? 6 : 0, 0, 0);
        LocalClockText.FontSize = ClockText.FontSize = mode.Header ? 18 : 22;
        DashboardFooter.Margin = new Thickness(mode.Rail ? 76 : 220, 0, 0, 0);
        FooterBrand.Visibility = width < 1000 ? Visibility.Collapsed : Visibility.Visible;
        VersionText.Visibility = width < 900 ? Visibility.Collapsed : Visibility.Visible;

        SetGrid(HeroAlertsLayout, mode.Hero ? [1] : [2.7, 0, 1], mode.Hero ? 2 : 1);
        Place(HeroPanel, 0, 0);
        Place(AlertsPanel, mode.Hero ? 1 : 0, mode.Hero ? 0 : 2);
        AlertsPanel.Margin = mode.Hero ? new Thickness(0, 12, 0, 0) : new Thickness(0);
        AlertsPanel.MinHeight = 353;
        HeroQuote.Visibility = mode.Hero ? Visibility.Collapsed : Visibility.Visible;

        SetGrid(ModulesMapLayout, mode.Map ? [1] : [1.85, 0, 1], mode.Map ? 2 : 1);
        Place(ModuleTiles, 0, 0);
        Place(NetworkPanel, mode.Map ? 1 : 0, mode.Map ? 0 : 2);
        NetworkPanel.MinHeight = mode.Map ? 270 : 240;
        NetworkPanel.Margin = new Thickness(0, mode.Map ? 12 : 0, 0, 0);

        SetGrid(SummaryLayout, mode.Summary == 2 ? [1] : mode.Summary == 1 ? [1, 0, 1] : [1.65, 0, 1, 0, 1], mode.Summary == 2 ? 3 : mode.Summary == 1 ? 2 : 1);
        Place(OperationsPanel, 0, 0, mode.Summary == 1 ? 3 : 1);
        Place(FleetPanel, mode.Summary == 0 ? 0 : 1, mode.Summary == 0 ? 2 : 0);
        Place(MessagesPanel, mode.Summary == 2 ? 2 : mode.Summary == 1 ? 1 : 0, mode.Summary == 2 ? 0 : mode.Summary == 1 ? 2 : 4);
        FleetPanel.Margin = MessagesPanel.Margin = new Thickness(0, mode.Summary == 0 ? 0 : 12, 0, 0);
    }

    private static void SetGrid(Grid grid, double[] columns, int rows)
    {
        grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
        foreach (var column in columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column == 0 ? new GridLength(12) : new GridLength(column, GridUnitType.Star) });
        for (var row = 0; row < rows; row++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
    }

    private static void Place(UIElement element, int row, int column, int span = 1)
    {
        Grid.SetRow(element, row); Grid.SetColumn(element, column); Grid.SetColumnSpan(element, span);
    }
}
