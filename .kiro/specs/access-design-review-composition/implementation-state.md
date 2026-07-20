# Access Design-Review Composition Implementation State

Status: pr-open-review-fixes-validated-awaiting-fresh-ack

Branch: `codex/access-design-review-composition`

Target base: `dev`

Public claim level: hidden

## Scope Decision

Implement one dedicated Access design-evidence section in release review. Reuse
existing single/combined SQLite facts and manifests through a narrow read hook.
Do not add a new Access extraction path or reconnect the platform-neutral
UI/VBA/macro projectors to the Windows product reader.

## Read Gate

The existing index contains database inventory, schema, field/index,
relationship, saved-query, dependency, external-boundary, count-only UI/VBA/
macro metadata, and rule-backed Access gaps with the provenance required by
release review. Therefore no extraction or fact-schema change is required.

## Safety Boundary

- no Access COM or Windows changes;
- no database rows or query execution;
- no forms/reports opened or rendered;
- no VBA source or macro bodies;
- no raw SQL, connections, credentials, local paths, private names, captions,
  expressions, or infrastructure identities;
- count-only UI/VBA/macro coverage remains explicitly reduced.

## Validation

Focused Access/release-review validation passed: 84 tests. The full solution
build passed with zero errors and the eight existing SQLite package-advisory
warnings; the full solution test run passed 832/832 tests. The private-path
guard, rule-catalog YAML parse, and `git diff --check` also passed.

PowerShell is not installed in the macOS validation environment (confirmed via
Homebrew discovery), so no local PowerShell parse was run. Harness assertions
were updated for the new composed release-review contract; Access COM,
product-reader, and fixture-generation code are unchanged. The completed
Windows smoke evidence remains authoritative and no new Windows probe is
required for this read-side slice.

PR #506 ACK review on initial head `cbf80097` found source-selector handling,
cross-platform path redaction, and per-file gap identity issues. The review
patch also added evidence-scope metadata to the no-facts gap and applied the
supported allocation/pool-lifetime cleanups. Focused tests and the full 832-test
solution run passed again after the patch; a fresh ACK run is pending.

## Deferred

Richer UI identities/bindings, VBA identity/flow, macro identities/bodies,
runtime behavior, encrypted files, effective permissions, public claims, and
dedicated route/property-flow composition remain outside this PR.
