# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: demo

Review the `site-tracemap-tools-public-demo-runbook` spec for spec-review
findings first. This is a spec-only site phase; it should not implement site
code.

## Review Orientation

Branch: codex/spec-site-public-demo-runbook
Last verified: 2026-06-18
Review cycle: Opus and Sonnet reviews and re-reviews completed; Medium+ and
High findings patched; final spec-phase validation passed.

Local review artifacts, if generated, should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-public-demo-runbook/` and not committed.

Remaining open questions: none known before first review.

## Scope

The future page should publish `/demo/runbook/`, a public-safe operator
checklist for running the public demo, inspecting generated public-safe
summaries, deciding what can be shared, and avoiding stronger claims than the
demo evidence supports.

It must bridge these existing surfaces without duplicating their source-of-truth
roles:

- `/demo/start-here/`
- `/demo/result/`
- `/demo/evidence-trail/`
- `/demo/proof-upgrades/`
- `/proof-paths/`
- `/validation/`
- `/limitations/`

Please inspect:

- `.kiro/specs/site-tracemap-tools-public-demo-runbook/requirements.md`
- `.kiro/specs/site-tracemap-tools-public-demo-runbook/design.md`
- `.kiro/specs/site-tracemap-tools-public-demo-runbook/tasks.md`
- `.kiro/specs/site-tracemap-tools-public-demo-runbook/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-public-demo-runbook/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness: review-needed` or later
  `ready-for-implementation`, and `Public claim level: demo` present and
  consistent?
- Are future implementation tasks unchecked?
- Does the spec stay spec-only and avoid site implementation work?
- Does `/demo/runbook/` have a clear role distinct from `/demo/start-here/`,
  `/demo/result/`, `/demo/evidence-trail/`, `/demo/proof-upgrades/`,
  `/proof-paths/`, `/validation/`, and `/limitations/`?
- Does the runbook require a concrete operator checklist for pre-run, run,
  inspect, evidence-follow, validation, limitations, sharing, and stop
  conditions?
- Does the spec distinguish public-safe summaries and reviewed public-safe
  reports from local-only raw artifacts?
- Does the spec forbid publishing raw facts, raw SQLite, analyzer logs, raw
  source snippets, raw SQL, config values, secrets, local absolute paths, raw
  remotes, generated scan directories, and private sample names?
- Does the spec avoid claims about runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety, AI
  impact analysis, LLM analysis, and complete product coverage?
- Does the spec require visible rule IDs, evidence tiers, coverage labels,
  gaps, proof paths, and limitations before public demo conclusions are
  repeated?
- Does the spec avoid saying or implying `impacted` without a deterministic
  reducer output and cited evidence?
- Does the spec define discovery metadata, sitemap coverage, cross-links, and
  focused validation for the future implementation?
- Does the spec require future implementation to run site tests, site
  validation, site build, browser sanity checks, `git diff --check`, and
  `./scripts/check-private-paths.sh`?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
