using System.Text.Json;

namespace TraceMap.Core;

// Flat, canonical and bounded. Unknown is the top element: alternatives never
// recover precision after a limit, unsupported origin or missing predecessor.
internal static class IlArgumentAlternatives
{
    internal const int MaxAlternatives = 4;
    internal const int MaxEncodedLength = 768;
    internal static IEnumerable<IlValueOrigin> Members(IlValueOrigin value) => Expand(value);
    internal static IlValueOrigin Join(IlValueOrigin left, IlValueOrigin right)
    {
        if (left == right) return left;
        var values = Expand(left).Concat(Expand(right)).Distinct()
            .OrderBy(value => value.Kind, StringComparer.Ordinal).ThenBy(value => value.Identity, StringComparer.Ordinal).ToArray();
        if (values.Length > MaxAlternatives || values.Any(value => !Atom(value))) return new("unknown", "");
        var encoded = JsonSerializer.Serialize(values);
        return encoded.Length <= MaxEncodedLength ? new("argument-alternatives", encoded) : new("unknown", "");
    }
    private static bool Atom(IlValueOrigin value) => value.Kind is "argument-slot" or "call-result" or "constant-string-hash" or "constant-int32" or "null"
        && value.Identity.Length <= 128;
    private static IlValueOrigin[] Expand(IlValueOrigin value)
    {
        if (value.Kind != "argument-alternatives") return [value];
        if (value.Identity.Length > MaxEncodedLength) return [new("unknown", "")];
        try { return JsonSerializer.Deserialize<IlValueOrigin[]>(value.Identity) ?? [new("unknown", "")]; }
        catch (JsonException) { return [new("unknown", "")]; }
    }
}
