# Cross-App Endpoint Alignment Review Prompts

Use these prompts after the spec is written and before implementation starts.

## Opus Product and Evidence Review

```text
You are reviewing the TraceMap Cross-App Endpoint Alignment Kiro spec.

Context:
- TraceMap is a deterministic repository indexer and contract-change reducer.
- Core rule: no conclusion without evidence, no evidence without a rule ID, no raw snippets by default.
- This spec aligns client-side HTTP calls with server-side endpoints across two TraceMap indexes.
- The motivating system is an Angular client calling an ASP.NET Core API, but the design should stay general enough for later languages/frameworks.
- Endpoint alignment must not claim runtime traffic, runtime route reachability, proxy behavior, auth state, or deployment correctness.

Files to review:
- .kiro/specs/cross-app-endpoint-alignment/requirements.md
- .kiro/specs/cross-app-endpoint-alignment/design.md
- .kiro/specs/cross-app-endpoint-alignment/tasks.md

Please review for:
- Missing user workflows.
- Requirements that overclaim runtime behavior or dependency coverage.
- Evidence tier ambiguity.
- Places where reduced coverage is not labeled strongly enough.
- Report classifications that are confusing or incomplete.
- Whether the two-index MVP still leaves the right seam for future `tracemap combine`.
- Whether `ServerEndpointNoClientMatch` and `ClientCallNoServerEndpoint` caveats are strong enough to avoid dead-code or broken-call claims.
- Whether cleartext relative base path storage, such as `/api`, is scoped correctly and host/scheme values remain protected.

Return:
- Blockers.
- Recommended wording changes.
- Scope cuts or additions.
- Open questions that must be answered before implementation.
```

## Sonnet Implementation Review

```text
You are reviewing the TraceMap Cross-App Endpoint Alignment Kiro spec for implementation feasibility.

Context:
- Existing .NET scanner lives under src/dotnet.
- Existing TypeScript scanner lives under src/typescript.
- Existing facts include HttpCallDetected and HttpRouteBinding.
- Existing .NET semantic route extraction can miss ASP.NET routes when framework references are unresolved, so this spec adds C# syntax fallback.
- Existing TypeScript integration extraction catches fetch/axios, but not Angular HttpClient.

Files to review:
- .kiro/specs/cross-app-endpoint-alignment/requirements.md
- .kiro/specs/cross-app-endpoint-alignment/design.md
- .kiro/specs/cross-app-endpoint-alignment/tasks.md

Please review for:
- Incorrect TypeScript compiler API assumptions.
- Incorrect Roslyn syntax assumptions around ASP.NET attributes.
- Route normalization edge cases that need smaller task slices.
- Ambiguous matching/scoring behavior.
- Optional route segment expansion behavior.
- Base-path extraction and scan-time client normalization behavior.
- SQLite reader/report model risks.
- Tests that should be added earlier.
- Places where the locked `TraceMap.EndpointAlignment` project/module layout should be changed before implementation.

Return:
- Implementation blockers.
- Proposed task reordering.
- MVP slice recommendation.
- Specific design edits before coding.
```

## Database and Provenance Review

```text
You are reviewing the TraceMap Cross-App Endpoint Alignment spec specifically for future multi-index dependency analysis.

Context:
- The MVP compares two existing index.sqlite files and emits a derived endpoint report.
- Future work may add tracemap combine to merge multiple indexes into one queryable database.
- The user wants to analyze multiple apps together, know which folder/root each scan represented, and compare results across commit SHAs over time.
- Existing scan manifests already include repo name, remote URL, branch, commit SHA, scanner version, analysis level, and build status.
- The spec requires additive scan-root fields: scanRootRelativePath, scanRootPathHash, and gitRootHash, with raw absolute path only behind an explicit local option.

Files to review:
- .kiro/specs/cross-app-endpoint-alignment/requirements.md
- .kiro/specs/cross-app-endpoint-alignment/design.md
- .kiro/specs/cross-app-endpoint-alignment/tasks.md

Please review for:
- Whether the MVP report JSON has enough source metadata for a future combine command.
- Whether scan-root identity fields are sufficient and should be mandatory for new scans.
- Whether hash-only local path storage is enough for long-term local analysis.
- How fact IDs and symbol IDs should be namespaced in a combined database.
- Whether endpoint matches should be stored as derived rows or derived facts.
- Whether commit SHA, scan root, remote URL, and labels are enough to compare two snapshots.
- Any privacy or portability concerns with raw local paths.

Return:
- Recommended schema changes.
- Risks in the proposed combined DB shape.
- MVP changes needed now to avoid future migration pain.
- What should stay explicitly out of MVP.
```

## Private Fixture Review

```text
You are reviewing whether the Cross-App Endpoint Alignment spec handles a private Angular/ASP.NET fixture.

Fixture context:
- Angular root: <private-angular-client-app>
- ASP.NET root: <private-aspnet-server-root>
- Angular service calls use environment.apiUri plus template literals.
- The fixture's development environment.apiUri includes the `/api` base path, while production uses `/api`; base-path extraction is required for matching.
- ASP.NET endpoints use controller and method attributes such as Route("api/[controller]"), Route("api/admin/[controller]"), HttpGet, HttpPost, and Route("...").
- The solution path has a space-vs-%20 mismatch that can break dotnet build.
- A throwaway static matcher found 34 client calls and matched all 34 to server endpoints when base-path and optional route segments were handled. These counts are smoke observations, not test assertions.

Files to review:
- .kiro/specs/cross-app-endpoint-alignment/requirements.md
- .kiro/specs/cross-app-endpoint-alignment/design.md
- .kiro/specs/cross-app-endpoint-alignment/tasks.md

Please review for:
- Missing route patterns from this fixture.
- Whether base-path handling is specified clearly enough for this fixture.
- Whether optional route segment matching is specified clearly enough.
- Whether the spec handles reduced .NET semantic coverage honestly.
- Whether Angular dependency/build quirks are handled without making scanner behavior unsafe.
- Whether expected smoke outcomes are too brittle.

Return:
- Fixture-specific blockers.
- Expected false positives/false negatives.
- Suggested fixture reductions for test samples.
- Implementation notes before coding.
```
