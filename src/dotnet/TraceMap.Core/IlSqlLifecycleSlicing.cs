using System.Globalization;
using Mono.Cecil;
using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

internal sealed record IlLifecycleRootSelection(string BinarySha256, string MetadataToken)
{
    internal static IReadOnlyList<IlLifecycleRootSelection> Parse(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return [];
        if (values.Count > 16) throw new ArgumentException("il-lifecycle-root-limit");
        var roots = new List<IlLifecycleRootSelection>();
        foreach (var value in values)
        {
            var parts = value?.Split(':', 2);
            if (parts is not { Length: 2 } || parts[0].Length != 64 || !parts[0].All(char.IsAsciiHexDigit)
                || parts[1].Length != 10 || !parts[1].StartsWith("0x06", StringComparison.OrdinalIgnoreCase)
                || !uint.TryParse(parts[1][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var token)
                || (token & 0x00ffffff) == 0)
                throw new ArgumentException("il-lifecycle-root-format-sha256-methoddef-token");
            roots.Add(new(parts[0].ToLowerInvariant(), ManagedMetadataExtractor.Token(token)));
        }
        return roots.Distinct().OrderBy(root => root.BinarySha256, StringComparer.Ordinal)
            .ThenBy(root => root.MetadataToken, StringComparer.Ordinal).ToArray();
    }
}

internal sealed record IlLifecycleSliceMember(string Binary, string MetadataToken, string Body,
    string State, IlObjectBodyRetention? Retention, IlObjectBodyAgreement? Agreement);
internal sealed record IlLifecycleSliceReceipt(string Schema, string GeneratorSha256, string BoundedInputSha256,
    string Visibility, string Coverage, IlLifecycleRootSelection Root, long RetainedCharacterCapPerReader,
    long CecilCharacters, long SrmCharacters, long DecodeWorkBefore, long DecodeWorkAfter, long DecodeWorkCap,
    long VerifiedInputBytes, int? ParameterlessConstructorDefinitions, int UnresolvedReferenceCount,
    IReadOnlyList<string> UnresolvedReferenceDigests, IReadOnlyList<IlLifecycleSliceMember> Members, string? Failure);

/// <summary>Explicit root-local metadata lease. No corpus-order quota inheritance or one-reader fallback.</summary>
internal sealed class IlSqlLifecycleSlice : IDisposable
{
    internal const int MaxBodies = 128;
    internal const long MaxInputBytes = 67_108_864;
    private readonly IlBodyEvaluation evaluation;
    private readonly IlBodyLimits limits;
    private readonly IlBodyEvidenceExtractor.IlWorkBudget decode;
    private readonly IlBodyEvidenceExtractor.IlWorkBudget cecilObjects;
    private readonly IlBodyEvidenceExtractor.IlWorkBudget srmObjects;
    private readonly Dictionary<string, Body[]> methods;
    private readonly Dictionary<string, Body[]> tokens;
    private readonly Dictionary<string, Body> retained = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InputModule> modules = new(StringComparer.Ordinal);
    private readonly Queue<Body> pending = new();
    private readonly HashSet<string> queued = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Body[]> references = new(StringComparer.Ordinal);
    private readonly List<string> unresolved = [];
    private readonly IlLifecycleRootSelection selection;
    private readonly long workBefore;
    private long verifiedBytes;
    private int? constructorDefinitions;
    private string? failure;
    internal Body Root { get; private set; }
    internal IReadOnlyList<Body> Bodies => retained.Values.ToArray();
    internal string? Failure => failure;

    private sealed record InputModule(byte[] Bytes, ModuleDefinition Module,
        ManagedMetadataExtractor.RejectingAssemblyResolver Resolver) : IDisposable
    {
        public void Dispose() { Module.Dispose(); Resolver.Dispose(); }
    }
    private sealed class SliceFailure(string reason) : Exception(reason);
    private static string Key(Body body) => body.Input.RawFileSha256 + ":" + body.Observation.MetadataToken;

    internal IlSqlLifecycleSlice(IlBodyEvaluation evaluation, IReadOnlyList<Body> corpus, Body root,
        IlBodyEvidenceExtractor.IlWorkBudget decode)
    {
        this.evaluation = evaluation;
        limits = evaluation.Provenance!.EffectiveLimits;
        this.decode = decode;
        workBefore = decode.Consumed;
        selection = new(root.Input.RawFileSha256!, root.Observation.MetadataToken);
        Root = root;
        var cap = Math.Min(limits.MaxTotalWorkUnits, 1_048_576);
        cecilObjects = new(cap); srmObjects = new(cap);
        if (corpus.Count > 4096)
        {
            methods = new(StringComparer.Ordinal); tokens = new(StringComparer.Ordinal);
            failure = "lifecycle-slice-index-limit"; return;
        }
        methods = corpus.GroupBy(body => body.Observation.MethodIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        tokens = corpus.GroupBy(Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        try
        {
            if (corpus.Count > 4096) throw new SliceFailure("lifecycle-slice-index-limit");
            Spend(corpus.Count);
            Enqueue(root);
            while (pending.TryDequeue(out var original))
            {
                evaluation.SliceCancellationToken.ThrowIfCancellationRequested();
                var loaded = Load(original);
                retained.Add(Key(original), loaded);
                if (Key(original) == Key(root)) Root = loaded;
                if (loaded.Observation.ObjectBody is null) continue;
                if (Key(original) == Key(root) && loaded.Observation.ObjectBody.HasThis)
                    AddRootConstructor(loaded);
                foreach (var call in loaded.Observation.Calls)
                {
                    Spend(1);
                    if (IlSqlObjectPathExtractor.KnownFrameworkApi(call.TargetIdentity) is not null
                        || IlSqlLifecycleApis.Resolve(call.TargetIdentity, []) is not null) continue;
                    foreach (var target in Resolve(call.TargetIdentity)) Enqueue(target);
                }
            }
        }
        catch (SliceFailure error) { failure = error.Message; }
        catch (IlBodyEvidenceExtractor.IlEvidenceException error) { failure = "lifecycle-slice-" + error.GapKind; }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException
            or NotSupportedException or FormatException or OverflowException or BadImageFormatException or IndexOutOfRangeException)
        { failure = "lifecycle-slice-decode-unavailable"; }
    }

    private void Spend(long units)
    {
        if (!decode.TryConsume(units)) throw new SliceFailure("lifecycle-slice-decode-work-limit");
    }
    private void Enqueue(Body body)
    {
        if (!queued.Add(Key(body))) return;
        if (queued.Count > MaxBodies) throw new SliceFailure("lifecycle-slice-body-limit");
        pending.Enqueue(body);
    }
    private Body Unique(string binary, string token)
    {
        if (!tokens.TryGetValue(binary + ":" + token, out var matches) || matches.Length != 1)
            throw new SliceFailure("lifecycle-slice-body-not-uniquely-admitted");
        return matches[0];
    }
    private InputModule Module(Body body)
    {
        var sha = body.Input.RawFileSha256!;
        if (modules.TryGetValue(sha, out var cached)) return cached;
        if (body.Input.Outcome != "admitted" || body.Input.GapKinds.Count != 0)
            throw new SliceFailure("lifecycle-slice-input-not-admitted");
        var artifacts = evaluation.BindingArtifacts.Where(artifact => artifact.RawFileSha256 == sha
            && artifact.SafeLocator == body.Input.SafeLocator && artifact.Outcome == "admitted").Take(2).ToArray();
        if (artifacts.Length != 1) throw new SliceFailure("lifecycle-slice-source-ambiguous");
        var remaining = MaxInputBytes - verifiedBytes;
        if (remaining <= 0) throw new SliceFailure("lifecycle-slice-input-byte-limit");
        var bytes = PortablePdbExtractor.ReadVerifiedCompiledBytes(artifacts[0].FullPath, sha,
            Math.Min(evaluation.CompiledMaximumBytes, remaining), evaluation.SliceCancellationToken);
        if (bytes is null) throw new SliceFailure("lifecycle-slice-input-changed-or-byte-limit");
        var resolver = new ManagedMetadataExtractor.RejectingAssemblyResolver();
        ModuleDefinition module;
        try
        {
            module = ModuleDefinition.ReadModule(new MemoryStream(bytes, writable: false), new ReaderParameters
                { AssemblyResolver = resolver, InMemory = true, ReadSymbols = false, ReadingMode = ReadingMode.Deferred });
        }
        catch { resolver.Dispose(); throw; }
        if (!string.Equals(module.Mvid.ToString("D"), body.Input.ModuleMvid, StringComparison.OrdinalIgnoreCase))
        { module.Dispose(); resolver.Dispose(); throw new SliceFailure("lifecycle-slice-module-changed"); }
        cached = new(bytes, module, resolver);
        verifiedBytes += bytes.LongLength;
        modules.Add(sha, cached);
        return cached;
    }
    private Body Load(Body original)
    {
        var input = Module(original);
        var requested = new HashSet<string>(StringComparer.Ordinal) { original.Observation.MetadataToken };
        var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(input.Bytes, limits, decode,
            evaluation.SliceCancellationToken, valueBudget: new(0), objectBudget: srmObjects, selectedMethods: requested);
        var left = IlBodyEvidenceExtractor.ReadCecilBodies(input.Bytes, limits, decode,
            evaluation.SliceCancellationToken, valueBudget: new(0), objectBudget: cecilObjects, selectedMethods: requested);
        if (IlBodyEvidenceExtractor.CompareBodies(left, right).Count != 0 || left.Bodies.Count != 1
            || !IlBodyEvidenceExtractor.RawBodiesAgree(original.Observation, left.Bodies[0]))
            throw new SliceFailure("lifecycle-slice-reader-or-baseline-disagreement");
        var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies).Single();
        if (agreed.ObjectBody is not null && original.MemberReferenceAlias is not null
            && agreed.ObjectBody.MemberReference != original.MemberReferenceAlias)
            throw new SliceFailure("lifecycle-slice-member-reference-conflict");
        return original with { Observation = agreed };
    }
    private void AddRootConstructor(Body root)
    {
        var input = Module(root);
        var token = uint.Parse(root.Observation.MetadataToken[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (input.Module.LookupToken(new MetadataToken(token)) is not MethodDefinition method)
            throw new SliceFailure("lifecycle-slice-root-method-unavailable");
        var constructors = new List<string>();
        foreach (var candidate in method.DeclaringType.Methods)
        {
            Spend(1);
            if (candidate.Name == ".ctor" && candidate.HasThis && candidate.Parameters.Count == 0 && candidate.HasBody)
                constructors.Add(ManagedMetadataExtractor.Token(candidate.MetadataToken.ToUInt32()));
        }
        constructorDefinitions = constructors.Count;
        if (constructors.Count > 2) throw new SliceFailure("lifecycle-slice-constructor-ambiguity");
        foreach (var ctor in constructors) Enqueue(Unique(root.Input.RawFileSha256!, ctor));
    }
    private Body[] Resolve(string identity)
    {
        if (references.TryGetValue(identity, out var cached)) return cached;
        if (references.Count >= 4096) throw new SliceFailure("lifecycle-slice-reference-limit");
        if (methods.TryGetValue(identity, out var direct))
        { references.Add(identity, direct); return direct; }
        const string memberPrefix = "memberref|type:scope(";
        var assembly = identity.StartsWith(memberPrefix, StringComparison.Ordinal)
            ? AssemblyPrefix(identity, memberPrefix.Length) : null;
        var matches = new List<Body>();
        if (assembly is not null)
        {
            foreach (var source in tokens.Values.Select(items => items[0]).GroupBy(body => body.Input.RawFileSha256)
                .Select(group => group.First()).OrderBy(body => body.Input.RawFileSha256, StringComparer.Ordinal))
            {
                Spend(1);
                if (AssemblyPrefix(source.Input.AssemblyIdentity ?? "", 0) != assembly) continue;
                var input = Module(source);
                foreach (var type in ManagedMetadataExtractor.FlattenTypes(input.Module.Types))
                    foreach (var method in type.Methods)
                    {
                        Spend(1);
                        if (!method.HasBody) continue;
                        var reference = IlBodyEvidenceExtractor.ObjectMemberReference(method);
                        if (reference.Length > limits.MaxTextLength) continue;
                        if (reference != identity) continue;
                        var body = Unique(source.Input.RawFileSha256!, ManagedMetadataExtractor.Token(method.MetadataToken.ToUInt32()));
                        matches.Add(body with { MemberReferenceAlias = identity });
                    }
            }
        }
        cached = matches.ToArray();
        references.Add(identity, cached);
        if (cached.Length == 0) unresolved.Add(ManagedMetadataExtractor.CanonicalDigest(identity));
        return cached;
    }
    private static string? AssemblyPrefix(string value, int start)
    {
        var position = start;
        foreach (var label in new[] { "assembly:name:", "|version:", "|culture:", "|publicKeyToken:" })
        {
            if (position > value.Length || !value.AsSpan(position).StartsWith(label, StringComparison.Ordinal)) return null;
            position += label.Length;
            var colon = value.IndexOf(':', position);
            if (colon < 0 || !int.TryParse(value.AsSpan(position, colon - position), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || length < 0 || length > value.Length - colon - 1) return null;
            position = colon + 1 + length;
        }
        return value[start..position];
    }
    internal IlLifecycleSliceReceipt Receipt()
    {
        var members = retained.Values.Select(body => new IlLifecycleSliceMember(body.Input.RawFileSha256!,
            body.Observation.MetadataToken, body.Observation.BodyIdentity, body.Observation.ObjectBody is null ? "unavailable" : "retained",
            body.Observation.ObjectBodyRetention, body.Observation.ObjectBodyAgreement)).ToArray();
        var inputHash = ManagedMetadataExtractor.CanonicalDigest(new { schema = "il-sql-lifecycle-slice/1", selection,
            limits, members, maxBodies = MaxBodies, maxInputBytes = MaxInputBytes, verifiedBytes, constructorDefinitions,
            unresolved, retainedCharacterCapPerReader = cecilObjects.Maximum, decodeWorkCap = decode.Maximum,
            workBefore, workAfter = decode.Consumed, failure });
        return new("il-sql-lifecycle-slice/1", evaluation.Provenance!.GeneratorSha256, inputHash,
            "local-only", "selected-root-slice-only", selection, cecilObjects.Maximum, cecilObjects.Consumed, srmObjects.Consumed,
            workBefore, decode.Consumed, decode.Maximum, verifiedBytes, constructorDefinitions, unresolved.Count,
            unresolved.Take(128).ToArray(), members, failure);
    }
    public void Dispose()
    {
        foreach (var input in modules.Values) input.Dispose();
        modules.Clear();
    }
}
