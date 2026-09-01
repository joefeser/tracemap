namespace TraceMap.Reporting;

// Admission counts serialized UTF-8 text, not CLR heap bytes. Row limits also
// bound dictionary/object overhead; the OS working set is measured separately.
internal sealed class ReportInputBudget(int maxFacts, int maxEdges, long maxTextBytes)
{
    internal const int DefaultMaxFacts = 250_000;
    internal const int DefaultMaxEdges = 250_000;
    internal const int DefaultMaxTextBytes = 128 * 1024 * 1024;
    internal const int MaxRowTextBytes = 1024 * 1024;

    public int MaxFacts { get; } = maxFacts > 0 ? maxFacts : throw new ArgumentOutOfRangeException(nameof(maxFacts));
    public int MaxEdges { get; } = maxEdges > 0 ? maxEdges : throw new ArgumentOutOfRangeException(nameof(maxEdges));
    public long MaxTextBytes { get; } = maxTextBytes > 0 ? maxTextBytes : throw new ArgumentOutOfRangeException(nameof(maxTextBytes));
    public long FactsVisited { get; private set; }
    public int FactsRetained { get; private set; }
    public int EdgesRetained { get; private set; }
    public long TextBytesRetained { get; private set; }

    public void VisitFact() => FactsVisited++;

    public void CheckRow(long textBytes)
    {
        if (textBytes > Math.Min(MaxRowTextBytes, MaxTextBytes))
            throw new ReportInputLimitException("row-text-bytes");
    }

    public void Retain(long textBytes, bool edge = false)
    {
        CheckRow(textBytes);
        if (edge ? EdgesRetained >= MaxEdges : FactsRetained >= MaxFacts)
            throw new ReportInputLimitException(edge ? "edge-rows" : "fact-rows");
        if (textBytes > MaxTextBytes - TextBytesRetained)
            throw new ReportInputLimitException("retained-text-bytes");
        if (edge) EdgesRetained++; else FactsRetained++;
        TextBytesRetained += textBytes;
    }
}

internal sealed class ReportInputLimitException(string limit) : Exception("ReportInputLimitReached")
{
    public string Limit { get; } = limit;
}
