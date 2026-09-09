using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Alpha6Ops.Desktop;

// Native offline route globe for the planned SimBrief route.
public sealed class FlightRouteMap : UserControl
{
    private const double CanvasWidth=900,CanvasHeight=520,MapLeft=35,MapTop=20,MapWidth=830,MapHeight=470;
    private readonly Canvas mapLayer=new(){Width=CanvasWidth,Height=CanvasHeight};
    private readonly ScaleTransform userScale=new(1,1,CanvasWidth/2,CanvasHeight/2);
    private readonly TranslateTransform userPan=new();
    private readonly TextBlock caption=new(){Foreground=Brush("#89A1B1"),FontSize=10,Margin=new Thickness(15,4,0,9)};
    private IReadOnlyList<FlightRoutePoint> route=[];
    private Point? drag;
    private double centerLongitude;
    private double fittedZoom=1;

    internal int RoutePointCount=>route.Count;
    internal bool CrossesDateLine {get;private set;}
    internal double ZoomLevel=>userScale.ScaleX;
    internal double FittedZoom=>fittedZoom;

    public FlightRouteMap()
    {
        var root=new Grid{Background=Brush("#040C13"),ClipToBounds=true};root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var viewport=new Grid{ClipToBounds=true};
        var group=new TransformGroup();group.Children.Add(userScale);group.Children.Add(userPan);mapLayer.RenderTransform=group;
        viewport.Children.Add(new Viewbox{Child=mapLayer,Stretch=Stretch.Uniform});root.Children.Add(viewport);
        var controls=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,11,13,0)};
        AddControl(controls,"+","Zoom route globe in",()=>Zoom(1.25));AddControl(controls,"−","Zoom route globe out",()=>Zoom(.8));AddControl(controls,"⌖","Reset route globe",ResetView);
        root.Children.Add(controls);Grid.SetRow(caption,1);root.Children.Add(caption);Content=root;
        mapLayer.MouseWheel+=(_,e)=>{Zoom(e.Delta>0?1.15:1/1.15);e.Handled=true;};
        mapLayer.MouseLeftButtonDown+=(_,e)=>{drag=e.GetPosition(this);mapLayer.CaptureMouse();};
        mapLayer.MouseMove+=(_,e)=>{if(drag is not{} previous||e.LeftButton!=MouseButtonState.Pressed)return;var current=e.GetPosition(this);userPan.X=Math.Clamp(userPan.X+current.X-previous.X,-260,260);userPan.Y=Math.Clamp(userPan.Y+current.Y-previous.Y,-150,150);drag=current;};
        mapLayer.MouseLeftButtonUp+=(_,_)=>{drag=null;mapLayer.ReleaseMouseCapture();};
        Draw();
    }

    internal void SetRoute(IReadOnlyList<FlightRoutePoint>? points)
    {
        route=points?.Where(p=>p.Latitude is>=-90 and<=90&&p.Longitude is>=-180 and<=180).ToArray()??[];
        CrossesDateLine=route.Zip(route.Skip(1),(a,b)=>Math.Abs(a.Longitude-b.Longitude)>180).Any(value=>value);
        centerLongitude=RouteCenter(route);
        fittedZoom=CalculateFittedZoom(route);
        ResetView();Draw();
    }

    internal void Zoom(double factor){userScale.ScaleX=userScale.ScaleY=Math.Clamp(userScale.ScaleX*factor,fittedZoom,3);if(userScale.ScaleX==fittedZoom)userPan.X=userPan.Y=0;}
    internal void ResetView(){userScale.ScaleX=userScale.ScaleY=fittedZoom;userPan.X=userPan.Y=0;}

    private void AddControl(Panel panel,string label,string name,Action action)
    {
        var button=new Button{Content=label,Width=32,Height=32,Padding=new Thickness(0),Margin=new Thickness(0,0,0,6),Style=(Style)FindResource("OpsButton"),ToolTip=name};
        AutomationProperties.SetName(button,name);button.Click+=(_,_)=>action();panel.Children.Add(button);
    }

    private void Draw()
    {
        mapLayer.Children.Clear();
        var globeBounds=new Rect(MapLeft,MapTop,MapWidth,MapHeight);
        var ocean=new RadialGradientBrush{GradientOrigin=new Point(.35,.28),Center=new Point(.43,.43),RadiusX=.72,RadiusY=.72,GradientStops=new GradientStopCollection{new(Color.FromRgb(25,62,84),0),new(Color.FromRgb(8,28,42),.62),new(Color.FromRgb(2,10,17),1)}};
        Add(new Ellipse{Width=MapWidth,Height=MapHeight,Fill=ocean,Stroke=Brush("#6D91A5"),StrokeThickness=1.5,Effect=new DropShadowEffect{Color=Color.FromRgb(24,124,174),BlurRadius=22,ShadowDepth=0,Opacity=.25}},MapLeft,MapTop);
        var clipped=new Canvas{Width=Width,Height=Height,Clip=new EllipseGeometry(globeBounds)};mapLayer.Children.Add(clipped);
        void Layer(UIElement element)=>clipped.Children.Add(element);

        foreach(var latitude in new[]{-60d,-30d,0d,30d,60d})
        {
            var y=Y(latitude);Layer(new Path{Data=Geometry.Parse(FormattableString.Invariant($"M {MapLeft},{y} Q {CanvasWidth/2},{y+(latitude>0?25:-25)} {MapLeft+MapWidth},{y}")),Stroke=Brush("#285267"),StrokeThickness=.8,Opacity=.58});
        }
        for(var step=-150;step<=150;step+=30)
        {
            var x=X(centerLongitude+step);var bend=(x-CanvasWidth/2)*.18;
            Layer(new Path{Data=Geometry.Parse(FormattableString.Invariant($"M {x},{MapTop} C {x-bend},{MapTop+140} {x-bend},{MapTop+330} {x},{MapTop+MapHeight}")),Stroke=Brush("#285267"),StrokeThickness=.7,Opacity=.5});
        }

        var land=Brush("#203E51");var coast=Brush("#6B8B9E");
        var continents=new[]{
            new[]{(-168d,70d),(-140d,62d),(-125d,50d),(-105d,50d),(-82d,26d),(-65d,45d),(-52d,58d),(-75d,72d),(-110d,72d)},
            new[]{(-81d,12d),(-68d,5d),(-54d,-15d),(-60d,-38d),(-72d,-55d),(-78d,-25d)},
            new[]{(-12d,36d),(5d,58d),(35d,70d),(70d,72d),(105d,60d),(145d,52d),(172d,65d),(160d,38d),(120d,20d),(105d,5d),(78d,8d),(55d,25d),(32d,32d),(15d,42d)},
            new[]{(-17d,35d),(12d,37d),(35d,22d),(50d,10d),(40d,-20d),(20d,-35d),(2d,-30d),(-12d,5d)},
            new[]{(112d,-11d),(154d,-10d),(153d,-39d),(132d,-44d),(113d,-28d)},
            new[]{(-52d,83d),(-20d,76d),(-30d,60d),(-55d,60d)}
        };
        foreach(var continent in continents)for(var copy=-1;copy<=1;copy++)
        {
            var polygon=new Polygon{Fill=land,Stroke=coast,StrokeThickness=1.1,Opacity=.96};
            foreach(var (lon,lat) in continent)polygon.Points.Add(new Point(XUnwrapped(lon+copy*360),Y(lat)));
            Layer(polygon);
        }

        if(route.Count>=2)
        {
            var unwrapped=Unwrap(route.Select(p=>p.Longitude).ToArray());var points=new PointCollection();
            for(var index=0;index<route.Count;index++)points.Add(new Point(XUnwrapped(unwrapped[index]),Y(route[index].Latitude)));
            Layer(new Polyline{Points=points,Stroke=Brush("#132A34"),StrokeThickness=8,Opacity=.8,StrokeLineJoin=PenLineJoin.Round});
            Layer(new Polyline{Points=points,Stroke=Brush("#FFDA00"),StrokeThickness=2.6,StrokeLineJoin=PenLineJoin.Round,Effect=new DropShadowEffect{Color=Color.FromRgb(255,218,0),BlurRadius=10,ShadowDepth=0,Opacity=.5}});
            for(var index=0;index<points.Count;index++)
            {
                var endpoint=index==0||index==points.Count-1;var dot=new Ellipse{Width=endpoint?13:5,Height=endpoint?13:5,Fill=Brush(index==0?"#6ED77B":index==points.Count-1?"#FFDA00":"#BBD2DF"),Stroke=Brush("#061018"),StrokeThickness=endpoint?2:1,ToolTip=$"{route[index].Ident} • {route[index].Kind}"};
                AddToLayer(clipped,dot,points[index].X-dot.Width/2,points[index].Y-dot.Height/2);
                if(endpoint){var label=new TextBlock{Text=route[index].Ident,Foreground=Brush(index==0?"#8FE49A":"#FFE34A"),Background=Brush("#DD061018"),FontWeight=FontWeights.SemiBold,FontSize=13,Padding=new Thickness(6,3,6,3)};AddToLayer(clipped,label,points[index].X+8,points[index].Y-25);}
            }
            var start=points[0];var plane=new Path{Data=Geometry.Parse("M 0,5 L 7,4 12,0 15,0 12,4 22,5 12,7 15,12 12,12 7,7 0,6 Z"),Fill=Brush("#FFDA00"),Width=25,Height=16,Stretch=Stretch.Fill,ToolTip="Planned departure position • waiting for live aircraft telemetry"};
            AddToLayer(clipped,plane,start.X-12,start.Y-8);
            caption.Text=$"PLANNED SIMBRIEF ROUTE  •  {route[0].Ident} TO {route[^1].Ident}  •  {route.Count} POINTS  •  DRAG TO EXPLORE";
        }
        else caption.Text="ROUTE UNAVAILABLE  •  REIMPORT THE LATEST SIMBRIEF PLAN TO LOAD WAYPOINTS";
        Add(new Ellipse{Width=MapWidth,Height=MapHeight,Stroke=Brush("#9EC0D0"),StrokeThickness=.8,Opacity=.5,IsHitTestVisible=false},MapLeft,MapTop);
    }

    private static void AddToLayer(Canvas layer,UIElement element,double left,double top){Canvas.SetLeft(element,left);Canvas.SetTop(element,top);layer.Children.Add(element);}
    private void Add(UIElement element,double left,double top){Canvas.SetLeft(element,left);Canvas.SetTop(element,top);mapLayer.Children.Add(element);}
    private double X(double longitude)=>XUnwrapped(Near(longitude,centerLongitude));
    private double XUnwrapped(double longitude)=>MapLeft+(longitude-(centerLongitude-180))/360*MapWidth;
    private static double Y(double latitude)=>MapTop+(90-latitude)/180*MapHeight;
    private static double Near(double longitude,double center){while(longitude-center>180)longitude-=360;while(longitude-center< -180)longitude+=360;return longitude;}
    private static double[] Unwrap(double[] values)
    {
        if(values.Length==0)return values;var result=new double[values.Length];result[0]=values[0];
        for(var i=1;i<values.Length;i++)result[i]=Near(values[i],result[i-1]);return result;
    }
    private static double RouteCenter(IReadOnlyList<FlightRoutePoint> points)
    {
        if(points.Count==0)return 0;var values=Unwrap(points.Select(p=>p.Longitude).ToArray());return (values.Min()+values.Max())/2;
    }
    private static double CalculateFittedZoom(IReadOnlyList<FlightRoutePoint> points)
    {
        if(points.Count<2)return 1;
        var longitudes=Unwrap(points.Select(point=>point.Longitude).ToArray());
        var projectedWidth=(longitudes.Max()-longitudes.Min())/360*MapWidth;
        var projectedHeight=(points.Max(point=>point.Latitude)-points.Min(point=>point.Latitude))/180*MapHeight;
        return Math.Clamp(Math.Min(600/(projectedWidth+120),300/(projectedHeight+100)),1,2.2);
    }
    private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
}
