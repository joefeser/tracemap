# Validate semantic WebForms declaration consumers

On the Windows work machine, update the existing clean
`codex/evidence-admission-hardening` checkout with a fast-forward pull. Preserve
unrelated files. Record the tool repository HEAD separately from the private
application commit. Use the installed .NET 10 SDK; security patch differences
are not failures. Do not change application sources, references, approved
compilation options, source membership, or analysis budgets.

Build the test project and run the exact Compilation context and interaction
gate command in `.github/workflows/vb-compilation-context-matrix.yml`, with its
two-minute hang timeout. Run `LegacyWebFormsExtractorTests` as well. Report
passed/failed/skipped counts. This change adds twelve semantic registration
matrix cases. Confirm `legacy-webforms/0.13.12` in the new scan.

Reuse the already approved compilation context and inputs. If their hashes or
application commit changed, stop and report the mismatch; do not silently
regenerate approval. Use a new output/receipt location and the existing fresh
scan workflow. Run at most one fresh scan of the same selected page and scope
as the last explicit-context scan. Do not compare this selected packet against
the older whole-application gap denominator.

Compare retained evidence, not just total counts:

1. Namespace-only App_Code registration candidates: list aggregate counts of
   restored candidates, unresolved registrations and ambiguous declarations.
   Each candidate must retain registration and exact source declaration fact
   references. Candidates remain Tier3 even when supported by Tier1 types;
   no assembly binding or control ancestry is established by this join.
2. Inline-server-reference candidates: compare the same markup sites, supporting
   declaration references and Tier3 labels. Missing candidates need a specific
   reason; do not assume all earlier counts must return.
3. Separately report the eight handler chains, their semantic/syntax evidence,
   supported terminal counts, database-operation facts, parameter facts and
   batch-data-movement candidates. This patch does not claim to repair missing
   database terminals. If operations remain zero, inspect existing retained
   facts at the expected source sites and classify whether they were not emitted,
   excluded by the query, or disconnected from the selected handlers. Do not
   rerun the scan to guess, invent bindings, or suppress compilation gaps.
4. Preserve compilation diagnostics, reduced coverage, unresolved references,
   originalBuildCorrespondence=not-established and the approved source-membership
   discrepancy. Exit zero is not proof of a clean application build.

Return only a sanitized aggregate summary, tool HEAD, extractor versions, test
counts, matched comparison scope and remaining blockers. Keep private paths,
source, DLLs, hashes, context files and raw diagnostics local. Do not push private
artifacts or execute application methods, SQL or HTTP calls.
