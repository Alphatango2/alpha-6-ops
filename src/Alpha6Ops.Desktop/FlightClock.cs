using System;

namespace Alpha6Ops.Desktop;

internal static class FlightClock
{
    internal static readonly TimeSpan ScheduleTolerance=TimeSpan.FromHours(8);
    internal static bool ScheduleIsPlausible(ActiveFlightPlan plan,DateTimeOffset simulatorUtc)
        =>simulatorUtc>=plan.PlannedDepartureUtc-ScheduleTolerance&&simulatorUtc<=plan.PlannedArrivalUtc+ScheduleTolerance;
    internal static bool DepartureScheduleIsPlausible(ActiveFlightPlan plan,DateTimeOffset actualOut)
        =>Math.Abs((actualOut-plan.PlannedDepartureUtc).TotalHours)<=ScheduleTolerance.TotalHours;
    internal static string ReviewMessage(ActiveFlightPlan plan,DateTimeOffset simulatorUtc)
    {
        var difference=simulatorUtc-plan.PlannedDepartureUtc;
        return $"SCHEDULE / SIM CLOCK REVIEW • {Math.Abs(difference.TotalHours):0.#} H {(difference<TimeSpan.Zero?"BEFORE":"AFTER")} PLANNED OUT";
    }

    internal static string FormatScheduled(DateTimeOffset utc,int? offsetMinutes)
    {
        var utcText=utc.UtcDateTime.ToString("dd MMM • HH:mm'Z'");
        if(offsetMinutes is null)return utcText;
        var local=utc.ToOffset(TimeSpan.FromMinutes(offsetMinutes.Value));
        return $"{utcText}  /  {local:dd MMM • HH:mm} LOCAL";
    }
}
