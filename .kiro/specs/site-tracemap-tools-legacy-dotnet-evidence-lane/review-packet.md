# TraceMap Legacy .NET Evidence Lane Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-legacy-dotnet-evidence-lane` spec for future
implementation readiness. This is a spec-only public-site phase; it must not
implement site code.

## Review Orientation

Branch: `codex/spec-site-legacy-dotnet-evidence-lane-20260625190237`

Base: `origin/dev`

PR target: `dev`

Review cycle: initial spec review pending. Review findings, patched items, and
dispositions should be recorded in `implementation-state.md`.

Local review artifacts should remain under `.tmp/kiro-reviews/` and must not
be committed.

Note: this file is the review-harness input artifact for external Kiro spec
reviews. It is not the future site page and is not itself public copy.

Note: this file's header fields (`Status`, `Readiness`, and
`Public claim level`) are kept aligned with packet readiness. See
`implementation-state.md` for review outcomes.

## Scope

The future page or section should explain a legacy .NET evidence lane for
`tracemap.tools`. It should frame WCF, ASMX/SOAP, .NET Remoting, WebForms,
WinForms, legacy data metadata, project/toolchain diagnostics, and
modernization review as deterministic static evidence surfaces.

The lane must stay honest about target branch status. It must not claim
shipped scanner coverage unless public-safe evidence supports the exact wording
on the relevant branch. It must not upgrade hidden legacy .NET work into public
support language.

Please inspect:

- `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/requirements.md`
- `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/design.md`
- `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/tasks.md`
- `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/review-packet.md`

## Required Review Focus

- Is the packet clearly spec-only?
- Are `Status: not-started`, current `Readiness`, and
  `Public claim level: concept` present and consistent?
- Does the future lane require visible `Public claim level: concept` and
  visible `No public conclusion without evidence`?
- Does the spec define an evidence-status matrix with statuses such as
  `shipped`, `demo`, `dev` or `dev-only`, `future`, and `hidden`?
- Does every row require a proof path and adjacent limitation text?
- Are row promotions gated on public-safe proof for the exact wording and
  relevant branch?
- Does the spec require reconciliation with the existing legacy evidence story
  claim ledger before assigning row statuses?
- Does it default hidden, absent, or unreconciled legacy .NET surfaces to
  `hidden`, `future`, or omitted instead of promoted language?
- Does the spec cover WCF, ASMX/SOAP, .NET Remoting, WebForms, WinForms,
  legacy data metadata, project/toolchain diagnostics, and modernization
  review?
- Does it distinguish general static-evidence-model rows from legacy-surface
  support rows?
- Does it treat failed build or project load as reduced coverage, not a clean
  repository?
- Does it describe syntax fallback as useful reduced-coverage evidence, not
  semantic proof?
- Does it avoid saying `impacted` without deterministic reducer output and
  public-safe evidence?
- Does it forbid runtime behavior, production traffic, deployed endpoint
  existence, endpoint performance, service reachability, UI reachability,
  outage cause, release approval, release safety, operational safety, security
  posture, exploitability, database existence, query execution, schema
  compatibility, package compatibility, migration feasibility, migration
  completeness, AI/LLM analysis, and complete product coverage claims?
- Does it forbid raw source snippets, raw SQL, config values, connection
  strings, secrets, local paths, raw remotes, generated scan directories,
  private sample names, raw facts, raw SQLite indexes, analyzer logs, raw
  service addresses, raw endpoint values, production identifiers, hidden
  validation details, hidden capability counts, and unreleased sequencing?
- Does it define navigation, metadata, discovery, cross-link, and focused
  validation expectations without inflating claim maturity?
- Are future implementation tasks unchecked and sufficiently specific?
- Are spec-only validation requirements limited to `git diff --check`,
  `./scripts/check-private-paths.sh`, and diff-scope confirmation?
- Are future site implementation validation requirements specific enough for
  site tests, validation, build, and browser checks?

## Non-Negotiables

- No public conclusion without evidence.
- No evidence-status row without proof path and limitation text.
- No shipped claim without public-safe proof on `main`.
- No dev claim without explicit `dev` or `dev-only` labeling.
- No hidden legacy .NET theme promoted by concept-level page framing.
- No runtime proof, production proof, release safety, operational safety,
  migration completeness, complete coverage, AI/LLM analysis, embeddings,
  vector database, prompt-based classification, or replacement of human review.
- No raw or private material in future public copy.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
