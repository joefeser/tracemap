using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static partial class SqlServerSourceBinding
{
    private static bool Same(SyntaxNode? node, string expected) => node is not null
        && SyntaxFactory.AreEquivalent(node, SyntaxFactory.ParseExpression(expected));
    private static string ShortType(SqlSourceBindingInput input, string role) => input.Methods.Single(item => item.Role == role).TypeName.Split('.')[^1];
    private static bool Signature(MethodDeclarationSyntax method, bool isStatic, params string[] types) =>
        method.Body is not null && method.ExpressionBody is null && method.TypeParameterList is null
        && method.Modifiers.Any(SyntaxKind.StaticKeyword) == isStatic && !method.Modifiers.Any(SyntaxKind.AsyncKeyword)
        && method.ParameterList.Parameters.Select(parameter => parameter.Type?.WithoutTrivia().ToString()).SequenceEqual(types)
        && method.ParameterList.Parameters.All(parameter => parameter.Default is null && parameter.AttributeLists.Count == 0
            && SqlServerSchemaEvidence.Identifier(parameter.Identifier.ValueText) && SyntaxFacts.GetKeywordKind(parameter.Identifier.ValueText) == SyntaxKind.None)
        && !method.DescendantNodes().Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or YieldStatementSyntax);
    private static bool Params(MethodDeclarationSyntax method, int? index) => method.ParameterList.Parameters.Select((parameter, ordinal) =>
        ordinal == index ? parameter.Modifiers.Count == 1 && parameter.Modifiers[0].IsKind(SyntaxKind.ParamsKeyword) : parameter.Modifiers.Count == 0).All(value => value);
    private static string P(MethodDeclarationSyntax method, int index) => method.ParameterList.Parameters[index].Identifier.ValueText;
    private static InvocationExpressionSyntax? ReturnCall(MethodDeclarationSyntax method) =>
        method.Body?.Statements is { Count: 1 } statements && statements[0] is ReturnStatementSyntax { Expression: InvocationExpressionSyntax call } ? call : null;
    private static bool Call(InvocationExpressionSyntax? call, string target, params string[] args) => call is not null
        && Same(call.Expression, target) && call.ArgumentList.Arguments.Count == args.Length
        && call.ArgumentList.Arguments.Select((argument, index) => argument.NameColon is null && argument.RefKindKeyword.IsKind(SyntaxKind.None)
            && Same(argument.Expression, args[index])).All(value => value);
    private static bool StatementCall(StatementSyntax statement, string target, params string[] args) =>
        statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } && Call(call, target, args);
    private static bool Assignment(StatementSyntax statement, string left, string right) => statement is ExpressionStatementSyntax
        { Expression: AssignmentExpressionSyntax assignment } && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
        && Same(assignment.Left, left) && Same(assignment.Right, right);
    private static bool One(StatementSyntax? statement, Func<StatementSyntax, bool> match) =>
        statement is BlockSyntax { Statements.Count: 1 } block ? match(block.Statements[0]) : statement is not null && match(statement);
    private static bool Local(StatementSyntax statement, out string name, out ExpressionSyntax? value)
    {
        name = ""; value = null;
        if (statement is not LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } local
            || local.Modifiers.Count != 0 || local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)) return false;
        var variable = local.Declaration.Variables[0]; name = variable.Identifier.ValueText; value = variable.Initializer?.Value;
        return SqlServerSchemaEvidence.Identifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None && value is not null;
    }
    private static bool String(ExpressionSyntax? expression, string value) => expression is LiteralExpressionSyntax literal
        && literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == value;

    private static bool Entry(MethodDeclarationSyntax method, out string? procedure)
    {
        procedure = null;
        if (!Signature(method, false, "int") || !Params(method, null) || method.ReturnType.WithoutTrivia().ToString() != "IDataReader") return false;
        var call = ReturnCall(method);
        if (call is null || !Same(call.Expression, "this.ExecuteReader") || call.ArgumentList.Arguments.Count != 2
            || call.ArgumentList.Arguments.Any(argument => argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            || call.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)
            || !SqlServerSchemaEvidence.Identifier(literal.Token.ValueText) || !Same(call.ArgumentList.Arguments[1].Expression, P(method, 0))) return false;
        // Reject visible competing overloads. Other partial files/compilation lookup remain a gap.
        if (method.Parent is not ClassDeclarationSyntax { BaseList: null } owner) return false;
        var overloads = owner.Members.OfType<MethodDeclarationSyntax>().Where(item => item.Identifier.ValueText == "ExecuteReader").ToArray();
        if (overloads.Length != 2 || !overloads.Any(item => SignatureWithoutBody(item, "string", "object[]"))
            || !overloads.Any(item => SignatureWithoutBody(item, "int", "string", "object[]"))) return false;
        procedure = literal.Token.ValueText; return true;
    }
    private static bool SignatureWithoutBody(MethodDeclarationSyntax method, params string[] types) => method.Body is null
        && method.ExpressionBody is null && method.TypeParameterList is null && method.ReturnType.WithoutTrivia().ToString() == "IDataReader"
        && method.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && method.ParameterList.Parameters.Select(parameter => parameter.Type?.WithoutTrivia().ToString()).SequenceEqual(types)
        && Params(method, types.Length - 1);

    private static bool Provider(MethodDeclarationSyntax method, SqlSourceBindingInput input)
    {
        if (!Signature(method, false, "string", "object[]") || !Params(method, 1) || !method.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || method.Identifier.ValueText != "ExecuteReader" || method.Parent is not ClassDeclarationSyntax owner
            || owner.BaseList?.Types.Count != 1 || owner.BaseList.Types[0].Type.WithoutTrivia().ToString() != ShortType(input, "entry")) return false;
        var call = ReturnCall(method);
        if (call?.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } member || member.Name.Identifier.ValueText != "ExecuteReader") return false;
        var fieldName = receiver.Identifier.ValueText;
        var fields = owner.Members.OfType<FieldDeclarationSyntax>().Where(field => field.Declaration.Type.WithoutTrivia().ToString() == "DatabaseConnectionProvider")
            .SelectMany(field => field.Declaration.Variables).Where(variable => variable.Identifier.ValueText == fieldName).ToArray();
        if (fields.Length != 1 || !Same(fields[0].Initializer?.Value, "DatabaseConnectionProvider.Instance() ?? new " + ShortType(input, "connection") + "()")
            || method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == fieldName)) return false;
        return Call(call, fieldName + ".ExecuteReader", "this.ConnectionString", "CommandType.StoredProcedure", "0",
            "this.DatabaseOwner + this.ObjectQualifier + " + P(method, 0), P(method, 1));
    }

    private static bool Connection(MethodDeclarationSyntax method, SqlSourceBindingInput input) =>
        Signature(method, false, "string", "CommandType", "int", "string", "object[]") && Params(method, 4)
        && method.Modifiers.Any(SyntaxKind.OverrideKeyword) && method.Identifier.ValueText == "ExecuteReader"
        && Call(ReturnCall(method), ShortType(input, "helper") + ".ExecuteReader", Enumerable.Range(0, 5).Select(index => P(method, index)).ToArray());

    private static bool Helper(MethodDeclarationSyntax method, SqlSourceBindingInput input)
    {
        if (!Signature(method, true, "string", "CommandType", "int", "string", "object[]") || !Params(method, 4)
            || method.Identifier.ValueText != "ExecuteReader" || method.Body!.Statements.Count != 4) return false;
        var statements = method.Body.Statements;
        if (!Local(statements[0], out var database, out var creation)
            || creation is not ObjectCreationExpressionSyntax obj || obj.Type.WithoutTrivia().ToString() != "Database"
            || obj.ArgumentList?.Arguments.Count != 2 || !Same(obj.ArgumentList.Arguments[0].Expression, P(method, 0))
            || !Same(obj.ArgumentList.Arguments[1].Expression, "SqlProviderName") || obj.Initializer?.Expressions.Count != 1
            || !Same(obj.Initializer.Expressions[0], "EnableAutoSelect = false")) return false;
        var providerConstants = method.Parent!.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword)).SelectMany(field => field.Declaration.Variables)
            .Where(variable => variable.Identifier.ValueText == "SqlProviderName").ToArray();
        if (providerConstants.Length != 1 || !String(providerConstants[0].Initializer?.Value, "System.Data.SqlClient")) return false;
        if (statements[1] is not IfStatementSyntax { Else: null } command || !Same(command.Condition, P(method, 1) + " == CommandType.StoredProcedure")
            || !One(command.Statement, statement => Assignment(statement, P(method, 3), ShortType(input, "formatter") + "." + input.Methods.Single(item => item.Role == "formatter").MethodName
                + "(" + P(method, 3) + ", " + P(method, 4) + ")"))) return false;
        if (statements[2] is not IfStatementSyntax { Else: null } timeout || !Same(timeout.Condition, P(method, 2) + " > 0")
            || !One(timeout.Statement, statement => Assignment(statement, database + ".CommandTimeout", P(method, 2)))) return false;
        if (statements[3] is not TryStatementSyntax { Finally: null, Block.Statements.Count: 1 } guarded
            || guarded.Block.Statements[0] is not ReturnStatementSyntax { Expression: InvocationExpressionSyntax terminal }
            || !Call(terminal, database + ".ExecuteReader", P(method, 3), P(method, 4)) || guarded.Catches.Count != 1) return false;
        var catchBlock = guarded.Catches[0].Block;
        return catchBlock.Statements.LastOrDefault() is ThrowStatementSyntax { Expression: null }
            && !catchBlock.DescendantNodes().Any(node => node is ReturnStatementSyntax || node is InvocationExpressionSyntax invoke
                && invoke.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "ExecuteReader");
    }

    private static bool Formatter(MethodDeclarationSyntax method)
    {
        if (!Signature(method, true, "string", "object[]") || !Params(method, 1) || method.ReturnType.WithoutTrivia().ToString() != "string"
            || method.Body!.Statements.Count != 4) return false;
        var statements = method.Body.Statements;
        if (!Local(statements[0], out var builder, out var creation) || creation is not ObjectCreationExpressionSyntax obj
            || obj.Type.WithoutTrivia().ToString() != "StringBuilder" || obj.Initializer is not null || obj.ArgumentList?.Arguments.Count != 1
            || !String(obj.ArgumentList.Arguments[0].Expression, ";Exec ") || !StatementCall(statements[1], builder + ".Append", P(method, 0))) return false;
        if (statements[2] is not ForStatementSyntax { Declaration.Variables.Count: 1, Statement: BlockSyntax block } loop
            || loop.Initializers.Count != 0 || loop.Incrementors.Count != 1 || block.Statements.Count != 6) return false;
        var index = loop.Declaration.Variables[0]; var i = index.Identifier.ValueText; var args = P(method, 1);
        if (!SqlServerSchemaEvidence.Identifier(i) || !Same(index.Initializer?.Value, "0") || !Same(loop.Condition, i + " < " + args + ".Length")
            || !Same(loop.Incrementors[0], i + "++")) return false;
        var body = block.Statements;
        if (!Local(body[0], out var format, out var formatValue) || !String(formatValue, " @{0}")
            || !Local(body[1], out var name, out var nameValue) || !Same(nameValue, "null")
            || !Local(body[2], out var parameter, out var parameterValue) || !Same(parameterValue, args + "[" + i + "] as IDataParameter")) return false;
        if (body[3] is not IfStatementSyntax { Else: null } named || !Same(named.Condition, parameter + " != null")) return false;
        // For the conditional built-in-int path the IDataParameter branch is not taken.
        // All framework symbol identities remain explicit gaps, never semantic proof.
        if (!StatementCall(body[4], builder + ".AppendFormat", "CultureInfo.InvariantCulture", format, i, name)
            || body[5] is not IfStatementSyntax { Else: null } separator || !Same(separator.Condition, i + " < " + args + ".Length - 1")
            || !One(separator.Statement, statement => statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
                && Same(call.Expression, builder + ".Append") && call.ArgumentList.Arguments.Count == 1 && String(call.ArgumentList.Arguments[0].Expression, ","))) return false;
        return statements[3] is ReturnStatementSyntax { Expression: InvocationExpressionSyntax result } && Call(result, builder + ".ToString");
    }
}
