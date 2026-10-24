using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>Conservative source-only local request observation, not general dataflow or execution proof.</summary>
internal static class HttpRequestMethodEvidence
{
    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { OperatorMethod: null } conversion)
            operation = conversion.Operand;
        return operation;
    }

    private static bool Framework(ISymbol symbol) =>
        symbol.ContainingType?.ToDisplayString() is "System.Net.WebRequest" or "System.Net.HttpWebRequest"
        && VisualBasicSemanticExtractor.IsRecognizedWebRequestAssemblyIdentity(symbol.ContainingAssembly.Identity.Name,
            Convert.ToHexString(symbol.ContainingAssembly.Identity.PublicKeyToken.ToArray()));

    private static IOperation? Statement(IOperation operation, IBlockOperation block)
    {
        while (operation.Parent is not null && operation.Parent != block) operation = operation.Parent;
        return operation.Parent == block ? operation : null;
    }

    internal static bool TryResolve(IInvocationOperation call, out string verb, out ISimpleAssignmentOperation? assignment)
    {
        verb = "UNKNOWN";
        assignment = null;
        if (call.TargetMethod.Name is not ("GetResponse" or "GetResponseAsync" or "BeginGetResponse")
            || !Framework(call.TargetMethod) || call.Instance is null
            || Unwrap(call.Instance) is not ILocalReferenceOperation receiver) return false;
        var block = call.Parent;
        while (block is not null && block is not IBlockOperation) block = block.Parent;
        if (block is not IBlockOperation scope) return false;
        var root = (IOperation)scope;
        while (root.Parent is not null) root = root.Parent;
        // A loop or jump can revisit the terminal with a different instance/state.
        // Refuse instead of guessing dominance across control-flow edges.
        var all = root.DescendantsAndSelf().Take(20_001).ToArray();
        if (all.Length > 20_000 || all.Any(o => o is ILoopOperation or IBranchOperation or IInvalidOperation)) return false;
        // VB unstructured error handling can continue after a failed setter.
        if (root.Syntax.DescendantNodesAndSelf().Any(n => n.GetType().Name is
            "OnErrorGoToStatementSyntax" or "OnErrorResumeNextStatementSyntax" or "ResumeStatementSyntax")) return false;
        var declarations = all.OfType<IVariableDeclaratorOperation>()
            .Where(d => SymbolEqualityComparer.Default.Equals(d.Symbol, receiver.Local)).ToArray();
        if (declarations.Length != 1) return false;
        var declaration = declarations[0];
        var declarationStatement = Statement(declaration, scope);
        if (declarationStatement is not IVariableDeclarationGroupOperation) return false;
        var initializer = declaration.Initializer ?? (declaration.Parent as IVariableDeclarationOperation)?.Initializer;
        if (initializer is null || Unwrap(initializer.Value) is not IInvocationOperation factory
            || !Framework(factory.TargetMethod) || !factory.TargetMethod.IsStatic
            || factory.TargetMethod.Name is not ("Create" or "CreateHttp")
            || factory.Arguments.Length != 1) return false;
        var callStatement = Statement(call, scope);
        if (callStatement is null || declaration.Syntax.SpanStart >= call.Syntax.SpanStart) return false;
        // Only a top-level assignment may establish the method; nested conditional
        // assignments and writes inside lambdas/local functions cannot qualify.
        foreach (var reference in all.OfType<ILocalReferenceOperation>()
                     .Where(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver.Local)))
        {
            for (IOperation? ancestor = reference.Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is IAnonymousFunctionOperation or ILocalFunctionOperation) return false;
            IOperation use = reference;
            while (use.Parent is IConversionOperation { OperatorMethod: null }) use = use.Parent;
            if (use.Parent == call && call.Instance == use) continue;
            if (use.Parent is IPropertyReferenceOperation property && property.Instance == use && Framework(property.Property))
            {
                if (property.Property.Name == "Method")
                {
                    if (property.Parent is not ISimpleAssignmentOperation write || write.Target != property
                        || write.Parent is not IExpressionStatementOperation expression || expression.Parent != scope
                        || write.Syntax.SpanStart <= declaration.Syntax.SpanStart || write.Syntax.Span.End >= call.Syntax.SpanStart
                        || assignment is not null || write.Value.ConstantValue is not { HasValue: true, Value: string value }
                        || !new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT" }.Contains(value, StringComparer.Ordinal)) return false;
                    assignment = write;
                    verb = value;
                    continue;
                }
                // These framework properties do not expose the request instance.
                if (property.Property.Name is "ContentType" or "Headers" or "ContentLength"
                    && Statement(property, scope) is IExpressionStatementOperation
                    && property.Syntax.SpanStart < call.Syntax.SpanStart) continue;
            }
            if (use.Parent is IInvocationOperation stream && stream.Instance == use && Framework(stream.TargetMethod)
                && stream.TargetMethod.Name == "GetRequestStream" && stream.Arguments.Length == 0
                && stream.Syntax.SpanStart < call.Syntax.SpanStart && Statement(stream, scope) is not null) continue;
            // Includes aliases, arguments/ByRef escapes, receiver replacement,
            // additional terminals and captures. No private values are retained.
            return false;
        }
        if (assignment is null) return false;
        // Factory-created locals must not be used from deferred/nested bodies.
        for (IOperation? ancestor = call.Parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor is IAnonymousFunctionOperation or ILocalFunctionOperation) return false;
        return true;
    }
}
