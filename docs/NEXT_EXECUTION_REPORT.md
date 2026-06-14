# TraceMap Next Execution Report

Date: 2026-06-14

## Workspace Cleanup

- Main checkout is on `dev` at `115d0a3`.
- `dev` is up to date with `origin/dev`.
- Removed stale local worktrees from the recent spec and implementation loops.
- Removed empty worktree parent folders.
- Removed local merged branches for completed spec/implementation loops.
- Left remote branches alone.
- PR #58, snapshot-diff combined delegation, is merged into `dev`.
- PR #59, the first `tracemap.tools` static site, is merged into `dev`.

## Current Product Shape

TraceMap now has a useful static analysis base across multiple language families:

- .NET scanner and reducer.
- TypeScript scanner.
- Python scanner.
- JVM scanner MVP.
- Combined index builder.
- Combined dependency report.
- Combined dependency paths.
- Combined dependency diff.
- Combined change impact.
- Reverse dependency query.
- Endpoint alignment.
- SQL dependency surfaces.
- Query pattern reporting.
- Contract delta impact v2.
- Kiro review wrapper with profile-auth fallback.
- Private path guard.
- Snapshot diff by commit/index now delegates combined endpoint, surface, graph, and opt-in path evidence to the combined diff engine.
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

Public site surface:

- `site/src/` is the editable static site source.
- `site/dist/` is generated and ignored.
- Amplify app root is `site`; publish directory is `dist`.
- Future site specs should use the `site-` prefix.

## Specs Already In The Repo

### Implemented Or Mostly Implemented

- `combined-change-impact`
- `combined-dependency-reporting`
- `query-pattern-reporting`
- `query-pattern-reporting-v2`
- `reverse-impact-query`
- `snapshot-diff-by-sha`
- `python-depth-pass`
- `python-endpoint-sql-details`
- `typescript-indexer`
- `cross-app-endpoint-alignment`
- `sql-dependency-surfaces`
- `contract-delta-impact-v2`
- `site-tracemap-tools-launch`

Some implemented specs still have stale unchecked task boxes. Treat code and tests as the source of truth, then reconcile task files only if we want a tidy documentation pass.

### Spec-Ready, Not Yet Implemented

- `release-review-report`
- `api-dto-contract-diff`
- `sql-schema-change-impact`
- `parameter-value-origin-flow`
- `public-combined-path-validation`

### Older Foundational Specs

- `combined-dependency-diff`
- `combined-dependency-paths`
- `python-indexer`
- `jvm-indexer`

These largely describe shipped work or known MVP boundaries. They are still useful for limitations and future slices, but their task files are not reliable implementation status.

## Open GitHub Backlog

Priority-next issues:

- #24 Contract delta impact v2
- #25 API and DTO contract diff
- #26 SQL/schema change impact
- #27 Package upgrade impact
- #28 Snapshot diff by commit SHA
- #29 Reverse impact from endpoint/table/package/event
- #31 Release review report
- #32 Package and dependency surfaces
- #34 Parameter and value-origin flow
- #36 Multi-index portfolio dependency report
- #37 One-command public demo

Priority-later issues:

- #30 Deterministic risk scoring
- #33 Event and message dependency surfaces
- #35 Interface, override, and DI approximation
- #38 Static HTML evidence explorer
- #1 Low-priority Visual Basic .NET extractor

Other open item:

- #12 Render SQL query-pattern fields in reports

## Recommended Execution Order

### 1. Implement `release-review-report`

Why: this is the most demo-friendly next slice. It turns existing analysis into a higher-level artifact that answers, "What changed, what evidence supports it, and what is still unknown?"

Expected value:

- Easier PR/release storytelling.
- A single report that can reference diff, impact, reverse query, contract deltas, SQL/schema gaps, and unavailable sections without overclaiming.
- Good public-facing feature for open source.

Risk:

- Medium. Mostly composition and reporting, but it must avoid implying that unavailable workflows ran.

### 2. Implement `snapshot-diff-by-sha`

Why: this makes the "compare two repo states by commit" story concrete. It also supports the long-term goal of tracking dependency and impact changes across versions.

Expected value:

- Scan/compare workflow anchored to commit SHAs.
- Better repeatability for demos and regression examples.
- Stronger foundation for release review.

Risk:

- Medium. Needs careful command behavior around checkout/build cleanliness and no raw path leakage.

### 3. Implement `api-dto-contract-diff`

Why: API/DTO changes are one of the clearest ways to demonstrate TraceMap's evidence model. This builds on endpoint and type facts.

Expected value:

- Changed DTO/property/type reports.
- Stronger contract delta inputs.
- Better front-end/back-end alignment stories.

Risk:

- Medium-high. Needs precise classification boundaries so it does not claim runtime serialization behavior it cannot prove.

### 4. Implement `sql-schema-change-impact`

Why: SQL is a major dependency surface, and we now collect enough SQL evidence to make schema/table/column changes useful.

Expected value:

- Table/column/schema delta impact.
- Better backend/database dependency reporting.
- Useful for consulting and legacy-system analysis.

Risk:

- Medium. Static SQL parsing and dynamic SQL boundaries must stay honest.

### 5. Implement `parameter-value-origin-flow`

Why: this is the deeper analysis layer: request object to service call to external dependency. It is high-value, but also more complex than the report-oriented work above.

Expected value:

- Better explanation of how parameters, locals, fields, and arguments move.
- Stronger "controller receives object, service passes object, external system uses it" stories.
- Better reverse and impact context.

Risk:

- High. Needs tight limits, clear gaps, and deterministic evidence tiers.

## Specs To Queue Next

Queue these in parallel spec loops while implementation work runs:

1. Package and dependency surfaces (#32)
2. Multi-index portfolio dependency report (#36)
3. One-command public demo (#37)
4. Package upgrade impact (#27)

Suggested later spec loops:

1. Static HTML evidence explorer (#38)
2. Event and message dependency surfaces (#33)
3. Interface, override, and DI approximation (#35)
4. Deterministic risk scoring (#30)

## Cleanup Worth Doing Soon

- Reconcile stale task checkboxes for implemented specs.
- Decide whether to close implemented GitHub issues or comment with the PR/spec that satisfied them.
- Add a small `docs/BACKLOG.md` or generate one from issue labels if we want this inventory to stay easy to refresh.
- Consider a smoke script that runs a small "release review style" flow once `release-review-report` lands.
- Keep `.env.kiro.local` ignored and use the wrapper's profile-auth fallback for worktree spec loops.
- Promote `dev` to `main` once this runway/site-state cleanup lands, so AWS Amplify can be configured from `main` with `amplify.yml` and `site/` present.
- Configure Amplify for `tracemap.tools` after `main` contains the site setup.

## My Next Move

After the site workflow cleanup, create the dev-to-main promotion PR. Then continue with `release-review-report` implementation, while queueing two spec loops in parallel for:

- Package and dependency surfaces.
- Multi-index portfolio dependency report.

That keeps implementation moving and restores the four-spec runway without jumping into the highest-risk flow work too early.
