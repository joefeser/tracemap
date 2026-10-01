namespace TraceMap.Tests;

// An unrun Windows publish is a visible skip, never a passing test on macOS.
public sealed class WindowsDeepCorpusTheoryAttribute : TheoryAttribute
{
    public WindowsDeepCorpusTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Authentic ASP.NET Framework publishing requires Windows.";
    }
}
