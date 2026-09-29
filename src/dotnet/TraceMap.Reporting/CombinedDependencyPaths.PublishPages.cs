using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // Build each complete typed roster once. The scratch inventory retains all
    // facts; these lookups only replace repeated historical page-join predicates.
    internal sealed class PublishPageCandidateInventory
    {
        internal readonly Dictionary<(string Source, string? File), CombinedFactRow[]> Pages;
        internal readonly Dictionary<string, CombinedFactRow[]> Assemblies;
        internal readonly Dictionary<string, CombinedFactRow[]> Types;
        internal readonly Dictionary<(string Source, string? File), CombinedFactRow[]> Handlers;
        internal readonly Dictionary<(string Source, string? File), CombinedFactRow[]> Bindings;
        internal readonly Dictionary<(string Source, string? File), CombinedFactRow[]> Declarations;
        internal readonly PublishMemberCandidateIndex Methods;

        internal PublishPageCandidateInventory(IReadOnlyList<CombinedFactRow> facts)
        {
            Pages = ByFile(FactsOfTypes(facts, FactTypes.WebFormsPageDeclared));
            Assemblies = FactsOfTypes(facts, FactTypes.WebFormsPublishAssemblyBound)
                .GroupBy(fact => fact.SourceIndexId).ToDictionary(group => group.Key, group => group.ToArray());
            // Mapless uniqueness includes unbound type competitors, exactly as
            // the original predicate did; boundness is checked after counting.
            Types = FactsOfTypes(facts, FactTypes.ManagedTypeDeclared)
                .GroupBy(fact => fact.SourceIndexId).ToDictionary(group => group.Key, group => group.ToArray());
            Handlers = FactsOfTypes(facts, FactTypes.WebFormsHandlerResolved)
                .GroupBy(fact => (fact.SourceIndexId, fact.Properties.GetValueOrDefault("markupFile")))
                .ToDictionary(group => group.Key, group => group.ToArray());
            Bindings = ByFile(FactsOfTypes(facts, FactTypes.WebFormsPublishSourceBound));
            Declarations = ByFile(FactsOfTypes(facts, FactTypes.MethodDeclared)
                .Where(fact => fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations));
            Methods = new(FactsOfTypes(facts, FactTypes.ManagedMethodDeclared)
                .Where(fact => fact.Properties.GetValueOrDefault("provenanceState") == "bound"));
        }

        private static Dictionary<(string Source, string? File), CombinedFactRow[]> ByFile(IEnumerable<CombinedFactRow> facts)
            => facts.GroupBy(fact => (Source: fact.SourceIndexId, File: (string?)fact.FilePath))
                .ToDictionary(group => group.Key, group => group.ToArray());
    }
}
