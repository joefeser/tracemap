using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

internal static partial class IlSqlLifecycleExtractor
{
    internal sealed record ConstructorOwnerView(Site Call, string ReceiverAllocation,
        string DerivedOwnerSha256, string BaseOwnerSha256, string TargetBody, string TargetBinary,
        string Evidence = "dual-reader-declared-direct-base;inspected-nonvirtual-constructor;shared-this-allocation");
    internal sealed record FieldOwnerRejection(Site Site, string Opcode, string ReceiverAllocation,
        string AllocationOwnerSha256, string ExecutingOwnerSha256, string FieldIdentitySha256,
        IReadOnlyList<string> AdmittedOwnerSha256s, bool ExecutingConstructor, bool ReceiverIsThis,
        bool FieldDeclaredOnExecutingOwner, string StoredValueKind);

    internal sealed record FieldAccessRejection(Site Site, string MetadataToken, string Opcode,
        string ReceiverValueKind, string? ReceiverAllocation, string? ReceiverHeapKind,
        string? AllocationOwnerSha256, string ExecutingOwnerSha256, string FieldIdentitySha256,
        string? FieldDeclaredType, bool? FieldOwnerAdmitted, bool ReceiverIsThis,
        bool ExecutingConstructor, string StoredValueKind, IlObjectBodyAgreement? Agreement,
        IReadOnlyList<string> PrecedingOpcodes,
        string Limitation = "diagnostic-only;preceding-opcodes-are-not-value-provenance;field-operation-refused;no-bypass");

    private static string? DiagnosticFieldType(string identity)
    {
        var member = identity.IndexOf("|member:", StringComparison.Ordinal);
        if (member < 0) return null;
        var start = member + "|member:".Length;
        var colon = identity.IndexOf(':', start);
        if (colon < 0 || !int.TryParse(identity.AsSpan(start, colon - start),
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var length)
            || length < 0 || length >= identity.Length - colon) return null;
        var separator = colon + 1 + length;
        return separator < identity.Length && identity[separator] == '|' ? identity[(separator + 1)..] : null;
    }

    private sealed partial class Machine
    {
        internal readonly List<ConstructorOwnerView> OwnerViews = [];
        internal FieldOwnerRejection? OwnerFailure;
        internal FieldAccessRejection? FieldAccessFailure;

        private void AdmitConstructorOwnerView(Body source, IlObjectBody meta, Body target, IlObjectBody targetMeta,
            Value receiver, Value[] args, string opcode, Site site)
        {
            var state = Get(receiver, "custom");
            // Delegation to another constructor of the same already-admitted owner
            // does not change ownership. Unrelated types are not inferred compatible.
            if (state.FieldOwners.Contains(targetMeta.OwnerType)) return;
            if (opcode != "call" || meta.Name != ".ctor" || !meta.HasThis || args.Length == 0
                || receiver != args[0] || !state.FieldOwners.Contains(meta.OwnerType)
                || meta.BaseType is null || meta.BaseType != targetMeta.OwnerType
                || !targetMeta.HasThis || targetMeta.IsVirtual || targetMeta.IsGeneric
                || source.Observation.ObjectBodyAgreement is not { RawBody: true, CallShapes: true, ObjectBody: true }
                || target.Observation.ObjectBodyAgreement is not { RawBody: true, CallShapes: true, ObjectBody: true }
                || source.Input.Outcome != "admitted" || target.Input.Outcome != "admitted"
                || source.Input.GapKinds.Count != 0 || target.Input.GapKinds.Count != 0)
                throw new Refusal("lifecycle-constructor-owner-unproven", MetadataFailure(target, "constructor-owner"));
            if (state.FieldOwners.Count >= 24 || OwnerViews.Count >= 256 || !budget.TryConsume(1))
                throw new Refusal("lifecycle-constructor-owner-limit");
            state.FieldOwners.Add(targetMeta.OwnerType);
            OwnerViews.Add(new(site, receiver.Text, Hash(meta.OwnerType), Hash(targetMeta.OwnerType),
                target.Observation.BodyIdentity, target.Input.RawFileSha256!));
        }
    }
}
