# TraceMap Reduced Coverage Playbook Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-reduced-coverage-playbook` spec for future
implementation readiness. This is a spec-only public-site phase; it must not
implement site code.

## Review Orientation

Branch: `codex/spec-site-reduced-coverage-playbook`

Base: `origin/dev`

PR target: `dev`

Review cycle: initial reviews and feasible re-review cycles completed.
Medium or higher findings were patched or explicitly dispositioned; see
`implementation-state.md` for review and validation details.

Local review artifacts should remain under `.tmp/kiro-reviews/` and must not
be committed.

Note: this file is the review-harness input artifact for external Kiro spec
reviews. It is not the future site page and is not itself public copy.

## Scope

The future page or section should teach readers what to do when TraceMap
evidence is partial, coverage is reduced, or gaps are present. It should show
how to label the state, what not to conclude, which evidence to collect next,
who should own the next step, and where review must stop.

Please inspect:

- `.kiro/specs/site-tracemap-tools-reduced-coverage-playbook/requirements.md`
- `.kiro/specs/site-tracemap-tools-reduced-coverage-playbook/design.md`
- `.kiro/specs/site-tracemap-tools-reduced-coverage-playbook/tasks.md`
- `.kiro/specs/site-tracemap-tools-reduced-coverage-playbook/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-reduced-coverage-playbook/review-packet.md`

## Required Review Focus

- Is the packet clearly spec-only?
- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present and consistent before review?
- Does the future page require visible `Public claim level: concept` and
  visible `No public conclusion without evidence`?
- Are candidate placements bounded to `/coverage/reduced/`,
  `/limitations/reduced-coverage/`, section on `/limitations/`, or section on
  `/validation/` unless the future implementation records an equivalent?
- Does the spec distinguish the playbook from `/limitations/`,
  `/validation/`, `/static-vs-runtime/`, `/questions/objections/`,
  `/proof-paths/faq/`, and `/review-claim-checklist/`?
- Are required sections present: what reduced coverage means, how to label it,
  safe conclusions, unsafe conclusions, next evidence to collect, owner
  handoff, stop conditions, and non-claims?
- Are required rows present: build/load failure, syntax fallback, missing
  semantic evidence, unsupported framework surface, missing generated
  artifact, private-only support, stale commit context, and unknown evidence
  tier?
- Does each row require coverage label, evidence tier, evidence available,
  what cannot be concluded, next owner, safe wording, stop condition, and
  proof/validation link?
- Does the spec forbid absence-of-impact proof, clean-repo claim under failed
  or reduced analysis, runtime proof, release approval or safety, operational
  safety, complete coverage, AI/LLM analysis, prompt-based classification,
  embedding search, vector database analysis, and replacement of human review?
- Does the spec avoid claiming TraceMap performs AI impact analysis, LLM
  analysis, prompt-based classification, embedding search, or vector database
  analysis?
- Does the spec forbid raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, remotes, generated scan dirs, private
  sample names, command output, hidden validation details, and credential-like
  values?
- Does the spec avoid blame language while still preserving the required
  `build/load failure` row label?
- Are implementation validation expectations specific enough for required
  rows, required links, metadata, standalone sitemap/discovery metadata,
  section host metadata, forbidden claims, private/raw material, word count
  bounds, and desktop/mobile browser sanity?

## Non-Negotiables

- No public conclusion without evidence.
- No absence-of-impact proof.
- No clean-repo claim under failed or reduced analysis.
- No runtime proof, production traffic proof, endpoint performance proof, or
  outage-cause proof.
- No release approval, release safety, operational safety, complete coverage,
  autonomous approval, AI/LLM analysis, prompt-based classification,
  embedding search, vector database analysis, or replacement of human review.
- No raw facts, raw SQLite, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, command output, hidden validation details, or
  credential-like values.
- No blame language.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
