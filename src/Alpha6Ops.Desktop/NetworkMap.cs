using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Alpha6Ops.Desktop;

// A native, offline globe-style network. No symbol represents a live aircraft position.
public sealed class NetworkMap : UserControl
{
    private readonly Canvas chart = new() { Width = 460, Height = 237, Background = Brushes.Transparent };
    private readonly TextBlock caption = new() { FontSize = 9, Foreground = Brush("#8FA8BB"), Margin = new Thickness(12, 3, 10, 6) };
    private readonly ScaleTransform scale = new(1, 1);
    private readonly TranslateTransform pan = new();
    private Point? drag;
    internal string SelectedStation { get; private set; } = "ATL";
    internal double ZoomLevel => scale.ScaleX;
    protected override Size MeasureOverride(Size constraint)
    {
        // Inside the scrolling dashboard the map has a compact natural footprint;
        // its Viewbox expands only after its parent assigns additional space.
        return base.MeasureOverride(new Size(constraint.Width, double.IsInfinity(constraint.Height) ? 200 : constraint.Height));
    }
    private static readonly Dictionary<string, Point> Stations = new()
    {
        ["SEA"] = new(51,35), ["LAX"] = new(65,132), ["DEN"] = new(160,99), ["MSP"] = new(236,59),
        ["ORD"] = new(267,83), ["MKE"] = new(267,66), ["DTW"] = new(300,78), ["ATL"] = new(303,153),
        ["JFK"] = new(373,94), ["MIA"] = new(345,207)
    };
    public NetworkMap()
    {
        var root = new Grid { Background = Brush("#050D14"), ClipToBounds = true };
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
        Control("+","Zoom network globe in",()=>Zoom(1.25)); Control("−","Zoom network globe out",()=>Zoom(.8)); Control("⌖","Reset network globe",ResetView);
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
        var globeBounds=new Rect(26,5,408,226);
        var ocean=new RadialGradientBrush
        {
            GradientOrigin=new Point(.36,.28),Center=new Point(.43,.42),RadiusX=.72,RadiusY=.72,
            GradientStops=new GradientStopCollection
            {
                new(Color.FromRgb(27,62,82),0),new(Color.FromRgb(9,29,42),.58),new(Color.FromRgb(3,12,20),1)
            }
        };
        var globe=new Ellipse { Width=globeBounds.Width,Height=globeBounds.Height,Fill=ocean,Stroke=Brush("#4A7187"),StrokeThickness=1.4,
            Effect=new DropShadowEffect { Color=Color.FromRgb(29,123,170),BlurRadius=18,ShadowDepth=0,Opacity=.28 } };
        Canvas.SetLeft(globe,globeBounds.Left);Canvas.SetTop(globe,globeBounds.Top);chart.Children.Add(globe);

        var globeLayer=new Canvas { Width=460,Height=237,Clip=new EllipseGeometry(globeBounds) };
        chart.Children.Add(globeLayer);
        void Add(UIElement element)=>globeLayer.Children.Add(element);

        // Curved graticule gives the compact native control depth without an online map dependency.
        foreach(var y in new[]{43d,72d,111d,150d,182d})
        {
            var latitude=new Path {Data=Geometry.Parse($"M 28,{y} Q 230,{y+(y<111?24:-24)} 432,{y}"),Stroke=Brush("#285064"),StrokeThickness=.65,Opacity=.55};
            Add(latitude);
        }
        foreach(var offset in new[]{-145d,-75d,0d,75d,145d})
        {
            var x=230+offset;
            var bend=offset*.42;
            Add(new Path {Data=Geometry.Parse($"M {x},4 C {x-bend},65 {x-bend},171 {x},232"),Stroke=Brush("#285064"),StrokeThickness=.65,Opacity=.5});
        }

        // Stylized North America silhouette, centered for this release's station network.
        var landBrush=new LinearGradientBrush(Brush("#244357").Color,Brush("#142B3A").Color,new Point(0,0),new Point(1,1));
        var land = new Path { Data=Geometry.Parse("M 33,22 L 63,29 105,35 139,37 198,39 208,31 219,45 242,43 259,52 275,46 291,54 296,68 312,71 322,60 347,62 374,51 389,29 400,24 411,40 406,61 391,76 394,91 380,104 371,122 355,135 343,146 333,161 341,178 354,205 351,217 342,216 326,194 317,170 301,168 290,174 270,170 256,173 246,162 232,171 219,190 207,189 200,177 189,163 166,158 145,157 126,150 97,150 82,141 70,139 62,120 52,110 46,92 38,80 35,63 28,56 Z"),Fill=landBrush,Stroke=Brush("#608094"),StrokeThickness=1.15 };
        Add(land);
        Add(new Path {Data=Geometry.Parse("M 54,108 Q 170,119 314,115 M 82,141 Q 196,147 317,139 M 208,40 Q 225,100 219,188 M 291,55 Q 275,108 301,168"),Stroke=Brush("#34586B"),StrokeThickness=.55,Opacity=.75});

        var hub=Stations[SelectedStation];
        var index=0;
        foreach(var (name,point) in Stations.Where(s=>s.Key!=SelectedStation))
        {
            var color=index%3==0?"#FFDA00":"#48A8D8";
            var middle=new Point((point.X+hub.X)/2,Math.Min(point.Y,hub.Y)-24-Math.Abs(point.X-hub.X)*.05);
            var geometry=new PathGeometry([new PathFigure(point,[new QuadraticBezierSegment(middle,hub,true)],false)]);
            Add(new Path { Data=geometry,Stroke=Brush(color),StrokeThickness=4,Opacity=.09 });
            Add(new Path { Data=geometry,Stroke=Brush(color),StrokeThickness=index%3==0?1.35:.9,Opacity=.9 });
            if(index%2==0)
            {
                var t=.45;var pos=new Point((1-t)*(1-t)*point.X+2*(1-t)*t*middle.X+t*t*hub.X,(1-t)*(1-t)*point.Y+2*(1-t)*t*middle.Y+t*t*hub.Y);
                var plane=new Path {Data=Geometry.Parse("M 0,4 L 5,3 8,0 10,0 8,3 13,4 8,5 10,8 8,8 5,5 0,4 Z"),Fill=Brush(index%3==0?"#FFDC24":"#74D68A"),Width=18,Height=12,Stretch=Stretch.Fill,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(Math.Atan2(hub.Y-point.Y,hub.X-point.X)*180/Math.PI),ToolTip="Illustrative route symbol — not live traffic"};
                Canvas.SetLeft(plane,pos.X-9);Canvas.SetTop(plane,pos.Y-6);Add(plane);
            }
            index++;
        }
        var labelOffsets=new Dictionary<string,Point>{{"SEA",new(-8,-12)},{"LAX",new(-7,-7)},{"DEN",new(-6,-8)},{"MSP",new(-5,-10)},{"MKE",new(3,-22)},{"ORD",new(-7,3)},{"DTW",new(2,-7)},{"ATL",new(2,2)},{"JFK",new(3,-8)},{"MIA",new(2,-2)}};
        foreach(var (name,point) in Stations)
        {
            if(name==SelectedStation)
            {
                foreach(var size in new[]{18d,28d,38d}) { var ring=new Ellipse{Width=size,Height=size,Stroke=Brush("#FFDA00"),StrokeThickness=size==18?2:1,Opacity=size==38?.22:.7};Canvas.SetLeft(ring,point.X-size/2);Canvas.SetTop(ring,point.Y-size/2);Add(ring); }
            }
            var button=new Button { Content="● "+name,Foreground=Brush(name==SelectedStation?"#FFDB2F":"#EDF7FC"),FontSize=10,FontWeight=FontWeights.SemiBold,Padding=new Thickness(3,1,3,1),Style=(Style)FindResource("OpsLink"),ToolTip=$"Focus {DashboardData.City(name)} connections",Background=Brush("#C0061119"),BorderBrush=Brush("#304B5C"),BorderThickness=new Thickness(.6) };
            AutomationProperties.SetName(button,$"Show {name} network connections");button.Click+=(_,e)=>{SelectStation(name);e.Handled=true;};
            var offset=labelOffsets[name];Canvas.SetLeft(button,point.X+offset.X);Canvas.SetTop(button,point.Y+offset.Y);Add(button);
        }
        var rim=new Ellipse {Width=globeBounds.Width,Height=globeBounds.Height,Stroke=Brush("#8FB4C7"),StrokeThickness=.7,Opacity=.45,IsHitTestVisible=false};
        Canvas.SetLeft(rim,globeBounds.Left);Canvas.SetTop(rim,globeBounds.Top);chart.Children.Add(rim);
        caption.Text=$"{SelectedStation} NETWORK  •  {Stations.Count-1} CONNECTIONS  •  DRAG TO EXPLORE";
    }
}
