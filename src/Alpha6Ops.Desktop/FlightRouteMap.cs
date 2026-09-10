using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

public sealed class FlightRouteMap : UserControl
{
    private const double CanvasWidth=900,CanvasHeight=520,CenterX=450,CenterY=250,BaseRadius=220;
    private static readonly Lazy<IReadOnlyList<IReadOnlyList<GeoPoint>>> Land=new(LoadLand);
    private static readonly Lazy<LandTexture> LandMask=new(LoadLandMask);
    private readonly Canvas globe=new(){Width=CanvasWidth,Height=CanvasHeight,ClipToBounds=true,Cursor=Cursors.Hand};
    private readonly TextBlock caption=new(){Foreground=Brush("#89A1B1"),FontSize=10,Margin=new Thickness(15,4,0,9)};
    private IReadOnlyList<FlightRoutePoint> route=[];
    private Point? drag;
    private double centerLatitude;
    private double centerLongitude;
    private double zoom=1;
    private GeoPoint? livePosition;
    private double liveHeading=double.NaN;
    private double completedFraction;
    private readonly List<GeoPoint> actualTrack=[];

    internal int RoutePointCount=>route.Count;
    internal bool CrossesDateLine {get;private set;}
    internal double ZoomLevel=>zoom;
    internal double FittedZoom {get;private set;}=1;
    internal int VisibleWaypointLabelCount {get;private set;}
    internal bool HasLiveAircraft=>livePosition is not null;
    internal double CompletedFraction=>completedFraction;
    internal int TrackPointCount=>actualTrack.Count;

    public FlightRouteMap()
    {
        var root=new Grid{Background=Brush("#02080E"),ClipToBounds=true};
        root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        root.Children.Add(new Viewbox{Child=globe,Stretch=Stretch.Uniform});
        var controls=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,11,13,0)};
        AddControl(controls,"+","Zoom route globe in",()=>SetZoom(zoom*1.3));
        AddControl(controls,"−","Zoom route globe out",()=>SetZoom(zoom/1.2));
        AddControl(controls,"⌖","Reset and frame route globe",FrameRoute);
        root.Children.Add(controls);Grid.SetRow(caption,1);root.Children.Add(caption);Content=root;
        globe.MouseWheel+=(_,e)=>{SetZoom(zoom*(e.Delta>0?1.12:1/1.12));e.Handled=true;};
        globe.MouseLeftButtonDown+=(_,e)=>{drag=e.GetPosition(globe);globe.CaptureMouse();};
        globe.MouseMove+=(_,e)=>
        {
            if(drag is not{} previous||e.LeftButton!=MouseButtonState.Pressed)return;
            var current=e.GetPosition(globe);centerLongitude=NormalizeLongitude(centerLongitude-(current.X-previous.X)*.35/zoom);
            centerLatitude=Math.Clamp(centerLatitude+(current.Y-previous.Y)*.25/zoom,-75,75);drag=current;Draw();
        };
        globe.MouseLeftButtonUp+=(_,_)=>{drag=null;globe.ReleaseMouseCapture();};
        Draw();
    }

    internal void SetRoute(IReadOnlyList<FlightRoutePoint>? points)
    {
        var next=points?.Where(point=>point.Latitude is>=-90 and<=90&&point.Longitude is>=-180 and<=180).ToArray()??[];
        if(route.SequenceEqual(next))return;
        route=next;livePosition=null;liveHeading=double.NaN;completedFraction=0;actualTrack.Clear();
        CrossesDateLine=route.Zip(route.Skip(1),(a,b)=>Math.Abs(a.Longitude-b.Longitude)>180).Any(crosses=>crosses);
        FrameRoute();
    }

    internal void SetTelemetry(Telemetry? telemetry,double? routeProgress)
    {
        GeoPoint? position=null;
        if(telemetry?.HasPosition==true)position=new GeoPoint(telemetry.LatitudeDegrees,telemetry.LongitudeDegrees);
        else if(routeProgress is>=0 and<=1&&route.Count>=2)position=PositionAtProgress(routeProgress.Value);
        if(position is null)return;
        livePosition=position;liveHeading=telemetry is not null&&double.IsFinite(telemetry.HeadingDegrees)?telemetry.HeadingDegrees:RouteHeading(Math.Clamp(routeProgress??completedFraction,0,1));
        completedFraction=Math.Clamp(routeProgress??NearestRouteProgress(position.Value),0,1);
        if(actualTrack.Count==0||AngularDistance(actualTrack[^1],position.Value)>Radians(.015))actualTrack.Add(position.Value);
        Draw();
    }

    internal void Zoom(double factor)=>SetZoom(zoom*factor);
    internal void ResetView()=>FrameRoute();
    internal void SetView(double latitude,double longitude,double scale){centerLatitude=Math.Clamp(latitude,-75,75);centerLongitude=NormalizeLongitude(longitude);zoom=Math.Clamp(scale,.8,12);Draw();}

    private void FrameRoute()
    {
        if(route.Count>0)
        {
            var center=MeanPoint(route.Select(point=>new GeoPoint(point.Latitude,point.Longitude)));
            centerLatitude=center.Latitude;centerLongitude=center.Longitude;
            var maximum=route.Max(point=>AngularDistance(center,new GeoPoint(point.Latitude,point.Longitude)));
            FittedZoom=Math.Clamp(.82/Math.Max(.24,Math.Sin(Math.Min(maximum+Radians(10),Math.PI/2))),1,1.65);
        }
        else {centerLatitude=18;centerLongitude=0;FittedZoom=1;}
        zoom=FittedZoom;Draw();
    }

    private void SetZoom(double value){zoom=Math.Clamp(value,.8,12);Draw();}

    private void Draw()
    {
        globe.Children.Clear();VisibleWaypointLabelCount=0;var radius=BaseRadius*zoom;
        Add(new Ellipse{Width=radius*2+14,Height=radius*2+14,Stroke=Brush("#4FA9D2"),StrokeThickness=3,Opacity=.28,Effect=new BlurEffect{Radius=8}},CenterX-radius-7,CenterY-radius-7);
        var ocean=new RadialGradientBrush{GradientOrigin=new Point(.29,.25),Center=new Point(.38,.35),RadiusX=.76,RadiusY=.76,GradientStops=new GradientStopCollection{new(Color.FromRgb(33,83,111),0),new(Color.FromRgb(9,36,54),.53),new(Color.FromRgb(2,12,21),1)}};
        Add(new Ellipse{Width=radius*2,Height=radius*2,Fill=ocean,Stroke=Brush("#8EB5CA"),StrokeThickness=1.4,Effect=new DropShadowEffect{Color=Color.FromRgb(27,123,170),BlurRadius=22,ShadowDepth=0,Opacity=.25}},CenterX-radius,CenterY-radius);
        DrawLandSurface(radius);DrawGraticule(radius);
        foreach(var ring in Land.Value)DrawGeoLine(ring,radius,Brush("#7798AA"),1,.96);
        DrawTerminator(radius);DrawRoute(radius);
        Add(new Ellipse{Width=radius*2,Height=radius*2,Stroke=Brush("#B1CDDA"),StrokeThickness=1,Opacity=.58,IsHitTestVisible=false},CenterX-radius,CenterY-radius);
        caption.Text=route.Count>=2?$"SIMBRIEF ROUTE  •  {route[0].Ident} TO {route[^1].Ident}  •  {route.Count} POINTS  •  DRAG GLOBE TO ROTATE":"ROUTE UNAVAILABLE  •  REIMPORT THE LATEST SIMBRIEF PLAN TO LOAD WAYPOINTS";
    }

    private void DrawGraticule(double radius)
    {
        for(var latitude=-60;latitude<=60;latitude+=30)DrawGeoLine(Enumerable.Range(0,145).Select(index=>new GeoPoint(latitude,-180+index*2.5)),radius,Brush("#397087"),.75,.48);
        for(var longitude=-180;longitude<180;longitude+=30)DrawGeoLine(Enumerable.Range(0,73).Select(index=>new GeoPoint(-90+index*2.5,longitude)),radius,Brush("#397087"),.7,.42);
    }

    private void DrawLandSurface(double radius)
    {
        const int width=(int)CanvasWidth,height=(int)CanvasHeight,stride=width*4;var output=new byte[stride*height];var texture=LandMask.Value;
        var minimumX=Math.Max(0,(int)(CenterX-radius));var maximumX=Math.Min(width-1,(int)(CenterX+radius));
        var minimumY=Math.Max(0,(int)(CenterY-radius));var maximumY=Math.Min(height-1,(int)(CenterY+radius));
        var centerLatitudeRadians=Radians(centerLatitude);var centerLongitudeRadians=Radians(centerLongitude);
        for(var y=minimumY;y<=maximumY;y++)for(var x=minimumX;x<=maximumX;x++)
        {
            var normalizedX=(x-CenterX)/radius;var normalizedY=(CenterY-y)/radius;var distance=Math.Sqrt(normalizedX*normalizedX+normalizedY*normalizedY);if(distance>1)continue;
            var angle=Math.Asin(distance);var sine=Math.Sin(angle);var cosine=Math.Cos(angle);double latitude,longitude;
            if(distance<.000001){latitude=centerLatitudeRadians;longitude=centerLongitudeRadians;}
            else
            {
                latitude=Math.Asin(cosine*Math.Sin(centerLatitudeRadians)+normalizedY*sine*Math.Cos(centerLatitudeRadians)/distance);
                longitude=centerLongitudeRadians+Math.Atan2(normalizedX*sine,distance*Math.Cos(centerLatitudeRadians)*cosine-normalizedY*Math.Sin(centerLatitudeRadians)*sine);
            }
            var sourceX=(int)Math.Floor((longitude/Math.PI+1)*.5*texture.Width)%texture.Width;if(sourceX<0)sourceX+=texture.Width;
            var sourceY=Math.Clamp((int)Math.Floor((.5-latitude/Math.PI)*texture.Height),0,texture.Height-1);
            if(texture.Pixels[(sourceY*texture.Width+sourceX)*4+3]<128)continue;
            var light=Math.Clamp(.84+(-normalizedX-normalizedY)*.08,.68,1);var offset=y*stride+x*4;
            output[offset]=(byte)(94*light);output[offset+1]=(byte)(73*light);output[offset+2]=(byte)(36*light);output[offset+3]=242;
        }
        var bitmap=new WriteableBitmap(width,height,96,96,PixelFormats.Bgra32,null);bitmap.WritePixels(new Int32Rect(0,0,width,height),output,stride,0);bitmap.Freeze();Add(new Image{Source=bitmap,Width=CanvasWidth,Height=CanvasHeight,IsHitTestVisible=false});
    }

    private void DrawTerminator(double radius)
    {
        var shade=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection{new(Color.FromArgb(0,0,0,0),.25),new(Color.FromArgb(25,0,0,0),.6),new(Color.FromArgb(125,0,0,0),1)}};
        Add(new Ellipse{Width=radius*2,Height=radius*2,Fill=shade,IsHitTestVisible=false},CenterX-radius,CenterY-radius);
        Add(new Ellipse{Width=radius*1.5,Height=radius*.55,Fill=new RadialGradientBrush(Color.FromArgb(26,80,184,225),Colors.Transparent),IsHitTestVisible=false},CenterX-radius*.95,CenterY-radius*.8);
    }

    private void DrawRoute(double radius)
    {
        if(route.Count<2)return;var routePath=BuildRoutePath();var split=Math.Clamp((int)Math.Round(completedFraction*(routePath.Count-1)),0,routePath.Count-1);
        DrawGeoLine(routePath,radius,Brush("#071118"),8,.88);
        if(split>0)DrawGeoLine(routePath.Take(split+1),radius,Brush("#42C8F5"),3.2,1,true);
        DrawGeoLine(routePath.Skip(split),radius,Brush("#FFDA00"),2.8,1,true);
        if(actualTrack.Count>1)DrawGeoLine(actualTrack,radius,Brush("#8BE8FF"),1.6,.95,true);
        Point? previousWaypointLabel=null;
        for(var index=0;index<route.Count;index++)
        {
            var geo=new GeoPoint(route[index].Latitude,route[index].Longitude);if(!Project(geo,radius,out var point))continue;
            var endpoint=index==0||index==route.Count-1;var dot=new Ellipse{Width=endpoint?14:6,Height=endpoint?14:6,Fill=Brush(index==0?"#72DB83":index==route.Count-1?"#FFDA00":"#D5E4EC"),Stroke=Brush("#031019"),StrokeThickness=2,ToolTip=$"{route[index].Ident} • {route[index].Kind}"};
            Add(dot,point.X-dot.Width/2,point.Y-dot.Height/2);
            if(endpoint){var label=new TextBlock{Text=route[index].Ident,Foreground=Brush(index==0?"#8BE29A":"#FFE34A"),Background=Brush("#E6040C13"),FontWeight=FontWeights.SemiBold,FontSize=13,Padding=new Thickness(6,3,6,3)};Add(label,point.X+(index==0?9:-55),point.Y-29);}
            else if(zoom>=2.15&&(previousWaypointLabel is null||(point-previousWaypointLabel.Value).Length>=42))
            {
                var label=new TextBlock{Text=route[index].Ident,Foreground=Brush("#F1F4F7"),Background=Brush("#F0040C13"),FontSize=12,FontWeight=FontWeights.SemiBold,Padding=new Thickness(5,2,5,2),ToolTip=route[index].Kind};
                Add(label,point.X+6,point.Y+(VisibleWaypointLabelCount%2==0?-22:7));previousWaypointLabel=point;VisibleWaypointLabelCount++;
            }
        }
        var aircraft=livePosition??new GeoPoint(route[0].Latitude,route[0].Longitude);
        if(Project(aircraft,radius,out var start))
        {
            var angle=180d;var look=DestinationPoint(aircraft,double.IsFinite(liveHeading)?liveHeading:RouteHeading(completedFraction),2/Math.Max(1d,zoom));if(Project(look,radius,out var next))angle=Math.Atan2(next.Y-start.Y,next.X-start.X)*180/Math.PI+180;
            var plane=new Path{Data=Geometry.Parse("M 0,7 L 10,7 L 17,1 L 21,1 L 18,7 L 34,8 L 18,10 L 21,16 L 17,16 L 10,10 L 0,10 Z"),Fill=Brush("#FFDA00"),Width=34,Height=17,Stretch=Stretch.Fill,RenderTransform=new RotateTransform(angle,17,8.5),Effect=new DropShadowEffect{Color=Colors.Gold,BlurRadius=10,ShadowDepth=0,Opacity=.72},ToolTip=livePosition is null?"Planned departure position • waiting for live aircraft telemetry":$"Live aircraft • {completedFraction:P0} complete"};Add(plane,start.X-17,start.Y-8.5);
        }
    }

    private List<GeoPoint> BuildRoutePath()
    {
        var path=new List<GeoPoint>();
        for(var index=0;index<route.Count-1;index++)
        {
            var start=new GeoPoint(route[index].Latitude,route[index].Longitude);var end=new GeoPoint(route[index+1].Latitude,route[index+1].Longitude);var samples=Math.Clamp((int)Math.Ceiling(AngularDistance(start,end)*180/Math.PI*2),4,240);
            path.AddRange(GreatCircle(start,end,samples).Skip(index==0?0:1));
        }
        return path;
    }

    private GeoPoint PositionAtProgress(double progress)
    {
        var path=BuildRoutePath();var position=Math.Clamp(progress,0,1)*(path.Count-1);var index=Math.Min((int)position,path.Count-2);return GreatCircle(path[index],path[index+1],100)[Math.Clamp((int)Math.Round((position-index)*100),0,100)];
    }

    private double NearestRouteProgress(GeoPoint position)
    {
        var path=BuildRoutePath();var nearest=0;var distance=double.MaxValue;for(var index=0;index<path.Count;index++){var candidate=AngularDistance(path[index],position);if(candidate<distance){distance=candidate;nearest=index;}}return path.Count<=1?0:(double)nearest/(path.Count-1);
    }

    private double RouteHeading(double progress)
    {
        var path=BuildRoutePath();var index=Math.Clamp((int)(Math.Clamp(progress,0,1)*(path.Count-1)),0,path.Count-2);return Bearing(path[index],path[index+1]);
    }

    private void DrawGeoLine(IEnumerable<GeoPoint> points,double radius,Brush stroke,double thickness,double opacity,bool glow=false)
    {
        var run=new List<Point>();
        void Flush(){if(run.Count>1)Add(new Polyline{Points=new PointCollection(run),Stroke=stroke,StrokeThickness=thickness,Opacity=opacity,StrokeLineJoin=PenLineJoin.Round,Effect=glow?new DropShadowEffect{Color=Colors.Gold,BlurRadius=10,ShadowDepth=0,Opacity=.5}:null});run.Clear();}
        foreach(var geo in points){if(Project(geo,radius,out var point))run.Add(point);else Flush();}Flush();
    }

    private bool Project(GeoPoint geo,double radius,out Point point)
    {
        var latitude=Radians(geo.Latitude);var longitude=Radians(geo.Longitude-centerLongitude);var center=Radians(centerLatitude);
        var visibility=Math.Sin(center)*Math.Sin(latitude)+Math.Cos(center)*Math.Cos(latitude)*Math.Cos(longitude);
        point=new Point(CenterX+radius*Math.Cos(latitude)*Math.Sin(longitude),CenterY-radius*(Math.Cos(center)*Math.Sin(latitude)-Math.Sin(center)*Math.Cos(latitude)*Math.Cos(longitude)));
        return visibility>=-.012;
    }

    private void AddControl(Panel panel,string label,string name,Action action)
    {
        var button=new Button{Content=label,Width=32,Height=32,Padding=new Thickness(0),Margin=new Thickness(0,0,0,6),Style=(Style)FindResource("OpsButton"),ToolTip=name};AutomationProperties.SetName(button,name);button.Click+=(_,_)=>action();panel.Children.Add(button);
    }

    private void Add(UIElement element,double left=0,double top=0){Canvas.SetLeft(element,left);Canvas.SetTop(element,top);globe.Children.Add(element);}

    private static IReadOnlyList<GeoPoint> GreatCircle(GeoPoint start,GeoPoint end,int segments)
    {
        var a=Vector(start);var b=Vector(end);var angle=Math.Acos(Math.Clamp(a.X*b.X+a.Y*b.Y+a.Z*b.Z,-1,1));var points=new List<GeoPoint>();
        for(var index=0;index<=segments;index++)
        {
            var amount=(double)index/segments;Vector3 value;if(angle<.0001)value=a;else {var denominator=Math.Sin(angle);value=(a*(Math.Sin((1-amount)*angle)/denominator))+(b*(Math.Sin(amount*angle)/denominator));}
            points.Add(new GeoPoint(Math.Asin(value.Z)*180/Math.PI,Math.Atan2(value.Y,value.X)*180/Math.PI));
        }
        return points;
    }

    private static GeoPoint MeanPoint(IEnumerable<GeoPoint> points)
    {
        var source=points.ToArray();var vectors=source.Select(Vector).ToArray();var sum=new Vector3(vectors.Sum(value=>value.X),vectors.Sum(value=>value.Y),vectors.Sum(value=>value.Z));var length=Math.Sqrt(sum.X*sum.X+sum.Y*sum.Y+sum.Z*sum.Z);if(length<.0001)return source[0];sum=sum*(1/length);
        return new GeoPoint(Math.Asin(sum.Z)*180/Math.PI,Math.Atan2(sum.Y,sum.X)*180/Math.PI);
    }

    private static double AngularDistance(GeoPoint a,GeoPoint b){var first=Vector(a);var second=Vector(b);return Math.Acos(Math.Clamp(first.X*second.X+first.Y*second.Y+first.Z*second.Z,-1,1));}
    private static double Bearing(GeoPoint from,GeoPoint to)
    {
        var first=Radians(from.Latitude);var second=Radians(to.Latitude);var delta=Radians(to.Longitude-from.Longitude);return (Math.Atan2(Math.Sin(delta)*Math.Cos(second),Math.Cos(first)*Math.Sin(second)-Math.Sin(first)*Math.Cos(second)*Math.Cos(delta))*180/Math.PI+360)%360;
    }
    private static GeoPoint DestinationPoint(GeoPoint from,double bearing,double angularDegrees)
    {
        var latitude=Radians(from.Latitude);var longitude=Radians(from.Longitude);var course=Radians(bearing);var distance=Radians(angularDegrees);
        var nextLatitude=Math.Asin(Math.Sin(latitude)*Math.Cos(distance)+Math.Cos(latitude)*Math.Sin(distance)*Math.Cos(course));var nextLongitude=longitude+Math.Atan2(Math.Sin(course)*Math.Sin(distance)*Math.Cos(latitude),Math.Cos(distance)-Math.Sin(latitude)*Math.Sin(nextLatitude));
        return new GeoPoint(nextLatitude*180/Math.PI,NormalizeLongitude(nextLongitude*180/Math.PI));
    }
    private static Vector3 Vector(GeoPoint point){var latitude=Radians(point.Latitude);var longitude=Radians(point.Longitude);return new Vector3(Math.Cos(latitude)*Math.Cos(longitude),Math.Cos(latitude)*Math.Sin(longitude),Math.Sin(latitude));}
    private static double Radians(double degrees)=>degrees*Math.PI/180;
    private static double NormalizeLongitude(double value){while(value>180)value-=360;while(value< -180)value+=360;return value;}

    private static IReadOnlyList<IReadOnlyList<GeoPoint>> LoadLand()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ne_110m_land.geojson")??throw new System.IO.InvalidDataException("Bundled world map is missing.");using var document=JsonDocument.Parse(stream);var rings=new List<IReadOnlyList<GeoPoint>>();
        foreach(var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var geometry=feature.GetProperty("geometry");var coordinates=geometry.GetProperty("coordinates");var type=geometry.GetProperty("type").GetString();
            if(type=="Polygon")ReadPolygon(coordinates,rings);else if(type=="MultiPolygon")foreach(var polygon in coordinates.EnumerateArray())ReadPolygon(polygon,rings);
        }
        return rings;
    }

    private static LandTexture LoadLandMask()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ne_110m_land-mask.png")??throw new System.IO.InvalidDataException("Bundled world land mask is missing.");
        var decoder=new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var source=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);var stride=source.PixelWidth*4;var pixels=new byte[stride*source.PixelHeight];source.CopyPixels(pixels,stride,0);return new LandTexture(source.PixelWidth,source.PixelHeight,pixels);
    }

    private static void ReadPolygon(JsonElement polygon,List<IReadOnlyList<GeoPoint>> rings)
    {
        foreach(var ring in polygon.EnumerateArray())rings.Add(ring.EnumerateArray().Select(coordinate=>new GeoPoint(coordinate[1].GetDouble(),coordinate[0].GetDouble())).ToArray());
    }

    private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    private readonly record struct GeoPoint(double Latitude,double Longitude);
    private sealed record LandTexture(int Width,int Height,byte[] Pixels);
    private readonly record struct Vector3(double X,double Y,double Z)
    {
        public static Vector3 operator *(Vector3 value,double factor)=>new(value.X*factor,value.Y*factor,value.Z*factor);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    }
}
