# TraceMap Next Execution Report

Date: 2026-06-27

## Current State

- `dev` is the active integration branch for implementation loops.
- Main promotions remain human-mediated and should happen only at slice
  boundaries.
- Legacy .NET v0 is complete on `dev`. Route-flow Task 10 and the follow-up
  hardening slices landed; remaining legacy .NET work is focused depth or polish,
  not v0 foundation.
- Keep the site manager lane separate from core implementation work.

## Product Shape On Main

TraceMap now has a broad deterministic static-evidence base:

- .NET scanner, reducer, graph/flow facts, and reporting commands.
- TypeScript scanner.
- Python scanner MVP plus endpoint/SQL detail slices.
- JVM scanner MVP for Java plus Kotlin syntax fallback.
- Combined index builder.
- Combined dependency report, paths, diff, reverse query, and change impact.
- Route-flow and property-flow reports.
- Snapshot diff by SHA/index.
- SQL dependency surfaces, query pattern reporting, and SQL schema impact.
- Package dependency surfaces and package upgrade impact.
- Multi-index portfolio report.
- API/DTO contract diff and contract delta reducer paths.
- Release-review report with opt-in deterministic review priority scoring.
- Static HTML evidence explorer first slice.
- Obsidian/vault export with hidden/local safety fixes.
- Legacy .NET evidence families including WebForms, WinForms, ASMX/SOAP, WCF,
  Remoting, legacy ASP.NET routes, legacy build diagnostics, legacy data
  metadata, and legacy sample evidence packs.
- Static `tracemap.tools` site under `site/`, built by Amplify from `site/dist`.

Current CLI surface includes:

- `tracemap scan`
- `tracemap report`
- `tracemap reduce`
- `tracemap flow`
- `tracemap relate`
- `tracemap export`
- `tracemap endpoints`
- `tracemap combine`
- `tracemap paths`
- `tracemap route-flow`
- `tracemap property-flow`
- `tracemap diff`
- `tracemap impact`
- `tracemap reverse`
- `tracemap snapshot-diff`
- `tracemap portfolio`
- `tracemap package-impact`
- `tracemap vault`
- `tracemap docs-export`
- `tracemap contract-diff`
- `tracemap baseline`
- `tracemap evidence-pack`
- `tracemap explorer generate`
- `tracemap release-review`

## Runway Interpretation Rules

- Treat `Status: implemented*` as shipped for the implemented slice, even if
  the spec still has unchecked continuation tasks.
- Treat `Status: continuation-ready` or `follow-up-slices-available` as backlog,
  not abandoned current work.
- Do not use raw unchecked checkbox count as the source of truth. Read the
  implementation-state header first.
- Site specs are owned by the site lane unless explicitly handed back to core.
- Main promotions remain human-mediated.

## Core Specs With Follow-Up Value

These are good future implementation choices if legacy .NET depth becomes
valuable again. They are not blockers for Swift v0.

1. `route-centered-endpoint-trace-completeness`
   - First touched-file/touched-symbol slice is implemented.
   - Follow-up value: selector trace metadata, method/service grouping,
     data/query/dependency rows, value-origin/fact-symbol projection, and
     stronger downgrade tests.

2. `route-flow-service-data-composition`
   - First composition slice is implemented.
   - Follow-up value: richer service/data grouping and projection polish for
     endpoint-centered reports.

3. `ui-field-property-lineage`
   - V1 `property-flow` is implemented.
   - Follow-up value: deeper property-to-property mapping, route-flow joins,
     and optional browser/computer-use evidence as labeled external context.

4. `legacy-data-model-metadata-extraction`
   - Earlier legacy data metadata and reporting slices are implemented.
   - Follow-up value: deeper relationship extraction, unsupported-shape gaps,
     and old ORM metadata normalization.

5. `legacy-data-model-reporting-integration`
   - First descriptor projection/reporting slice is implemented.
   - Follow-up value: vault/RAG/export integration and richer selector support.

6. `static-html-evidence-explorer`
   - First explorer slice is implemented.
   - Follow-up value: richer rendering, provenance conflict UI, stronger
     public/demo safety parity, and no-JavaScript/accessibility hardening.

7. `event-message-surfaces`
   - V1 message surfaces are implemented.
   - Follow-up value: semantic extraction, direction filtering, route-flow async
     message-hop rendering, and adapter parity in TypeScript/Python/JVM.

## Site Lane

- Keep site changes in a separate worktree.
- Site specs use the `site-` prefix and include `implementation-state.md`.
- `site/src/` is editable source. `site/dist/` and `site/output/` are generated
  and ignored.
- Validate site changes with `npm run build` from `site/`; layout or
  interaction changes also need desktop and mobile browser sanity checks.
- Public claims must stay evidence-bound: rule IDs, evidence tiers, coverage
  labels, limitations, generated artifacts, and no LLM/AI impact-analysis
  claims.

## Recommended Next Move

1. Promote the current `dev` cleanup slice to `main`.
2. Start the Swift v0 adapter lane from the Swift runway issues:
   - #377 Swift adapter v0 runway;
   - #378 scaffold CLI and output contract;
   - #379 inventory and project/package discovery.
3. Keep legacy .NET follow-ups available for targeted depth, especially
   property-flow composition, data model depth, static dispatch approximation,
   evidence export polish, and route-flow projection performance.
4. In parallel, keep no more than 2 to 4 reviewed specs ahead of
   implementation. Avoid creating a large spec pile that outruns the product.

## Notes For Future Agents

- Do not rewrite old spec histories to pretend all future work is complete.
  Mark shipped slices and leave continuation tasks visible.
- If a spec says `implemented-v1-with-follow-ups`, consume it only after
  choosing a specific follow-up slice.
- If a required tool is missing, check Homebrew and known local tool locations
  before stopping, per `AGENTS.md`.
- Do not merge or auto-promote `main`; report merge readiness and let Joe make
  the owner call.
