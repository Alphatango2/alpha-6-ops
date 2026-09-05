using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class FlightHistoryWindow : Window
{
    private readonly FlightHistoryDatabase database;
    internal FlightHistoryWindow(FlightHistoryDatabase database)
    {
        InitializeComponent();
        this.database = database;
        DatabaseText.Text = database.DatabasePath;
        RefreshRows();
    }
    private void RefreshRows() => FlightsGrid.ItemsSource = database.ReadRecentFlights();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRows();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
