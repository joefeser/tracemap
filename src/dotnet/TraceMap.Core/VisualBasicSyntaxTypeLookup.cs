using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

// Root identity matters: configured imports can create a different immutable root.
// Weak keys prevent a process-wide cache from retaining completed scans.
internal sealed class VisualBasicSyntaxTypeLookup
{
    private static readonly ConditionalWeakTable<CompilationUnitSyntax, Lazy<VisualBasicSyntaxTypeLookup>> Cache = new();
    private readonly HashSet<string> declarations;
    private readonly Dictionary<string, string[]> aliases;
    private readonly string[] imports;
    private readonly ConcurrentDictionary<string, ImmutableArray<string>> resolutions = new(StringComparer.Ordinal);

    internal static VisualBasicSyntaxTypeLookup For(CompilationUnitSyntax root) =>
        Cache.GetValue(root, static key => new Lazy<VisualBasicSyntaxTypeLookup>(() => new(key))).Value;

    private VisualBasicSyntaxTypeLookup(CompilationUnitSyntax root)
    {
        declarations = root.DescendantNodes().OfType<TypeBlockSyntax>()
            .Select(t => t.BlockStatement.Identifier.ValueText).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clauses = root.Imports.SelectMany(s => s.ImportsClauses).OfType<SimpleImportsClauseSyntax>().ToArray();
        imports = clauses.Where(c => c.Alias is null).Select(c => Strip(c.Name.ToString())).ToArray();
        aliases = clauses.Where(c => c.Alias is not null)
            .GroupBy(c => c.Alias!.Identifier.ValueText, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(c => Strip(c.Name.ToString())).ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    internal ImmutableArray<string> Resolve(string text) => resolutions.GetOrAdd(text.Trim(), ResolveCore);

    private ImmutableArray<string> ResolveCore(string text)
    {
        if (text.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)) return [text[7..]];
        var separator = text.IndexOf('.');
        var first = separator < 0 ? text : text[..separator];
        if (declarations.Contains(first)) return [];
        if (aliases.TryGetValue(first, out var targets))
            return targets.Select(target => target + text[first.Length..]).ToImmutableArray();
        if (separator >= 0) return [text];
        return imports.Select(prefix => prefix + "." + text).ToImmutableArray();
    }

    private static string Strip(string name) => name.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? name[7..] : name;
}
