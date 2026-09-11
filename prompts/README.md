# Private exchange prompts

These prompts produce small, aggregate-only maintainer handoffs. They must be
run locally in the private environment. Never commit source labels, repository
identities, private commit SHAs, run IDs, paths, values, indexes, reports, SQL,
credentials, connection material, logs, or business identifiers.

For restricted work environments, this repository is the transport boundary.
Prompts may invoke only tooling already committed in the checked-out TraceMap
head. If a required diagnostic projection does not exist, the prompt must stop
with a typed capability-missing result so the capability can be built and
published from the normal development environment first.

- `collect-maintainer-edge-case-followup.md`
- `collect-migration-extraction-summary.md`
- `collect-webforms-extraction-readiness.md`
- `collect-focused-webforms-gap-extractor-summary.md`
- `collect-webforms-scope-discovery.md`
- `review-focused-webforms-coverage-gaps.md`
- `run-webforms-monorepo-correction.md`
- `run-focused-webforms-monorepo-scan.md`
- `run-focused-webforms-one-repo-windows.md`
