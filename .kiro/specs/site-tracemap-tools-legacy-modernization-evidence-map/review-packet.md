# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-legacy-modernization-evidence-map` spec for
spec-review findings first. This is a spec-only site phase; it should not
implement site code.

## Review Orientation

Branch: `codex/spec-site-legacy-modernization-evidence-map`
Last verified: 2026-06-18
Review cycle: Opus and Sonnet initial reviews completed; multiple re-reviews
completed; Medium findings patched; final Sonnet re-review found no Medium or
higher findings

Local review artifacts, if generated, should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/` and
not committed.

Standing open questions for this review:

- Is `Public claim level: concept` sufficiently conservative for a public
  modernization planning page?
- Are demo rows properly gated on existing public-safe proof?
- Are dev-only and hidden legacy capabilities bounded enough?

## Scope

The future page or section should explain how TraceMap can organize static
evidence during legacy modernization and migration planning without claiming
runtime proof or product completeness. It must connect existing legacy
validation and evidence themes to reviewer questions:

- old frameworks and toolchains
- project load failures and failed builds
- syntax fallback after semantic load failure
- WCF and service references
- ASMX/SOAP services
- remoting
- WinForms navigation or event surfaces
- WebForms event, route, and navigation surfaces
- config/project metadata
- legacy data metadata
- coverage gaps and hidden or unsanitized validation

Please inspect:

- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/requirements.md`
- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/design.md`
- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/tasks.md`
- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-legacy-modernization-evidence-map/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness: ready-for-review` or later
  `ready-for-implementation`, and `Public claim level: concept` present and
  consistent?
- Are future implementation tasks unchecked?
- Does the spec stay spec-only and avoid site implementation work?
- Does the page role stay distinct from capabilities, validation, limitations,
  manager packet, claim-governance pages, `/legacy-evidence/`, and
  `/legacy-validation/`?
- Does the spec clearly explain why the public claim level is `concept`?
- Does the spec require exact public-safe proof before specific demo rows are
  shown?
- Does the spec label dev-only capabilities as `dev-only` or omit them?
- Does the spec require row labels to be reconciled with the existing
  `site-tracemap-tools-legacy-evidence-story` theme claim ledger and
  `legacy-story-reconciliation` before publish?
- Does the spec default row themes absent from both sibling reconciliation
  sources, currently WinForms and ASMX/SOAP, to `hidden` or `omitted` until a
  public-safe ledger update exists?
- Does the spec keep hidden/private legacy validation abstract enough to avoid
  leaking private sample names, counts, cadence, raw artifacts, or unreleased
  sequencing?
- Does the spec connect old framework/toolchain clues, project-load failures,
  syntax fallback, WCF, ASMX, remoting, WinForms, WebForms, and legacy data
  metadata to reviewer questions without claiming completeness?
- Does the spec treat failed build or project load as reduced coverage, not a
  clean repository?
- Does the spec avoid claims about runtime behavior, production traffic,
  endpoint performance, outage cause, release safety, operational safety,
  exploitability, database existence, package compatibility, migration safety,
  AI impact analysis, LLM analysis, and complete product coverage?
- Does the spec avoid saying or implying `impacted` without deterministic
  reducer output and public-safe evidence?
- Does the spec forbid publishing raw source snippets, raw SQL, config values,
  connection strings, secrets, local absolute paths, raw remotes, raw facts,
  SQLite files, analyzer logs, generated scan directories, raw service
  addresses, raw endpoint values, private sample identities, customer data, and
  production identifiers?
- Does the spec define route/placement, metadata, discovery, cross-link, and
  focused validation expectations without inflating the claim level?
- Does the spec require future implementation to run site tests, site
  validation, build, browser sanity checks for layout changes,
  `git diff --check`, and `./scripts/check-private-paths.sh`?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
