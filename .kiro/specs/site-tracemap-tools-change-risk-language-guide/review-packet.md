# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-change-risk-language-guide` spec for
spec-review findings first. This is a spec-only site phase; it must not
implement site code.

## Review Orientation

Branch: `codex/spec-site-change-risk-language-guide`
Base: `origin/dev`
Target PR base: `dev`

Local review artifacts are not committed and should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-change-risk-language-guide/`.

Review outcome: available Kiro reviews were run with `claude-opus-4.8` and
`claude-sonnet-4.6`. Medium or higher findings were patched or dispositioned
in `implementation-state.md`. Review coverage remains reduced where Kiro
denied shell/write tools; exact artifact paths and denied-tool messages are
recorded in `implementation-state.md`.

## Scope

The future page or section would create a public change-risk language guide for
reviewers, managers, engineers, architects, and implementation agents. It
should help them choose bounded words when describing static evidence around a
change.

The guide is wording and claim-boundary guidance only. It does not implement
scanner, reducer, adapter, validation, or site code in this phase. It does not
make raw facts, raw SQLite indexes, analyzer logs, raw source snippets, raw
SQL, config values, secrets, local paths, raw remotes, generated scan
directories, private sample names, command output, hidden validation details,
or credential-like values public.

Please inspect:

- `.kiro/specs/site-tracemap-tools-change-risk-language-guide/requirements.md`
- `.kiro/specs/site-tracemap-tools-change-risk-language-guide/design.md`
- `.kiro/specs/site-tracemap-tools-change-risk-language-guide/tasks.md`
- `.kiro/specs/site-tracemap-tools-change-risk-language-guide/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-change-risk-language-guide/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness`, and
  `Public claim level: concept` present and consistent?
- Does the spec remain spec-only without implementing site code or editing
  existing specs?
- Are future implementation tasks unchecked?
- Does it require visible `Public claim level: concept` and
  `No public conclusion without evidence`?
- Does it evaluate candidate placements `/language/change-risk/`,
  `/review-claim-checklist/language/`, a section on
  `/review-claim-checklist/`, and a section on `/questions/objections/`?
- Does it distinguish the guide from `/review-claim-checklist/`,
  `/questions/objections/`, `/release-review-boundary/`,
  `/static-vs-runtime/`, `/proof-paths/faq/`, and `/manager-faq/`?
- Does it require sections for why wording matters, safe static-evidence
  phrases, unsafe phrases, evidence-required wording, reduced-coverage wording,
  owner-handoff wording, stop conditions, and non-claims?
- Does it require tables for safe phrasing, unsafe/blocked phrasing, when to
  use `needs review`, when to say `evidence shows`, when to say
  `coverage is reduced`, and when to stop?
- Does it forbid impact proof, absence-of-impact proof, release
  approval/safety, operational safety, runtime proof, production traffic,
  endpoint performance, complete coverage, AI/LLM analysis, and replacement of
  human judgment?
- Does it forbid raw facts, raw SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, remotes, generated scan directories,
  private sample names, command output, hidden validation details, and
  credential-like values?
- Does it require implementation validation for wording tables, required
  links, metadata, discovery/sitemap metadata if standalone, forbidden claims,
  private/raw material, word-count bounds, and desktop/mobile browser sanity?
- Does it avoid blame language?
- Is `implementation-state.md` sufficient for a future agent to resume without
  guessing?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
