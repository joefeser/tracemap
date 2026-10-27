using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static TraceMap.Core.SqlServerReaderColumns;

namespace TraceMap.Core;

/// <summary>One Field-read site: the direct DataTable/DataSet Fill, or one call site of a bounded helper.</summary>
/// <param name="Path">Internal-helper origin only: the caller document's repo-relative path from the extractor's resolver; otherwise null.</param>
/// <param name="Caller">Display string of the member containing the Fill (direct origins) or the call site (helper origins).</param>
/// <param name="Coverage">bounded only when the site's rows carry at least one Field read; field-read-absent sites stay their own route's evidence.</param>
/// <param name="CommandTextHash">SHA-256 of the constant command text (direct) or this call's procedure argument (helpers); null when unknown.</param>
public sealed record SqlServerFieldReadSite(string? Path, int Line, int EndLine, string? Caller, string Coverage, string? Gap, string? CommandTextHash,
    IReadOnlyList<string> Selectors, IReadOnlyList<string> Guards, IReadOnlyList<SqlServerFieldReadAccess> Accesses);
/// <param name="Origin">datatable, dataset, helper (Phase 4 private) or internal-helper (Phase 5): the one bounded row-binding origin.</param>
/// <param name="OriginGaps">With no bounded origin: every origin's Field-mode gap as origin:gap in origin order; otherwise empty.</param>
/// <param name="Sites">Direct origins: exactly one site. Helper origins: one site per call site, in the helper state's caller order.</param>
public sealed record SqlServerFieldReadState(string Version, string Coverage, string? Gap, string? Origin, IReadOnlyList<string> OriginGaps,
    string? ResultKind, string? CommandType, string? CommandTextSource, string? AdapterAssociation, string? FillOverload, string? TableName,
    IReadOnlyList<SqlServerFieldReadSite> Sites);

/// <summary>
/// Phase 6 sibling of the DataTable, DataSet, helper and internal-helper states: the identical Phase 2-5 row-binding rules with exactly
/// one widening, <c>DataRowExtensions.Field&lt;T&gt;(string)</c> / <c>Field&lt;T&gt;(int)</c> (C# extension form, VB reduced form,
/// checked declaring assembly) as a column name/ordinal access. The type argument is recorded, never compared. The Phase 2-5
/// states are unchanged and keep Field reads as datarow-field-extension-unsupported; a site without any Field read is
/// field-read-absent here so no access is compared on two routes. No execution, row, type, conversion, nullability or value claim.
/// </summary>
internal static class SqlServerFieldReads
{
    internal const string Version = "sqlserver-field-read/1";
    internal const string AbsentGap = "field-read-absent";
    internal static readonly string[] Origins = ["datatable", "dataset", "helper", "internal-helper"];

    private sealed record CandidateScope(bool MayContainField);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Compilation, CandidateScope> CandidateScopes = new();

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
    {
        // A conservative compilation-wide presence check includes cross-document helper callers,
        // escaped identifiers, unresolved/lookalike calls and unsupported overloads. Absence is
        // the only omission proof; uncertain/oversized scopes retain the original refusal state.
        // Scope registration can change independently of the immutable compilation. Check it
        // on every call, outside the syntax-only cache (including after a previous absence).
        if (call.SemanticModel is { } model
            && SqlServerCompilationScopes.Find(model.Compilation) is { Reduced: false, HasErrors: false }
            && !CandidateScopes.GetValue(model.Compilation, Candidate).MayContainField) return;
        properties["sqlServerFieldReadState"] = SqlServerSchemaEvidence.Json(Extract(call));
    }

    private static CandidateScope Candidate(Compilation compilation)
    {
        var trees = compilation.SyntaxTrees.Take(SqlServerCompilationScope.MaxTrees + 1).ToArray();
        if (trees.Length > SqlServerCompilationScope.MaxTrees || trees.Sum(t => (long)t.Length) > SqlServerCompilationScope.MaxCharacters)
            return new(true);
        return new(trees.Any(t => t.GetRoot().DescendantTokens().Any(token => string.Equals(token.ValueText, "Field", StringComparison.OrdinalIgnoreCase))));
    }

    private static SqlServerFieldReadState Unknown(string gap, IReadOnlyList<string> originGaps)
        => new(Version, "unknown", gap, null, originGaps, null, null, null, null, null, null, []);

    internal static SqlServerFieldReadState Extract(IInvocationOperation call)
    {
        var table = SqlServerDataTableConsumers.Extract(call, fieldReads: true, out var tableAccesses);
        var set = SqlServerDataSetConsumers.Extract(call, fieldReads: true, out var setAccesses);
        var helper = SqlServerHelperResults.Analyze(call, internalMode: false, fieldReads: true);
        var shared = SqlServerHelperResults.Analyze(call, internalMode: true, fieldReads: true);
        string?[] gaps = [table.Coverage == "bounded" ? null : table.Gap ?? "datatable-state-unavailable",
            set.Coverage == "bounded" ? null : set.Gap ?? "dataset-state-unavailable", helper.Gap, shared.Gap];
        var bounded = Enumerable.Range(0, Origins.Length).Where(i => gaps[i] is null).ToArray();
        // Overload, returned local and accessibility make the origins exclusive; two bounded origins would be ambiguous evidence.
        if (bounded.Length > 1) return Unknown("field-read-origin-ambiguous", []);
        if (bounded.Length == 0) return Unknown("field-read-origin-unbound", Origins.Select((o, i) => o + ":" + gaps[i]).ToArray());
        var (line, end) = Lines(call);
        switch (bounded[0])
        {
            case 0:
                return new(Version, "bounded", null, Origins[0], [], "datatable", table.CommandType, null, table.AdapterAssociation, "fill-datatable", null,
                    [Site(null, line, end, Member(call), table.CommandTextHash, [], Guards(table.Guard), tableAccesses)]);
            case 1:
                return new(Version, "bounded", null, Origins[1], [], "dataset", set.CommandType, null, set.AdapterAssociation, set.FillOverload, set.TableName,
                    [Site(null, line, end, Member(call), set.CommandTextHash, set.Selectors, set.Guards, setAccesses)]);
            default:
                var analysis = bounded[0] == 2 ? helper : shared;
                var sites = analysis.Callers.Select(c => c.Caller.Coverage == "bounded"
                    ? Site(c.Path, c.Caller.Line, c.Caller.EndLine, c.Caller.Caller, c.Caller.CommandTextHash, c.Caller.Selectors, c.Caller.Guards, c.Accesses)
                    : new SqlServerFieldReadSite(c.Path, c.Caller.Line, c.Caller.EndLine, c.Caller.Caller, "unknown", c.Caller.Gap, null, [], [], [])).ToArray();
                return new(Version, "bounded", null, Origins[bounded[0]], [], analysis.ResultKind, analysis.CommandType, analysis.CommandTextSource,
                    analysis.AdapterAssociation, analysis.FillOverload, analysis.TableName, sites);
        }
    }

    /// <summary>A bound site is Field evidence only when at least one access is a Field read; otherwise its own route owns it.</summary>
    private static SqlServerFieldReadSite Site(string? path, int line, int end, string? caller, string? hash, IReadOnlyList<string> selectors,
        IReadOnlyList<string> guards, IReadOnlyList<SqlServerFieldReadAccess> accesses)
        => accesses.Any(a => a.Member == "Field") ? new(path, line, end, caller, "bounded", null, hash, selectors, guards, accesses)
            : new(path, line, end, caller, "unknown", AbsentGap, null, [], [], []);

    private static IReadOnlyList<string> Guards(string guard) => guard switch
    {
        "rows-count-and-foreach" => ["foreach", "rows-count"],
        "rows-count" => ["rows-count"],
        "foreach" => ["foreach"],
        _ => []
    };

    /// <summary>The ordinary member containing the Fill (lambdas and local functions climb to it), as the helper callers display it.</summary>
    private static string? Member(IInvocationOperation call)
    {
        ISymbol? member = call.SemanticModel?.GetEnclosingSymbol(call.Syntax.SpanStart);
        while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }) member = member.ContainingSymbol;
        return member?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }
}
