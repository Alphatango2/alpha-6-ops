namespace Alpha6Ops.Core;

public enum EngineCondition { Unknown, Off, Starting, Running, Stopping }
public sealed record EngineReading(int Index,double? Combustion,double? FuelFlowPph,double? N1Percent,double? N2Percent,double? Starter,double? OilPressurePsf=null);
public sealed record EngineAssessment(int Index,EngineCondition Condition,string Source);

// Conservative generic fallback. All-zero, never-observed signals remain unknown.
// These are sensor-state thresholds, not aircraft operating limits.
public sealed class EngineEvidenceTracker
{
    private readonly bool[] seenRunning=new bool[4],seenCombustion=new bool[4];
    private readonly EngineCondition[] stable=new EngineCondition[4],candidate=new EngineCondition[4];
    private readonly DateTimeOffset?[] since=new DateTimeOffset?[4];
    private readonly double?[] previousN1=new double?[4],previousN2=new double?[4],previousOil=new double?[4];
    public IReadOnlyList<EngineAssessment> Observe(IReadOnlyList<EngineReading> readings,DateTimeOffset at)
    {
        var result=new List<EngineAssessment>();
        foreach(var r in readings.Where(r=>r.Index is>=1 and<=4))
        {
            var i=r.Index-1;
            var fueled=r.FuelFlowPph is >10;
            var n1Running=r.N1Percent is >=15;
            var n2Running=r.N2Percent is >=40;
            var spinning=n1Running||n2Running;
            var dualSpool=n1Running&&n2Running;
            var spooling=r.N1Percent is >=3&&r.N2Percent is >=5;
            var oilPressurized=r.OilPressurePsf is >144;
            var combustion=r.Combustion is >0;
            var spoolDecay=(r.N1Percent is{} n1&&previousN1[i] is{} oldN1&&n1<oldN1-.25?1:0)
                +(r.N2Percent is{} n2&&previousN2[i] is{} oldN2&&n2<oldN2-.25?1:0)
                +(r.OilPressurePsf is{} oil&&previousOil[i] is{} oldOil&&oil<oldOil-25?1:0)>=2;
            var state=combustion?EngineCondition.Running:r.Starter is >0?EngineCondition.Starting:
                seenRunning[i]&&seenCombustion[i]&&r.Combustion is 0&&spoolDecay?EngineCondition.Stopping:
                (fueled&&spinning)||dualSpool||(oilPressurized&&spinning)?EngineCondition.Running:
                !seenRunning[i]&&spooling?EngineCondition.Starting:
                seenRunning[i]&&r.FuelFlowPph is >=0 and <=1&&r.N1Percent is >=0 and <3&&r.N2Percent is >=0 and <5?EngineCondition.Off:
                seenRunning[i]&&r.FuelFlowPph is >=0 and <=1&&(r.N1Percent is >=3||r.N2Percent is >=5)?EngineCondition.Stopping:EngineCondition.Unknown;
            if(candidate[i]!=state||since[i] is null||at<since[i]){candidate[i]=state;since[i]=at;}
            if(at-since[i]>=TimeSpan.FromSeconds(2)){stable[i]=state;if(state==EngineCondition.Running)seenRunning[i]=true;}
            // Missing or contradictory samples must not retain a stale running/off claim.
            if(state==EngineCondition.Unknown)stable[i]=EngineCondition.Unknown;
            if(combustion)seenCombustion[i]=true;
            var source=combustion?"combustion":state==EngineCondition.Stopping&&spoolDecay?(seenCombustion[i]&&r.Combustion is 0?"combustion loss / spool decay":"N1 / N2 spool decay"):dualSpool?"N1 / N2 spool":oilPressurized&&spinning?"oil pressure / spool":fueled&&spinning?"fuel flow / spool":r.Starter is >0?"starter":"N1 / N2 spool";
            result.Add(new(r.Index,stable[i],source));
            previousN1[i]=r.N1Percent;previousN2[i]=r.N2Percent;previousOil[i]=r.OilPressurePsf;
        }
        return result;
    }
}
