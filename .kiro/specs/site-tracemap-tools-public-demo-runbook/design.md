# Site TraceMap Tools Public Demo Runbook Design

Status: implemented
Readiness: implemented
Public claim level: demo

## Purpose

This design note exists so the Kiro spec-review wrapper can review the site
phase with its standard `requirements.md`, `design.md`, and `tasks.md` inputs.
The user-requested deliverables remain `requirements.md`, `tasks.md`,
`implementation-state.md`, and `review-packet.md`.

## Proposed Route

The future implementation should add `/demo/runbook/` as a static page using
existing TraceMap site patterns. The route is distinct from existing public
demo surfaces:

- `/demo/start-here/` is the guided first-run walkthrough.
- `/demo/result/` explains current public demo result shape.
- `/demo/evidence-trail/` walks one bounded evidence question.
- `/demo/proof-upgrades/` is the upgraded-row ledger.
- `/proof-paths/` is the proof path index.
- `/validation/` is the validation reference.
- `/limitations/` is the non-claims reference.
- `/demo/proof-assets/` is deliberately out of the core bridge set because it
  is visual orientation, not the operator checklist.

The runbook should connect those routes into one operator checklist without
becoming a new evidence source.

## Page Shape

Use existing long-form static page composition:

- Hero with `Public claim level: demo` and the shared principle.
- Checklist section for pre-run, run, inspect, evidence-follow, validation,
  limitations, and sharing decisions.
- Artifact boundary section that separates public-safe summaries and reviewed
  public-safe reports from local-only raw/generated artifacts.
- Evidence checklist section for rule IDs, evidence tiers, coverage labels,
  gaps, proof paths, checked-in sources, and limitations.
- Claim-safe sharing section with safe wording, red flags, and escalation
  rules.
- Link section back to the existing demo, proof, validation, and limitations
  surfaces.

## Validation Shape

Future implementation should add a focused rendered-output validator using the
site's existing validator pattern. The validator should check route links,
claim-level labels, discovery metadata, forbidden raw/private text, forbidden
AI/LLM positioning, and runtime or operational overclaims. Because the runbook
is an artifact-boundary page, the validator must distinguish sanctioned warning
vocabulary from actual private content: artifact-family names and forbidden
category labels may appear only in artifact-boundary, sharing-guidance, or
red-flag sections, while raw values, private instances, local paths,
connection strings, raw statements, and unsupported positioning are rejected
everywhere.

The validator module should follow the existing per-page convention:
`site/scripts/demo-runbook.mjs` exporting `validateDemoRunbookDist`, imported
and called by aggregate site validation so `npm run validate` exercises it. It
must have a companion `site/scripts/*.test.mjs` module so `npm test` covers pass
and fail cases, including required inbound links from the existing demo,
proof-path, validation, and limitations pages.
Fail-case tests for private text must compose forbidden path and
connection-string examples at runtime so the tests do not introduce literals
that private-path validation flags.
Future implementation also adds sitemap metadata in `site/src/_site/pages.json`
and discovery metadata in `site/src/_site/discovery.json` for the route, then
confirms generated `sitemap.xml` includes `/demo/runbook/`.

## Non-Goals

- Do not implement site code in this spec phase.
- Do not introduce a runtime service, client-side fetch, analytics dependency,
  or generated evidence artifact.
- Do not publish raw facts, raw SQLite, analyzer logs, raw snippets, raw SQL,
  config values, secrets, local paths, raw remotes, generated scan directories,
  or private sample names.
- Do not claim runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- The back-link requirement does not include `/demo/proof-assets/`; an optional
  link from that page is allowed but not required.
- The stop condition is not optional: a future page that lacks a public-safe
  summary, rule IDs, evidence tiers, coverage labels, or limitations for a
  claim must not publish or repeat that claim.
