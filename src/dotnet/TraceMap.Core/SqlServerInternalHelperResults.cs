using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>One call site of a bounded internal helper, in any ordinary document of the helper's compilation.</summary>
/// <param name="Path">Repo-relative path of the caller document from the extractor's normalized resolver; never absolute.</param>
/// <param name="Caller">Display string of the calling member (the ordinary member containing any lambda).</param>
/// <param name="CommandTextHash">SHA-256 of this call's constant procedure argument; the text itself is not retained.</param>
public sealed record SqlServerInternalHelperCaller(string Path, int Line, int EndLine, string? Caller, string Coverage, string? Gap, string? CommandTextHash,
    int? ResultLine, IReadOnlyList<string> Selectors, IReadOnlyList<string> Guards, IReadOnlyList<SqlServerDataTableAccess> Accesses);
/// <param name="Callers">Every invocation of the helper in the registered compilation, ordered by (path, source position).</param>
public sealed record SqlServerInternalHelperResultState(string Version, string Coverage, string? Gap, string? ResultKind, string? CommandType,
    string? CommandTextSource, string? AdapterAssociation, string? FillOverload, string? TableName, int? HelperLine, int? AdapterLine,
    int? ResultLine, IReadOnlyList<SqlServerInternalHelperCaller> Callers);

/// <summary>
/// Phase 5 sibling of <see cref="SqlServerHelperResults"/>: the identical helper-body, signature and caller rules for exactly one
/// widening, an <c>internal</c> / <c>Friend</c> helper whose call sites are enumerated across every ordinary document of the
/// same registered compilation. Private helpers stay Phase 4 evidence only; InternalsVisibleTo, generated, linked and external
/// documents, reduced or erroneous compilations and the compilation bounds are typed unknowns. Enumerated call sites are never
/// a runtime-caller completeness claim (reflection, dynamic dispatch and other compilations are not observed).
/// </summary>
internal static class SqlServerInternalHelperResults
{
    internal const string Version = "sqlserver-internal-helper-result/1";

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
        => properties["sqlServerInternalHelperResultState"] = SqlServerSchemaEvidence.Json(Extract(call));

    internal static SqlServerInternalHelperResultState Extract(IInvocationOperation call)
    {
        var analysis = SqlServerHelperResults.Analyze(call, internalMode: true);
        if (analysis.Gap is { } gap) return new(Version, "unknown", gap, null, null, null, null, null, null, null, null, null, []);
        var callers = analysis.Callers.Select(c => new SqlServerInternalHelperCaller(c.Path!, c.Caller.Line, c.Caller.EndLine, c.Caller.Caller,
            c.Caller.Coverage, c.Caller.Gap, c.Caller.CommandTextHash, c.Caller.ResultLine, c.Caller.Selectors, c.Caller.Guards, c.Caller.Accesses)).ToArray();
        return new(Version, "bounded", null, analysis.ResultKind, analysis.CommandType, analysis.CommandTextSource, analysis.AdapterAssociation,
            analysis.FillOverload, analysis.TableName, analysis.HelperLine, analysis.AdapterLine, analysis.ResultLine, callers);
    }
}
