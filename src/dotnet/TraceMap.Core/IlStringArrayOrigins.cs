using System.Text.Json;

namespace TraceMap.Core;

// Snapshot only at an exact String.Concat(String[]) call. Never a general heap value.
internal static class IlStringArrayOrigins
{
    internal const int MaxArrays = 16;
    internal const int MaxElements = 16;
    internal const string Kind = "string-array-elements";
    internal static bool IsStringType(string identity) => new[] { "2.0.0.0", "4.0.0.0" }.Any(version =>
        identity == $"t:scope(assembly:name:8:mscorlib|version:7:{version}|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)type(namespace:6:System|names:6:String)");

    internal static bool IsConcat(string identity) => new[] { "2.0.0.0", "4.0.0.0" }.Any(version =>
        identity == $"memberref|type:scope(assembly:name:8:mscorlib|version:7:{version}|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)type(namespace:6:System|names:6:String)|member:6:Concat|arity:0|call:default|hasThis:false|explicitThis:false|(type(namespace:6:System|names:6:String)[])->type(namespace:6:System|names:6:String)");

    internal static bool IsElement(IlValueOrigin value) => value.Identity.Length <= 1024
        && value.Kind is "constant-string-hash" or "argument-slot" or "call-result" or "null";

    internal static IlValueOrigin Snapshot(IlValueOrigin?[] elements) =>
        new(Kind, JsonSerializer.Serialize(elements));
}
