namespace LondonVIP.Shared.Drivers;

public enum ComplianceState { Valid, ExpiringSoon, Expired, NotRecorded }

public static class ComplianceCalculator
{
    public static ComplianceState Calculate(IEnumerable<DateOnly?> dates, DateOnly? today = null)
    {
        var recorded = dates.Where(date => date.HasValue).Select(date => date!.Value).ToArray();
        if (recorded.Length == 0) return ComplianceState.NotRecorded;
        var current = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (recorded.Any(date => date < current)) return ComplianceState.Expired;
        return recorded.Any(date => date <= current.AddDays(30)) ? ComplianceState.ExpiringSoon : ComplianceState.Valid;
    }
}
