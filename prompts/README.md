# Private exchange prompts

For public local development (not the work machine), use
[Build the DNN SQL validation harness](build-dnn-sql-validation-harness.md), issue
#908. Static corpus/tests come first; the optional SQL Server container lane is
prepared but requires separate activation approval.

Start with [Verify work-machine HTTP readiness](verify-work-machine-http-readiness.md)
after merged PR #901. This is the current Claude prompt: one build, at most three
retained HTTP requeries, no rescan, and a sanitized H1–H4 readiness matrix.
Use the accepted fresh H4 index for lifecycle inventory; preserve old baselines.
Its verification-only follow-up checks existing receipts against their preserved
CLI/reporting binaries with no build, scan, or requery.

If the accepted inventory is unclear, [Locate the accepted work baseline](locate-accepted-work-baseline.md)
remains available for read-only discovery. The completed one-off HTTP debugging,
implementation and acceptance prompts have been removed from the active catalog;
Git history retains them. Their historical scan authorization does not carry forward.

For the next gap-planning step, use
[Inventory retained analysis gaps](inventory-retained-analysis-gaps.md).
It reads the existing scan once with a bounded Python utility—no build, scan or
full test suite—and keeps unresolved calls separate from AnalysisGap facts.

For public-only validation of database evidence admission, use
[Verify evidence-admission hardening](verify-evidence-admission-hardening.md).
It pins the implementation revision and synthetic acceptance checks; no private
application rescan is required. Previously accepted results carry forward across
prompt/CI-only edits. Do not rerun the full solution on the work machine; the
Windows test-host baseline comparison workflow isolates the reported failures
against the pinned base without claiming that unchanged files prove causality.

For automatic projectless constructor evidence, use
[Inspect projectless SQL parameter candidates](inspect-projectless-sql-parameter-candidates.md).
This does not replace schema verification or establish wrapper argument mapping.

After consolidation, use [Verify one local SQL contract](verify-one-local-sql-contract.md)
to prepare one evidence-backed contract review or precise owner questions, without
another scan or automatic API mapping.

To connect page/event evidence to retained SQL roots, use
[Reconcile WebForms SQL entry wiring](reconcile-webforms-sql-entry-wiring.md).
It reuses existing evidence first and keeps unresolved source-to-compiled bridges explicit.

After recovery, use [Consolidate local SQL inventory](consolidate-local-sql-inventory.md)
to combine the existing native root reports into one private evidence inventory.

After compiled SQL route reconciliation, use
[Recover local SQL command literals](recover-local-sql-command-literals.md) for
explicit local-only literal recovery and native truncation diagnostics.

For a source-only SQL result that conflicts with an earlier reported compiled
route, use [Reconcile existing compiled SQL evidence](reconcile-existing-compiled-sql-evidence.md)
first. It explicitly forbids another source-only assessment or application build.

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
- `guided-corporate-claude-launcher-setup.md`
- `produce-sql-wrapper-inventory.md` — SQL-only selected-caller report using existing admitted evidence; local output, no private contribution.

## Authorized agent-review prompt

`review-webforms-modernization-evidence.md` is a generic, ready-to-paste prompt
for a work-machine Claude Code review of an already generated packet, evidence
corpus, and application workbench. It asks the agent to preserve provenance,
static-evidence limitations, aliases, and fact/inference/unknown boundaries.
See `docs/WEBFORMS_AGENT_HANDOFF.md` for the matching commands. Agent output is
downstream planning material, never TraceMap scanner evidence.

## Corporate Claude launcher discovery

`guided-corporate-claude-launcher-setup.md` asks the work-machine Claude session
to inspect an approved BAT/Python launcher locally and return only a sanitized
argument-forwarding contract. It does not ask for launcher source, corporate
configuration, or secrets. Use its result before adding a launcher override to
the TraceMap PowerShell handoff. A verified launcher can then be supplied with
`Start-FocusedWebFormsClaudeReview.ps1 -ClaudeLauncherPath`.


## Public HTTP regression gate

[`validate-http-regressions-before-pr.md`](validate-http-regressions-before-pr.md)
maps the acceptance fixes to public synthetic regressions and runs focused plus
final-head full gates before a PR. It does not authorize opening or merging a PR.

## SQL report comparison

[`assess-sql-migration-report-improvement.md`](assess-sql-migration-report-improvement.md)
is an application-neutral local comparison of the pinned HTTP baseline and SQL
implementation. Supply the authorized application separately. It uses independently
named controls, separates evidence from planning judgments, and requests only
workplace-approved manually sanitized feedback. It does not authorize private
artifact/code transfer, database execution or application changes.
