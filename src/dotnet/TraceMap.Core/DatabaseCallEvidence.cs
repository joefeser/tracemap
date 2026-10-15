namespace TraceMap.Core;

/// <summary>Bounded availability and operation metadata; raw SQL and credentials are excluded.</summary>
public sealed record DatabaseCallEvidence(string MethodName, string OperationKind, string OperationForm,
    string CommandTextAvailability, string? CommandTextHash, string CommandTypeEvidence,
    string ParameterEvidence, string TransactionEvidence, string ConnectionEvidence, string DispatchBoundary,
    string CoverageLabel, string Limitations,
    string? OriginalSignature = null, string? ProviderAssemblyIdentity = null,
    string? ParameterDirectionKinds = null, string? ParameterTypeKinds = null, string CommandStateCoverage = "unavailable", string? ParameterTypeNames = null)
{
    private static readonly string[] Directions = ["Input", "Output", "InputOutput", "ReturnValue"];
    private static readonly string[] TypeKinds = ["enum-argument-witness", "enum-assignment-witness"];

    public bool IsWellFormed() => new[] { MethodName, OperationKind, OperationForm, CommandTextAvailability,
            CommandTypeEvidence, ParameterEvidence, TransactionEvidence, ConnectionEvidence, DispatchBoundary,
            CoverageLabel, Limitations, CommandStateCoverage }.All(value => !string.IsNullOrEmpty(value))
        && (MethodName == "unavailable" || DatabaseOperationApi.Kind(MethodName).Length > 0)
        && new[] { "unavailable", "select-candidate", "scalar-candidate", "execute-candidate", "xml-reader-candidate",
            "data-adapter-fill", "data-adapter-schema", "data-adapter-update", "bulk-write-boundary",
            "insert-candidate", "update-candidate", "delete-candidate", "upsert-candidate", "stored-routine-call-candidate" }.Contains(OperationKind, StringComparer.Ordinal)
        && OperationForm is "unavailable" or "sync" or "async" or "apm-begin" or "apm-end"
        && CommandTextAvailability is "unavailable" or "dynamic" or "hash-only-constant"
        && CommandTypeEvidence is "unavailable" or "Text" or "StoredProcedure" or "TableDirect"
        && ParameterEvidence is "unavailable" or "separate-witness-required" or "local-collection-mutation-witness"
        && TransactionEvidence is "unavailable" or "separate-witness-required" or "local-assignment-witness"
        && ConnectionEvidence is "unavailable" or "local-assignment-witness" or "constructor-argument-witness"
        && DispatchBoundary is "unavailable" or "interface" or "abstract" or "declared-api"
        && CoverageLabel is "unavailable" or "bounded-static-call" or "reduced" or "reduced-syntax-vb-database-operation"
        && CommandStateCoverage is "unavailable" or "bounded-local-straight-line" or "local-work-limit"
        && (CommandTextHash is null || CommandTextHash.Length == 64 && CommandTextHash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
        && (ProviderAssemblyIdentity is null || Assembly(ProviderAssemblyIdentity) == ProviderAssemblyIdentity)
        && (OriginalSignature is null || Signature(OriginalSignature, MethodName) == OriginalSignature)
        && (ParameterTypeNames is null || ParameterType(ParameterTypeNames) == ParameterTypeNames)
        && ValidKinds(ParameterDirectionKinds, Directions)
        && ValidKinds(ParameterTypeKinds, TypeKinds);

    private static bool ValidKinds(string? value, string[] allowed) => value is null or "unavailable"
        || NormalizeList(value, token => allowed.Contains(token, StringComparer.Ordinal)) == value;

    // Bound both raw text and item count before Split allocates any token strings.
    private static string? NormalizeList(string? value, Func<string, bool> valid)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 8192) return null;
        var count = 1;
        foreach (var c in value)
            if (c == ';' && ++count > 128) return null;
        var values = value.Split(';');
        return values.All(valid)
            ? string.Join(";", values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) : null;
    }

    internal static DatabaseCallEvidence FromProperties(IReadOnlyDictionary<string,string> p)
    {
        string Value(string key, params string[] allowed) => p.TryGetValue(key, out var value) && allowed.Contains(value, StringComparer.Ordinal) ? value : "unavailable";
        string Values(string key, params string[] allowed)
        {
            return NormalizeList(p.GetValueOrDefault(key), v => allowed.Contains(v, StringComparer.Ordinal)) ?? "unavailable";
        }
        var method = p.GetValueOrDefault("methodName", "");
        var hash = p.GetValueOrDefault("commandTextHash");
        return new(DatabaseOperationApi.Kind(method).Length > 0 ? method : "unavailable",
            Value("operationKind", "select-candidate", "scalar-candidate", "execute-candidate", "xml-reader-candidate", "data-adapter-fill", "data-adapter-schema", "data-adapter-update", "bulk-write-boundary", "insert-candidate", "update-candidate", "delete-candidate", "upsert-candidate", "stored-routine-call-candidate"),
            Value("operationForm", "sync", "async", "apm-begin", "apm-end"),
            Value("commandTextAvailability", "unavailable", "dynamic", "hash-only-constant"),
            hash is { Length:64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? hash : null,
            Value("commandTypeEvidence", "unavailable", "Text", "StoredProcedure", "TableDirect"),
            Value("parameterEvidence", "separate-witness-required", "local-collection-mutation-witness"), Value("transactionEvidence", "separate-witness-required", "local-assignment-witness"),
            Value("connectionEvidence", "unavailable", "local-assignment-witness", "constructor-argument-witness"), Value("dispatchBoundary", "interface", "abstract", "declared-api"),
            Value("coverageLabel", "bounded-static-call", "reduced", "reduced-syntax-vb-database-operation"), DatabaseOperationApi.Limitations,
            Signature(p.GetValueOrDefault("originalSignature"),method), Assembly(p.GetValueOrDefault("providerAssemblyIdentity")),
            Values("parameterDirectionKinds", "Input", "Output", "InputOutput", "ReturnValue"),
            Values("parameterTypeKinds", "enum-argument-witness", "enum-assignment-witness"),
            Value("commandStateCoverage", "bounded-local-straight-line", "local-work-limit"), ParameterType(p.GetValueOrDefault("parameterTypeNames")));
    }
    private static string? ParameterType(string? value)
    {
        return NormalizeList(value, ValidParameterType);
    }
    private static bool ValidParameterType(string value)
    {
        var parts = value.Split('.');
        return parts.Length == 2 && (parts[0] == "SqlDbType" && Enum.GetNames<System.Data.SqlDbType>().Contains(parts[1],StringComparer.Ordinal)
            || parts[0] == "DbType" && Enum.GetNames<System.Data.DbType>().Contains(parts[1],StringComparer.Ordinal));
    }
    private static string? Signature(string? value, string method)
    {
        if (value is null || value.Length > 2048 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && !"._(), []<>".Contains(c))) return null;
        var index = value.IndexOf("." + method + "(",StringComparison.Ordinal);
        if (index < 0 || !value.EndsWith(')') || !DatabaseOperationApi.IsSyntaxType(value[..index],method)) return null;
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
    private static string? Assembly(string? value)
    {
        if (value is not { Length: < 256 }) return null;
        var match = System.Text.RegularExpressions.Regex.Match(value,
            @"^(?<name>System\.Data|System\.Data\.Common|System\.Data\.SqlClient|Microsoft\.Data\.SqlClient|Microsoft\.Data\.Sqlite), Version=\d+\.\d+\.\d+\.\d+, Culture=neutral, PublicKeyToken=(?<token>[a-f0-9]{16})$");
        var token = match.Groups["name"].Value switch
        {
            "System.Data" => "b77a5c561934e089",
            "System.Data.Common" or "System.Data.SqlClient" => "b03f5f7f11d50a3a",
            "Microsoft.Data.SqlClient" => "23ec7fc2d6eaa4a5",
            "Microsoft.Data.Sqlite" => "adb9793829ddae60",
            _ => null
        };
        return match.Success && match.Groups["token"].Value == token ? value : null;
    }
}
