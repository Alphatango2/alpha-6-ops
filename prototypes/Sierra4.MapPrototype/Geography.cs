using System.Collections.Immutable;
namespace Sierra4.MapPrototype;
internal static class Geography
{
    internal static ImmutableArray<ImmutableArray<GeoPoint>> GreatCircle(GeoPoint a,GeoPoint b)
    {
        static (double X,double Y,double Z) Unit(GeoPoint p)
        { var lat=p.Latitude*Math.PI/180; var lon=p.Longitude*Math.PI/180;return(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat)); }
        var u=Unit(a);var v=Unit(b);var angle=Math.Acos(Math.Clamp(u.X*v.X+u.Y*v.Y+u.Z*v.Z,-1,1));
        if(angle<1e-8)return [[a,b]];
        if(Math.PI-angle<1e-6)throw new ArgumentException("Antipodal route needs an intermediate waypoint.");
        var count=Math.Max(16,(int)Math.Ceiling(angle/(Math.PI/180)));
        var segments=ImmutableArray.CreateBuilder<ImmutableArray<GeoPoint>>();var points=ImmutableArray.CreateBuilder<GeoPoint>();points.Add(a);
        for(var i=1;i<=count;i++)
        {
            var t=(double)i/count;var l=Math.Sin((1-t)*angle)/Math.Sin(angle);var r=Math.Sin(t*angle)/Math.Sin(angle);
            var x=l*u.X+r*v.X;var y=l*u.Y+r*v.Y;var z=l*u.Z+r*v.Z;
            var p=i==count?b:new GeoPoint(Math.Atan2(y,x)*180/Math.PI,Math.Atan2(z,Math.Sqrt(x*x+y*y))*180/Math.PI);
            var previous=points[^1];var difference=p.Longitude-previous.Longitude;
            if(Math.Abs(difference)>180)
            {
                var unwrapped=p.Longitude+(difference>0?-360:360);var edge=difference>0?-180d:180d;
                var f=(edge-previous.Longitude)/(unwrapped-previous.Longitude);
                var latitude=previous.Latitude+f*(p.Latitude-previous.Latitude);
                points.Add(new(edge,latitude));segments.Add(points.ToImmutable());points.Clear();points.Add(new(-edge,latitude));
            }
            points.Add(p);
        }
        segments.Add(points.ToImmutable());return segments.ToImmutable();
    }
}
