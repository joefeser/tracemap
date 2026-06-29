# TraceMap Kiro Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

Review the `site-tracemap-tools-static-vs-runtime-field-guide` spec for
spec-review findings first.

This is a spec-only public-site phase. It should define a future
`tracemap.tools` field guide or article explaining deterministic static
evidence beside runtime telemetry, APM, logs, traces, metrics, dashboards,
tests, incident response, and service-owner interpretation.

The field guide must position TraceMap as deterministic static evidence that
complements runtime tools. It must not claim runtime behavior, production
traffic, endpoint performance, incident truth, outage cause, release safety,
operational safety, complete coverage, AI/LLM impact analysis, or replacement
of runtime tools and human judgment.

## Review Orientation

Branch: `codex/spec-site-static-vs-runtime-field-guide-20260625190224`
Base: `origin/dev`
Target PR base: `dev`

Local review artifacts are not committed and should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/`.

Review outcome: requested Kiro reviews completed with reduced coverage because
Kiro reported denied tool access; Medium findings were patched, one bounded
re-review was run, and validation passed. See `implementation-state.md` for
artifact paths, findings, dispositions, and validation results.

## Scope

The future page or section should explain that TraceMap shows static
dependency evidence and limitations, while runtime tools show observed
behavior. Neither replaces the other.

This packet does not implement site code, scanner code, reducer behavior,
runtime telemetry ingestion, observability integrations, generated artifacts,
or validation scripts.

Please inspect:

- `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/requirements.md`
- `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/design.md`
- `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/tasks.md`
- `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/review-packet.md`

## Review Focus

- Are `Status: not-started`, `Readiness`, and
  `Public claim level: concept` present and consistent?
- Does the packet remain spec-only without implementing site code or editing
  existing specs?
- Are future implementation tasks unchecked?
- Does the spec require visible `Public claim level: concept` and
  `No public conclusion without evidence`?
- Does the spec require visible public copy equivalent to `TraceMap shows
  static dependency evidence and limitations; runtime tools show observed
  behavior. Neither replaces the other.`?
- Does the spec clearly separate deterministic static evidence questions from
  runtime observability questions?
- Does it keep TraceMap bounded to rule IDs, evidence tiers, file paths, line
  spans, commit SHA, extractor versions, coverage labels, limitations, and
  public-safe proof paths?
- Does it clearly state that runtime tools remain necessary for observed
  behavior, production traffic, endpoint performance, request behavior,
  runtime errors, incident timelines, outage cause, operational safety,
  service-owner conclusions, and release decisions?
- Does it avoid claiming runtime behavior, production traffic, endpoint
  performance, incident truth, outage cause, release safety, operational
  safety, production dependency understanding, service ownership, test
  sufficiency, complete product coverage, or merge/deploy readiness?
- Does it avoid AI/LLM impact-analysis, prompt-classification, embedding, or
  vector-database claims for the core scanner or reducer?
- Does it avoid implying TraceMap replaces logs, traces, metrics, APM,
  telemetry, dashboards, production metrics, tests, service-owner review,
  incident response, release approval, governance, or human judgment?
- Does it avoid naming specific observability vendors as shipped integrations
  without public repo evidence?
- Does it forbid raw source snippets, raw SQL, config values, secrets, local
  paths, raw remotes, generated scan directories, private sample names, raw
  facts, raw SQLite indexes, analyzer logs, raw telemetry payloads, incident
  timelines, customer data, service names, production identifiers, dashboard
  screenshots, command output, hidden validation details, and credential-like
  values?
- Does it require public-safe proof paths and visible limitations for examples?
- Does it define navigation, metadata, sitemap, discovery, hint-category,
  preferred-proof-path, limitations, and non-claim expectations without
  inflating the claim?
- Does it require future validation for visible copy, required sections,
  comparison table structure, metadata, discovery metadata, forbidden wording,
  forbidden private/raw material, link resolution, and browser sanity when
  layout changes are implemented?
- Is `implementation-state.md` sufficient for a future agent to resume without
  guessing?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.

Record review outcomes and dispositions in this spec's
`implementation-state.md`.
