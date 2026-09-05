using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Alpha6Ops.Desktop;

// A deliberately schematic, offline network. No symbol represents a live aircraft position.
public sealed class NetworkMap : UserControl
{
    private readonly Canvas chart = new() { Width = 460, Height = 237, Background = Brushes.Transparent };
    private readonly TextBlock caption = new() { FontSize = 9, Foreground = Brush("#8FA8BB"), Margin = new Thickness(12, 3, 10, 6) };
    private readonly ScaleTransform scale = new(1, 1);
    private readonly TranslateTransform pan = new();
    private Point? drag;
    internal string SelectedStation { get; private set; } = "ATL";
    internal double ZoomLevel => scale.ScaleX;
    private static readonly Dictionary<string, Point> Stations = new()
    {
        ["SEA"] = new(51,35), ["LAX"] = new(65,132), ["DEN"] = new(160,99), ["MSP"] = new(236,59),
        ["ORD"] = new(267,83), ["MKE"] = new(267,66), ["DTW"] = new(300,78), ["ATL"] = new(303,153),
        ["JFK"] = new(373,94), ["MIA"] = new(345,207)
    };
    public NetworkMap()
    {
        var root = new Grid { Background = Brush("#07111A"), ClipToBounds = true };
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var group = new TransformGroup(); group.Children.Add(scale); group.Children.Add(pan);
        chart.RenderTransform = group; chart.RenderTransformOrigin = new Point(.5,.5);
        var viewport = new Grid { ClipToBounds = true };
        viewport.Children.Add(new Viewbox { Child = chart, Stretch = Stretch.Uniform }); root.Children.Add(viewport);
        var controls = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,5,9,0) };
        void Control(string label, string name, Action action)
        {
            var button = new Button { Content = label, Width = 29, Height = 29, Padding = new Thickness(0), Margin = new Thickness(0,0,0,5), Style = (Style)FindResource("OpsButton"), ToolTip = name };
            AutomationProperties.SetName(button,name); button.Click += (_,_)=>action(); controls.Children.Add(button);
        }
        Control("+","Zoom network map in",()=>Zoom(1.25)); Control("−","Zoom network map out",()=>Zoom(.8)); Control("⌖","Reset network map",ResetView);
        root.Children.Add(controls); Grid.SetRow(caption,1); root.Children.Add(caption);
        Content=root;
        chart.MouseWheel += (_,e)=> { Zoom(e.Delta > 0 ? 1.15 : 1/1.15); e.Handled=true; };
        chart.MouseLeftButtonDown += (_,e)=> { if(e.OriginalSource is Button) return; drag=e.GetPosition(this); chart.CaptureMouse(); };
        chart.MouseMove += (_,e)=> { if(drag is not { } previous || e.LeftButton != MouseButtonState.Pressed) return; var current=e.GetPosition(this); pan.X=Math.Clamp(pan.X+current.X-previous.X,-160,160); pan.Y=Math.Clamp(pan.Y+current.Y-previous.Y,-90,90); drag=current; };
        chart.MouseLeftButtonUp += (_,_)=> { drag=null; chart.ReleaseMouseCapture(); };
        Draw();
    }
    internal void Zoom(double factor) { scale.ScaleX=scale.ScaleY=Math.Clamp(scale.ScaleX*factor,1,2.5); if(scale.ScaleX==1) pan.X=pan.Y=0; }
    internal void ResetView() { scale.ScaleX=scale.ScaleY=1;pan.X=pan.Y=0;SelectStation("ATL"); }
    internal void SelectStation(string station) { if(!Stations.ContainsKey(station))return;SelectedStation=station;Draw(); }
    private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    private void Draw()
    {
        chart.Children.Clear();
        for(var x=15;x<460;x+=32) chart.Children.Add(new Line { X1=x,Y1=0,X2=x-42,Y2=237,Stroke=Brush("#122633"),StrokeThickness=.6 });
        for(var y=12;y<237;y+=27) chart.Children.Add(new Line { X1=0,Y1=y,X2=460,Y2=y+13,Stroke=Brush("#122633"),StrokeThickness=.6 });
        // Hand-drawn continental outline for an illustrative operations overview, not a basemap.
        var land = new Path { Data=Geometry.Parse("M 33,22 L 63,29 105,35 139,37 198,39 208,31 219,45 242,43 259,52 275,46 291,54 296,68 312,71 322,60 347,62 374,51 389,29 400,24 411,40 406,61 391,76 394,91 380,104 371,122 355,135 343,146 333,161 341,178 354,205 351,217 342,216 326,194 317,170 301,168 290,174 270,170 256,173 246,162 232,171 219,190 207,189 200,177 189,163 166,158 145,157 126,150 97,150 82,141 70,139 62,120 52,110 46,92 38,80 35,63 28,56 Z"),Fill=Brush("#142532"),Stroke=Brush("#385161"),StrokeThickness=1 };
        chart.Children.Add(land);
        foreach(var (x,y,h) in new[]{(100,35,109),(138,37,117),(175,39,123),(210,46,120),(245,54,106),(282,77,82),(317,83,66)}) chart.Children.Add(new Line { X1=x,Y1=y,X2=x-5,Y2=y+h,Stroke=Brush("#29404E"),StrokeThickness=.7 });
        foreach(var (x,y,w) in new[]{(42,75,243),(54,108,260),(71,133,255),(135,154,176)}) chart.Children.Add(new Line{X1=x,Y1=y,X2=x+w,Y2=y+7,Stroke=Brush("#29404E"),StrokeThickness=.7});
        var hub=Stations[SelectedStation];
        var index=0;
        foreach(var (name,point) in Stations.Where(s=>s.Key!=SelectedStation))
        {
            var color=index%3==0?"#C9AB25":"#347FAC";
            var middle=new Point((point.X+hub.X)/2,Math.Min(point.Y,hub.Y)-30);
            var geometry=new PathGeometry([new PathFigure(point,[new QuadraticBezierSegment(middle,hub,true)],false)]);
            chart.Children.Add(new Path { Data=geometry,Stroke=Brush(color),StrokeThickness=index%3==0?1.3:.8,Opacity=.85 });
            if(index%2==0)
            {
                var t=.45;var pos=new Point((1-t)*(1-t)*point.X+2*(1-t)*t*middle.X+t*t*hub.X,(1-t)*(1-t)*point.Y+2*(1-t)*t*middle.Y+t*t*hub.Y);
                var plane=new TextBlock{Text="✈",FontSize=18,Foreground=Brush(index%3==0?"#FFDC24":"#72C865"),RenderTransform=new RotateTransform(Math.Atan2(hub.Y-point.Y,hub.X-point.X)*180/Math.PI),ToolTip="Illustrative route symbol — not live traffic"};
                Canvas.SetLeft(plane,pos.X-8);Canvas.SetTop(plane,pos.Y-11);chart.Children.Add(plane);
            }
            index++;
        }
        foreach(var (name,point) in Stations)
        {
            if(name==SelectedStation)
            {
                foreach(var size in new[]{20d,30d}) { var ring=new Ellipse{Width=size,Height=size,Stroke=Brush("#FF5354"),StrokeThickness=size==20?1.8:1,Opacity=.9};Canvas.SetLeft(ring,point.X-size/2);Canvas.SetTop(ring,point.Y-size/2);chart.Children.Add(ring); }
            }
            var button=new Button { Content="● "+name,Foreground=Brush(name==SelectedStation?"#FFDB2F":"#E5F1F8"),FontSize=11,Padding=new Thickness(2),Style=(Style)FindResource("OpsLink"),ToolTip=$"Focus {DashboardData.City(name)} connections",Background=Brush("#A007111A") };
            AutomationProperties.SetName(button,$"Show {name} network connections");button.Click+=(_,e)=>{SelectStation(name);e.Handled=true;};
            Canvas.SetLeft(button,point.X-5);Canvas.SetTop(button,point.Y+(name=="MKE"?-18:-7));chart.Children.Add(button);
        }
        caption.Text=$"{SelectedStation} CONNECTIONS  •  SCHEMATIC ROUTES  •  DRAG TO PAN";
    }
}
