# Compiled .NET Evidence Foundation Tasks

The first implementation PR must remain within tasks 1-7. Tasks 8-11 are later
slices and require an implementation-state update before work begins.

- [x] 1. Finalize the first-slice compiled input, assembly, member, and gap rule
  contracts in the rule catalog, including limitations and coverage labels;
  keep the later reconciliation rule inactive and non-emitting.
- [x] 2. Add public minimal C#, VB.NET, and F# fixture projects for assembly,
  type, full member-signature, nested/generic, generated-member, overload, and
  ambiguity cases, including identical simple type/member names in distinct
  metadata namespaces; record stable case IDs and expected CLR shapes.
- [x] 3. Add the bounded compiled-input policy and deterministic input-set
  digest, privacy-projected provenance-binding-input digest, generator SHA-256,
  local/private raw file digests, shareable privacy-projected-input digests,
  safe locators, expected-input declarations, effective limits, and provenance
  states. Commit every output-affecting admission-policy input to the local
  bounded-input digest through a canonical pre-digest payload that excludes the
  digest and all identities derived from it; attach the result afterward.
  Recompute shareable digests only over privacy-projected fields and never
  retain a raw private assembly digest. Emit unconditional
  manifest-level compiled-input provenance, including for a zero-admission
  scan, and bind the view-appropriate digest into `scanId` before fact IDs are
  derived. Keep private compiled fact/index outputs local-only; any shareable
  summary uses a distinct non-`CodeFact` schema and privacy-projected artifact
  identity.
- [x] 4. Implement assembly/module/type/member inventory with a pinned
  Mono.Cecil version; reject native, mixed-mode, unreadable, and over-budget
  inputs with explicit gaps. Disable ambient dependency probing and resolve
  only declared, admitted, hashed dependency inputs; bind resolution roots and
  ordered outcomes into the bounded-input digest.
- [x] 5. Cross-check admitted normalized identities with
  `System.Reflection.Metadata`; emit disagreement gaps and withhold disputed
  facts so no later reconciliation can consume them.
- [x] 6. Keep source and compiled facts separate; add missing, stale, ambiguous,
  unbound, mismatch, and unsupported-input gap tests without emitting a
  source-to-metadata identity edge. Preserve the mandatory scan repository and
  commit on every compiled fact while keeping optional receipt-validated binary
  source/build provenance in separate fields. Serialize metadata-only evidence
  with the `managed-metadata-v1` safe-locator/token convention and documented
  non-source `EvidenceSpan` sentinel.
- [x] 7. Validate byte determinism, privacy, unchanged Roslyn/syntax behavior,
  CLI sample output, full .NET tests, and the portable managed fixture matrix on
  both macOS and Windows. Explicitly defer only the later Windows-specific
  lanes. Include host-resolution decoys, zero/multiple dependency candidates,
  metadata-location round trips, cross-namespace identity collisions,
  pre-digest recursion guards, and private-output non-shareability checks.
  Update docs and implementation state with exact results.
- [x] 8. Later slice: add exact deterministic source-to-metadata reconciliation
  with zero/multiple-candidate gaps and both endpoint identities; consume the
  independent source canonical-identity matrix from `docs/VALIDATION.md`
  without replacing it with compiled fixtures.
- [x] 9. Later slice: add PDB identity and sequence-point contracts and portable
  versus Windows validation lanes.
- [ ] 10. Later slice: add operand-aware IL body/call evidence and the public
  ECMA-335/rewrite suite from #766.
- [ ] 11. Later slice: validate legacy .NET Framework/Web Forms build behavior,
  run the isolated Windows `dotnetperf`/C++/CLI lane from #768, and
  mine/minimize the optional stress corpus from #769.
  - [x] First bounded #768 runner, synthetic fail-closed guards, independent
    public ILAsm smoke, and C++/CLI feasibility inventory. Private corpus
    execution and #769 remain open.

- [x] 12. Bounded #767 continuation: add matched public C#/VB/F# CLR method
  signatures for overloads, ref/ByRef, generic arity and array rank, exact
  golden assertions with distinct endpoints/provenance, duplicate/malformed/
  bounded rejection and deterministic repeat coverage. See
  `docs/validation/CLR_SIGNATURE_EQUIVALENCE_MATRIX.md`; this does not close
  the broader language matrix or Tasks 10/11.

- [x] 13. Bounded #767 optional-parameter continuation: cross-check existing
  method/property optional ordinals independently, fix numeric ordering and
  sparse setter-only parameter selection, and pin C#/VB/F# marker evidence
  with positive, negative and reader-disagreement regressions. Default-value
  semantics, F# source extraction and broad #767 acceptance remain open.

- [x] 14. Bounded #767 source optional-marker continuation: prove numeric wide
  C#/VB source-to-bound-metadata joins with both endpoint evidence envelopes;
  reject noncanonical/mismatched markers and missing/ambiguous candidates,
  retain summary omission commitments, and include source reconciliation in
  portable cross-platform CI. No production sorting change was necessary.

- [x] 15. Bounded #767 nullable/option continuation: matched public C#/VB/F#
  Nullable<Int32> signature goldens, distinct F# option/voption and ?arg
  metadata, duplicate/malformed/member-limit/determinism tests, and explicit
  F# source-unsupported coverage. Pin the option dependency scope; runtime,
  default-value semantics and broader language/epic acceptance remain open.

- [x] 16. Bounded #767 property/event continuation: matched public C#/VB/F#
  property/event signatures and independent metadata-handle accessor oracles,
  getter-looking decoy, VB raiser distinction, duplicate/malformed/member-limit
  and repeatability regressions; preserve the F# source-unsupported gap.
  These tests do not introduce a production accessor association rule.

- [x] 17. Bounded #767 generic-constraint continuation: public C#/VB/F# free,
  reference, value, constructor and interface constraints; independent raw
  metadata oracles and exact single/combined-input method-token joins; preserve F# struct encoding
  differences, rejection/determinism coverage and unsupported-source boundary.
  Production constraint facts and full constraint equivalence remain open.

- [x] 18. Bounded #767 default-value continuation: public C#/VB/F# required,
  integer/string/null Constant-table defaults and decimal attribute defaults;
  independent raw/decoded oracles, exact token/provenance assertions, negative,
  ambiguous, malformed, bounded and repeat cases; F# source remains unsupported.
  Production default-value facts and complete default semantics remain open.

- [x] 19. Bounded #767 explicit-interface continuation: matched C#/VB/F#
  declarations/explicit implementations with an ordinary same-signature decoy;
  independent MethodImpl/override token oracles, single/combined fact assertions,
  language flag differences, duplicate/malformed/limit/repeat coverage, and
  retained F# unsupported-source boundary. Per-input hashes are checked against
  fixture bytes/outcomes, with cross-assembly corruption regressions and shared
  accessor/constraint/default coverage. Production dispatch edges remain open.

- [x] 20. Bounded #767 operator/conversion continuation: public C#/VB/F#
  addition, implicit/explicit conversions and ordinary op_-prefixed decoy;
  independent raw signature/token/flag oracles, assembly-scoped goldens,
  single/combined per-input provenance, rejection/repeat cases, bound C#/VB
  source joins and retained F# unsupported-source gap. F# decoy SpecialName
  distinction is explicit; runtime resolution/conversion behavior remains open.

- [x] 21. Bounded #767 module/currying continuation: public C# static class,
  VB Module and F# module, exact method identities/signatures and independent
  container/attribute oracles; F# argument groups and consumed compiled-name
  attributes remain distinct from CLR signature equality. Include single/
  combined provenance, rejection/repeat tests, bound C#/VB joins and explicit
  F# source gap. Pin retained CompilationSourceName aliases independently,
  exact source declaration/span oracles and active build configuration fixtures.
  Production module/currying relationships remain open.

- [x] 22. Bounded #767 record-generated-member continuation: selected C#/F#
  synthesized object overrides versus ordinary VB lookalikes, raw SRM/Cecil
  MethodDef/attribute oracles, exact single/combined identities and provenance,
  ambiguity/malformed/limit/repeat cases and explicit F# source-unsupported gap.
  Runtime record equality, full helper coverage and broader acceptance remain open.

- [x] 23. Bounded #767 union-factory continuation: F# type/case mappings and
  exact factory signatures versus ordinary C#/VB lookalikes, independent raw
  SRM and Cecil oracles with a pinned test-only enum dependency, single/combined
  provenance, negative/ambiguous/malformed/bounded/repeat coverage and F# source gap.
  Full union layout/helpers, runtime and quotation coverage remain open.

- [x] 24. Bounded #767 quotation/expression-tree continuation: public C#/VB
  expression trees, F# typed quotations and shared Func delegates; independent
  raw SRM/Cecil nested signature/scope/token oracles, exact single/combined
  provenance, duplicate/malformed/member-limit/repeat cases and F# source gap.
  Quotation contents/conversion/runtime and broader language acceptance remain open.

- [x] 25. Bounded #767 C#/VB receiver continuation: base/virtual and
  Me/MyBase/MyClass encoded targets, independent raw SRM/Cecil token/opcode
  oracles, exact metadata/body/call links and provenance, same-opcode/different-
  operand identity regression, per-input ambiguity, malformed-token/work-limit
  refusal and repeats. Runtime dispatch and broader source/PDB acceptance remain open.

- [x] 26. Bounded #767 C#/VB nested generic continuation: exact outer/inner/
  method generic ownership and positions, constructed argument order, nested
  total arity, independent raw SRM/Cecil oracles, single/combined provenance,
  per-input ambiguity, malformed/member-limit refusal and deterministic repeats.
  Source/PDB/IL/runtime and broader language/epic acceptance remain open.

- [x] 27. Bounded #767 C#/VB async/iterator PDB continuation: independent
  kickoff/attribute/token oracles, exact generated metadata/PDB/document/sequence
  identities and provenance, ordinary-name counterexample, missing/ambiguous
  method candidates, malformed/sequence-limit refusal and repeated scans.
  Runtime/rewrite, F# state machines and broad source acceptance remain open.

- [x] 28. Bounded #767 VB WithEvents/Handles versus explicit C# wiring:
  independent accessor/signature/flag and original-IL token oracles, exact
  metadata/body/call and ldftn identities/provenance, per-input ambiguity,
  wrong-table operand/work-limit gaps and repeatability. Runtime subscription,
  source/PDB/rewrite and broader language acceptance remain open.

- [x] 29. Bounded #767 F# cached event-wiring continuation: shared exact
  metadata/accessor/body/call checks, independent constructor/helper/OnTick
  identities selected by encoded tokens (including renamed generated members),
  cached-field operand assertions, duplicate/malformed/limit gaps
  and deterministic repeats. No F# source/PDB/runtime/rewrite equivalence claim.

- [x] 30. Bounded #767 C#/VB/F# collection continuation: ArrayList versus
  List<Object> indexer MethodDef/MemberRef signatures and scopes, generic !0
  return, unbox.any versus VB conversion-helper original IL; independent raw
  SRM/opcode/Cecil oracles, exact endpoint/provenance checks, same-opcode/different-
  operand hashes, duplicate/malformed/work-limit gaps and deterministic repeats.
  Source/PDB/rewrite/runtime, late binding and broader language acceptance remain open.

- [x] 31. Bounded #767 VB late-binding continuation: exact LateGet helper
  MemberRef versus typed MethodDef, independent raw SRM/opcode/Cecil signatures,
  scopes and string operands; source-gap spans/tiers, full compiled provenance,
  string-only mutation, duplicate/malformed/work-limit gaps and repeats.
  Runtime receiver/overload resolution, other helpers, source-binding/PDB/rewrite
  joins and broader language acceptance remain open.

- [x] 32. Bounded #767 C# ref-like signature continuation: Span/ReadOnlySpan
  constructions, mutable/readonly by-reference return/parameter signatures,
  independent SRM/Cecil flags/attribute/modifier checks, exact single/combined
  provenance, modreq/modopt/absent counterexamples, duplicate/malformed/member-limit
  gaps and deterministic repeats. Lifetime/runtime, external type classification,
  source/PDB/IL/rewrite and broader language acceptance remain open.


- [x] 33. Bounded #767 VB imports continuation: project namespace, file alias
  and SDK default imports; independent SRM/Cecil signatures/scopes/tokens,
  hand-authored source declarations, exact joins/spans/provenance, same-name
  substitution rejection, unbound/duplicate/malformed/member-limit gaps and
  deterministic repeats. Compiler import conflicts/configuration variants,
  PDB/IL/rewrite/runtime and broader language acceptance remain open.
