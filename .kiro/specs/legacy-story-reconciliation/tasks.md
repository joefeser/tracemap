# Legacy Story Reconciliation Tasks

## Implementation Tasks

- [x] 1. Add the reconciliation spec. Requirements: 2, 3.
  - [x] Capture the cleanup scope and non-goals.
  - [x] Keep public claim level hidden.

- [x] 2. Add coexistence regression tests. Requirements: 1, 3.
  - [x] Build one synthetic repository containing WCF/service-reference,
        Remoting, and legacy data metadata evidence.
  - [x] Assert facts from all three families are emitted.
  - [x] Assert raw URLs, object URIs, connection strings, and unsafe values do
        not appear in facts or Markdown reports.

- [x] 3. Clean stale legacy spec state. Requirements: 2, 3.
  - [x] Mark fully implemented WCF/service-reference mapping tasks complete.
  - [x] Update legacy data metadata state from spec-only/ready wording to MVP
        implemented wording.
  - [x] Update legacy flow composition wording now that legacy data metadata is
        implemented as optional input.
  - [x] Leave real deferred follow-ups unchecked or explicitly documented.

- [x] 4. Validate and prepare PR. Requirements: 4.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Run site validation only if site files changed.
