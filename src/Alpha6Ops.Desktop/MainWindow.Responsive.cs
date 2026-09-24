using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private void UpdateResponsiveLayout()
    {
        // SierraShell responds to its actual viewport. Retained feature pages keep
        // their internal layout and scroll inside LegacyViewport at narrow widths.
        if (DashboardRoot is null) return;
        DashboardRoot.MinWidth=(double)FindResource("S4.LegacyMinimumWidth");
        DashboardRoot.MinHeight=(double)FindResource("S4.LegacyMinimumHeight");
    }
}
