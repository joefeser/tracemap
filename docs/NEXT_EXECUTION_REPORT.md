# TraceMap Next Execution Report

Date: 2026-07-20

## Branch State

- `main` and `dev` were synchronized by promotion PR #505 at
  `81033b4fee9abc6b3dbed63234570e9bb7cb66ca` before the current Access
  design-review composition slice.
- Main promotions remain owner-mediated. ACK `merge_ready` on a feature-to-dev
  PR does not authorize an automatic dev-to-main promotion.
- The public site lane remains separate from core implementation unless it is
  explicitly selected.

## Product Shape On Main

`main` contains the deterministic multi-language evidence foundation and the
SQL evidence runway:

- .NET/C#, TypeScript, Python, JVM/Java/Kotlin, and Swift scanners;
- standard manifest, NDJSON, SQLite, Markdown, and analyzer-log artifacts;
- combine, report, paths, reverse, route-flow, property-flow, diff, impact,
  portfolio, contract-diff, snapshot-diff, vault, docs-export, explorer, and
  release-review workflows;
- legacy .NET evidence families for WebForms, WinForms, ASMX/SOAP, WCF,
  Remoting, legacy routes/build diagnostics, and legacy data metadata;
- PostgreSQL-first SQL execution-context, protected-material, permission,
  archive-link, and operator-runbook evidence;
- release-review SQL runway composition with structured gaps and preserved
  upstream provenance;
- route-flow SQL-context composition and the public-safe SQL operator proof
  packet;
- the Microsoft Access adapter v0 foundation, count-only form/report/VBA/macro
  boundaries, downstream gaps, and completed Windows validation;
- the Base44 adapter and typed DataSet relationship-field completion;
- hidden terminal-context vault navigation and the static
  `tracemap.tools` site.

These are static, coverage-relative evidence lanes. They do not establish
runtime execution, production traffic/state, deployment, authorization,
vulnerability, release approval, or that a change is safe.

## Current Product Slice

The active `codex/access-design-review-composition` slice composes already
persisted Access inventory, schema, relationship, saved-query, external-boundary,
count-only metadata, and coverage gaps into release review. It does not add COM
reads or reopen UI, VBA, macro, row-data, execution, or Windows probe boundaries.

## Runway Interpretation Rules

- Read each spec's parseable `Status:` header before interpreting unchecked
  tasks.
- `implemented*` or `*-merged-with-follow-ups` means the named slice is
  shipped; unchecked continuation tasks remain backlog.
- `spec-merged-implementation-ready` means design/review work is complete but
  product implementation has not begun.
- `continuation-ready` and `follow-up-slices-available` are backlog states,
  not abandoned in-flight work.
- Historical branch/PR narratives may describe earlier waits; the current
  status header and final merge record are authoritative.
- No unchecked checkbox may be marked complete solely because adjacent code
  exists. Require a merged PR or current code/test evidence for that exact
  behavior.

## Reconciled Follow-Up Lanes

- Evidence export usability polish: PR #193 merged the first navigation and
  docs-export ergonomics slice; review graph mode, dedicated route indexes, and
  explicit question-family request gaps remain follow-ups.
- Route-centered endpoint trace completeness: PRs #241 and #253 merged touched
  summaries and selector trace metadata; method/service grouping and remaining
  presentation polish stay follow-ups.
- Static dispatch candidate bridges: PRs #331 and #333 merged the shared
  builder and bounded override traversal; DI annotations and broader consumer
  composition remain follow-ups.
- Legacy data model relationship completion is implemented through PR #504,
  including the typed DataSet relationship-field classification follow-up.
- Route-flow service/data composition final reports Task 10 public-safe
  validation complete. Issues #159, #179, and #201 should be closed or narrowed
  from merged evidence before being treated as new implementation queues.
- Base44 issue #484 is complete through PRs #494/#496. Cross-product validation
  remains owned by the 88mph consumer contract, not a second TraceMap capability
  registry.

## Recommended Next Product Story

Complete the current Access design-review composition, then choose one bounded
follow-up from current evidence rather than reopening the v0 reader:

1. compose Access evidence into one additional downstream consumer such as
   vault or route/property-flow, preserving count-only gaps; or
2. specify the reserved `sql-validation-summary/v1` checked-in provenance
   contract if operator-observed validation evidence is the higher priority.

Richer Access UI/VBA/macro identity or body extraction is not the default next
story. It requires a separate threat review and Windows authorization because
the v0 probes proved that apparently simple item access can load surfaces.

## Subsequent Choices

After the Access composition slice:

1. choose one static-dispatch follow-up (DI context or one downstream consumer),
   not the entire remaining task list;
2. choose one event/message follow-up such as release-review context or
   route-flow async-boundary rendering;
3. reconcile and close the already-delivered route/property-flow issues before
   reopening them as product work;
4. promote `dev` to `main` only as a separate owner-mediated release PR.

## Site Lane

- Keep site changes in a separate worktree.
- Edit `site/src/`; never hand-edit ignored `site/dist/` or `site/output/`.
- Run `npm run build` and relevant validation from `site/`; use desktop and
  mobile browser checks for layout/interaction changes.
- Keep public claims bounded to deterministic evidence, rule IDs, tiers,
  coverage labels, limitations, and generated artifacts.

## Notes For Future Agents

- Do not recommend completed Swift v0 work as the next runway.
- Do not describe `dev`-only features as already on `main`.
- Do not turn stale open issues into duplicate implementations without checking
  merged PRs and current spec authority.
- Do not add LLMs, embeddings, vector databases, prompt classification, or
  runtime systems to the scanner/reducer.
- If a required tool is missing, follow `AGENTS.md`: check Homebrew and known
  local tool paths before stopping.
