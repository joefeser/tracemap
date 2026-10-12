namespace TraceMap.Core;

/// <summary>Bounded availability and operation metadata; raw SQL and credentials are excluded.</summary>
public sealed record DatabaseCallEvidence(string MethodName, string OperationKind, string OperationForm,
    string CommandTextAvailability, string? CommandTextHash, string CommandTypeEvidence,
    string ParameterEvidence, string TransactionEvidence, string ConnectionEvidence, string DispatchBoundary,
    string CoverageLabel, string Limitations,
    string? OriginalSignature = null, string? ProviderAssemblyIdentity = null,
    string? ParameterDirectionKinds = null, string? ParameterTypeKinds = null, string CommandStateCoverage = "unavailable")
{
    internal static DatabaseCallEvidence FromProperties(IReadOnlyDictionary<string,string> p)
    {
        string Value(string key, params string[] allowed) => p.TryGetValue(key, out var value) && allowed.Contains(value, StringComparer.Ordinal) ? value : "unavailable";
        var method = p.GetValueOrDefault("methodName", "");
        var hash = p.GetValueOrDefault("commandTextHash");
        return new(DatabaseOperationApi.Kind(method).Length > 0 ? method : "unavailable",
            Value("operationKind", "select-candidate", "scalar-candidate", "execute-candidate", "xml-reader-candidate", "data-adapter-fill", "data-adapter-schema", "data-adapter-update", "bulk-write-boundary"),
            Value("operationForm", "sync", "async", "apm-begin", "apm-end"),
            Value("commandTextAvailability", "unavailable", "dynamic", "hash-only-constant"),
            hash is { Length:64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? hash : null,
            Value("commandTypeEvidence", "unavailable", "Text", "StoredProcedure", "TableDirect"),
            Value("parameterEvidence", "separate-witness-required", "local-collection-mutation-witness"), Value("transactionEvidence", "separate-witness-required", "local-assignment-witness"),
            Value("connectionEvidence", "unavailable", "local-assignment-witness", "constructor-argument-witness"), Value("dispatchBoundary", "interface", "abstract", "declared-api"),
            Value("coverageLabel", "bounded-static-call", "reduced", "reduced-syntax-vb-database-operation"), DatabaseOperationApi.Limitations,
            Signature(p.GetValueOrDefault("originalSignature"),method), Assembly(p.GetValueOrDefault("providerAssemblyIdentity")),
            Value("parameterDirectionKinds", "Input", "Output", "InputOutput", "ReturnValue"),
            Value("parameterTypeKinds", "enum-argument-witness", "enum-assignment-witness"),
            Value("commandStateCoverage", "bounded-local-straight-line", "local-work-limit"));
    }
    private static string? Signature(string? value, string method)
    {
        if (value is null || value.Length > 2048 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && !"._(), []<>".Contains(c))) return null;
        var index = value.IndexOf("." + method + "(",StringComparison.Ordinal);
        if (index < 0 || !DatabaseOperationApi.IsSyntaxType(value[..index],method)) return null;
        var parameters = value[(index + method.Length + 2)..^1];
        var allowed = new[] { "int", "string", "object", "Integer", "String", "Object", "params", "ParamArray",
            "System.Int32", "System.String", "System.Object", "System.AsyncCallback", "System.IAsyncResult",
            "System.Threading.CancellationToken", "System.Data.CommandBehavior", "System.Data.DataSet", "System.Data.SchemaType",
            "System.Data.DataTable", "System.Data.DataRow", "System.Data.DataRowState", "System.Data.IDataReader",
            "System.Data.Common.DbDataReader", "System.Data.SqlClient.SqlDataReader", "Microsoft.Data.SqlClient.SqlDataReader" };
        if (!value.EndsWith(')') || System.Text.RegularExpressions.Regex.Matches(parameters,@"[A-Za-z_][A-Za-z0-9_.]*")
            .Any(m => !allowed.Contains(m.Value,StringComparer.Ordinal))) return null;
        return value;
    }
    private static string? Assembly(string? value) => value is { Length: < 256 }
        && System.Text.RegularExpressions.Regex.IsMatch(value, @"^(System\.Data|System\.Data\.Common|System\.Data\.SqlClient|Microsoft\.Data\.SqlClient), Version=\d+\.\d+\.\d+\.\d+, Culture=neutral, PublicKeyToken=(b77a5c561934e089|b03f5f7f11d50a3a|23ec7fc2d6eaa4a5)$") ? value : null;
}
