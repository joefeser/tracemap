using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>Bounded framework API identities shared by C# and VB; never dispatch or execution proof.</summary>
internal static class OutboundHttpApi
{
    internal const string Limitations = "Static framework API call only; runtime execution, reachability, actual protocol, host, payload, response and success are unproven. Wrapper bodies are not specialized. Syntax and compiled-only coverage are separate; absence never means unused.";
    private static readonly HashSet<string> Client = new(StringComparer.Ordinal)
    { "GetAsync", "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync", "Send", "SendAsync" };
    private static readonly HashSet<string> Json = new(StringComparer.Ordinal)
    { "GetFromJsonAsync", "GetFromJsonAsAsyncEnumerable", "PostAsJsonAsync", "PutAsJsonAsync", "PatchAsJsonAsync" };
    private static readonly HashSet<string> Web = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Candidates = new(StringComparer.Ordinal);
    static OutboundHttpApi()
    {
        foreach (var name in new[] { "DownloadString", "DownloadData", "DownloadFile", "UploadString", "UploadData", "UploadFile", "UploadValues", "OpenRead", "OpenWrite" })
            foreach (var suffix in new[] { "", "Async", "TaskAsync" }) Web.Add(name + suffix);
        Candidates.UnionWith(Client);
        Candidates.UnionWith(Json);
        Candidates.UnionWith(Web);
        Candidates.UnionWith(["GetResponse", "GetResponseAsync", "BeginGetResponse", "EndGetResponse", "ReadFromJsonAsync"]);
    }
    internal static bool IsCandidate(string name, bool ignoreCase = false) => ignoreCase
        ? Candidates.Any(candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase)) : Candidates.Contains(name);

    internal static bool IsSyntaxCandidate(string type, string method) =>
        type.Equals("System.Net.Http.HttpClient", StringComparison.OrdinalIgnoreCase)
            && Client.Concat(Json).Contains(method, StringComparer.OrdinalIgnoreCase)
        || type.Equals("System.Net.Http.Json.HttpClientJsonExtensions", StringComparison.OrdinalIgnoreCase)
            && Json.Contains(method, StringComparer.OrdinalIgnoreCase)
        || type.Equals("System.Net.WebClient", StringComparison.OrdinalIgnoreCase)
            && Web.Contains(method, StringComparer.OrdinalIgnoreCase)
        || (type.Equals("System.Net.WebRequest", StringComparison.OrdinalIgnoreCase)
            || type.Equals("System.Net.HttpWebRequest", StringComparison.OrdinalIgnoreCase))
            && new[] { "GetResponse", "GetResponseAsync", "BeginGetResponse", "EndGetResponse" }
                .Contains(method, StringComparer.OrdinalIgnoreCase);

    private static bool Trusted(IAssemblySymbol assembly, string name, string token) => assembly.Identity.Name == name
        && Convert.ToHexString(assembly.Identity.PublicKeyToken.ToArray()).Equals(token, StringComparison.OrdinalIgnoreCase);
    private static string TypeName(ITypeSymbol type) => type.ContainingNamespace.ToDisplayString() + "." + type.Name;

    internal static bool TryClassify(IMethodSymbol method, out string family, out string verb)
    {
        family = string.Empty;
        verb = "UNKNOWN";
        var definition = method.ReducedFrom ?? method;
        var type = TypeName(definition.ContainingType);
        var assembly = definition.ContainingAssembly;
        var parameters = definition.Parameters;
        if (type == "System.Net.Http.HttpClient" && Trusted(assembly, "System.Net.Http", "b03f5f7f11d50a3a")
            && Client.Contains(method.Name) && parameters.Length > 0
            && TypeName(parameters[0].Type) is "string" or "String" or "System.String" or "System.Uri" or "System.Net.Http.HttpRequestMessage")
            family = "HttpClient";
        else if (type == "System.Net.Http.Json.HttpClientJsonExtensions" && Trusted(assembly, "System.Net.Http.Json", "cc7b13ffcd2ddd51")
            && Json.Contains(method.Name) && parameters.Length > 1 && TypeName(parameters[0].Type) == "System.Net.Http.HttpClient")
            family = "JsonHttpExtension";
        else if (type == "System.Net.WebClient" && Web.Contains(method.Name) && parameters.Length > 0
            && TypeName(parameters[0].Type) is "string" or "String" or "System.String" or "System.Uri"
            && VisualBasicSemanticExtractor.IsRecognizedWebClientAssemblyIdentity(assembly.Identity.Name, Convert.ToHexString(assembly.Identity.PublicKeyToken.ToArray())))
            family = "WebClient";
        else if (type is "System.Net.WebRequest" or "System.Net.HttpWebRequest"
            && method.Name is "GetResponse" or "GetResponseAsync" or "BeginGetResponse" or "EndGetResponse"
            && VisualBasicSemanticExtractor.IsRecognizedWebRequestAssemblyIdentity(assembly.Identity.Name, Convert.ToHexString(assembly.Identity.PublicKeyToken.ToArray())))
            family = "WebRequest";
        else return false;
        verb = method.Name.StartsWith("Get", StringComparison.Ordinal) && family != "WebRequest" ? "GET"
            : method.Name.StartsWith("Post", StringComparison.Ordinal) ? "POST"
            : method.Name.StartsWith("Put", StringComparison.Ordinal) ? "PUT"
            : method.Name.StartsWith("Delete", StringComparison.Ordinal) ? "DELETE"
            : method.Name.StartsWith("Patch", StringComparison.Ordinal) ? "PATCH"
            : family == "WebClient" && (method.Name.StartsWith("Download", StringComparison.Ordinal) || method.Name.StartsWith("OpenRead", StringComparison.Ordinal)) ? "GET" : "UNKNOWN";
        return true;
    }

    internal static void AddCallProperties(SortedDictionary<string, string> properties, IInvocationOperation? operation, string family, string verb)
    {
        properties["methodFamily"] = family;
        properties["httpMethod"] = verb;
        properties["coverageLabel"] = "bounded-static-http-call";
        properties["limitations"] = Limitations;
        properties["surfaceKind"] = "http-client";
        properties["urlKind"] = "unavailable";
        properties["protocolKind"] = family == "WebClient" ? "unproven-address-protocol" : "framework-http-api";
        if (operation is null) return;
        if (family == "WebRequest" && HttpRequestMethodEvidence.TryResolve(operation, out var resolvedVerb, out var assignment))
        {
            properties["httpMethod"] = resolvedVerb;
            properties["httpMethodEvidence"] = "same-block-local-property-assignment";
            var span = assignment!.Syntax.GetLocation().GetLineSpan();
            properties["httpMethodAssignmentStartLine"] = (span.StartLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            properties["httpMethodAssignmentEndLine"] = (span.EndLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        properties["targetAssemblyName"] = operation.TargetMethod.ContainingAssembly.Identity.Name;
        properties["targetAssemblyPublicKeyToken"] = Convert.ToHexStringLower(operation.TargetMethod.ContainingAssembly.Identity.PublicKeyToken.ToArray());
        var arguments = operation.Arguments.ToList();
        foreach (var argument in operation.Arguments)
            if (argument.Parameter?.Name == "request" && argument.Value is IObjectCreationOperation request
                && request.Constructor?.ContainingType.ToDisplayString() == "System.Net.Http.HttpRequestMessage"
                && Trusted(request.Constructor.ContainingAssembly, "System.Net.Http", "b03f5f7f11d50a3a"))
                arguments.AddRange(request.Arguments);
        foreach (var argument in arguments)
        {
            if (argument.Parameter?.Name == "method")
            {
                // Only a bounded set of literal verbs; never retain arbitrary private strings.
                if (argument.Value.ConstantValue is { HasValue: true, Value: string explicitVerb }
                    && new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT" }.Contains(explicitVerb.ToUpperInvariant(), StringComparer.Ordinal))
                    properties["httpMethod"] = explicitVerb.ToUpperInvariant();
                else if (argument.Value is IPropertyReferenceOperation property
                    && TypeName(property.Property.ContainingType) == "System.Net.Http.HttpMethod"
                    && Trusted(property.Property.ContainingAssembly, "System.Net.Http", "b03f5f7f11d50a3a")
                    && new[] { "Get", "Post", "Put", "Patch", "Delete", "Head", "Options", "Trace", "Connect" }.Contains(property.Property.Name, StringComparer.Ordinal))
                    properties["httpMethod"] = property.Property.Name.ToUpperInvariant();
                else properties["httpMethod"] = "UNKNOWN";
            }
            if (argument.Parameter?.Name is "requestUri" or "requestUriString" or "address")
            {
                var value = argument.Value;
                if (value is IObjectCreationOperation creation && TypeName(creation.Type!) == "System.Uri") value = creation.Arguments.FirstOrDefault()?.Value ?? value;
                if (value.ConstantValue is { HasValue: true, Value: string destination })
                {
                    properties["urlKind"] = "compile-time-constant-hash";
                    properties["urlHash"] = FactFactory.Hash(destination, 32);
                }
            }
        }
    }
}
