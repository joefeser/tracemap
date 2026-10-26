using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>Public signed API declaration evidence; no provider dispatch or execution inference.</summary>
internal static class DatabaseOperationApi
{
    internal const string Limitations = "Static API operation evidence only; runtime execution, actual use, concrete factory dispatch, database identity and effects are unproven. Mutable command state, parameters and transactions require separate witnesses. Local state is bounded to 256 statements, 1024 operations per statement and 4096 total operations; limit exhaustion leaves state unavailable. Generic wrapper bodies are not specialized. No connection never means unused. SQL/procedure text is not reconstructed from hashes.";
    internal static string Kind(string name) => name.ToLowerInvariant() switch
    {
        "executereader" or "executereaderasync" or "beginexecutereader" or "endexecutereader" => "select-candidate",
        "executescalar" or "executescalarasync" => "scalar-candidate",
        "executenonquery" or "executenonqueryasync" or "beginexecutenonquery" or "endexecutenonquery" => "execute-candidate",
        "executexmlreader" or "executexmlreaderasync" or "beginexecutexmlreader" or "endexecutexmlreader" => "xml-reader-candidate",
        "fill" => "data-adapter-fill", "fillschema" => "data-adapter-schema", "update" => "data-adapter-update",
        "writetoserver" or "writetoserverasync" => "bulk-write-boundary", _ => string.Empty
    };
    private static string TypeName(ITypeSymbol type) => type.ContainingNamespace + "." + type.Name;
    internal static bool Trusted(IAssemblySymbol assembly)
    {
        var name = assembly.Identity.Name;
        var token = Convert.ToHexStringLower(assembly.Identity.PublicKeyToken.ToArray());
        return (name == "System.Data" && token == "b77a5c561934e089")
            || (name == "System.Data.Common" && token == "b03f5f7f11d50a3a")
            || (name == "System.Data.SqlClient" && token == "b03f5f7f11d50a3a")
            || (name == "Microsoft.Data.SqlClient" && token == "23ec7fc2d6eaa4a5")
            || (name == "Microsoft.Data.Sqlite" && token == "adb9793829ddae60");
    }
    internal static bool IsSyntaxType(string type, string name)
    {
        var kind = Kind(name); if (kind.Length == 0) return false;
        return kind.StartsWith("data-adapter", StringComparison.Ordinal)
            ? type is "System.Data.Common.DataAdapter" or "System.Data.Common.DbDataAdapter" or "System.Data.IDataAdapter" or "System.Data.SqlClient.SqlDataAdapter" or "Microsoft.Data.SqlClient.SqlDataAdapter"
            : kind == "bulk-write-boundary"
                ? type is "System.Data.SqlClient.SqlBulkCopy" or "Microsoft.Data.SqlClient.SqlBulkCopy"
                : type is "System.Data.Common.DbCommand" or "System.Data.IDbCommand" or "System.Data.SqlClient.SqlCommand" or "Microsoft.Data.SqlClient.SqlCommand" or "Microsoft.Data.Sqlite.SqliteCommand";
    }
    internal static bool TryClassify(IMethodSymbol method, out string kind, out string result)
    {
        while (method.OverriddenMethod is { } overridden) method = overridden;
        kind = Kind(method.Name); result = kind switch
        { "select-candidate" => "data-reader", "scalar-candidate" => "scalar", "xml-reader-candidate" => "xml-reader", "data-adapter-schema" => "schema-container", "data-adapter-fill" => "data-container", "bulk-write-boundary" => "none", _ => "row-count-or-none" };
        return method.DeclaredAccessibility == Accessibility.Public && Trusted(method.ContainingAssembly)
            && IsSyntaxType(TypeName(method.ContainingType), method.Name);
    }
    internal static void AddProperties(SortedDictionary<string,string> properties, IInvocationOperation? call)
    {
        properties["limitations"] = Limitations;
        properties["commandTextAvailability"] = "unavailable";
        properties["commandTypeEvidence"] = "unavailable";
        properties["parameterEvidence"] = "separate-witness-required";
        properties["transactionEvidence"] = "separate-witness-required";
        properties["connectionEvidence"] = "unavailable";
        if (call is null) return;
        var method = call.TargetMethod;
        properties["targetAssemblyName"] = method.ContainingAssembly.Identity.Name;
        properties["targetAssemblyPublicKeyToken"] = Convert.ToHexStringLower(method.ContainingAssembly.Identity.PublicKeyToken.ToArray());
        properties["originalSignature"] = method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        properties["providerAssemblyIdentity"] = method.ContainingAssembly.Identity.ToString();
        properties["operationForm"] = method.Name.StartsWith("Begin", StringComparison.Ordinal) ? "apm-begin"
            : method.Name.StartsWith("End", StringComparison.Ordinal) ? "apm-end"
            : method.Name.EndsWith("Async", StringComparison.Ordinal) ? "async" : "sync";
        properties["hasCancellationToken"] = method.Parameters.Any(p => TypeName(p.Type) == "System.Threading.CancellationToken") ? "true" : "false";
        properties["dispatchBoundary"] = method.ContainingType.TypeKind == TypeKind.Interface ? "interface"
            : method.IsAbstract ? "abstract" : "declared-api";
        DatabaseCommandState.Add(properties, call);
        SqlServerCommandParameters.Add(properties, call);
        // Separate first-result reader consumer state; never derived from parameter state.
        if (Kind(method.Name) == "select-candidate") SqlServerReaderColumns.Add(properties, call);
        // Separate Fill -> DataTable/DataRow consumer state; never derived from reader or parameter state.
        if (Kind(method.Name) == "data-adapter-fill")
        {
            SqlServerDataTableConsumers.Add(properties, call);
            // Sibling Fill(DataSet) -> first-table consumer state; each property refuses the other's overloads.
            SqlServerDataSetConsumers.Add(properties, call);
        }
        // Only inline construction is local to this call. Mutable receiver history is
        // retained elsewhere; it must not silently become current command state.
        var instance = call.Instance;
        while (instance is IConversionOperation or IParenthesizedOperation)
            instance = instance is IConversionOperation conversion ? conversion.Operand : ((IParenthesizedOperation)instance).Operand;
        if (instance is IObjectCreationOperation creation)
        {
            if (creation.Arguments.Any(a => a.Parameter?.Name == "connection")) properties["connectionEvidence"] = "constructor-argument-witness";
            var text = creation.Arguments.FirstOrDefault(a => a.Parameter?.Name is "commandText" or "cmdText");
            if (text?.Value.ConstantValue is { HasValue:true, Value:string value })
            {
                properties["commandTextAvailability"] = "hash-only-constant";
                properties["commandTextHash"] = FactFactory.Hash(value, 64);
            }
            else if (text is not null) properties["commandTextAvailability"] = "dynamic";
        }
    }
}
