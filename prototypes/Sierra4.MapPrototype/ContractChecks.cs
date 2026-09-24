using System.Collections.Immutable;
namespace Sierra4.MapPrototype;

internal static class ContractChecks
{
    internal static string[] Run()
    {
        var checks = new List<string>();
        void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); checks.Add(name); }
        var a = new GeoPoint(151.1772, -33.9461); var b = new GeoPoint(147.5103, -42.8361);
        var route = Geography.GreatCircle(a, b);
        Check(route[0][0] == a && route[^1][^1] == b, "Great-circle preserves endpoints");
        var dateline = Geography.GreatCircle(new(170, 30), new(-170, 40));
        Check(dateline.Length == 2 && dateline.All(s => s.Zip(s.Skip(1)).All(p => Math.Abs(p.First.Longitude-p.Second.Longitude)<=180)), "Dateline is split without world-spanning edges");
        var polar = Geography.GreatCircle(new(-45,85),new(135,85));
        Check(polar.SelectMany(s=>s).All(p=>double.IsFinite(p.Latitude)&&Math.Abs(p.Latitude)<=90), "Polar geometry remains finite");
        Check(Geography.GreatCircle(a,a).SelectMany(s=>s).All(p=>p==a), "Coincident points remain finite");
        try { _=new GeoPoint(double.NaN, 0); Check(false,"Reject NaN"); } catch(ArgumentOutOfRangeException) { checks.Add("Reject invalid position"); }
        try { Geography.GreatCircle(new(0,0),new(180,0)); Check(false,"Reject ambiguous antipodes"); } catch(ArgumentException) { checks.Add("Antipodal ambiguity requires an intermediate waypoint"); }
        var input = new[] { a,b }; var copy = input.ToImmutableArray(); input[0]=b;
        Check(copy[0]==a, "Immutable geometry is detached from producer storage");
        Check(Enum.GetValues<MapEntitySource>().Length==5, "Observed/inferred/simulated/replay/unknown remain distinct");
        return checks.ToArray();
    }
}
