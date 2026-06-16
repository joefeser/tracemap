# Legacy Story Reconciliation Implementation State

Status: implemented
Branch: codex/legacy-story-reconciliation
Public claim level: hidden

## Scope

Reconcile the current legacy-analysis state after the main/dev merge. This is a
cleanup and regression-proof slice, not a new product capability.

## Validation

Completed:

- Reconciliation spec added with hidden public claim level.
- Coexistence regression tests added for WCF/service-reference, Remoting, and
  legacy data metadata facts.
- WCF/service-reference mapping state and tasks updated to implemented.
- Legacy data metadata state updated to implemented MVP with deferred follow-ups
  explicitly labeled.
- Legacy flow composition wording updated to treat legacy data metadata as an
  optional implemented MVP input.

Validation:

- `node scripts/kiro-review.mjs --phase legacy-story-reconciliation --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text`: full coverage; initial blockers patched.
- `node scripts/kiro-review.mjs --phase legacy-story-reconciliation --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000 --save-review-text`: full coverage; no blockers.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~LegacyStoryReconciliationTests`: passed, 3 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with zero warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 348 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Site validation is not required unless site files change.

## Follow-Ups

Keep any remaining legacy-data compatibility breadth, public legacy sample
evidence packs, and public site claim promotion in separate specs.
