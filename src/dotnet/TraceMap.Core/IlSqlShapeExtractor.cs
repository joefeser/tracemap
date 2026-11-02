namespace TraceMap.Core;

/// <summary>A separate, deliberately smaller contract than command text/type flow.</summary>
internal static class IlSqlShapeExtractor
{
    internal const string Schema = "il-sql-shape.v1";
    internal const int MaxCalls = 256;
    internal const int MaxParameters = 16;
    internal const string Limitation = "Encoded System.Data SqlParameter construction/attachment and Fill-return candidate only; Tier3, not authenticated framework, SQL contract, parameter value, deployment or runtime evidence. One acyclic branch-free body, fresh local command/adapter/DataSet and at most 16 fresh parameters. No custom object field/property forwarding, caller substitution, alias/escape/mutation, exception flow, multiple executions, merged DataSet schema or consumer comparison. Names are UTF-16 hashes, never raw strings. Legacy command gaps remain unchanged.";

    internal static IlSqlShapeResult Extract(IlBodyObservation body, IlCommandBindingResult commands,
        IlBodyEvidenceExtractor.IlWorkBudget budget)
    {
        if (commands.Bindings.Count == 0) return new([], []);
        var flow = body.ValueFlow;
        if (body.Calls.Count > MaxCalls || !budget.TryConsume(1L + body.Calls.Count + (flow?.ControlFlow?.Count ?? 0)))
            return Unknown("IlSqlShapeWorkLimit");
        // Generic call effects remain unproven. Only the exact calls below are
        // admitted as encoded shape evidence, never as runtime effect proof.
        if (commands.Bindings.Count != 1 || flow?.ControlFlow is not { Count: > 0 }
            || flow.Gaps.Any(gap => gap != "IlValueCallEffectsUnmodeled")
            || body.ExceptionRegionCount != 0 || flow.ControlFlow.Any(node => node.Successors.Count > 1
                || node.Successors.Any(next => next <= node.Offset) || node.InvalidatesConfiguration
                || node.ExposedOrigins?.Count > 0)
            || flow.Calls.Count != body.Calls.Count || flow.Calls.Any(call => call.State != "control-flow-candidate"))
            return Unknown("IlSqlShapeFlowUnsupported");
        var binding = commands.Bindings[0];
        if (binding.AdapterBindingOffset is null || binding.CommandReceiver.Kind != "allocation-site"
            || binding.EndpointReceiver.Kind != "allocation-site") return Unknown("IlSqlShapeFreshReceiverRequired");
        var values = flow.Calls.ToDictionary(call => call.Offset);
        var allowed = binding.ConfigurationOffsets.Append(binding.AdapterBindingOffset.Value).ToHashSet();
        var parameters = new Dictionary<IlValueOrigin, IlSqlParameterShape>();
        var collections = new HashSet<IlValueOrigin>();
        var attached = new List<IlSqlParameterShape>();
        var dataSets = new HashSet<IlValueOrigin>();
        IlValueOrigin? filled = null;
        foreach (var call in body.Calls.OrderBy(call => call.Offset))
        {
            var value = values[call.Offset];
            if (allowed.Contains(call.Offset)) continue;
            if (call.Offset > binding.EndpointOffset) return Unknown("IlSqlShapePostExecutionCall");
            if (Is(call, "System.Data.SqlClient", "SqlParameter", ".ctor", "newobj", out var scope, out var signature)
                && value.Result.Kind == "allocation-site")
            {
                var prefix = "(type(namespace:6:System|names:6:String)," + scope + "type(namespace:11:System.Data|names:9:SqlDbType)";
                if (signature != prefix + ",type(namespace:6:System|names:5:Int32))->type(namespace:6:System|names:4:Void)")
                    return Unknown("IlSqlShapeParameterConstructorUnsupported");
                if (value.Arguments.Count != 3
                    || value.Arguments[0].Kind != "constant-string-hash" || value.Arguments[1].Kind != "constant-int32"
                    || value.Arguments[2].Kind != "constant-int32") return Unknown("IlSqlShapeParameterOriginUnresolved");
                if (parameters.Count + attached.Count >= MaxParameters) return Unknown("IlSqlShapeParameterLimit");
                parameters.Add(value.Result, new(value.Result, value.Arguments[0], value.Arguments[1],
                    value.Arguments[2], call.Offset, -1));
                continue;
            }
            if (Is(call, "System.Data.SqlClient", "SqlCommand", "get_Parameters", "callvirt", out scope, out signature)
                && signature == "()->" + scope + "type(namespace:21:System.Data.SqlClient|names:22:SqlParameterCollection)"
                && value.Receiver == binding.CommandReceiver && value.Result.Kind == "call-result")
            { collections.Add(value.Result); continue; }
            if (Is(call, "System.Data.SqlClient", "SqlParameterCollection", "Add", "callvirt", out scope, out signature)
                && signature == "(" + scope + "type(namespace:21:System.Data.SqlClient|names:12:SqlParameter))->"
                    + scope + "type(namespace:21:System.Data.SqlClient|names:12:SqlParameter)"
                && collections.Contains(value.Receiver) && value.Arguments.Count == 1)
            {
                if (!parameters.Remove(value.Arguments[0], out var parameter)) return Unknown("IlSqlShapeParameterCallerOrAliasUnresolved");
                attached.Add(parameter with { AttachmentOffset = call.Offset }); continue;
            }
            if (Is(call, "System.Data", "DataSet", ".ctor", "newobj", out _, out signature)
                && signature == "()->type(namespace:6:System|names:4:Void)" && value.Result.Kind == "allocation-site")
            { dataSets.Add(value.Result); continue; }
            if (call.Offset == binding.EndpointOffset
                && (Is(call, "System.Data.Common", "DbDataAdapter", "Fill", "callvirt", out scope, out signature)
                    || Is(call, "System.Data.Common", "DataAdapter", "Fill", "callvirt", out scope, out signature))
                && signature == "(" + scope + "type(namespace:11:System.Data|names:7:DataSet))->type(namespace:6:System|names:5:Int32)"
                && value.Arguments.Count == 1 && dataSets.Contains(value.Arguments[0]))
            { filled = value.Arguments[0]; continue; }
            return Unknown("IlSqlShapeCallOrMutationUnsupported");
        }
        if (filled is null || attached.Count == 0 || parameters.Count != 0 || dataSets.Count != 1)
            return Unknown("IlSqlShapeIncompleteAttachment");
        var returns = flow.Returns;
        if (returns is null || returns.Count != 1 || returns[0].Origin != filled
            || returns[0].Offset <= binding.EndpointOffset || returns[0].State != "return-operand-candidate")
            return Unknown("IlSqlShapeReturnUnresolved");
        return new([new(binding.EndpointOffset, filled, returns[0].Offset, attached)], []);
    }

    private static IlSqlShapeResult Unknown(string gap) => new([], [gap]);

    private static bool Is(IlCallObservation call, string ns, string type, string member, string opcode,
        out string scope, out string signature)
    {
        scope = signature = "";
        const string prefix = "memberref|type:";
        if (call.ReferenceKind != "memberref" || call.Opcode != opcode
            || call.StackShape is not { HasThis: true, Supported: true } shape
            || shape.ByReferenceParameters.Contains('1') || !call.TargetIdentity.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var text = call.TargetIdentity[prefix.Length..];
        var endScope = text.IndexOf(")type(", StringComparison.Ordinal);
        if (endScope < 0) return false;
        scope = text[..(endScope + 1)];
        if (!scope.StartsWith("scope(assembly:name:11:System.Data|", StringComparison.Ordinal)
            || !scope.EndsWith("|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)", StringComparison.Ordinal)) return false;
        var expected = $"{scope}type(namespace:{ns.Length}:{ns}|names:{type.Length}:{type})|member:{member.Length}:{member}|";
        if (!text.StartsWith(expected, StringComparison.Ordinal)) return false;
        var signatureOffset = text.IndexOf("|explicitThis:false|", expected.Length, StringComparison.Ordinal);
        if (signatureOffset < 0) return false;
        signature = text[(signatureOffset + "|explicitThis:false|".Length)..];
        return true;
    }
}

internal sealed record IlSqlParameterShape(IlValueOrigin Origin, IlValueOrigin Name, IlValueOrigin SqlDbType,
    IlValueOrigin Size, long ConstructorOffset, long AttachmentOffset);
internal sealed record IlSqlShapeObservation(long EndpointOffset, IlValueOrigin DataSetOrigin, long ReturnOffset,
    IReadOnlyList<IlSqlParameterShape> Parameters);
internal sealed record IlSqlShapeResult(IReadOnlyList<IlSqlShapeObservation> Observations, IReadOnlyList<string> Gaps);
