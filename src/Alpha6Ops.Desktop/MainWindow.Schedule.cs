using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private void CorrectSchedule()
    {
        if(activePlan is null)return;
        var dialog=new Window{Title="Correct flight schedule",Width=520,SizeToContent=SizeToContent.Height,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)FindResource("OpsWindow"),Background=OpsUi.Brush("#071019"),Foreground=OpsUi.Brush("#F2F6FA")};
        var panel=new StackPanel{Margin=new Thickness(24)};dialog.Content=panel;
        panel.Children.Add(new TextBlock{Text="FLIGHT SCHEDULE · UTC",FontSize=23});
        panel.Children.Add(new TextBlock{Text=liveLast is {} now?$"Simulator: {now.UtcDateTime:dd MMM yyyy HH:mm}Z":"Connect to compare the simulator clock.",Margin=new Thickness(0,12,0,0)});
        TextBox Field(string label,DateTimeOffset value){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,16,0,6)});var box=new TextBox{Text=value.UtcDateTime.ToString("yyyy-MM-dd HH:mm"),Padding=new Thickness(8)};panel.Children.Add(box);return box;}
        var departure=Field("Scheduled OUT · YYYY-MM-DD HH:MM",activePlan.PlannedDepartureUtc);var arrival=Field("Scheduled IN · YYYY-MM-DD HH:MM",activePlan.PlannedArrivalUtc);
        var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};panel.Children.Add(error);
        var save=new Button{Content="SAVE UTC SCHEDULE",Style=(Style)FindResource("OpsPrimary"),Margin=new Thickness(0,18,0,0),Padding=new Thickness(12)};panel.Children.Add(save);
        save.Click+=(_,_)=>{if(!DateTimeOffset.TryParseExact(departure.Text,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var dep)||!DateTimeOffset.TryParseExact(arrival.Text,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var arr)||arr<=dep){error.Text="Enter both UTC dates and times, with arrival after departure.";return;}try{ApplyScheduleCorrection(dep,arr);dialog.DialogResult=true;}catch(Exception ex)when(ex is System.IO.IOException or UnauthorizedAccessException){error.Text=ex.Message;}};
        dialog.ShowDialog();
    }
    internal void ApplyScheduleCorrection(DateTimeOffset departure,DateTimeOffset arrival)
    {
        coordinator.CorrectSchedule(departure,arrival);
    }
}
