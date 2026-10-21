# GLM reporting and HTTP evidence triage

Review the reporting-stability slice and HTTP findings without changing code,
merging branches, or interrupting your ongoing ECMA-335 implementation. The open
upgrade-authority work is separate. This is triage, not an ECMA compliance verdict.

First record the exact dev SHA, your active branch SHA, and the SHA or working
diff you reviewed. Read `docs/validation/REPORT_EVIDENCE_STABILITY.md` and inspect
the implementation and tests rather than accepting the summary as proof.

## Questions to answer

1. Does representative selection now remain stable when fact/scan IDs and input
   ordering change? Check full, bounded-single and bounded-combined readers,
   SQLite ordering, declaration preference, evidence tiers and supporting facts.
   Does single-index compaction preserve the same chosen witness?
2. Could the change alter path classification or hide a stronger/weaker witness
   incorrectly? Distinguish a representative display field from edge evidence
   and retained fact inventory. Do not solve this by taking the strongest tier.
3. Does the larger ECMA branch change these code paths or invalidate any baseline
   assumptions? Classify concrete conflicts separately from future enhancements.
4. In `samples/http-entry-inheritance-csharp`, confirm the direct System.Web
   override witness, absent semantic method/property declarations, and missing
   entry joins. Also inspect WebFormsAssemblyRegistrationResolver.Create: its
   TypeFactId ordering selected a syntax type candidate despite a retained
   semantic declaration in the direct-control example, causing a receiver gap.
   Test ID permutations within the same exact declaration/assembly identity;
   do not bridge merely similar names. Which changes belong in extraction versus
   reporting, and which extractor versions would need increments?
5. Propose the smallest next HTTP slice: C# declaration parity first, then bounded
   inherited getters/intermediary overrides, then template instance scope.
   Preserve refusal tests for ambiguous receivers, wrong assemblies, Bind/Eval,
   indexed/chained expressions and same-name non-framework methods.

## Evidence and output

Use the focused test command in the validation note and inspect available full
Debug results. A prior passing count is not a current-head result. The public
corpus diagnostic must not execute downloaded builds, scripts, HTTP requests or
database operations. Private application artifacts must stay off this branch.

Return a compact table with finding, confirmed/hypothesis status, file and line,
impact, overlap with your active work, and recommended next action. Include
test commands/outcomes, exact SHAs, any missing evidence, and your verdict on
whether this slice can proceed independently. Do not infer full extraction or
ECMA compliance from two samples, equal path counts or green focused tests.
