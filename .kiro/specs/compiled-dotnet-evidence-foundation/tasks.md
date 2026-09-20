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
- [ ] 7. Validate byte determinism, privacy, unchanged Roslyn/syntax behavior,
  CLI sample output, full .NET tests, and the portable managed fixture matrix on
  both macOS and Windows. Explicitly defer only the later Windows-specific
  lanes. Include host-resolution decoys, zero/multiple dependency candidates,
  metadata-location round trips, cross-namespace identity collisions,
  pre-digest recursion guards, and private-output non-shareability checks.
  Update docs and implementation state with exact results.
- [ ] 8. Later slice: add exact deterministic source-to-metadata reconciliation
  with zero/multiple-candidate gaps and both endpoint identities; consume the
  independent source canonical-identity matrix from `docs/VALIDATION.md`
  without replacing it with compiled fixtures.
- [ ] 9. Later slice: add PDB identity and sequence-point contracts and portable
  versus Windows validation lanes.
- [ ] 10. Later slice: add operand-aware IL body/call evidence and the public
  ECMA-335/rewrite suite from #766.
- [ ] 11. Later slice: validate legacy .NET Framework/Web Forms build behavior,
  run the isolated Windows `dotnetperf`/C++/CLI lane from #768, and
  mine/minimize the optional stress corpus from #769.
