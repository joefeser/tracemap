# TraceMap Next Execution Report

Date: 2026-07-20

## Branch State

- `main` is the human-mediated release branch at
  `3dd7e455503e5bad5028323254c59976ffa75a10`.
- `dev` is the active integration branch at
  `da7f4f2c56dcd4b88c2abd82bdff04a9a7687309` before this closeout slice.
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
- hidden terminal-context vault navigation and the static
  `tracemap.tools` site.

These are static, coverage-relative evidence lanes. They do not establish
runtime execution, production traffic/state, deployment, authorization,
vulnerability, release approval, or that a change is safe.

## Additional Product Shape On Dev

`dev` additionally contains:

- Route-flow SQL-context composition for already-selected SQL-facing static
  paths (PR #483);
- The public-safe SQL operator proof page and generated-site guard hardening
  (PR #486);
- The source/tree/commit-bound Base44 static evidence adapter and its
  service-role/SDK follow-up (PRs #494 and #496);
- Microsoft Access adapter v0 foundation, bounded form/report counts,
  conservative VBA/module counts, macro counts, downstream gaps, and local-only
  Windows validation (PRs #487, #492, #493, #495, and closeout #497).

Access UI/VBA/macro item identities and bodies remain deliberately unavailable
in the shipped count-only v0. Their absence is rule-backed reduced coverage, not
evidence that those objects or flows do not exist.

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
- Legacy data model relationship completion: PR #398 merged only the reviewed
  spec. Its shared relationship gap classifier/harness is not implemented.
- Route-flow service/data composition final reports Task 10 public-safe
  validation complete. Issues #159, #179, and #201 should be closed or narrowed
  from merged evidence before being treated as new implementation queues.
- Base44 issue #484 is complete through PRs #494/#496. Cross-product validation
  remains owned by the 88mph consumer contract, not a second TraceMap capability
  registry.

## Recommended Next Product Story

Implement PR 1 from
`.kiro/specs/legacy-data-model-relationship-completion/`:

1. Re-audit current `origin/dev` and the live rule catalog/extractors.
2. Add the small shared deterministic relationship-gap classifier/harness
   described by the reviewed spec.
3. Wire at most one descriptor family if the shared harness alone is not a
   useful reviewable slice.
4. Preserve existing DBML, EDMX, typed DataSet, and NHibernate family behavior;
   do not invent endpoints, runtime mappings, database access, or provider
   compatibility.
5. Add rule-catalog coverage before any new reason/gap string, focused
   determinism and ambiguity tests, full .NET validation, private-path checking,
   and ACK review-loop evidence.

This is the next authoritative product story because its spec is merged and
reviewed, its first product slice is explicitly unstarted, it deepens an
existing buyer-relevant data-design lane, and it requires no private customer
artifact or Windows-only capability.

## Subsequent Choices

After the relationship classifier slice:

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
