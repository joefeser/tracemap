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
