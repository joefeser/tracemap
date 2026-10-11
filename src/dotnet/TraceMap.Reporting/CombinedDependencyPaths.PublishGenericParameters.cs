using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private static bool PublishGenericParameterMatches(string syntax, string metadata,
        string lexicalNamespace, string importedNamespaces, int nesting, IReadOnlyDictionary<string, string>? methodBindings)
    {
        // Open method parameters need retained declaration ordinal evidence.
        // Containing-type parameters (!0) are not guessed from spelling.
        var parsed = SyntaxFactory.ParseTypeName(syntax);
        if (parsed.ContainsDiagnostics || parsed.ToFullString() != syntax) return false;
        var generic = parsed switch
        {
            GenericNameSyntax direct => direct,
            QualifiedNameSyntax { Right: GenericNameSyntax right } => right,
            _ => null
        };
        if (generic is null || generic.TypeArgumentList.Arguments.Count is < 1 or > 32) return false;
        var arity = generic.TypeArgumentList.Arguments.Count;
        var baseName = generic.Identifier.ValueText + "`" + arity;
        if (parsed is QualifiedNameSyntax qualified) baseName = qualified.Left + "." + baseName;
        if (!TrySplitPublishGeneric(metadata, out var metadataBase, out var arguments)
            || arguments.Length != arity
            || !PublishParameterMatchesCore(baseName, metadataBase, lexicalNamespace, importedNamespaces, nesting + 1, methodBindings)) return false;
        for (var index = 0; index < arity; index++)
            if (!PublishParameterMatchesCore(generic.TypeArgumentList.Arguments[index].ToString(), arguments[index],
                    lexicalNamespace, importedNamespaces, nesting + 1, methodBindings)) return false;
        return true;
    }

    private static bool TrySplitPublishGeneric(string value, out string baseType, out string[] arguments)
    {
        baseType = string.Empty;
        arguments = [];
        var parens = 0;
        var generics = 0;
        var start = -1;
        var parts = new List<string>();
        for (var index = 0; index < value.Length; index++)
        {
            // Identity names are length framed. A delimiter within a name is
            // not generic syntax and must not affect list boundaries.
            if (char.IsAsciiDigit(value[index]))
            {
                var end = index;
                while (end < value.Length && char.IsAsciiDigit(value[end])) end++;
                if (end < value.Length && value[end] == ':')
                {
                    var position = index;
                    if (!ReadComponent(value, ref position, out _)) return false;
                    index = position - 1;
                    continue;
                }
            }
            switch (value[index])
            {
                case '(': parens++; break;
                case ')': if (--parens < 0) return false; break;
                case '<' when parens == 0:
                    if (++generics > 16) return false;
                    if (start < 0) { baseType = value[..index]; start = index + 1; }
                    break;
                case ',' when parens == 0 && generics == 1:
                    if (index == start || parts.Count >= 32) return false;
                    parts.Add(value[start..index]); start = index + 1;
                    break;
                case '>' when parens == 0:
                    if (--generics < 0) return false;
                    if (generics != 0) break;
                    if (start < 0 || index == start || index != value.Length - 1 || parts.Count >= 32) return false;
                    parts.Add(value[start..index]); arguments = parts.ToArray(); return true;
            }
        }
        return false;
    }
}
