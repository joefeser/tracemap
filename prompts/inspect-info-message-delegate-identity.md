# Inspect the existing InfoMessage delegate identity — no rebuild or scan

Read AGENTS.md. This is a bounded read-only inspection of the existing compiled-only
v9-004 diagnostic output and its retained local inputs. Do not build, scan, restore,
edit implementation/private sources, install tools, run the application, execute
SQL, launch subagents, or broaden inputs. Preserve all existing output and receipts.

The completed compiled-only run reached lifecycle analysis and reported
`lifecycle-info-function-escape` for both selected inner roots. The diagnostic says
an adjacent newobj is present but its identity differs from the catalog. We need
the actual mismatch, not another capability guess. Use the existing local receipt
paths; do not invent private paths, source names, or metadata values.

1. Inspect the public tool's exact current DelegateCtor construction in
   src/dotnet/TraceMap.Core/IlSqlInfoMessageBoundary.cs and the adjacent ldftn/newobj
   check in IlSqlLifecycleExtractor.cs. Retain the expected identity verbatim.
2. Read only the existing lifecycle gap and retained failing constructor call
   evidence from the v9-004 output. Bound inspection to those two selected roots,
   the one failing constructor, and its adjacent ldftn/newobj pair. Use the actual
   invocation/body provenance; do not infer reachability from corpus call facts.
3. Extract the actual newobj TargetIdentity, ReferenceKind and StackShape fields.
   Report the exact declaring framework assembly name/version/culture/public key
   token, declaring type, method name, calling convention, hasThis/explicitThis,
   generic arity, parameter identities and return identity. Distinguish encoded
   primitive IntPtr from scoped System.IntPtr type references. Do not normalize.
4. Compare expected and observed identities field by field. If the declaring type
   is a public signed framework delegate, return its FULL actual encoded identity
   verbatim (public framework identifiers only), expected identity, and a minimal
   diff. Do not disclose private target/owner/member names, hashes, paths or code.
   Alias private callback and body identifiers. If the identity contains private
   identifiers, provide only the differing public framework components plus the
   categorical private component position.
5. Verify body/call metadata retention and dual-reader agreement from existing
   evidence. Report unknown if unavailable. Do not infer an assembly version or
   primitive encoding from display text. If the existing output lacks the required
   call evidence, say exactly which fields are absent and STOP; no new scan.

Return at most 25 sanitized text lines: tool SHA from receipt, observed gap,
expected/actual PUBLIC framework identity (or safely projected components), exact
mismatch, opcode/relative adjacency, call stack shape, reader agreement, and
whether both roots share the same constructor. Explicitly separate observed
metadata from proposed fixes. No private source, SQL or full receipt transfer.
STOP after this one report. Entry lineage remains unproven.
