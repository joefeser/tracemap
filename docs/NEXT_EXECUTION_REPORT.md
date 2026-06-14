# TraceMap Next Execution Report

Date: 2026-06-14

## Current State

- Main checkout was synced from `origin/dev` at `cb83827`.
- Local cleanup branch: `codex/spec-task-state-cleanup`.
- PR #61, `dev` to `main`, is open for the main promotion and currently mergeable.
- Private path guard is green on PR #61.
- Sourcery skipped the promotion review because the diff is larger than its review limit.
- Do not push this cleanup branch until the promotion-review bots are done or a human asks for it.

## Product Shape

TraceMap now has a useful static analysis base across multiple language families:

- .NET scanner, reducer, graph/flow facts, and reporting commands.
- TypeScript scanner.
- Python scanner MVP.
- JVM scanner MVP for Java plus Kotlin syntax fallback.
- Combined index builder.
- Combined dependency report.
- Combined dependency paths.
- Combined dependency diff.
- Combined change impact.
- Reverse dependency query.
- Endpoint alignment.
- Snapshot diff by SHA/index, with combined delegation.
- SQL dependency surfaces.
- Query pattern reporting.
- Package dependency surfaces.
- Multi-index portfolio report.
- Contract delta impact v2.
- Public demo workflow.
- Kiro review wrapper with profile-auth fallback.
- Private path guard.
- Initial static `tracemap.tools` site under `site/`, with root `amplify.yml` for AWS Amplify deployment from this repository.

Current CLI surface includes:

- `tracemap scan`
- `tracemap report`
- `tracemap reduce`
- `tracemap flow`
- `tracemap relate`
- `tracemap export`
- `tracemap endpoints`
- `tracemap combine`
- `tracemap paths`
- `tracemap diff`
- `tracemap impact`
- `tracemap reverse`
- `tracemap snapshot-diff`
- `tracemap portfolio`

Public site surface:

- `site/src/` is the editable static site source.
- `site/dist/` is generated and ignored.
- Amplify app root is `site`; publish directory is `dist`.
- Future site specs should use the `site-` prefix and include an `implementation-state.md` file.

## Spec Status

### Implemented Or Mostly Implemented

- `combined-change-impact`
- `combined-dependency-diff`
- `combined-dependency-paths`
- `combined-dependency-reporting`
- `contract-delta-impact-v2`
- `cross-app-endpoint-alignment`
- `jvm-indexer`
- `multi-index-portfolio-report`
- `package-dependency-surfaces`
- `public-demo-workflow`
- `python-depth-pass`
- `python-endpoint-sql-details`
- `python-indexer`
- `query-pattern-reporting`
- `query-pattern-reporting-v2`
- `release-review-report`
- `reverse-impact-query`
- `snapshot-diff-by-sha`
- `sql-dependency-surfaces`
- `site-tracemap-tools-launch`
- `typescript-indexer`

Notes:

- `python-indexer` and `jvm-indexer` now have `implementation-state.md` files because their original task lists contain both shipped MVP work and explicit post-MVP backlog.
- `snapshot-diff-by-sha` still has some single-index projector and malformed-metadata work open. Combined-index delegation is implemented.
- `public-demo-workflow` has the first script/scan/summary slice implemented. Combine/report/path/reverse/portfolio demo sections remain follow-up slices.

### Spec-Ready, Not Yet Implemented

- `api-dto-contract-diff`
- `public-combined-path-validation`
- `sql-schema-change-impact`

### Partial Or Future-Heavy Specs

- `parameter-value-origin-flow`

This is the deeper analysis layer for request/object/value movement. It is intentionally more cautious and should stay high-value but bounded.

## Backlog Snapshot

Highest-value next implementation choices:

1. `api-dto-contract-diff`
   - Good public demo value.
   - Builds on endpoint/type/DTO facts.
   - Must avoid claiming runtime serializer behavior without evidence.

2. `sql-schema-change-impact`
   - Strong consulting and legacy-system value.
   - Builds on SQL dependency surfaces and query pattern reporting.
   - Needs careful dynamic SQL and reduced-coverage labeling.

3. `public-combined-path-validation`
   - Good trust-building slice for sample validation and repeatability.
   - Useful before a larger main/public push.

4. `parameter-value-origin-flow`
   - High-value explanation layer.
   - Higher risk and should likely follow one more report/diff-oriented feature.

Site coordinator track:

- Keep site changes in separate worktrees.
- Create future site specs with the `site-` prefix.
- Do not edit `site/dist/` by hand.
- Validate site changes with `npm run build` from `site/` and browser/mobile sanity checks for layout changes.

## Cleanup Done In This Branch

- Checked off task lists for implemented specs that still looked unstarted.
- Added `implementation-state.md` for foundational Python and JVM specs.
- Updated this report so future agents can tell which specs are shipped, queued, partial, or future-heavy.

## My Recommended Next Move

Wait for PR #61 review bots to finish. If PR #61 remains clean, merge `dev` to `main` so `tracemap.tools` can be configured from `main`.

After the promotion settles, run the Feature Delivery Loop for `api-dto-contract-diff` first. While that implementation runs, queue specs or site updates in separate worktrees so the core and public-site tracks do not collide.
