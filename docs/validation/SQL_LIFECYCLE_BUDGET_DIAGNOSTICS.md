# Lifecycle work-budget accounting and production-fit pins

IL extractor 0.2.69 / evidence policy v70 adds diagnostics only. The production
work maximum remains 2,000,000; state capacity remains 256 paths / 16 sites.
No interpreter admission, comparison fold, origin contract or candidate-selection
behavior changes. No budget is replenished between roots, guards or alternatives.

## What is shared

Lifecycle materialization creates one interpretation work budget for the selected
roots and their guarded runs. Root selectors are canonicalized by binary digest
and metadata token; reversing command-line selector order does not reverse the
materialization order. Slice-decode work has a separate budget and is not included
in the interpretation accounting below. These counters are deterministic work
units, not elapsed time, bytes or a prediction of runtime memory use.

Every attempted interpreter extraction adds `workAccounting` to its result:
maximum, consumedBefore/After, rootConsumed, remainingBefore/After and cumulative
refusedBefore/After. Native lifecycle candidate/gap facts expose the same ledger
as `lifecycleWork`, with `lifecycleRootOrdinal` (zero-based canonical root order)
and `lifecycleBudgetScope=shared-across-roots-and-guards`. A Boolean root may have
multiple guarded runs at the same root ordinal; each ledger covers that run.
A slice rejected before interpretation has no interpretation ledger; do not infer
zero interpretation cost or complete coverage from an absent field.

The existing fact rule, tier, generator/input bindings and extractor version apply
to these additive properties; no unbound standalone machine artifact is introduced.

## What a stop proves

A `lifecycle-work-limit` refusal now carries `workLimitFailure`: runs, completed
alternatives, queued assignments, visited sites and a sorted fork census, plus:

- `shared-request-refused-during-extraction`: this extraction incremented the shared budget's
  refused-request count. Remaining units may be positive because the next whole
  request did not fit. No overrun or partial charge is claimed.
- `no-shared-request-refused-during-extraction`: that shared counter did not
  increase. The existing per-invocation work/stack/local bound uses the same
  refusal name. Raising shared work is not established as a remedy.

These are `budgetObservation` values, not a causal attribution of the exact
throw site: a refused optional probe could precede a later local-bound stop.

Failure results remain failures: completed prefixes are counted in the census,
not promoted into complete entry candidates. Existing `lifecycle-index-limit`
and other refusal names remain unchanged. A fully spent budget can stop indexing
with zero newly consumed units; its ledger still identifies prior consumption.

## Public regression denominator

C#-only tests use existing public fixtures; no VB assembly grows. Pins cover:

1. A work stop after forks have been explored: accounting, nonempty fork census,
   refusal counter, and no successful or partial entry output.
2. A fully spent caller budget: zero new work and the unchanged indexing refusal.
3. Direct interpreter root sequences in both orders, with the production maximum,
   shared consumption and standalone-vs-remaining-work outcome checks.
4. Native compiled-only scans with both selector orders. Both must retain canonical
   root order and the same shared ledger chain, not reset the budget per root.
   The public pair completes at the current cap: 3 alternatives / 2 entries for
   the simpler fixture and 36 / 36 for the four-input fixture. These counts are
   pinned, not merely 'no gaps'. This pair is not proof of another input's cost.

The original three diagnostic pins were run before the change and failed because
these diagnostics were absent. The new fields are therefore exercised. Larger
4M/8M direct harness tests remain useful semantic tests but do not establish
completion under the native shared 2M configuration.

## One owner-dispatched validation, if requested

This document is a protocol template, not authorization for a private scan.
After the public gates and owner dispatch, use the exact validated implementation
commit and the existing two selectors, compiled inputs and boundary flags under
`--compiled-only`. Keep the production interpretation maximum at 2M; do not raise
any work/path/site/memory/time limit. Use the prior authorized Windows job guard,
self-test, fresh output and progress paths. Preserve input receipts and baselines.

Run once and stop. For each canonical root/guard report its outcome, gap kind,
ledger, budget observation and fork census. Include slice failures separately. Match root
identity within its module; do not substitute candidates from the other root.
No missing property, zero exit, or published artifact establishes entry completion.
New surprises are issue/owner decisions, not automatic patches or reruns.

A stopped root's standalone cost is not known from a shared-budget refusal alone.
Any future isolation experiment or budget increase requires its own owner decision.
The current conversion artifact remains a historical structure example until the
same method/root has completed under the desired evidence revision and origin
contract. No budget was raised, private scan run or conversion regenerated here.
