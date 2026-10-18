namespace TraceMap.Core;

/// <summary>
/// Shared wall-clock bound for regex calls made through the static <c>Regex</c> API.
/// It is a safety net against pathological input, not a performance expectation: tight
/// budgets (100–250 ms) failed full-suite runs under parallel load while every case
/// passed in isolation (#857), because the bound is measured in wall-clock time that a
/// descheduled thread cannot control.
/// </summary>
internal static class RegexBudget
{
    public static readonly TimeSpan LoadTolerant = TimeSpan.FromSeconds(2);
}
