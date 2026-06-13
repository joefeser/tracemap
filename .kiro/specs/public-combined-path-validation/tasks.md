# Public Combined Path Validation Tasks

## Implementation Tasks

- [ ] Confirm validation scope.
  - [ ] Confirm default smoke uses only checked-in public samples.
  - [ ] Confirm OSS scans are optional and pinned.
  - [ ] Confirm optional external repos use generic environment variables only.
  - [ ] Confirm generated outputs are not committed.
  - [ ] Confirm this slice does not change path-search behavior unless a validation blocker is found.

- [ ] Inspect current endpoint samples.
  - [ ] Scan `samples/endpoint-client-angular`.
  - [ ] Scan `samples/endpoint-server-aspnet`.
  - [ ] Combine the two indexes with labels `sample-client` and `sample-server`.
  - [ ] Run `tracemap report`.
  - [ ] Run `tracemap paths`.
  - [ ] Determine whether current samples already produce endpoint-to-`sql-query` paths.
  - [ ] Determine whether current samples already produce endpoint-to-`package-config` paths.
  - [ ] Record any sample evidence gaps in implementation notes or tests.

- [ ] Extend checked-in samples if needed.
  - [ ] Add minimal server code that emits SQL/query evidence reachable from a matched route.
  - [ ] Add minimal config/package evidence reachable from a stable symbol if current sample lacks it.
  - [ ] Keep sample code small and realistic.
  - [ ] Avoid raw secrets, real hostnames, private names, and local paths.
  - [ ] Update any existing sample expectations affected by the added code.

- [ ] Add `scripts/smoke-combined-paths.sh`.
  - [ ] Resolve repo root from the script location.
  - [ ] Accept optional output root argument.
  - [ ] Use `mktemp -d` when no output root is provided.
  - [ ] Build the TypeScript CLI.
  - [ ] Run TypeScript scan for the client sample.
  - [ ] Run .NET scan for the server sample.
  - [ ] Assert required scan artifacts for both outputs.
  - [ ] Run `tracemap combine`.
  - [ ] Run `tracemap report`.
  - [ ] Run default `tracemap paths`.
  - [ ] Run targeted endpoint-to-surface `tracemap paths` queries where fixture evidence supports them.
  - [ ] Assert report and paths artifacts exist.
  - [ ] Print a concise summary.

- [ ] Add JSON assertion helpers.
  - [ ] Assert dependency report includes at least one `MatchedEndpoint`.
  - [ ] Assert path report has expected source labels.
  - [ ] Assert path report includes at least one `endpoint-match` edge.
  - [ ] Assert path report includes a source transition across sample labels.
  - [ ] Assert a path reaches `sql-query` when supported.
  - [ ] Assert a path reaches `package-config` when supported.
  - [ ] Assert path edges have rule IDs and evidence tiers.
  - [ ] Assert gaps have rule IDs and evidence tiers.
  - [ ] Assert JSON has no generated timestamp fields.

- [ ] Add Markdown safety assertions.
  - [ ] Assert paths Markdown contains `source transition:`.
  - [ ] Assert paths Markdown does not contain raw SQL fixture text.
  - [ ] Assert paths Markdown does not contain raw config values.
  - [ ] Assert paths Markdown does not contain developer-local absolute path patterns.
  - [ ] Assert generated reports use safe relative paths or hashes.

- [ ] Add optional external validation support.
  - [ ] Support `TRACEMAP_EXTERNAL_CLIENT_REPO`.
  - [ ] Support `TRACEMAP_EXTERNAL_SERVER_REPO`.
  - [ ] Support `TRACEMAP_EXTERNAL_SERVER_PROJECT`.
  - [ ] Use generic labels such as `external-client` and `external-server`.
  - [ ] Skip cleanly when env vars are absent.
  - [ ] Do not print private defaults.
  - [ ] Do not make this path required for success.

- [ ] Decide optional OSS integration.
  - [ ] Either delegate to `scripts/smoke-open-source-repos.sh` when `TRACEMAP_INCLUDE_OSS=1`.
  - [ ] Or document that OSS smoke remains a separate command.
  - [ ] Keep all OSS URLs and SHAs pinned in `docs/VALIDATION.md`.
  - [ ] Keep OSS smoke separate from deterministic path assertions unless a public paired fixture is added.

- [ ] Update docs.
  - [ ] Add README workflow for public combined path smoke.
  - [ ] Add README manual `combine -> report -> paths` command sketch.
  - [ ] Update `docs/VALIDATION.md` with the new script.
  - [ ] Document expected outputs and assertions.
  - [ ] Document prerequisites.
  - [ ] Document optional external repo env vars generically.
  - [ ] Document reduced coverage interpretation.
  - [ ] Document troubleshooting for missing endpoint matches, missing surfaces, reduced coverage, and unlinked surfaces.

- [ ] Add or update tests where useful.
  - [ ] Prefer existing .NET and TypeScript unit tests for scanner behavior.
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
- Hosted demo output generated by release automation.
- Snapshot path diff command across two commit SHAs.
