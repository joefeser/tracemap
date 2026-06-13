# Public Combined Path Validation Tasks

## Implementation Tasks

- [ ] Confirm validation scope.
  - [ ] Confirm default smoke uses only checked-in public samples.
  - [ ] Confirm OSS scans remain in the separate pinned OSS smoke.
  - [ ] Confirm optional external repo mode is deferred.
  - [ ] Confirm generated outputs are not committed.
  - [ ] Confirm path-search behavior changes are allowed only if the real-scanned linkage spike proves they are required.
  - [ ] Confirm the new script stays independent from `scripts/smoke-typescript-endpoints.sh`.
  - [ ] Decide after the spike whether to split into two PRs: path-graph reconciliation first, smoke/docs second.

### PR 1: Real-Scanned Path Linkage

- [ ] Run the linkage spike before changing samples.
  - [ ] Scan `samples/endpoint-client-angular`.
  - [ ] Scan `samples/endpoint-server-aspnet`.
  - [ ] Combine the two indexes with labels `sample-client` and `sample-server`.
  - [ ] Run `tracemap report`.
  - [ ] Run `tracemap paths`.
  - [ ] Determine whether current samples already produce endpoint-to-`sql-query` paths.
  - [ ] Determine whether current samples already produce endpoint-to-`package-config` paths.
  - [ ] Verify the sample matched endpoint key, expected to be `GET /api/admin/runner/get-by-id/{}` unless scanner output says otherwise.
  - [ ] Inspect route fact source symbols, call-edge source/target symbols, and surface source symbols from real scanned indexes.
  - [ ] Record whether SQL/config surfaces are reachable or only reported as `UnlinkedSurface`.

- [ ] If scanned symbols do not connect, add path-graph reconciliation.
  - [ ] Add focused tests that reproduce route/call/query source-symbol mismatch using facts shaped like real scanner output.
  - [ ] Keep reconciliation source-local to a single `sourceIndexId`.
  - [ ] Prefer exact symbol IDs and exact display names before any short-name alias.
  - [ ] Add a documented derived rule ID for any symbol alias/reconciliation edge.
  - [ ] Add the rule and limitations to `rules/rule-catalog.yml`.
  - [ ] Classify syntax/name-only reconciled paths no stronger than `NeedsReviewPath`.
  - [ ] Preserve supporting fact IDs or combined edge IDs on derived reconciliation evidence.
  - [ ] Emit a gap or review-tier ambiguity when multiple source-local candidates match.
  - [ ] Prove SQL surfaces that remain unlinked do not satisfy path success.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.

### PR 2: Public Fixture and Smoke

- [ ] Extend checked-in samples for reachable SQL evidence.
  - [ ] Add minimal server code that emits SQL/query evidence reachable from a matched route.
  - [ ] Add a controller-to-service-or-repository call chain if the current controller has no downstream edges.
  - [ ] Include a synthetic SQL sentinel token for negative Markdown leak assertions.
  - [ ] Consider reachable config/package evidence only if it stays small and deterministic; otherwise defer `package-config` path assertion.
  - [ ] Keep sample code small and realistic.
  - [ ] Avoid raw secrets, real hostnames, private names, private schemas, and local paths.
  - [ ] Update any existing sample expectations affected by the added code.
  - [ ] Re-run the real-scanned workflow and prove endpoint-to-`sql-query` now works before writing smoke assertions.

- [ ] Add `scripts/smoke-combined-paths.sh`.
  - [ ] Resolve repo root from the script location.
  - [ ] Accept optional output root argument.
  - [ ] Use `mktemp -d` when no output root is provided.
  - [ ] Fail early with a clear message if `dotnet`, `npm`, or `node` is missing.
  - [ ] Build the TypeScript CLI.
  - [ ] Run TypeScript scan for the client sample.
  - [ ] Run .NET scan for the server sample.
  - [ ] Assert required scan artifacts for both outputs.
  - [ ] Run `tracemap combine`.
  - [ ] Run `tracemap report`.
  - [ ] Run default `tracemap paths`.
  - [ ] Run targeted endpoint-to-`sql-query` `tracemap paths` query for the verified sample endpoint key.
  - [ ] Run targeted paths query twice and compare JSON bytes for deterministic output.
  - [ ] Run one bogus endpoint query and assert it returns a valid zero-path report with a rule-backed gap.
  - [ ] Assert report and paths artifacts exist.
  - [ ] Print a concise summary.

- [ ] Add JSON assertion helpers.
  - [ ] Assert dependency report includes at least one `MatchedEndpoint`.
  - [ ] Assert dependency report imports exactly two sources with labels `sample-client` and `sample-server`.
  - [ ] Assert matching client/server endpoint evidence share the same normalized path key.
  - [ ] Assert path report has expected source labels.
  - [ ] Assert path report includes at least one `endpoint-match` edge.
  - [ ] Assert path report includes a source transition across sample labels.
  - [ ] Assert a path reaches `sql-query`.
  - [ ] Assert the SQL path includes an endpoint-match edge and at least one code traversal or documented reconciliation edge before the SQL terminal.
  - [ ] Assert SQL evidence reported only as `UnlinkedSurface` does not count as success.
  - [ ] Assert at least one path is classified as `StrongStaticPath`, `ProbableStaticPath`, or `NeedsReviewPath`.
  - [ ] Assert path edges have rule IDs and evidence tiers.
  - [ ] Assert gaps have rule IDs and evidence tiers.
  - [ ] Assert JSON has no generated timestamp fields.
  - [ ] Use inline Node.js for JSON assertions; do not require `jq`.

- [ ] Add Markdown safety assertions.
  - [ ] Prefer JSON structural source-transition assertions over exact Markdown transition text.
  - [ ] Assert paths Markdown does not contain the SQL sentinel token.
  - [ ] Assert paths Markdown does not contain raw config values.
  - [ ] Assert paths Markdown does not contain developer-local absolute path patterns.
  - [ ] Assert generated reports use safe relative paths or hashes.

- [ ] Document deferred external and OSS validation.
  - [ ] Reserve generic external env var names only in docs, not in script behavior.
  - [ ] State that future external diagnostics must print labels, basenames, or hashes only.
  - [ ] Document that OSS smoke remains a separate command.
  - [ ] Keep all OSS URLs and SHAs pinned in `docs/VALIDATION.md`.
  - [ ] Keep OSS smoke separate from deterministic path assertions unless a public paired fixture is added.

- [ ] Update docs.
  - [ ] Add README workflow for public combined path smoke.
  - [ ] Add README manual `combine -> report -> paths` command sketch.
  - [ ] Update `docs/VALIDATION.md` with the new script.
  - [ ] Document expected outputs and assertions.
  - [ ] Document prerequisites.
  - [ ] Document the separate role of `scripts/smoke-typescript-endpoints.sh`.
  - [ ] Document reserved external repo env vars generically as a follow-up, not current behavior.
  - [ ] Document reduced coverage interpretation.
  - [ ] Document troubleshooting for missing endpoint matches, missing surfaces, reduced coverage, and unlinked surfaces.

- [ ] Add or update tests where useful.
  - [ ] Prefer existing .NET and TypeScript unit tests for scanner behavior.
  - [ ] Add path-graph tests for any reconciliation rules added in PR 1.
  - [ ] Add small tests only if sample changes affect extractor behavior.
  - [ ] Avoid brittle golden Markdown snapshots.
  - [ ] Assert semantic JSON fields rather than full report text.

- [ ] Validate implementation.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] TypeScript build/check required by the smoke.
  - [ ] `./scripts/smoke-combined-paths.sh`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Review Checklist

- [ ] Does the smoke prove path value rather than only artifact creation?
- [ ] Are all defaults public and generic?
- [ ] Are generated artifacts excluded from the repo?
- [ ] Are docs honest about static evidence versus runtime behavior?
- [ ] Are reduced coverage and gaps treated as expected outcomes when evidence is incomplete?
- [ ] Is the script short enough to run locally during PR review?
- [ ] Does it avoid raw SQL, config values, snippets, and local absolute paths?

## Deferred Follow-Ups

- CI workflow for sample-only smoke.
- Golden JSON fixture once path report schema stabilizes further.
- Public real full-stack repo pair with pinned commits.
- Optional external repo validation with sanitized diagnostics.
- Hosted demo output generated by release automation.
- Snapshot path diff command across two commit SHAs.
