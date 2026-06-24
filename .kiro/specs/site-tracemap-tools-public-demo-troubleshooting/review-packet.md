# TraceMap Public Demo Troubleshooting Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-public-demo-troubleshooting` spec for future
implementation readiness. This is a spec-only public-site phase; it must not
implement site code.

## Review Orientation

Branch: `codex/spec-site-public-demo-troubleshooting`

Base: `origin/dev`

PR target: `dev`

Review cycle: re-review completed after initial review findings were patched;
see `implementation-state.md` for review artifacts, coverage, and
dispositions.

Note: re-review is complete, Medium or higher findings are patched, and
spec-phase gates passed (`git diff --check` and
`./scripts/check-private-paths.sh`). The packet is ready for future
implementation.

Local review artifacts should remain under `.tmp/kiro-reviews/` and must not
be committed.

Note: this file is the review-harness input artifact for external Kiro spec
reviews. It is not the future site page and is not itself public copy.

## Scope

The future page or section should help public visitors understand what to
check when the public demo, proof route, demo summary, validation expectation,
or claim wording is confusing, stale, or incomplete. It should identify
public-safe causes, route checks, non-claims, stop conditions, and next
owner/routes without becoming a support contract or runtime diagnostic tool.

Please inspect:

- `.kiro/specs/site-tracemap-tools-public-demo-troubleshooting/requirements.md`
- `.kiro/specs/site-tracemap-tools-public-demo-troubleshooting/design.md`
- `.kiro/specs/site-tracemap-tools-public-demo-troubleshooting/tasks.md`
- `.kiro/specs/site-tracemap-tools-public-demo-troubleshooting/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-public-demo-troubleshooting/review-packet.md`

## Required Review Focus

- Is the packet clearly spec-only?
- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present and consistent before review?
- Does the future page require visible `Public claim level: concept` and
  visible `No public conclusion without evidence`?
- Are candidate placements bounded to `/demo/troubleshooting/`,
  `/demo/help/`, section on `/demo/runbook/`, or section on
  `/demo/start-here/`?
- Does the spec distinguish the surface from `/demo/runbook/`,
  `/demo/start-here/`, `/demo/result/`, `/demo/proof-upgrades/`,
  `/validation/`, `/limitations/`, and `/questions/objections/`?
- Is the future surface clearly site/demo guidance rather than a support
  contract, runtime diagnostic tool, production proof, release safety or
  approval surface, endpoint performance checker, complete coverage claim, or
  replacement for validation and human review?
- Are required rows present: missing route, outdated demo summary, broken
  proof expectation, reduced coverage label, private-only evidence,
  unsupported claim wording, validation mismatch, and where to ask next?
- Does each row require symptom, likely public-safe cause, what to check, what
  not to conclude, next owner/route, stop condition, and non-claim?
- Does the spec require that required matrix fields progressively disclosed on
  narrow viewports use accessible, programmatically associated disclosure
  controls rather than being removed?
- Does the spec forbid live support SLA, runtime diagnosis, production proof,
  release safety or approval, endpoint performance, complete coverage,
  absence-of-impact proof, clean-repo claim under reduced analysis, AI/LLM
  analysis, prompt-based classification, embedding search, vector database
  analysis, autonomous approval, and replacement of validation or human review?
- Does the spec avoid claiming TraceMap performs AI impact analysis, LLM
  analysis, prompt-based classification, embedding search, or vector database
  analysis?
- Does the spec forbid raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan dirs,
  private sample names, command output, hidden validation details, and
  credential-like values?
- Does the spec avoid blame language while preserving the required row labels?
- Are implementation validation expectations specific enough for required
  troubleshooting rows, required links, metadata, discovery/sitemap metadata
  if standalone, forbidden claims, private/raw material, word count bounds, and
  desktop/mobile browser sanity?

## Non-Negotiables

- No public conclusion without evidence.
- No support SLA.
- No runtime diagnosis.
- No production proof.
- No release safety or approval.
- No endpoint performance proof.
- No complete coverage claim.
- No absence-of-impact proof.
- No clean-repo claim under reduced analysis.
- No autonomous approval.
- No AI/LLM analysis, prompt-based classification, embedding search, or vector
  database analysis.
- No replacement of validation or human review.
- No raw facts, raw SQLite, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, command output, hidden validation details, or
  credential-like values.
- No blame language.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
