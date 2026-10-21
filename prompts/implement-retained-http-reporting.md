# Implement bounded retained HTTP reporting

Close the retained-index HTTP reporting capability gap without changing extraction
or upgrading evidence. Work in the public TraceMap development environment with
synthetic fixtures only. Do not run this implementation task against private work
artifacts or ask the work machine to rescan before the capability exists.

## Establish the current boundary

1. Read AGENTS.md and inspect the current branch, diff and upstream. Continue on
   `codex/http-evidence-regressions` only if its worktree is available and clean;
   preserve unrelated work. Do not merge dev, reset, force-push or open a PR.
2. Inspect `WebFormsReviewExecutionCommand.HandlerRequery.cs`, the CLI help in
   `WebFormsReviewPreflightCommand.cs`, and the existing combined path engine.
   Confirm whether an existing supported command already provides an equivalent
   bounded HTTP query over a verified retained combined index. Do not confuse
   generic `--to-surface http-client` support with support in the handler wrapper.
3. The wrapper currently derives `database-api` or `database-operations` as its
   terminal scope. Prefer a small explicit surface selector using the existing
   path engine rather than a parallel implementation. Preserve the default
   database behavior and all existing invocation compatibility. If current code
   has already closed the gap, pin that behavior with tests and document the
   supported command instead of adding a second route.

## Implement the smallest supported change

- Support a source handler root and the `http-client` terminal family against the
  existing verified combined index. Preserve bounded traversal and native reports.
- Validate selector combinations explicitly. Database-specific surface filters,
  compiled-only scope and method-graph mode must not silently change HTTP meaning.
  Document supported combinations and reject unsupported ones before writing output.
- Preserve receipt, checkpoint, artifact hash, output-overlap, root identity and
  provenance checks. Do not bypass generator/input checks to accommodate old runs.
- Record the actual terminal scope, limits, algorithm/version, generator SHA-256,
  bounded-input SHA-256, coverage and truncation in native receipts/reports.
- Keep event-binding evidence, source calls and HTTP operation evidence distinct.
  Join only supported exact identities; retain original rule IDs, tiers, locations
  and supporting facts. Do not upgrade a route to its terminal's evidence tier.
- No extraction changes, new inference from names, containment-as-call edges,
  runtime execution, HTTP requests, database access or private-source fixtures.
- Bump a version only when its actual semantic/schema contract changes. Document
  compatibility and limitations; do not bump extractor versions for reporting work.

## Synthetic acceptance tests

Use existing execution/path test infrastructure, including
`WebFormsReviewExecutionTests.cs` and `CombinedDependencyPathTests.cs`.

1. A retained event binding, exact source caller edge and HTTP operation produce
   a handler-to-HTTP report with correct ordered evidence, rules, tiers, locations
   and supporting IDs. Verify the receipt scope is HTTP, not database operations.
2. Mixed database and HTTP terminals do not leak across selected surfaces.
   Existing default database invocations retain their previous behavior.
3. Same-named handlers in different types, missing joins and ambiguous identities
   do not create invented paths. Preserve known semantic facts when a path cannot
   be established; zero reported paths must not mean the operation is absent.
4. Missing/partial semantics and weaker bridges remain honestly labeled. A
   semantic HTTP terminal must not upgrade a syntax or structural entry hop.
5. Depth/work/path limits and truncation remain explicit and deterministic.
   Representative selection stays stable under fact-ID/order changes; compare
   representative evidence and supporting facts, not only path counts.
6. Invalid selectors, hash mismatch, rejected context and overlapping/existing
   output directories fail safely without modifying accepted inputs.
7. Where ordinary and bounded reader paths are supported, pin equivalent retained
   HTTP evidence across them using the same synthetic index.

Run the relevant local lanes from `docs/VALIDATION.md`. For shared-core changes,
run the full Debug suite with `-warnaserror`; otherwise run the touched lanes and
state the full-suite deferral. Exercise the CLI against a public/synthetic sample.
Fix introduced compiler/analyzer warnings. Report exact commands and results.

## Deliver the implementation and work-machine prompt

Update CLI help and applicable documentation. Create a separate repository prompt
for one bounded work-machine HTTP acceptance run using the existing local baseline
inventory and retained index. That prompt must:

- Verify the selected tool revision and existing input hashes, build once, and
  run the newly supported exact invocation into a new private output directory.
- Use the locally recorded H1 root and facts, never hard-coded private identifiers.
- Compare the report's binding/call/terminal evidence against those retained facts.
  Label it FIRST HTTP REPORT BASELINE because no prior H1 query receipt exists.
- Preserve accepted database results without repeating the database query.
- Stop on incompatibility, missing capability or input mismatch; no rescan,
  checkpoint rewrite, invented helper, runtime request or automatic retry.
- Return a sanitized verdict with tiers, counts, truncation, coverage and first
  demonstrated gap. Keep all private artifacts and identifiers on the work machine.

Commit and push only the implementation, public synthetic tests, documentation
and acceptance prompt to this branch. Do not merge or open a PR. Return the pushed
SHA, validation results, exact acceptance-prompt path and remaining limitations.
Completion means the capability is tested locally and ready for private acceptance,
not that private HTTP reachability or runtime behavior has already been verified.
