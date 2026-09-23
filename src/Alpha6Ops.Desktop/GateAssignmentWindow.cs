using System;
using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

internal sealed class GateAssignmentWindow : Window
{
    internal ActiveFlightPlan? Plan { get; private set; }
    internal GateAssignmentWindow(ActiveFlightPlan current)
    {
        Title="Dispatch gates";Width=440;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;Style=(Style)FindResource("OpsWindow");
        Background=OpsUi.Brush("#071019");Foreground=OpsUi.Brush("#F2F6FA");
        var panel=new StackPanel{Margin=new Thickness(24)};Content=panel;
        panel.Children.Add(new TextBlock{Text=$"{current.FlightNumber} · Assign gates",FontSize=22});
        TextBox Field(string label,string? value)
        {
            panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,18,0,6)});
            var box=new TextBox{Text=value??"",MaxLength=6,MinHeight=34};panel.Children.Add(box);return box;
        }
        var departure=Field(current.Origin+" · Departure gate",current.DepartureGate);
        var arrival=Field(current.Destination+" · Arrival gate",current.ArrivalGate);
        panel.Children.Add(new TextBlock{Text="Leave blank if unassigned. Use the gate number only.",Margin=new Thickness(0,12,0,0)});
        var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=OpsUi.Brush("#FF9690"),Margin=new Thickness(0,10,0,0)};panel.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};
        var cancel=new Button{Content="Cancel",IsCancel=true,Padding=new Thickness(18,9,18,9),Margin=new Thickness(0,0,12,0)};
        var save=new Button{Content="Assign gates",Style=(Style)FindResource("OpsPrimary"),Padding=new Thickness(18,9,18,9),IsDefault=true};
        buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        save.Click+=(_,_)=>
        {
            var dep=GateAssignmentResolver.Normalize(departure.Text);var arr=GateAssignmentResolver.Normalize(arrival.Text);
            if((!string.IsNullOrWhiteSpace(departure.Text)&&dep is null)||(!string.IsNullOrWhiteSpace(arrival.Text)&&arr is null))
            {error.Text="Use up to six letters or numbers, such as 24 or A12.";return;}
            Plan=current with{DepartureGate=dep,ArrivalGate=arr,GateAssignmentSource="Dispatch",GateAssignmentConfidence="Assigned"};DialogResult=true;
        };
    }
}
