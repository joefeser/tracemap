# Public Combined Path Validation Tasks

## Implementation Tasks

- [x] Confirm validation scope.
  - [x] Confirm default smoke uses only checked-in public samples.
  - [x] Confirm OSS scans remain in the separate pinned OSS smoke.
  - [x] Confirm optional external repo mode is deferred.
  - [x] Confirm generated outputs are not committed.
  - [x] Confirm path-search behavior changes are allowed only if the real-scanned linkage spike proves they are required.
  - [x] Confirm the new script stays independent from `scripts/demo-public.sh`.
  - [x] Decide after the spike whether to split into two PRs: path-graph reconciliation first, smoke/docs second.

### PR 1: Real-Scanned Path Linkage

- [x] Run the linkage spike before changing samples.
  - [x] Scan `samples/endpoint-client-angular`.
  - [x] Scan `samples/endpoint-server-aspnet`.
  - [x] Combine the two indexes with labels `sample-client` and `sample-server`.
  - [x] Run `tracemap report`.
  - [x] Run `tracemap paths`.
  - [x] Determine whether current samples already produce endpoint-to-`sql-query` paths.
  - [x] Determine whether current samples already produce endpoint-to-`package-config` paths.
  - [x] Verify the sample matched endpoint key, expected to be `GET /api/admin/runner/get-by-id/{}` unless scanner output says otherwise.
  - [x] Inspect route fact source symbols, call-edge source/target symbols, and surface source symbols from real scanned indexes.
  - [x] Record whether SQL/config surfaces are reachable or only reported as `UnlinkedSurface`.

- [x] If scanned symbols do not connect, add path-graph reconciliation.
  - [x] Add focused tests that reproduce route/call/query source-symbol mismatch using facts shaped like real scanner output.
  - [x] Keep reconciliation source-local to a single `sourceIndexId`.
  - [x] Prefer exact symbol IDs and exact display names before any short-name alias.
  - [x] Add a documented derived rule ID for any symbol alias/reconciliation edge.
  - [x] Add the rule and limitations to `rules/rule-catalog.yml`.
  - [x] Classify syntax/name-only reconciled paths no stronger than `NeedsReviewPath`.
  - [x] Preserve supporting fact IDs or combined edge IDs on derived reconciliation evidence.
  - [x] Emit a gap or review-tier ambiguity when multiple source-local candidates match.
  - [x] Prove SQL surfaces that remain unlinked do not satisfy path success.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.

### PR 2: Public Fixture and Smoke

- [x] Extend checked-in samples for reachable SQL evidence.
  - [x] Add minimal server code that emits SQL/query evidence reachable from a matched route.
  - [x] Add a controller-to-service-or-repository call chain if the current controller has no downstream edges.
  - [x] Include a synthetic SQL sentinel token for negative Markdown leak assertions.
  - [x] Consider reachable config/package evidence only if it stays small and deterministic; otherwise defer `package-config` path assertion.
  - [x] Keep sample code small and realistic.
  - [x] Avoid raw secrets, real hostnames, private names, private schemas, and local paths.
  - [x] Update any existing sample expectations affected by the added code.
  - [x] Re-run the real-scanned workflow and prove endpoint-to-`sql-query` now works before writing smoke assertions.

- [x] Add `scripts/smoke-combined-paths.sh`.
  - [x] Resolve repo root from the script location.
  - [x] Accept optional output root argument.
  - [x] Use `mktemp -d` when no output root is provided.
  - [x] Fail early with a clear message if `dotnet`, `npm`, or `node` is missing.
  - [x] Build the TypeScript CLI.
  - [x] Run TypeScript scan for the client sample.
  - [x] Run .NET scan for the server sample.
  - [x] Assert required scan artifacts for both outputs.
  - [x] Run `tracemap combine`.
  - [x] Run `tracemap report`.
  - [x] Run default `tracemap paths`.
  - [x] Run targeted endpoint-to-`sql-query` `tracemap paths` query for the verified sample endpoint key.
  - [x] Run targeted paths query twice and compare JSON bytes for deterministic output.
  - [x] Run one bogus endpoint query and assert it returns a valid zero-path report with a rule-backed gap.
  - [x] Assert report and paths artifacts exist.
  - [x] Print a concise summary.

- [x] Add JSON assertion helpers.
  - [x] Assert dependency report includes target endpoint evidence classified as `MatchedEndpoint` or review-tier `AmbiguousMatch`.
  - [x] Assert dependency report imports exactly two sources with labels `sample-client` and `sample-server`.
  - [x] Assert matching client/server endpoint evidence share the same normalized path key.
  - [x] Assert path report has expected source labels.
  - [x] Assert path report includes at least one `endpoint-match` edge.
  - [x] Assert path report includes a source transition across sample labels.
  - [x] Assert a path reaches `sql-query`.
  - [x] Assert the SQL path includes an endpoint-match edge and at least one code traversal or documented reconciliation edge before the SQL terminal.
  - [x] Assert SQL evidence reported only as `UnlinkedSurface` does not count as success.
  - [x] Assert at least one path is classified as `StrongStaticPath`, `ProbableStaticPath`, or `NeedsReviewPath`.
  - [x] Assert path edges have rule IDs and evidence tiers.
  - [x] Assert gaps have rule IDs and evidence tiers.
  - [x] Assert JSON has no generated timestamp fields.
  - [x] Use inline Node.js for JSON assertions; do not require `jq`.

- [x] Add Markdown safety assertions.
  - [x] Prefer JSON structural source-transition assertions over exact Markdown transition text.
  - [x] Assert paths Markdown does not contain the SQL sentinel token.
  - [x] Assert paths Markdown does not contain raw config values.
  - [x] Assert paths Markdown does not contain developer-local absolute path patterns.
  - [x] Assert generated reports use safe relative paths or hashes.

- [x] Document deferred external and OSS validation.
  - [x] Reserve generic external env var names only in docs, not in script behavior.
  - [x] State that future external diagnostics must print labels, basenames, or hashes only.
  - [x] Document that OSS smoke remains a separate command.
  - [x] Keep all OSS URLs and SHAs pinned in `docs/VALIDATION.md`.
  - [x] Keep OSS smoke separate from deterministic path assertions unless a public paired fixture is added.

- [x] Update docs.
  - [x] Add README workflow for public combined path smoke.
  - [x] Add README manual `combine -> report -> paths` command sketch.
  - [x] Update `docs/VALIDATION.md` with the new script.
  - [x] Document expected outputs and assertions.
  - [x] Document prerequisites.
  - [x] Document the separate role of `scripts/demo-public.sh`.
  - [x] Document reserved external repo env vars generically as a follow-up, not current behavior.
  - [x] Document reduced coverage interpretation.
  - [x] Document troubleshooting for missing endpoint matches, missing surfaces, reduced coverage, and unlinked surfaces.

- [x] Add or update tests where useful.
  - [x] Prefer existing .NET and TypeScript unit tests for scanner behavior.
  - [x] Add path-graph tests for any reconciliation rules added in PR 1.
  - [x] Add small tests only if sample changes affect extractor behavior.
  - [x] Avoid brittle golden Markdown snapshots.
  - [x] Assert semantic JSON fields rather than full report text.

- [x] Validate implementation.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] TypeScript build/check required by the smoke.
  - [x] `./scripts/smoke-combined-paths.sh`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Review Checklist

- [x] Does the smoke prove path value rather than only artifact creation?
- [x] Are all defaults public and generic?
- [x] Are generated artifacts excluded from the repo?
- [x] Are docs honest about static evidence versus runtime behavior?
- [x] Are reduced coverage and gaps treated as expected outcomes when evidence is incomplete?
- [x] Is the script short enough to run locally during PR review?
- [x] Does it avoid raw SQL, config values, snippets, and local absolute paths?

## Deferred Follow-Ups

- CI workflow for sample-only smoke.
- Golden JSON fixture once path report schema stabilizes further.
- Public real full-stack repo pair with pinned commits.
- Optional external repo validation with sanitized diagnostics.
- Hosted demo output generated by release automation.
- Snapshot path diff command across two commit SHAs.
