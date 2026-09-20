# Compiled .NET Evidence Foundation Tasks

The first implementation PR must remain within tasks 1-7. Tasks 8-11 are later
slices and require an implementation-state update before work begins.

- [ ] 1. Finalize the compiled input, assembly, member, reconciliation, and gap
  rule contracts in the rule catalog, including limitations and coverage labels.
- [ ] 2. Add public minimal C#, VB.NET, and F# fixture projects for assembly,
  type, full member-signature, nested/generic, generated-member, overload, and
  ambiguity cases; record stable case IDs and expected CLR shapes.
- [ ] 3. Add the bounded compiled-input policy and deterministic input-set
  digest, generator SHA-256, file digests, safe locators, and provenance states.
- [ ] 4. Implement assembly/module/type/member inventory with a pinned
  Mono.Cecil version; reject native, mixed-mode, unreadable, and over-budget
  inputs with explicit gaps.
- [ ] 5. Cross-check admitted normalized identities with
  `System.Reflection.Metadata`; emit disagreement gaps and withhold disputed
  facts so no later reconciliation can consume them.
- [ ] 6. Keep source and compiled facts separate; add missing, stale, ambiguous,
  unbound, mismatch, and unsupported-input gap tests without emitting a
  source-to-metadata identity edge.
- [ ] 7. Validate byte determinism, privacy, unchanged Roslyn/syntax behavior,
  CLI sample output, full .NET tests, macOS portable fixtures, and explicit
  Windows deferrals. Update docs and implementation state with exact results.
- [ ] 8. Later slice: add exact deterministic source-to-metadata reconciliation
  with zero/multiple-candidate gaps and both endpoint identities.
- [ ] 9. Later slice: add PDB identity and sequence-point contracts and portable
  versus Windows validation lanes.
- [ ] 10. Later slice: add operand-aware IL body/call evidence and the public
  ECMA-335/rewrite suite from #766.
- [ ] 11. Later slice: run the isolated Windows `dotnetperf`/C++/CLI lane from
  #768 and mine/minimize the optional stress corpus from #769.
