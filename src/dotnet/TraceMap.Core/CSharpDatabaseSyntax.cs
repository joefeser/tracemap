using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

internal static class CSharpDatabaseSyntax
{
    internal static string? ReceiverType(CompilationUnitSyntax root, InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax access) return null;
        string? type = null;
        var expression = access.Expression;
        while (expression is ParenthesizedExpressionSyntax p) expression = p.Expression;
        if (expression is ObjectCreationExpressionSyntax creation) type = creation.Type.ToString();
        else
        {
            var name = expression is IdentifierNameSyntax id ? id.Identifier.ValueText
                : expression is MemberAccessExpressionSyntax { Expression:ThisExpressionSyntax, Name:IdentifierNameSyntax field } ? field.Identifier.ValueText : null;
            if (name is null) return null;
            var method = call.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
            var parameter = method?.ParameterList.Parameters.Where(p => p.Identifier.ValueText == name).ToArray();
            if (parameter is { Length:1 }) type = parameter[0].Type?.ToString();
            else
            {
                var locals = method?.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    .Where(v => v.Identifier.ValueText == name && v.SpanStart < call.SpanStart
                        && v.Ancestors().OfType<BlockSyntax>().FirstOrDefault()?.Span.Contains(call.Span) == true).ToArray();
                if (locals is { Length:1 } && locals[0].Parent is VariableDeclarationSyntax declaration)
                    type = declaration.Type.IsVar ? (locals[0].Initializer?.Value as ObjectCreationExpressionSyntax)?.Type.ToString() : declaration.Type.ToString();
                else
                {
                    var fields = call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Members.OfType<FieldDeclarationSyntax>()
                        .Where(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == name)).ToArray();
                    if (fields is { Length:1 }) type = fields[0].Declaration.Type.ToString();
                }
            }
        }
        if (type is null) return null;
        if (type.StartsWith("global::", StringComparison.Ordinal)) return type[8..];
        var first = type.Split('.')[0];
        if (root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any(t => t.Identifier.ValueText == first)) return null;
        var alias = root.Usings.Where(u => u.Alias?.Name.Identifier.ValueText == first).ToArray();
        var names = alias.Length > 0 ? alias.Select(u => u.Name + type[first.Length..])
            : type.Contains('.') ? [type] : root.Usings.Where(u => u.Alias is null).Select(u => u.Name + "." + type);
        var matches = names.Where(n => DatabaseOperationApi.IsSyntaxType(n.Replace("global::", "", StringComparison.Ordinal), access.Name.Identifier.ValueText)).Distinct(StringComparer.Ordinal).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
